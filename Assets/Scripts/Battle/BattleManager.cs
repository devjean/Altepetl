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
        public Comandante Comandante;      // el que fue, o null
        public bool ComandanteSalio;       // llegó a desplegarse
        public bool ComandanteCayo;        // cayó en el campo: vuelve herido al cuauhcalli
        public int ComandanteExperiencia;  // la que ganó en esta batalla
        public int AliadosCaidos;
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
        private const int MargenAgua = 2;       // y aún se puede soltar tropas en las primeras casillas del agua, fuera del campo

        public int IndiceNivel { get; private set; }
        public CampaignLevel Nivel { get; private set; }
        public bool Terminada { get; private set; }
        public ResultadoBatalla Resultado { get; private set; }
        public float TiempoRestante { get; private set; }
        public TroopId Seleccionada { get; set; }

        public Vector3 Centro => Origen + new Vector3(CampaignLevel.TamanoMapa * 0.5f, 0f, CampaignLevel.TamanoMapa * 0.5f);

        /// <summary>El campo tiene agua (orilla o lago): las tropas van en acalli hasta desembarcar.</summary>
        public bool HayAgua => Nivel != null && Nivel.Escenario != Escenario.Tierra;

        /// <summary>
        /// ¿Ese punto está sobre el agua? En la orilla, el agua queda al este del campo; en el lago, todo
        /// alrededor del islote. La calzada, si hay, va seca.
        /// </summary>
        public bool EnAgua(Vector3 punto)
        {
            if (!HayAgua) return false;
            float x = punto.x - Origen.x, z = punto.z - Origen.z;
            int tamano = CampaignLevel.TamanoMapa;
            if (Nivel.Calzada && x >= tamano && Mathf.Abs(z - tamano * 0.5f) < AnchoCalzada * 0.5f) return false;
            if (Nivel.Escenario == Escenario.Orilla) return x > tamano + MargenOrilla;
            return x < -MargenOrilla || z < -MargenOrilla || x > tamano + MargenOrilla || z > tamano + MargenOrilla;
        }

        private const float MargenOrilla = 0.4f;  // franja de tierra que sobresale del campo antes del agua
        private const float AnchoCalzada = 1.2f;
        private static readonly Color ColorAgua = new Color(0.28f, 0.50f, 0.58f);
        private static readonly Color ColorTierra = new Color(0.55f, 0.50f, 0.36f);
        private static readonly Color ColorPiedra = new Color(0.62f, 0.60f, 0.55f);
        private static readonly Color ColorLodo = new Color(0.36f, 0.30f, 0.20f);
        private static readonly Color ColorSiembra = new Color(0.40f, 0.62f, 0.28f);
        private static readonly Color ColorTule = new Color(0.45f, 0.60f, 0.30f);

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

        // ---------- Comandante ----------

        /// <summary>El comandante que se trajo a esta batalla (null si ninguno).</summary>
        public Comandante Comandante { get; private set; }
        /// <summary>Su monito en el campo, cuando ya se desplegó y sigue en pie.</summary>
        public TroopUnit UnidadComandante { get; private set; }
        public bool ComandanteDesplegado { get; private set; }
        public bool ComandanteCaido { get; private set; }
        /// <summary>El siguiente toque en el campo suelta al comandante en vez de una tropa.</summary>
        public bool SeleccionComandante { get; set; }
        public int UsosHabilidad { get; private set; }
        public float HabilidadHasta { get; private set; } = -1f;
        public bool HabilidadActiva => Time.time < HabilidadHasta;
        public float SegundosHabilidad => Mathf.Max(0f, HabilidadHasta - Time.time);
        /// <summary>Se puede soltar todavía: vino, no ha salido y no ha caído.</summary>
        public bool ComandantePendiente => Comandante != null && !ComandanteDesplegado;
        public bool PuedeUsarHabilidad => UnidadComandante != null && !UnidadComandante.Muerta && UsosHabilidad > 0 && !HabilidadActiva && !Terminada;
        private bool _habilidadEnCurso;
        private int _capturasEnAura;
        private int _aliadosCaidos;

        public void Empezar(GameManager manager, int indiceNivel, SeleccionEjercito seleccion)
        {
            _manager = manager;
            _ejercito = manager.Ejercito;
            if (seleccion.Comandante >= 0 && seleccion.Comandante < manager.Comandantes.Count
                && manager.Comandantes[seleccion.Comandante].Disponible)
            {
                Comandante = manager.Comandantes[seleccion.Comandante];
                UsosHabilidad = Comandante.UsosHabilidad;
            }
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

            CrearTerreno(tamano);

            foreach (var datos in Nivel.Edificios)
            {
                var definicion = BuildingCatalog.Get(datos.Id);
                var edificio = new GameObject().AddComponent<EnemyBuilding>();
                edificio.transform.SetParent(transform, false);
                var centro = Origen + new Vector3(datos.X + definicion.Tamano * 0.5f, 0f, datos.Y + definicion.Tamano * 0.5f);
                edificio.Inicializar(definicion, datos.Nivel, centro, this, plataforma: Nivel.Escenario == Escenario.Lago);
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

        /// <summary>
        /// El suelo: tierra; o la orilla del lago, con el agua al este y tules; o un islote entre chinampas.
        /// Con calzada, una franja de piedra sale de la aldea hacia el este sobre el agua.
        /// </summary>
        private void CrearTerreno(int tamano)
        {
            float lado = tamano + MargenDespliegue * 2;
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo enemigo";
            Destroy(suelo.GetComponent<Collider>());
            suelo.transform.SetParent(transform, false);
            suelo.transform.localScale = new Vector3(lado / 10f, 1f, lado / 10f);
            suelo.transform.position = Centro + new Vector3(0f, Nivel.Escenario == Escenario.Lago ? -0.05f : 0f, 0f);
            suelo.GetComponent<Renderer>().material.color = Nivel.Escenario == Escenario.Lago ? ColorAgua : ColorTierra;

            var azar = new System.Random(IndiceNivel * 131 + 7);
            switch (Nivel.Escenario)
            {
                case Escenario.Orilla:
                {
                    // Tierra hasta un poco después del campo; de ahí al este, el agua con tules en la orilla.
                    float orilla = tamano + MargenOrilla;
                    float anchoAgua = lado * 0.5f - tamano * 0.5f + tamano - orilla;
                    Pieza(Origen + new Vector3(orilla + anchoAgua * 0.5f, -0.04f, tamano * 0.5f), new Vector3(anchoAgua, 0.04f, lado), ColorAgua);
                    for (int i = 0; i < 18; i++)
                    {
                        float z = -MargenDespliegue + (float)azar.NextDouble() * lado;
                        if (Nivel.Calzada && Mathf.Abs(z - tamano * 0.5f) < AnchoCalzada) continue;
                        Tule(Origen + new Vector3(orilla + 0.2f + (float)azar.NextDouble() * 0.8f, 0f, z), azar);
                    }
                    break;
                }
                case Escenario.Lago:
                {
                    // El islote de la aldea, de lodo y piedra, y chinampas alrededor.
                    float isla = tamano + MargenOrilla * 2f;
                    Pieza(Centro + new Vector3(0f, -0.06f, 0f), new Vector3(isla, 0.12f, isla), ColorTierra);
                    for (int i = 0; i < 16; i++)
                    {
                        float x = -MargenDespliegue + 1f + (float)azar.NextDouble() * (lado - 2f);
                        float z = -MargenDespliegue + 1f + (float)azar.NextDouble() * (lado - 2f);
                        var p = Origen + new Vector3(x, 0f, z);
                        if (!EnAgua(p) || (Nivel.Calzada && x > tamano && Mathf.Abs(z - tamano * 0.5f) < 2.5f)) continue;
                        if (x > -1.5f && x < tamano + 1.5f && z > -1.5f && z < tamano + 1.5f) continue;
                        Chinampa(p, i % 2 == 0, azar);
                    }
                    break;
                }
            }
            if (Nivel.Calzada)
            {
                float largo = lado * 0.5f - tamano * 0.5f;
                Pieza(Origen + new Vector3(tamano + largo * 0.5f, -0.04f, tamano * 0.5f), new Vector3(largo, 0.08f, AnchoCalzada), ColorPiedra);
            }
        }

        private void Chinampa(Vector3 p, bool larga, System.Random azar)
        {
            var tam = larga ? new Vector3(1.2f, 0.08f, 2.4f) : new Vector3(2.4f, 0.08f, 1.2f);
            Pieza(p + new Vector3(0f, 0.0f, 0f), tam, ColorLodo);
            Pieza(p + new Vector3(0f, 0.06f, 0f), new Vector3(tam.x * 0.8f, 0.04f, tam.z * 0.8f), ColorSiembra);
            // Un ahuejote en una esquina, como los que sujetan las chinampas.
            var esquina = new Vector3(tam.x * 0.45f, 0f, tam.z * 0.45f);
            Pieza(p + esquina + new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.45f, 0.12f), ColorSiembra * 0.85f, PrimitiveType.Cylinder);
        }

        private void Tule(Vector3 p, System.Random azar)
        {
            float alto = 0.3f + (float)azar.NextDouble() * 0.3f;
            Pieza(p + new Vector3(0f, alto * 0.5f, 0f), new Vector3(0.06f, alto * 0.5f, 0.06f), ColorTule, PrimitiveType.Cylinder);
        }

        private void Pieza(Vector3 posicion, Vector3 escala, Color color, PrimitiveType tipo = PrimitiveType.Cube)
        {
            var pieza = GameObject.CreatePrimitive(tipo);
            Destroy(pieza.GetComponent<Collider>());
            pieza.transform.SetParent(transform, false);
            pieza.transform.position = posicion;
            pieza.transform.localScale = escala;
            pieza.GetComponent<Renderer>().material.color = color;
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
            int margen = MargenDespliegue + MargenAgua;
            if (x < -margen || y < -margen || x >= tamano + margen || y >= tamano + margen) return false;
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
            if (SeleccionComandante)
            {
                SeleccionComandante = false;
                if (ComandantePendiente) DesplegarComandante(new Vector3(punto.x, 0f, punto.z));
                return;
            }
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
                _manager.MostrarMensaje($"No te quedan {Terminos.Tropas} de ese tipo");
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

        /// <summary>El comandante sale una sola vez por batalla; el capitán de los aliados llega acompañado.</summary>
        private void DesplegarComandante(Vector3 punto)
        {
            var unidad = new GameObject().AddComponent<TroopUnit>();
            unidad.transform.SetParent(transform, false);
            unidad.MarcarComandante(Comandante);
            unidad.Inicializar(DefinicionDe(Comandante), Rangos.Joven, 1f, _manager.Pueblo, _manager.Culto, punto, this);
            _tropas.Add(unidad);
            UnidadComandante = unidad;
            ComandanteDesplegado = true;
            _algunaDesplegada = true;
            CrearAliados(punto, Comandante.Aliados);
        }

        /// <summary>Los números del comandante como si fuera una tropa, para reutilizar el monito de batalla.</summary>
        private static TroopDefinition DefinicionDe(Comandante comandante)
        {
            return new TroopDefinition
            {
                Id = TroopId.Macuahuitl,
                NombreEspanol = comandante.Nombre,
                NombreNahuatl = comandante.Nombre,
                Vida = comandante.VidaMaxima,
                DanoPorSegundo = ReglasComandante.DanoPorSegundo,
                Alcance = ReglasComandante.Alcance,
                Velocidad = ReglasComandante.Velocidad,
                Color = comandante.Definicion.Color,
            };
        }

        /// <summary>Guerreros aliados (acolhuas): llegan en círculo alrededor del punto. Mueren de verdad y no capturan.</summary>
        private void CrearAliados(Vector3 centro, int cantidad)
        {
            for (int i = 0; i < cantidad; i++)
            {
                float angulo = i * Mathf.PI * 2f / Mathf.Max(1, cantidad);
                var punto = centro + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * 0.6f;
                var aliado = new GameObject().AddComponent<TroopUnit>();
                aliado.transform.SetParent(transform, false);
                aliado.MarcarAliado();
                aliado.Inicializar(TroopCatalog.Get(TroopId.Macuahuitl), Comandante.RangoAliados, 1f, _manager.Pueblo, _manager.Culto, punto, this);
                _tropas.Add(aliado);
            }
        }

        /// <summary>El botón de la habilidad: una vez por batalla (dos, el llamado a nivel 5).</summary>
        public void UsarHabilidad()
        {
            if (!PuedeUsarHabilidad) return;
            UsosHabilidad--;
            if (Comandante.Definicion.Habilidad == Habilidad.Refuerzos)
            {
                CrearAliados(UnidadComandante.transform.position, ReglasComandante.RefuerzosAliados);
                return;
            }
            HabilidadHasta = Time.time + Comandante.DuracionHabilidad;
            _habilidadEnCurso = true;
        }

        /// <summary>Al terminar la embestida a nivel 5, quienes estaban en el aura conservan parte del ímpetu.</summary>
        private void AlTerminarHabilidad()
        {
            _habilidadEnCurso = false;
            if (Comandante.Definicion.Habilidad != Habilidad.Embestida || !Comandante.TieneRasgo5) return;
            foreach (var tropa in _tropas)
            {
                if (tropa != null && !tropa.Muerta && EnAura(tropa)) tropa.BonoResidual = Mathf.Max(tropa.BonoResidual, ReglasComandante.EmbestidaResidualNivel5);
            }
        }

        /// <summary>¿La tropa está dentro del aura del comandante (y no es él)?</summary>
        public bool EnAura(TroopUnit tropa)
        {
            if (UnidadComandante == null || UnidadComandante.Muerta || tropa == UnidadComandante) return false;
            float radio = ReglasComandante.Radio * ReglasComandante.Radio;
            return (UnidadComandante.transform.position - tropa.transform.position).sqrMagnitude <= radio;
        }

        private bool HabilidadActivaDe(Habilidad habilidad) =>
            HabilidadActiva && Comandante != null && Comandante.Definicion.Habilidad == habilidad;

        /// <summary>Todo lo que multiplica el ataque de una tropa: yaotequihua cerca, aura del comandante y su habilidad.</summary>
        public float MultiplicadorAtaque(TroopUnit tropa)
        {
            float bono = MultiplicadorLider(tropa) - 1f;
            bool enAura = EnAura(tropa);
            if (enAura && Comandante.Definicion.Aura == TipoAura.Ataque) bono = Mathf.Max(bono, Comandante.Aura);
            float mult = 1f + bono + tropa.BonoResidual;
            if (enAura && HabilidadActivaDe(Habilidad.Embestida)) mult *= 1f + ReglasComandante.EmbestidaAtaque;
            if (HabilidadActivaDe(Habilidad.Llamado)) mult *= 1f + ReglasComandante.LlamadoAtaque;
            if (enAura && HabilidadActivaDe(Habilidad.OrdenAsalto))
            {
                mult *= 1f + ReglasComandante.OrdenAtaque;
                if (Comandante.TieneRasgo5 && tropa.Definicion.Alcance > 1f) mult *= 1f + ReglasComandante.OrdenDistanciaNivel5;
            }
            return mult;
        }

        /// <summary>Cuánto del daño recibe de verdad una tropa: las auras de resistencia y Cohesión / Cerrar filas lo bajan.</summary>
        public float MultiplicadorDanoRecibido(TroopUnit tropa)
        {
            if (Comandante == null || tropa == UnidadComandante) return 1f;
            bool enAura = EnAura(tropa);
            float reduccion = 0f;
            if (enAura && Comandante.Definicion.Aura == TipoAura.Resistencia) reduccion += Comandante.Aura;
            bool cohesion = HabilidadActivaDe(Habilidad.Cohesion) && (enAura || Comandante.TieneRasgo5);
            bool cerrarFilas = HabilidadActivaDe(Habilidad.CerrarFilas) && enAura;
            if (cohesion || cerrarFilas) reduccion += ReglasComandante.CohesionReduccion;
            if (enAura && Comandante.Definicion.Habilidad == Habilidad.Cohesion && Comandante.TieneRasgo3 && Rangos.EsElite(tropa.Rango))
            {
                reduccion += ReglasComandante.CohesionEliteNivel3;
            }
            return Mathf.Max(ReglasComandante.MinimoDanoRecibido, 1f - reduccion);
        }

        public float MultiplicadorVelocidad(TroopUnit tropa)
        {
            if (Comandante == null || !HabilidadActiva || !Comandante.TieneRasgo3) return 1f;
            bool enAura = EnAura(tropa);
            switch (Comandante.Definicion.Habilidad)
            {
                case Habilidad.Embestida: return enAura ? 1f + ReglasComandante.EmbestidaVelocidadNivel3 : 1f;
                case Habilidad.Llamado: return 1f + ReglasComandante.LlamadoVelocidadNivel3;
                case Habilidad.OrdenAsalto: return enAura ? 1f + ReglasComandante.OrdenVelocidadNivel3 : 1f;
                default: return 1f;
            }
        }

        /// <summary>Con la orden de asalto, las tropas del aura van primero por las defensas.</summary>
        public bool PrefiereDefensasAhora(TroopUnit tropa) => HabilidadActivaDe(Habilidad.OrdenAsalto) && EnAura(tropa);

        public bool HayDefensasEnPie
        {
            get
            {
                foreach (var edificio in _edificios)
                {
                    if (!edificio.Destruido && edificio.EsDefensa) return true;
                }
                return false;
            }
        }

        /// <summary>El comandante no muere para siempre: cae, y vuelve herido al cuauhcalli.</summary>
        public void AlCaerComandante(TroopUnit unidad)
        {
            _tropas.Remove(unidad);
            if (unidad == UnidadComandante) UnidadComandante = null;
            ComandanteCaido = true;
            HabilidadHasta = -1f;
            _habilidadEnCurso = false;
        }

        public void AlMorirAliado(TroopUnit aliado)
        {
            _tropas.Remove(aliado);
            _aliadosCaidos++;
        }

        private void Update()
        {
            if (_habilidadEnCurso && !HabilidadActiva) AlTerminarHabilidad();
            if (_zonaProhibida != null && _zonaProhibida.activeSelf && Time.time > _ocultarZonaEn) _zonaProhibida.SetActive(false);
            if (Terminada) return;

            TiempoRestante -= Time.deltaTime;
            bool todoDestruido = Destruccion >= 1f;
            bool sinTropas = _algunaDesplegada && _tropas.Count == 0 && TotalDisponibles == 0 && !ComandantePendiente;
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
            resultado.AliadosCaidos = _aliadosCaidos;
            if (Comandante != null)
            {
                // El comandante vuelve con la vida que le quedó (herido si cayó), y gana experiencia por las
                // estrellas y por los cautivos que tomaron las tropas dentro de su aura.
                resultado.Comandante = Comandante;
                resultado.ComandanteSalio = ComandanteDesplegado;
                resultado.ComandanteCayo = ComandanteCaido;
                if (ComandanteDesplegado)
                {
                    Comandante.Regresar(UnidadComandante != null && !UnidadComandante.Muerta ? UnidadComandante.FraccionVida : 0.01f);
                    resultado.ComandanteExperiencia = resultado.Estrellas * ReglasComandante.ExperienciaPorEstrella
                                                      + _capturasEnAura / ReglasComandante.CapturasPorExperiencia;
                    Comandante.GanarExperiencia(resultado.ComandanteExperiencia);
                }
            }
            foreach (var tropa in _tropas)
            {
                if (tropa == null || tropa.Muerta || tropa.EsComandante || tropa.Aliado) continue;
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
            // Si se cierra en plena batalla, el comandante vuelve con la vida que lleva (herido si ya cayó).
            if (Comandante != null && ComandanteDesplegado)
            {
                Comandante.Regresar(UnidadComandante != null && !UnidadComandante.Muerta ? UnidadComandante.FraccionVida : 0.01f);
            }
            foreach (var tropa in _tropas)
            {
                if (tropa == null || tropa.Muerta || tropa.EsComandante || tropa.Aliado) continue;
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
            if (PuedeCapturar(atacante) && Random.value < Rangos.ProbabilidadCaptura(_manager.Pueblo))
            {
                atacante.Capturas++;
                _cautivos++;
                if (EnAura(atacante)) _capturasEnAura++;
            }

            // Derribar el tecpan da una estrella; la batalla sigue.
            if (edificio.Definicion.Id == BuildingId.Tecpan) TecpanDestruido = true;
        }

        /// <summary>Los aliados no toman cautivos y el comandante dirige, no captura.</summary>
        private static bool PuedeCapturar(TroopUnit atacante) =>
            atacante != null && !atacante.Muerta && !atacante.Aliado && !atacante.EsComandante;

        /// <summary>Si hay un yaotequihua (élite tlaxcalteca o acolhua) cerca de la tropa, pega más.</summary>
        public float MultiplicadorLider(TroopUnit tropa)
        {
            float radio = Rangos.RadioLider * Rangos.RadioLider;
            foreach (var otra in _tropas)
            {
                if (otra == null || otra == tropa || otra.Muerta || !Rangos.EsLider(_manager.Pueblo, otra.Rango)) continue;
                if ((otra.transform.position - tropa.transform.position).sqrMagnitude <= radio) return 1f + Rangos.BonoLider;
            }
            return 1f;
        }

        /// <summary>Al vencer a un defensor también se le puede tomar cautivo (la élite mexica, más seguido).</summary>
        public void AlMorirDefensor(Defensor defensor, TroopUnit atacante)
        {
            _defensores.Remove(defensor);
            _defensoresDerrotados++;
            if (PuedeCapturar(atacante) && Random.value < Rangos.ProbabilidadCapturaGuerrero(_manager.Pueblo, atacante.Rango))
            {
                atacante.Capturas++;
                _cautivos++;
                if (EnAura(atacante)) _capturasEnAura++;
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
