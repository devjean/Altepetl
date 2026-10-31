using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    /// <summary>Lo que dejó un ataque a tu aldea.</summary>
    public sealed class ResultadoAsalto
    {
        public bool Defendida;          // vencieron a todos los invasores
        public int Invasores;
        public int Vencidos;
        public int Derribados;          // edificios derribados (sin contar murallas)
        public int[] Perdidas = new int[ResourceInfo.Count];
        public int Defensores;
        public int Caidos;
        public int Heridos;
        public int Cautivos;
        public int[] Ascensos = new int[Rangos.Count]; // cuántos subieron a cada rango
    }

    /// <summary>
    /// Ataques de la IA a tu aldea, en vivo, como en Clash: de vez en cuando se acerca un grupo de guerreros
    /// (más fuerte según tu tecpan). Hay un aviso, más largo con torres de vigía, y luego atacan.
    /// Pelean tus torres, tus murallas los frenan y salen a defender los guerreros que elegiste en Defensa;
    /// los demás se resguardan. Los defensores que caen se pierden y los heridos van al temazcalli.
    /// Por cada edificio derribado se llevan parte de tus recursos. Al terminar, todo se repara.
    /// </summary>
    public sealed class Asalto : MonoBehaviour
    {
        public enum Estado
        {
            Tranquilo,
            Aviso,
            EnCurso,
            Resultado,
            Pendiente, // ocurrió mientras el juego estaba cerrado; falta verlo o saltarlo
        }

        private const float PrimerAtaqueMin = 240f, PrimerAtaqueMax = 420f;    // segundos jugando en la aldea
        private const float EntreAtaquesMin = 480f, EntreAtaquesMax = 840f;
        private const float AvisoBase = 10f, AvisoPorTorre = 10f, AvisoMaximo = 40f;
        public const float Duracion = 120f;               // si no los vences antes, se van con lo que tomaron
        private const float PerdidaMaxima = 0.2f;         // lo que se llevan si derriban todo
        private const float SegundosEntreDisparos = 1f;
        private static readonly int[] InvasoresPorTecpan = { 3, 5, 8, 12, 16 };
        // Ataques mientras el juego está cerrado: solo tras una ausencia larga, con probabilidad
        // creciente, nunca más de uno por regreso y con un escudo después, como en Clash.
        private const float AusenciaMinima = 2f * 3600f, AusenciaMedia = 6f * 3600f, AusenciaSegura = 12f * 3600f;
        private const float ProbabilidadMinima = 0.35f, ProbabilidadMedia = 0.70f;
        private const float EscudoHoras = 8f, EscudoHorasSaqueo = 12f;
        private const float AvisoAusente = 5f;        // al ver el ataque, apenas un momento para ubicarlo
        private const float PasoInstantaneo = 0.05f;  // paso de simulación al saltar el ataque
        // Guerreros con macuahuitl casi siempre; algunos arqueros y honderos.
        private static readonly TroopId[] Mezcla =
            { TroopId.Macuahuitl, TroopId.Macuahuitl, TroopId.Arquero, TroopId.Macuahuitl, TroopId.Hondero };

        public GameManager Manager;
        public Estado EstadoActual { get; private set; } = Estado.Tranquilo;
        public bool EnCurso => EstadoActual == Estado.EnCurso;
        /// <summary>Hay un ataque avisado, en curso o con su resultado por leer.</summary>
        public bool Activo => EstadoActual != Estado.Tranquilo;
        public float SegundosAviso { get; private set; }
        public float TiempoRestante { get; private set; }
        public int CuantosVienen { get; private set; }
        public bool HayVigia { get; private set; }
        public ResultadoAsalto Resultado { get; private set; }
        /// <summary>Segundos de ataque transcurridos; es el reloj de los combatientes y las torres.</summary>
        public float Reloj { get; private set; }
        /// <summary>El ataque actual (o su resultado) ocurrió mientras el juego estaba cerrado.</summary>
        public bool Ausente { get; private set; }
        public long EscudoHastaUtcTicks { get; private set; }
        public long UltimoAtaqueUtcTicks { get; private set; }
        public bool UltimoAtaqueDefendido { get; private set; }
        public float SegundosEscudo
        {
            get
            {
                long diferencia = EscudoHastaUtcTicks - System.DateTime.UtcNow.Ticks;
                return diferencia > 0 ? (float)System.TimeSpan.FromTicks(diferencia).TotalSeconds : 0f;
            }
        }
        public float SegundosDesdeUltimoAtaque
        {
            get
            {
                if (UltimoAtaqueUtcTicks <= 0) return -1f;
                long diferencia = System.DateTime.UtcNow.Ticks - UltimoAtaqueUtcTicks;
                return diferencia > 0 ? (float)System.TimeSpan.FromTicks(diferencia).TotalSeconds : 0f;
            }
        }
        public IReadOnlyList<Combatiente> Invasores => _invasores;
        public IReadOnlyList<Combatiente> Defensores => _defensores;
        public int InvasoresVivos
        {
            get
            {
                int vivos = 0;
                foreach (var invasor in _invasores)
                {
                    if (!invasor.Muerto) vivos++;
                }
                return vivos;
            }
        }

        private readonly List<Combatiente> _invasores = new List<Combatiente>();
        private readonly List<Combatiente> _defensores = new List<Combatiente>();
        private readonly Dictionary<Building, float> _proximoDisparo = new Dictionary<Building, float>();
        private float _proximo = -1f;
        private Vector3 _entrada;
        private Transform _marca;
        private int _cautivos;
        private int _vencidos;
        private bool _instantaneo; // se está resolviendo sin dibujar (ataque saltado)

        private void Update()
        {
            if (Manager == null || Manager.Pueblo == null) return;
            switch (EstadoActual)
            {
                case Estado.Tranquilo:
                    // Solo cuenta el tiempo jugando en la aldea, y no antes del tecpan 2.
                    if (Manager.ModoActual != GameManager.Modo.Aldea || Manager.NivelTecpan < 2) return;
                    if (_proximo < 0f) _proximo = Random.Range(PrimerAtaqueMin, PrimerAtaqueMax);
                    _proximo -= Time.deltaTime;
                    if (_proximo <= 0f) Avisar();
                    break;
                case Estado.Aviso:
                    SegundosAviso -= Time.deltaTime;
                    if (_marca != null) _marca.localScale = new Vector3(0.5f, 3f + Mathf.Sin(Time.time * 6f) * 0.5f, 0.5f);
                    if (SegundosAviso <= 0f) Empezar();
                    break;
                case Estado.EnCurso:
                    Avanzar(Time.deltaTime);
                    break;
            }
        }

        /// <summary>
        /// Un paso del ataque con reloj propio: los combatientes y las torres no usan Time, así el mismo
        /// código corre en vivo cada frame y, para un ataque saltado, en bucle dentro de un solo frame.
        /// </summary>
        private void Avanzar(float dt)
        {
            Reloj += dt;
            TiempoRestante -= dt;
            foreach (var invasor in _invasores) invasor.Avanzar(dt);
            foreach (var defensor in _defensores) defensor.Avanzar(dt);
            Disparar();
            if (InvasoresVivos == 0) Terminar(defendida: true);
            else if (TiempoRestante <= 0f || ObjetivoPara(Manager.Mapa.Centro, false) == null) Terminar(defendida: false);
        }

        // ---------- Ataques mientras el juego estaba cerrado ----------

        /// <summary>
        /// Al cargar la partida, decide si hubo un ataque durante la ausencia. Se llama después de aplicar
        /// la producción sin conexión, así parte de lo producido también está en juego. Con forzar, para probar.
        /// </summary>
        public bool PrepararAusente(float segundosFuera, bool forzar = false)
        {
            if (EstadoActual != Estado.Tranquilo || Manager.Pueblo == null || Manager.ModoActual != GameManager.Modo.Aldea) return false;
            if (!forzar)
            {
                if (Manager.NivelTecpan < 2 || segundosFuera < AusenciaMinima || SegundosEscudo > 0f) return false;
                float probabilidad = segundosFuera >= AusenciaSegura ? 1f
                    : segundosFuera >= AusenciaMedia
                        ? Mathf.Lerp(ProbabilidadMedia, 1f, (segundosFuera - AusenciaMedia) / (AusenciaSegura - AusenciaMedia))
                        : Mathf.Lerp(ProbabilidadMinima, ProbabilidadMedia, (segundosFuera - AusenciaMinima) / (AusenciaMedia - AusenciaMinima));
                if (Random.value >= probabilidad) return false;
            }
            int tecpan = Mathf.Clamp(Manager.NivelTecpan, 1, InvasoresPorTecpan.Length);
            CuantosVienen = InvasoresPorTecpan[tecpan - 1];
            HayVigia = TorresListas() > 0;
            _entrada = PuntoDeEntrada();
            Ausente = true;
            EstadoActual = Estado.Pendiente;
            return true;
        }

        /// <summary>Ver el ataque que ocurrió sin conexión: corre en la aldea como uno en vivo, con un aviso corto.</summary>
        public void VerAusente()
        {
            if (EstadoActual != Estado.Pendiente) return;
            Avisar(conservarEntrada: true);
            SegundosAviso = AvisoAusente;
        }

        /// <summary>Saltar el ataque: la misma simulación, resuelta en este mismo frame sin dibujarla.</summary>
        public void SaltarAusente()
        {
            if (EstadoActual != Estado.Pendiente) return;
            _instantaneo = true;
            Empezar();
            // Tope por seguridad: la duración máxima más un margen.
            int pasos = Mathf.CeilToInt(Duracion / PasoInstantaneo) + 10;
            while (EstadoActual == Estado.EnCurso && pasos-- > 0) Avanzar(PasoInstantaneo);
            if (EstadoActual == Estado.EnCurso) Terminar(defendida: false);
            _instantaneo = false;
        }

        public void Exportar(SaveData datos)
        {
            datos.escudoHastaUtcTicks = EscudoHastaUtcTicks;
            datos.ultimoAtaqueUtcTicks = UltimoAtaqueUtcTicks;
            datos.ultimoAtaqueDefendido = UltimoAtaqueDefendido;
            // Si se cierra el juego con el ataque sin resolver, al volver se vuelve a ofrecer.
            datos.ataquePendiente = Ausente && EstadoActual != Estado.Resultado;
            datos.invasoresPendientes = CuantosVienen;
        }

        public void Importar(SaveData datos)
        {
            EscudoHastaUtcTicks = datos.escudoHastaUtcTicks;
            UltimoAtaqueUtcTicks = datos.ultimoAtaqueUtcTicks;
            UltimoAtaqueDefendido = datos.ultimoAtaqueDefendido;
            if (datos.ataquePendiente && Manager.Pueblo != null && EstadoActual == Estado.Tranquilo)
            {
                CuantosVienen = Mathf.Max(1, datos.invasoresPendientes);
                HayVigia = TorresListas() > 0;
                _entrada = PuntoDeEntrada();
                Ausente = true;
                EstadoActual = Estado.Pendiente;
            }
        }

        /// <summary>Para probar: el aviso empieza ya (desde el menú del editor).</summary>
        public void ProvocarAhora()
        {
            if (EstadoActual != Estado.Tranquilo || Manager.Pueblo == null || Manager.ModoActual != GameManager.Modo.Aldea) return;
            Avisar();
        }

        private void Avisar(bool conservarEntrada = false)
        {
            int tecpan = Mathf.Clamp(Manager.NivelTecpan, 1, InvasoresPorTecpan.Length);
            CuantosVienen = InvasoresPorTecpan[tecpan - 1];
            int torres = TorresListas();
            HayVigia = torres > 0;
            SegundosAviso = Mathf.Min(AvisoMaximo, AvisoBase + AvisoPorTorre * torres);
            if (!conservarEntrada) _entrada = PuntoDeEntrada();
            EstadoActual = Estado.Aviso;
            Manager.AlAvisarAsalto();
            // La torre de vigía marca por dónde vienen.
            if (HayVigia)
            {
                var marca = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(marca.GetComponent<Collider>());
                marca.name = "Por aquí vienen";
                marca.transform.SetParent(transform, false);
                marca.transform.position = _entrada + Vector3.up * 1.5f;
                marca.GetComponent<Renderer>().material.color = new Color(0.85f, 0.15f, 0.1f);
                _marca = marca.transform;
            }
        }

        private void Empezar()
        {
            if (_marca != null) Destroy(_marca.gameObject);
            _marca = null;
            EstadoActual = Estado.EnCurso;
            TiempoRestante = Duracion;
            Reloj = 0f;
            _cautivos = 0;
            _vencidos = 0;
            _proximoDisparo.Clear();

            int tecpan = Mathf.Clamp(Manager.NivelTecpan, 1, 5);
            int rangoMaximo = tecpan >= 5 ? 2 : tecpan >= 3 ? 1 : 0;
            // Llegan en grupo, a lo largo de unas casillas del borde.
            Vector3 lado = Mathf.Abs(_entrada.x - Manager.Mapa.Centro.x) > Mathf.Abs(_entrada.z - Manager.Mapa.Centro.z)
                ? Vector3.forward
                : Vector3.right;
            for (int i = 0; i < CuantosVienen; i++)
            {
                var tipo = Mezcla[i % Mezcla.Length];
                int rango = Random.Range(0, rangoMaximo + 1);
                var posicion = _entrada + lado * Random.Range(-2.5f, 2.5f) + Random.insideUnitSphere.WithY0() * 0.4f;
                _invasores.Add(Crear(TroopCatalog.Get(tipo), rango, invasor: true, posicion));
            }

            // Tus defensores salen del tecpan, donde se resguardó el ejército.
            var puesto = PuestoDefensores();
            foreach (var tropa in TroopCatalog.Todos)
            {
                for (int r = 0; r < Rangos.Count; r++)
                {
                    int cuantos = Manager.DefensoresListos(tropa.Id, r);
                    for (int n = 0; n < cuantos; n++)
                    {
                        var posicion = puesto + Random.insideUnitSphere.WithY0() * 1.2f;
                        _defensores.Add(Crear(tropa, r, invasor: false, posicion));
                    }
                }
            }
            Manager.AlEmpezarAsalto();
        }

        private Combatiente Crear(TroopDefinition definicion, int rango, bool invasor, Vector3 posicion)
        {
            var combatiente = new GameObject().AddComponent<Combatiente>();
            combatiente.transform.SetParent(transform, false);
            combatiente.Inicializar(definicion, rango, invasor, Manager.Pueblo, Manager.Culto, posicion, this);
            return combatiente;
        }

        private void Terminar(bool defendida)
        {
            EstadoActual = Estado.Resultado;
            var resultado = new ResultadoAsalto
            {
                Defendida = defendida,
                Invasores = _invasores.Count,
                Vencidos = _vencidos,
                Defensores = _defensores.Count,
                Cautivos = _cautivos,
            };

            // Se llevan recursos en proporción a lo que derribaron.
            int edificios = 0;
            foreach (var edificio in Manager.Edificios)
            {
                if (edificio.Definicion.Id == BuildingId.Muralla) continue;
                edificios++;
                if (edificio.Derribado) resultado.Derribados++;
            }
            float fraccion = edificios > 0 ? (float)resultado.Derribados / edificios : 0f;
            foreach (var recurso in new[] { ResourceType.Maiz, ResourceType.Madera, ResourceType.Obsidiana })
            {
                int perdida = Mathf.FloorToInt(Manager.Banco.Get(recurso) * PerdidaMaxima * fraccion);
                resultado.Perdidas[(int)recurso] = perdida;
                if (perdida > 0) Manager.Banco.Establecer(recurso, Manager.Banco.GetExacto(recurso) - perdida);
            }
            if (_cautivos > 0) Manager.Banco.Add(ResourceType.Cautivos, _cautivos);

            // Los defensores que cayeron se pierden; los heridos van al temazcalli. Los que pelearon
            // y sobrevivieron suben de rango como al volver de batalla (tlamani si capturaron).
            foreach (var defensor in _defensores)
            {
                var id = defensor.Definicion.Id;
                if (defensor.Muerto)
                {
                    resultado.Caidos++;
                    Manager.Ejercito.Quitar(id, defensor.Rango);
                    Manager.AlCaerTropa(defensor.Definicion);
                    continue;
                }
                int nuevo = defensor.Peleo ? Rangos.AlRegresar(defensor.Rango, defensor.Capturas) : defensor.Rango;
                bool herido = defensor.FraccionVida < 0.999f;
                if (herido) resultado.Heridos++;
                if (nuevo > defensor.Rango) resultado.Ascensos[nuevo]++;
                if ((herido || nuevo != defensor.Rango) && Manager.Ejercito.Quitar(id, defensor.Rango))
                {
                    Manager.Ejercito.Regresar(id, nuevo, defensor.FraccionVida);
                }
            }
            Resultado = resultado;
            UltimoAtaqueUtcTicks = System.DateTime.UtcNow.Ticks;
            UltimoAtaqueDefendido = defendida;
            if (Ausente)
            {
                // Escudo: un tiempo sin otro ataque sin conexión; más largo si arrasaron la aldea.
                bool saqueo = edificios > 0 && resultado.Derribados * 2 >= edificios;
                EscudoHastaUtcTicks = System.DateTime.UtcNow.AddHours(saqueo ? EscudoHorasSaqueo : EscudoHoras).Ticks;
            }
            Limpiar();
            Manager.Guardar();
        }

        /// <summary>Al cerrar el resultado: todo se repara y la aldea vuelve a la calma.</summary>
        public void Cerrar()
        {
            if (EstadoActual != Estado.Resultado) return;
            // Los derribados se levantan poco a poco, unos después de otros.
            foreach (var edificio in Manager.Edificios) edificio.Reparar(Random.Range(0f, 4f));
            Resultado = null;
            Ausente = false;
            EstadoActual = Estado.Tranquilo;
            _proximo = Random.Range(EntreAtaquesMin, EntreAtaquesMax);
            Manager.AlTerminarAsalto();
        }

        private void Limpiar()
        {
            foreach (var combatiente in _invasores) Destroy(combatiente.gameObject);
            foreach (var combatiente in _defensores) Destroy(combatiente.gameObject);
            _invasores.Clear();
            _defensores.Clear();
            if (_marca != null) Destroy(_marca.gameObject);
            _marca = null;
        }

        /// <summary>Si se cambia de pueblo o se borra la partida a medio ataque.</summary>
        public void Cancelar()
        {
            Limpiar();
            Resultado = null;
            Ausente = false;
            EstadoActual = Estado.Tranquilo;
            _proximo = -1f;
        }

        public void AlCaer(Combatiente caido, Combatiente atacante)
        {
            if (!caido.Invasor) return;
            _vencidos++;
            // Como en batalla, al vencer a un invasor se puede tomar un cautivo.
            if (atacante != null && !atacante.Invasor && !atacante.Muerto
                && Random.value < Rangos.ProbabilidadCapturaGuerrero(Manager.Pueblo, atacante.Rango))
            {
                atacante.Capturas++;
                _cautivos++;
            }
        }

        /// <summary>El combatiente vivo más cercano del bando pedido, dentro del alcance; o null.</summary>
        public Combatiente MasCercano(Vector3 desde, bool invasores, float alcance)
        {
            Combatiente mejor = null;
            float mejorDistancia = alcance >= float.MaxValue ? float.MaxValue : alcance * alcance;
            foreach (var combatiente in invasores ? _invasores : _defensores)
            {
                if (combatiente.Muerto) continue;
                float distancia = (combatiente.transform.position - desde).sqrMagnitude;
                if (distancia <= mejorDistancia)
                {
                    mejorDistancia = distancia;
                    mejor = combatiente;
                }
            }
            return mejor;
        }

        /// <summary>Si hay un yaotequihua (élite tlaxcalteca o acolhua) cerca del defensor, pega más.</summary>
        public float MultiplicadorLider(Combatiente defensor)
        {
            float radio = Rangos.RadioLider * Rangos.RadioLider;
            foreach (var otro in _defensores)
            {
                if (otro == null || otro == defensor || otro.Muerto || !Rangos.EsLider(Manager.Pueblo, otro.Rango)) continue;
                if ((otro.transform.position - defensor.transform.position).sqrMagnitude <= radio) return 1f + Rangos.BonoLider;
            }
            return 1f;
        }

        /// <summary>El edificio en pie más cercano (sin murallas); los de alcance prefieren torres.</summary>
        public Building ObjetivoPara(Vector3 desde, bool prefiereDefensas)
        {
            Building mejor = null, mejorDefensa = null;
            float distancia = float.MaxValue, distanciaDefensa = float.MaxValue;
            foreach (var edificio in Manager.Edificios)
            {
                if (edificio == null || edificio.Derribado || edificio.Definicion.Id == BuildingId.Muralla) continue;
                float d = (edificio.transform.position - desde).sqrMagnitude;
                if (d < distancia)
                {
                    distancia = d;
                    mejor = edificio;
                }
                if (edificio.Definicion.EsDefensa && d < distanciaDefensa)
                {
                    distanciaDefensa = d;
                    mejorDefensa = edificio;
                }
            }
            return prefiereDefensas && mejorDefensa != null ? mejorDefensa : mejor;
        }

        /// <summary>La muralla en pie en ese punto, si hay.</summary>
        public Building MuroEn(Vector3 punto)
        {
            var casilla = Manager.Mapa.MundoACasilla(punto);
            if (!Manager.Mapa.DentroDelMapa(casilla)) return null;
            var edificio = Manager.Mapa.En(casilla);
            return edificio != null && edificio.Definicion.Id == BuildingId.Muralla && !edificio.Derribado ? edificio : null;
        }

        public void DanarEdificio(Building edificio, float cantidad)
        {
            edificio.RecibirDanoAsalto(cantidad);
        }

        /// <summary>Las torres de vigía terminadas disparan al invasor más cercano.</summary>
        private void Disparar()
        {
            foreach (var torre in Manager.Edificios)
            {
                if (!torre.Definicion.EsDefensa || torre.Nivel <= 0 || torre.Derribado) continue;
                if (_proximoDisparo.TryGetValue(torre, out float cuando) && Reloj < cuando) continue;
                var objetivo = MasCercano(torre.transform.position, invasores: true, torre.Definicion.AlcanceDefensa + torre.Radio);
                if (objetivo == null) continue;
                float mult = BuildingDefinition.Multiplicador(torre.Nivel);
                if (Manager.Culto != null) mult *= Manager.Culto.DefensasPorFavor;
                objetivo.RecibirDano(torre.Definicion.DanoDefensaPorSegundo * mult * SegundosEntreDisparos, null);
                _proximoDisparo[torre] = Reloj + SegundosEntreDisparos;
                if (!_instantaneo) Dardo(torre.transform.position + Vector3.up * torre.AlturaModelo, objetivo.transform.position + Vector3.up * 0.3f);
            }
        }

        /// <summary>Una raya breve de la torre al invasor, para ver el disparo.</summary>
        private void Dardo(Vector3 desde, Vector3 hasta)
        {
            var dardo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(dardo.GetComponent<Collider>());
            dardo.name = "Dardo";
            dardo.transform.SetParent(transform, false);
            dardo.transform.position = (desde + hasta) * 0.5f;
            dardo.transform.LookAt(hasta);
            dardo.transform.localScale = new Vector3(0.04f, 0.04f, Vector3.Distance(desde, hasta));
            dardo.GetComponent<Renderer>().material.color = new Color(0.15f, 0.12f, 0.1f);
            Destroy(dardo, 0.12f);
        }

        private int TorresListas()
        {
            int torres = 0;
            foreach (var edificio in Manager.Edificios)
            {
                if (edificio.Definicion.EsDefensa && edificio.Nivel > 0) torres++;
            }
            return torres;
        }

        /// <summary>Un punto en el borde del mapa, de un lado al azar.</summary>
        private Vector3 PuntoDeEntrada()
        {
            float lado = GameManager.TamanoMapa;
            float a = Random.Range(4f, lado - 4f);
            switch (Random.Range(0, 4))
            {
                case 0: return new Vector3(a, 0f, 0.5f);
                case 1: return new Vector3(a, 0f, lado - 0.5f);
                case 2: return new Vector3(0.5f, 0f, a);
                default: return new Vector3(lado - 0.5f, 0f, a);
            }
        }

        private Vector3 PuestoDefensores()
        {
            foreach (var edificio in Manager.Edificios)
            {
                if (edificio.Definicion.Id == BuildingId.Tecpan) return edificio.transform.position;
            }
            return Manager.Mapa.Centro;
        }
    }

    internal static class VectorExtensiones
    {
        public static Vector3 WithY0(this Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
