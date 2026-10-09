using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    public sealed class ResultadoBatalla
    {
        public float Porcentaje;   // 0..1 de edificios destruidos (sin contar murallas)
        public int Estrellas;      // 0 a 3, como en Clash: 50%, tecpan y 100%
        public bool Victoria;      // al menos una estrella
        public int[] Botin = ResourceInfo.Costo();
        public int Plumas;         // solo la primera vez que se gana cada nivel
        public bool TecpanDestruido;
        public int Cautivos;       // mamaltin capturados (ya incluidos en Botin)
        public int Regresan;       // tropas desplegadas que sobrevivieron
        public int Heridos;        // de las que regresan, cuántas vienen heridas
        public int[] Ascensos = new int[Rangos.Count]; // cuántas subieron a cada rango
        public int DefensoresDerrotados;
        public bool AvanzaHistoria; // se perdió, pero el capítulo cuenta (así pasó en la historia)
    }

    /// <summary>
    /// Una batalla contra una aldea de la campaña. Se arma lejos de la aldea del jugador
    /// (que sigue produciendo mientras tanto) y la cámara se mueve hasta aquí.
    /// </summary>
    public sealed class BattleManager : MonoBehaviour
    {
        public static readonly Vector3 Origen = new Vector3(100f, 0f, 0f);
        public const float SegundosLimite = 120f;
        public const float VictoriaMinima = 0.5f;
        private const int MargenDespliegue = 8; // casillas alrededor del mapa donde también se puede desplegar (como en Clash, casi todo el campo)

        public int IndiceNivel { get; private set; }
        public CampaignLevel Nivel { get; private set; }
        public bool Terminada { get; private set; }
        public ResultadoBatalla Resultado { get; private set; }
        public float TiempoRestante { get; private set; }
        public TroopId Seleccionada { get; set; }

        public Vector3 Centro => Origen + new Vector3(CampaignLevel.TamanoMapa * 0.5f, 0f, CampaignLevel.TamanoMapa * 0.5f);

        /// <summary>Derribar el tecpan da una estrella.</summary>
        public bool TecpanDestruido { get; private set; }
        public IReadOnlyList<EnemyBuilding> Edificios => _edificios;
        public IReadOnlyList<TroopUnit> Tropas => _tropas;
        public IReadOnlyList<Defensor> Defensores => _defensores;

        /// <summary>Parte destruida de la ciudad. Las murallas no cuentan, como en Clash.</summary>
        public float Destruccion
        {
            get
            {
                int total = 0;
                int destruidos = 0;
                foreach (var edificio in _edificios)
                {
                    if (edificio.Definicion.Id == BuildingId.Muralla) continue;
                    total++;
                    if (edificio.Destruido) destruidos++;
                }
                return total == 0 ? 1f : (float)destruidos / total;
            }
        }

        /// <summary>Una estrella por llegar al 50%, otra por derribar el tecpan y otra por destruirlo todo.</summary>
        public int Estrellas
        {
            get
            {
                float destruccion = Destruccion;
                int estrellas = 0;
                if (destruccion >= VictoriaMinima) estrellas++;
                if (TecpanDestruido) estrellas++;
                if (destruccion >= 1f) estrellas++;
                return estrellas;
            }
        }

        private readonly List<EnemyBuilding> _edificios = new List<EnemyBuilding>();
        private readonly List<TroopUnit> _tropas = new List<TroopUnit>();
        private readonly List<Defensor> _defensores = new List<Defensor>();
        private int _defensoresDerrotados;
        private bool[,] _ocupado;
        private GameManager _manager;
        private Army _ejercito;
        // Tropas que se eligieron para esta batalla, por tipo y rango, más las heridas que se llevaron;
        // lo que se entrene o se cure mientras tanto se queda en la aldea.
        private readonly int[,] _reserva = new int[TroopCatalog.Count, Rangos.Count];
        private readonly List<Herido> _reservaHeridos = new List<Herido>();
        private int _cautivos;

        public int Cautivos => _cautivos;

        public int Disponibles(TroopId id, int rango)
        {
            int total = _reserva[(int)id, rango];
            foreach (var herido in _reservaHeridos)
            {
                if (herido.tipo == id && herido.rango == rango) total++;
            }
            return total;
        }

        public int Disponibles(TroopId id)
        {
            int total = 0;
            for (int r = 0; r < Rangos.Count; r++) total += Disponibles(id, r);
            return total;
        }

        public int HeridosDisponibles(TroopId id)
        {
            int total = 0;
            foreach (var herido in _reservaHeridos)
            {
                if (herido.tipo == id) total++;
            }
            return total;
        }

        public int TotalDisponibles
        {
            get
            {
                int total = 0;
                for (int t = 0; t < TroopCatalog.Count; t++) total += Disponibles((TroopId)t);
                return total;
            }
        }
        private bool _algunaDesplegada;

        public void Empezar(GameManager manager, int indiceNivel, SeleccionEjercito seleccion)
        {
            _manager = manager;
            _ejercito = manager.Ejercito;
            foreach (var definicion in TroopCatalog.Todos)
            {
                for (int r = 0; r < Rangos.Count; r++)
                {
                    int i = Army.Indice(definicion.Id, r);
                    for (int n = 0; n < seleccion.Sanos[i] && _ejercito.Quitar(definicion.Id, r); n++)
                    {
                        _reserva[(int)definicion.Id, r]++;
                    }
                    _reservaHeridos.AddRange(_ejercito.SacarHeridos(definicion.Id, r, seleccion.Heridos[i]));
                }
            }
            IndiceNivel = indiceNivel;
            Nivel = manager.Campana[indiceNivel];
            TiempoRestante = SegundosLimite;
            Seleccionada = PrimeraTropaDisponible();

            int tamano = CampaignLevel.TamanoMapa;
            _ocupado = new bool[tamano, tamano];

            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo enemigo";
            Destroy(suelo.GetComponent<Collider>());
            float lado = tamano + MargenDespliegue * 2;
            suelo.transform.SetParent(transform, false);
            suelo.transform.localScale = new Vector3(lado / 10f, 1f, lado / 10f);
            suelo.transform.position = Centro;
            suelo.GetComponent<Renderer>().material.color = new Color(0.55f, 0.50f, 0.36f);

            foreach (var datos in Nivel.Edificios)
            {
                var definicion = BuildingCatalog.Get(datos.Id);
                var edificio = new GameObject().AddComponent<EnemyBuilding>();
                edificio.transform.SetParent(transform, false);
                var centro = Origen + new Vector3(datos.X + definicion.Tamano * 0.5f, 0f, datos.Y + definicion.Tamano * 0.5f);
                edificio.Inicializar(definicion, datos.Nivel, centro, this);
                _edificios.Add(edificio);

                for (int x = datos.X; x < datos.X + definicion.Tamano; x++)
                {
                    for (int y = datos.Y; y < datos.Y + definicion.Tamano; y++)
                    {
                        if (x >= 0 && y >= 0 && x < tamano && y < tamano) _ocupado[x, y] = true;
                    }
                }
            }
            CrearDefensores();
            CrearZonaProhibida();
        }

        public TroopId PrimeraTropaDisponible()
        {
            foreach (var definicion in TroopCatalog.Todos)
            {
                if (Disponibles(definicion.Id) > 0) return definicion.Id;
            }
            return TroopId.Macuahuitl;
        }

        /// <summary>Despliega la tropa seleccionada en ese punto del mundo, si se puede.</summary>
        /// <summary>¿Se puede soltar una tropa en ese punto? En el campo, fuera de las casillas con edificios.</summary>
        public bool PuedeDesplegarEn(Vector3 punto)
        {
            int x = Mathf.FloorToInt(punto.x - Origen.x);
            int y = Mathf.FloorToInt(punto.z - Origen.z);
            int tamano = CampaignLevel.TamanoMapa;
            if (x < -MargenDespliegue || y < -MargenDespliegue
                || x >= tamano + MargenDespliegue || y >= tamano + MargenDespliegue) return false;
            return !(x >= 0 && y >= 0 && x < tamano && y < tamano && _ocupado[x, y]);
        }

        /// <summary>Por un momento se marcan las casillas donde no se puede desplegar, como en Clash.</summary>
        public void MostrarZonaProhibida()
        {
            if (_zonaProhibida == null) return;
            _zonaProhibida.SetActive(true);
            _ocultarZonaEn = Time.time + 1.5f;
        }

        private GameObject _zonaProhibida;
        private float _ocultarZonaEn;

        private void CrearZonaProhibida()
        {
            _zonaProhibida = new GameObject("Zona prohibida");
            _zonaProhibida.transform.SetParent(transform, false);
            int tamano = CampaignLevel.TamanoMapa;
            for (int x = 0; x < tamano; x++)
            {
                for (int y = 0; y < tamano; y++)
                {
                    if (!_ocupado[x, y]) continue;
                    var casilla = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(casilla.GetComponent<Collider>());
                    casilla.transform.SetParent(_zonaProhibida.transform, false);
                    casilla.transform.localScale = new Vector3(0.98f, 0.02f, 0.98f);
                    casilla.transform.position = Origen + new Vector3(x + 0.5f, 0.01f, y + 0.5f);
                    casilla.GetComponent<Renderer>().material.color = new Color(0.60f, 0.16f, 0.12f);
                }
            }
            _zonaProhibida.SetActive(false);
        }

        public void Desplegar(Vector3 punto)
        {
            if (Terminada || !PuedeDesplegarEn(punto)) return;
            // Salen primero los de mayor rango; dentro del mismo rango, los sanos y luego los heridos con más vida.
            int rango = -1;
            for (int r = Rangos.Count - 1; r >= 0; r--)
            {
                if (Disponibles(Seleccionada, r) > 0)
                {
                    rango = r;
                    break;
                }
            }
            if (rango < 0)
            {
                _manager.MostrarMensaje("No te quedan yaoquizqueh de ese tipo");
                return;
            }

            float vida = 1f;
            if (_reserva[(int)Seleccionada, rango] > 0)
            {
                _reserva[(int)Seleccionada, rango]--;
            }
            else
            {
                Herido mejor = null;
                foreach (var herido in _reservaHeridos)
                {
                    if (herido.tipo != Seleccionada || herido.rango != rango) continue;
                    if (mejor == null || herido.vida > mejor.vida) mejor = herido;
                }
                _reservaHeridos.Remove(mejor);
                vida = mejor.vida;
            }
            var tropa = new GameObject().AddComponent<TroopUnit>();
            tropa.transform.SetParent(transform, false);
            tropa.Inicializar(TroopCatalog.Get(Seleccionada), rango, vida, _manager.Pueblo, _manager.Culto,
                new Vector3(punto.x, 0f, punto.z), this);
            _tropas.Add(tropa);
            _algunaDesplegada = true;

            if (Disponibles(Seleccionada) == 0) Seleccionada = PrimeraTropaDisponible();
        }

        private void Update()
        {
            if (_zonaProhibida != null && _zonaProhibida.activeSelf && Time.time > _ocultarZonaEn) _zonaProhibida.SetActive(false);
            if (Terminada) return;

            TiempoRestante -= Time.deltaTime;
            bool todoDestruido = Destruccion >= 1f;
            bool sinTropas = _algunaDesplegada && _tropas.Count == 0 && TotalDisponibles == 0;
            if (TiempoRestante <= 0f || todoDestruido || sinTropas) Terminar();
        }

        /// <summary>Termina la batalla (también al retirarse) y entrega el resultado.</summary>
        public void Terminar()
        {
            if (Terminada) return;
            Terminada = true;
            TiempoRestante = Mathf.Max(0f, TiempoRestante);

            float porcentaje = Destruccion;
            var resultado = new ResultadoBatalla
            {
                Porcentaje = porcentaje,
                Estrellas = Estrellas,
                Victoria = Estrellas > 0,
                TecpanDestruido = TecpanDestruido,
                Cautivos = _cautivos,
                DefensoresDerrotados = _defensoresDerrotados,
            };
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                resultado.Botin[i] = Mathf.FloorToInt(Nivel.Botin[i] * porcentaje * Nivel.ParteBotin);
            }
            resultado.Botin[(int)ResourceType.Cautivos] = _cautivos;

            // Las tropas que no se desplegaron regresan tal cual; las que sobrevivieron, con su nuevo rango
            // y con la vida que les quedó: si vienen heridas van al temazcalli.
            DevolverReserva();
            foreach (var tropa in _tropas)
            {
                if (tropa == null || tropa.Muerta) continue;
                int nuevo = Rangos.AlRegresar(tropa.Rango, tropa.Capturas);
                if (nuevo > tropa.Rango) resultado.Ascensos[nuevo]++;
                float vida = tropa.FraccionVida;
                _ejercito.Regresar(tropa.Definicion.Id, nuevo, vida);
                resultado.Regresan++;
                if (vida < 0.999f) resultado.Heridos++;
            }
            Resultado = resultado;
            _manager.AlTerminarBatalla(this);
        }

        /// <summary>Suma al guardado las tropas que aún volverían (sanas por tipo y rango, y heridas).</summary>
        public void SumarTropasPendientes(SaveData datos)
        {
            var porRango = datos.tropasPorRango;
            for (int t = 0; t < TroopCatalog.Count; t++)
            {
                for (int r = 0; r < Rangos.Count; r++) porRango[Army.Indice((TroopId)t, r)] += _reserva[t, r];
            }
            foreach (var herido in _reservaHeridos) datos.heridos.Add(new Herido(herido.tipo, herido.rango, herido.vida));
            foreach (var tropa in _tropas)
            {
                if (tropa == null || tropa.Muerta) continue;
                float vida = tropa.FraccionVida;
                if (vida >= 0.999f) porRango[Army.Indice(tropa.Definicion.Id, tropa.Rango)]++;
                else datos.heridos.Add(new Herido(tropa.Definicion.Id, tropa.Rango, vida));
            }
        }

        private void DevolverReserva()
        {
            for (int t = 0; t < TroopCatalog.Count; t++)
            {
                for (int r = 0; r < Rangos.Count; r++)
                {
                    _ejercito.Agregar((TroopId)t, r, _reserva[t, r]);
                    _reserva[t, r] = 0;
                }
            }
            foreach (var herido in _reservaHeridos) _ejercito.Regresar(herido.tipo, herido.rango, herido.vida);
            _reservaHeridos.Clear();
        }

        public EnemyBuilding ObjetivoPara(Vector3 desde, bool prefiereDefensas)
        {
            if (prefiereDefensas)
            {
                var defensa = MasCercano(desde, soloDefensas: true);
                if (defensa != null) return defensa;
            }
            return MasCercano(desde, soloDefensas: false);
        }

        private EnemyBuilding MasCercano(Vector3 desde, bool soloDefensas)
        {
            EnemyBuilding mejor = null;
            float mejorDistancia = float.MaxValue;
            foreach (var edificio in _edificios)
            {
                if (edificio.Destruido || (soloDefensas && !edificio.EsDefensa)) continue;
                float distancia = (edificio.transform.position - desde).sqrMagnitude;
                if (distancia < mejorDistancia)
                {
                    mejorDistancia = distancia;
                    mejor = edificio;
                }
            }
            return mejor;
        }

        public TroopUnit TropaMasCercana(Vector3 desde, float alcance)
        {
            TroopUnit mejor = null;
            float mejorDistancia = alcance * alcance;
            foreach (var tropa in _tropas)
            {
                if (tropa == null || tropa.Muerta) continue;
                float distancia = (tropa.transform.position - desde).sqrMagnitude;
                if (distancia <= mejorDistancia)
                {
                    mejorDistancia = distancia;
                    mejor = tropa;
                }
            }
            return mejor;
        }

        /// <summary>Quien derriba un edificio puede hacer una captura y traer un malli (cautivo).</summary>
        public void AlDestruirEdificio(EnemyBuilding edificio, TroopUnit atacante)
        {
            if (atacante != null && !atacante.Muerta && Random.value < Rangos.ProbabilidadCaptura(_manager.Pueblo))
            {
                atacante.Capturas++;
                _cautivos++;
            }

            // Derribar el tecpan da una estrella; la batalla sigue.
            if (edificio.Definicion.Id == BuildingId.Tecpan) TecpanDestruido = true;
        }

        /// <summary>Al vencer a un defensor también se le puede tomar cautivo.</summary>
        public void AlMorirDefensor(Defensor defensor, TroopUnit atacante)
        {
            _defensores.Remove(defensor);
            _defensoresDerrotados++;
            if (atacante != null && !atacante.Muerta && Random.value < Rangos.ProbabilidadCaptura(_manager.Pueblo))
            {
                atacante.Capturas++;
                _cautivos++;
            }
        }

        /// <summary>El defensor vivo más cercano dentro del alcance, o null.</summary>
        public Defensor DefensorCercano(Vector3 desde, float alcance)
        {
            Defensor mejor = null;
            float mejorDistancia = alcance * alcance;
            foreach (var defensor in _defensores)
            {
                if (defensor == null || defensor.Muerto) continue;
                float distancia = (defensor.transform.position - desde).sqrMagnitude;
                if (distancia <= mejorDistancia)
                {
                    mejorDistancia = distancia;
                    mejor = defensor;
                }
            }
            return mejor;
        }

        /// <summary>Los defensores esperan en círculo alrededor del tecpan.</summary>
        private void CrearDefensores()
        {
            int total = 0;
            foreach (var grupo in Nivel.Defensores) total += grupo.Cantidad;
            if (total == 0) return;

            Vector3 centro = Centro;
            foreach (var edificio in _edificios)
            {
                if (edificio.Definicion.Id == BuildingId.Tecpan) centro = edificio.transform.position;
            }
            centro.y = 0f;

            int i = 0;
            foreach (var grupo in Nivel.Defensores)
            {
                for (int n = 0; n < grupo.Cantidad; n++, i++)
                {
                    float angulo = i * Mathf.PI * 2f / total;
                    var puesto = centro + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * 2.4f;
                    var defensor = new GameObject().AddComponent<Defensor>();
                    defensor.transform.SetParent(transform, false);
                    defensor.Inicializar(TroopCatalog.Get(grupo.Tipo), grupo.Rango, puesto, this);
                    _defensores.Add(defensor);
                }
            }
        }

        public void AlMorirTropa(TroopUnit tropa)
        {
            _tropas.Remove(tropa);
            _manager.AlCaerTropa(tropa.Definicion);
        }
    }
}
