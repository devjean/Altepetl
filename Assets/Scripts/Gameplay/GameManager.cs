using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Altepetl
{
    /// <summary>
    /// Arma la aldea del prototipo: cámara, suelo, cuadrícula, colocación y selección de edificios.
    /// Se crea solo al pulsar Play en cualquier escena, así que no hace falta configurar nada.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public const int TamanoMapa = 24;
        /// <summary>Lado de la zona donde se puede construir, por nivel del tecpan; crece hacia afuera.</summary>
        public static readonly int[] LadoConstruible = { 12, 14, 16, 20, 24 };
        private const float SegundosEntreAutoguardados = 30f;

        public Pueblo Pueblo { get; private set; }
        public ResourceBank Banco { get; private set; }
        public GridMap Mapa { get; private set; }
        public PaisajeAldea Paisaje { get; private set; }
        public BuildingDefinition Colocando { get; private set; }
        public Building Seleccionado { get; private set; }
        public IReadOnlyList<Building> Edificios => _edificios;
        public Army Ejercito { get; } = new Army();
        /// <summary>Ofrendas en el teocalli y favor de Huitzilopochtli.</summary>
        public Culto Culto { get; private set; }
        public int NivelesCompletados { get; private set; }
        private readonly List<int> _estrellas = new List<int>();

        /// <summary>Mejor resultado en un capítulo, de 0 a 3 estrellas.</summary>
        public int EstrellasDe(int indice) => indice >= 0 && indice < _estrellas.Count ? _estrellas[indice] : 0;
        /// <summary>Los capítulos de la campaña del pueblo elegido.</summary>
        public CampaignLevel[] Campana => Altepetl.Campana.Para(Pueblo);
        /// <summary>Epílogo por leer al volver a la aldea (título y texto), o null.</summary>
        public string TituloHistoriaPendiente { get; private set; }
        public string HistoriaPendiente { get; private set; }

        public void LeerHistoriaPendiente()
        {
            TituloHistoriaPendiente = null;
            HistoriaPendiente = null;
        }

        public enum Modo
        {
            Aldea,
            Batalla,
        }

        public Modo ModoActual { get; private set; } = Modo.Aldea;
        public BattleManager Batalla { get; private set; }

        public string Mensaje { get; private set; }

        private readonly List<Building> _edificios = new List<Building>();
        private Renderer _suelo;
        private Renderer _zona; // parte del suelo donde ya se puede construir
        private Camera _camara;
        private Light _sol;
        private GameHud _hud;
        private Transform _fantasma;
        private Renderer _fantasmaRender;
        private Transform _marcaSeleccion;
        private float _mensajeHasta;
        private float _proximoAutoguardado;
        private Vector3 _centroCamara;
        private float _ladoCamara;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CrearAutomaticamente()
        {
            if (FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("Altepetl").AddComponent<GameManager>();
        }

        private void Awake()
        {
            Mapa = new GridMap(TamanoMapa, TamanoMapa);
            _centroCamara = Mapa.Centro;
            _ladoCamara = TamanoMapa;
            _hud = gameObject.AddComponent<GameHud>();
            _hud.Manager = this;
            var macehualtin = new GameObject("Macehualtin").AddComponent<Macehualtin>();
            macehualtin.Manager = this;
            _macehualtin = macehualtin;
            Asalto = new GameObject("Ataques a la aldea").AddComponent<Asalto>();
            Asalto.Manager = this;
            var tropasEnAldea = new GameObject("Tropas en la aldea").AddComponent<TropasEnAldea>();
            tropasEnAldea.Manager = this;
            _tropasEnAldea = tropasEnAldea;
            var paisaje = new GameObject("Paisaje").AddComponent<PaisajeAldea>();
            Paisaje = paisaje;
            paisaje.Manager = this;

            PrepararCamara();
            PrepararLuz();
            CrearSuelo();
            _fantasma = CrearMarcador("Fantasma", out _fantasmaRender);
            _marcaSeleccion = CrearMarcador("Seleccion", out var renderSeleccion);
            renderSeleccion.material.color = new Color(1f, 0.85f, 0.2f);

            var guardado = SaveSystem.Cargar();
            if (guardado != null) Restaurar(guardado);
        }

        public void ElegirPueblo(Pueblo pueblo)
        {
            Pueblo = pueblo;
            ActualizarSuelo();
            Culto = new Culto(pueblo.Id);
            Banco = new ResourceBank();
            Banco.Add(ResourceType.Maiz, 300);
            Banco.Add(ResourceType.Madera, 300);
            Banco.Add(ResourceType.Obsidiana, 100);
            Banco.Add(ResourceType.Plumas, 50);

            var tecpan = BuildingCatalog.Get(BuildingId.Tecpan);
            var origen = Mapa.AjustarOrigen(Mapa.MundoACasilla(Mapa.Centro) - Vector2Int.one, tecpan.Tamano);
            Construir(tecpan, origen, nivel: 1, segundosRestantes: 0f);
            Guardar();
        }

        public int Cantidad(BuildingId id)
        {
            int cantidad = 0;
            foreach (var edificio in _edificios)
            {
                if (edificio.Definicion.Id == id) cantidad++;
            }
            return cantidad;
        }

        public int Maximo(BuildingDefinition definicion) => definicion.Maximo(NivelTecpan);

        /// <summary>¿Ya tiene todos los que permite el nivel actual del tecpan?</summary>
        public bool EnLimite(BuildingDefinition definicion) => Cantidad(definicion.Id) >= Maximo(definicion);

        /// <summary>Se está colocando un edificio cuyos recursos faltantes se pagarán con plumas.</summary>
        public bool ColocandoConPlumas { get; private set; }

        public void EmpezarColocacion(BuildingDefinition definicion, bool conPlumas = false)
        {
            Seleccionar(null);
            if (EnLimite(definicion))
            {
                MostrarMensaje("Mejora el tecpan para construir más");
                return;
            }
            if (NecesitaGente(definicion) && !HayGenteParaObra)
            {
                MostrarMensaje(TextoSinGente);
                return;
            }
            bool alcanza = Banco.PuedePagar(definicion.Costo);
            if (!alcanza && !(conPlumas && PuedePagarConPlumas(definicion.Costo)))
            {
                MostrarMensaje(conPlumas ? "No tienes suficientes plumas de quetzal" : "Recursos insuficientes");
                return;
            }
            Colocando = definicion;
            ColocandoConPlumas = !alcanza;
        }

        public void CancelarColocacion()
        {
            Colocando = null;
            Moviendo = null;
            EligiendoMuros = false;
            _murosElegidos.Clear();
            _grupo.Clear();
            _desfases.Clear();
        }

        // ---------- Mover edificios ----------

        /// <summary>Edificio que se está cambiando de lugar (gratis, aunque esté en obra). Con murallas, la primera del grupo.</summary>
        public Building Moviendo { get; private set; }
        public bool EnModoColocar => Colocando != null || Moviendo != null || EligiendoMuros;

        /// <summary>Se están eligiendo murallas conectadas en línea para moverlas juntas.</summary>
        public bool EligiendoMuros { get; private set; }
        public int CuantosMurosElegidos => _murosElegidos.Count;
        public int CuantosEnGrupo => _grupo.Count;

        private readonly List<Building> _murosElegidos = new List<Building>();
        private readonly List<Building> _grupo = new List<Building>();          // lo que se mueve; el primero es el ancla
        private readonly List<Vector2Int> _desfases = new List<Vector2Int>();   // de cada uno respecto al ancla
        private bool _girado;

        public void EmpezarMover(Building edificio)
        {
            if (edificio == null) return;
            if (Asalto.EnCurso)
            {
                MostrarMensaje("No se puede mover nada durante un ataque");
                return;
            }
            CancelarColocacion();
            Seleccionar(null);
            if (edificio.Definicion.Id == BuildingId.Muralla)
            {
                // Con murallas primero se eligen las de la línea (arrastrando o "Toda la línea").
                EligiendoMuros = true;
                _murosElegidos.Add(edificio);
                return;
            }
            EmpezarGrupo(new List<Building> { edificio });
        }

        private void EmpezarGrupo(List<Building> edificios)
        {
            _grupo.Clear();
            _desfases.Clear();
            foreach (var edificio in edificios)
            {
                _grupo.Add(edificio);
                _desfases.Add(edificio.Origen - edificios[0].Origen);
            }
            _girado = false;
            Moviendo = edificios[0];
        }

        /// <summary>Pasa de elegir murallas a moverlas.</summary>
        public void MoverMurosElegidos()
        {
            if (!EligiendoMuros || _murosElegidos.Count == 0) return;
            var muros = new List<Building>(_murosElegidos);
            EligiendoMuros = false;
            _murosElegidos.Clear();
            EmpezarGrupo(muros);
        }

        /// <summary>Elige el tramo recto de murallas que pasa por la primera elegida.</summary>
        public void ElegirLineaCompleta()
        {
            if (!EligiendoMuros || _murosElegidos.Count == 0) return;
            var inicio = _murosElegidos[0].Origen;
            var eje = EjeElegido();
            if (eje == Vector2Int.zero)
            {
                // Una sola: se toma la dirección donde la línea es más larga.
                var x = new Vector2Int(1, 0);
                var y = new Vector2Int(0, 1);
                eje = LargoDeLinea(inicio, x) >= LargoDeLinea(inicio, y) ? x : y;
            }
            foreach (int signo in new[] { 1, -1 })
            {
                for (var c = inicio + eje * signo; EsMuralla(c); c += eje * signo)
                {
                    var muro = Mapa.En(c);
                    if (!_murosElegidos.Contains(muro)) _murosElegidos.Add(muro);
                }
            }
        }

        /// <summary>Elige todas las murallas unidas a las elegidas, con cruces y esquinas.</summary>
        public void ElegirConectados()
        {
            if (!EligiendoMuros) return;
            var pendientes = new Queue<Vector2Int>();
            foreach (var muro in _murosElegidos) pendientes.Enqueue(muro.Origen);
            var vecinos = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (pendientes.Count > 0)
            {
                var casilla = pendientes.Dequeue();
                foreach (var paso in vecinos)
                {
                    var siguiente = casilla + paso;
                    if (!EsMuralla(siguiente)) continue;
                    var muro = Mapa.En(siguiente);
                    if (_murosElegidos.Contains(muro)) continue;
                    _murosElegidos.Add(muro);
                    pendientes.Enqueue(siguiente);
                }
            }
        }

        private int LargoDeLinea(Vector2Int inicio, Vector2Int eje)
        {
            int largo = 0;
            for (var c = inicio + eje; EsMuralla(c); c += eje) largo++;
            for (var c = inicio - eje; EsMuralla(c); c -= eje) largo++;
            return largo;
        }

        private bool EsMuralla(Vector2Int casilla)
        {
            var edificio = Mapa.En(casilla);
            return edificio != null && edificio.Definicion.Id == BuildingId.Muralla;
        }

        /// <summary>(1,0) si las dos primeras elegidas van a lo largo de x, (0,1) si van a lo largo de y, cero si hay una sola.</summary>
        private Vector2Int EjeElegido()
        {
            if (_murosElegidos.Count < 2) return Vector2Int.zero;
            return _murosElegidos[1].Origen.y == _murosElegidos[0].Origen.y ? new Vector2Int(1, 0) : new Vector2Int(0, 1);
        }

        /// <summary>Agrega una muralla si está pegada a alguna elegida (puede dar vuelta en cruces y esquinas).</summary>
        private bool TryAgregarMuro(Vector2Int casilla)
        {
            if (!EligiendoMuros || !EsMuralla(casilla)) return false;
            var muro = Mapa.En(casilla);
            if (_murosElegidos.Contains(muro)) return false;
            bool pegada = false;
            foreach (var elegido in _murosElegidos)
            {
                var d = casilla - elegido.Origen;
                pegada |= Mathf.Abs(d.x) + Mathf.Abs(d.y) == 1;
            }
            if (!pegada) return false;
            _murosElegidos.Add(muro);
            return true;
        }

        /// <summary>
        /// Deja elegidas solo la primera, la tocada y las que siguen unidas a la primera sin pasar por la
        /// tocada: es decir, quita las que estaban "después" de ella. Devuelve cuántas quitó.
        /// </summary>
        private int RecortarDesde(Building muro)
        {
            var primera = _murosElegidos[0];
            var quedan = new HashSet<Building> { primera, muro };
            if (muro != primera)
            {
                var pendientes = new Queue<Building>();
                pendientes.Enqueue(primera);
                var vecinos = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
                while (pendientes.Count > 0)
                {
                    var actual = pendientes.Dequeue();
                    foreach (var paso in vecinos)
                    {
                        var vecino = Mapa.En(actual.Origen + paso);
                        if (vecino == null || vecino == muro || quedan.Contains(vecino) || !_murosElegidos.Contains(vecino)) continue;
                        quedan.Add(vecino);
                        pendientes.Enqueue(vecino);
                    }
                }
            }
            int antes = _murosElegidos.Count;
            _murosElegidos.RemoveAll(m => !quedan.Contains(m));
            return antes - _murosElegidos.Count;
        }

        /// <summary>Gira el grupo de murallas 90° alrededor de la primera.</summary>
        public void GirarGrupo()
        {
            for (int i = 0; i < _desfases.Count; i++) _desfases[i] = new Vector2Int(-_desfases[i].y, _desfases[i].x);
            _girado = !_girado;
        }

        /// <summary>¿Cabe todo el grupo con el ancla en ese origen? Puede encimarse sobre sí mismo.</summary>
        private bool GrupoCabeEn(Vector2Int ancla)
        {
            for (int i = 0; i < _grupo.Count; i++)
            {
                int tamano = _grupo[i].Definicion.Tamano;
                var origen = ancla + _desfases[i];
                if (!Mapa.EnArea(origen, tamano)) return false;
                for (int x = origen.x; x < origen.x + tamano; x++)
                {
                    for (int y = origen.y; y < origen.y + tamano; y++)
                    {
                        var casilla = new Vector2Int(x, y);
                        if (!Mapa.DentroDelMapa(casilla)) return false;
                        var ocupante = Mapa.En(casilla);
                        if (ocupante != null && !_grupo.Contains(ocupante)) return false;
                    }
                }
            }
            return true;
        }

        private void TryMover(Vector2Int ancla)
        {
            if (ancla == Moviendo.Origen && !_girado)
            {
                var mismo = _grupo.Count == 1 ? Moviendo : null;
                CancelarColocacion();
                Seleccionar(mismo);
                return;
            }
            if (!GrupoCabeEn(ancla))
            {
                bool fuera = false;
                for (int i = 0; i < _grupo.Count; i++) fuera |= !Mapa.EnArea(ancla + _desfases[i], _grupo[i].Definicion.Tamano);
                MostrarMensaje(fuera ? "Mejora el tecpan para abrir más terreno" : "Ese lugar está ocupado");
                return;
            }
            // Se quitan todos de sus casillas y se vuelven a crear en el nuevo lugar con el mismo nivel y obra,
            // como al cargar la partida.
            var anteriores = new List<Vector2Int>();
            foreach (var edificio in _grupo)
            {
                anteriores.Add(edificio.Origen);
                Mapa.Ocupar(edificio.Origen, edificio.Definicion.Tamano, null);
                _edificios.Remove(edificio);
            }
            Building movido = null;
            for (int i = 0; i < _grupo.Count; i++)
            {
                var edificio = _grupo[i];
                movido = Construir(edificio.Definicion, ancla + _desfases[i], edificio.Nivel, edificio.SegundosRestantes, edificio.Acumulado);
                edificio.Retirar();
            }
            for (int i = 0; i < _grupo.Count; i++)
            {
                if (_grupo[i].Definicion.Id != BuildingId.Muralla) continue;
                // Las vecinas del lugar viejo pierden su tramo hacia ella.
                Mapa.En(anteriores[i] + new Vector2Int(-1, 0))?.UnirMuralla(Mapa);
                Mapa.En(anteriores[i] + new Vector2Int(0, -1))?.UnirMuralla(Mapa);
            }
            bool uno = _grupo.Count == 1;
            CancelarColocacion();
            Seleccionar(uno ? movido : null);
            Guardar();
        }

        public void Seleccionar(Building edificio)
        {
            Seleccionado = edificio;
        }

        public void MostrarMensaje(string texto, float segundos = 2.5f)
        {
            Mensaje = texto;
            _mensajeHasta = Time.time + segundos;
        }

        private TropasEnAldea _tropasEnAldea;
        private Macehualtin _macehualtin;

        // ---------- Ataques a la aldea ----------

        public Asalto Asalto { get; private set; }
        /// <summary>Cuántos de cada tipo y rango salen a defender (de los sanos que haya en ese momento).</summary>
        private readonly int[] _defensa = new int[TroopCatalog.Count * Rangos.Count];

        public int DefensaElegida(TroopId id, int rango) => _defensa[Army.Indice(id, rango)];

        public void ElegirDefensa(TroopId id, int rango, int cantidad)
        {
            _defensa[Army.Indice(id, rango)] = Mathf.Max(0, cantidad);
        }

        /// <summary>Los que de verdad saldrían a defender: los elegidos, si están sanos en la aldea.</summary>
        public int DefensoresListos(TroopId id, int rango) => Mathf.Min(DefensaElegida(id, rango), Ejercito.Get(id, rango));

        public int TotalDefensoresListos
        {
            get
            {
                int total = 0;
                foreach (var tropa in TroopCatalog.Todos)
                {
                    for (int r = 0; r < Rangos.Count; r++) total += DefensoresListos(tropa.Id, r);
                }
                return total;
            }
        }

        public void AlAvisarAsalto()
        {
            MostrarMensaje(Asalto.HayVigia ? "¡La torre de vigía avisa: se acercan enemigos!" : "¡Se acercan enemigos!", 4f);
            // La gente corre a meterse a las casas y el ejército se junta en el tecpan.
            _macehualtin.Resguardar = true;
            _tropasEnAldea.Resguardar = true;
        }

        /// <summary>Durante el ataque, la gente se resguarda y solo se ven los defensores.</summary>
        public void AlEmpezarAsalto()
        {
            if (Moviendo != null || EligiendoMuros) CancelarColocacion();
            _tropasEnAldea.gameObject.SetActive(false);
            _macehualtin.gameObject.SetActive(false);
        }

        public void AlTerminarAsalto()
        {
            _tropasEnAldea.gameObject.SetActive(true);
            _macehualtin.gameObject.SetActive(true);
            _tropasEnAldea.Resguardar = false;
            _macehualtin.Resguardar = false;
            Guardar();
        }

        private void Update()
        {
            if (Mensaje != null && Time.time > _mensajeHasta) Mensaje = null;
            ActualizarZonaConstruible();
            AjustarCamara();
            ActualizarMarcadores();
            ActualizarAmbiente();

            if (Pueblo == null) return;
            if (NivelTelpochcalli > 0) Ejercito.Avanzar(Time.deltaTime, DuracionEntrenamiento);
            // Los heridos empiezan a sanar ya de vuelta en la aldea, cuando llegan al temazcalli.
            bool curando = ModoActual == Modo.Aldea && !_tropasEnAldea.HeridosLlegando;
            int curados = curando ? Ejercito.Curar(Time.deltaTime, CamasCuracion) : 0;
            if (curados > 0) MostrarMensaje(curados == 1 ? $"{Terminos.Un} {Terminos.Tropa} sanó en el temazcalli" : $"{curados} {Terminos.Tropas} sanaron en el temazcalli");
            Culto.Avanzar(Time.deltaTime);
            Banco.BonoCapacidad = Culto.Bono(TipoBono.Almacen);
            if (Time.time >= _proximoAutoguardado) Guardar();
            LeerPuntero(out _, out _, out bool cancelar);
            if (cancelar) CancelarColocacion();
            // Arrastrar mueve el mapa; un toque sin arrastrar es lo que selecciona, coloca o despliega.
            if (!ManejarToques(out Vector2 posicion) || _hud.PunteroSobreHud(posicion)) return;
            if (!PunteroEnSuelo(posicion, out Vector3 punto)) return;

            if (ModoActual == Modo.Batalla)
            {
                if (Batalla != null) Batalla.Desplegar(punto);
                return;
            }

            var casilla = Mapa.MundoACasilla(punto);
            if (EligiendoMuros)
            {
                TryAgregarMuro(casilla);
            }
            else if (Moviendo != null)
            {
                TryMover(_grupo.Count == 1 ? Mapa.AjustarOrigen(casilla, Moviendo.Definicion.Tamano) : casilla);
            }
            else if (Colocando != null)
            {
                TryColocar(Colocando, Mapa.AjustarOrigen(casilla, Colocando.Tamano));
            }
            else
            {
                Seleccionar(Mapa.En(casilla));
            }
        }

        private void TryColocar(BuildingDefinition definicion, Vector2Int origen)
        {
            if (!Mapa.EnArea(origen, definicion.Tamano))
            {
                MostrarMensaje("Mejora el tecpan para abrir más terreno");
                return;
            }
            if (!Mapa.EstaLibre(origen, definicion.Tamano))
            {
                MostrarMensaje("Ese lugar está ocupado");
                return;
            }
            if (NecesitaGente(definicion) && !HayGenteParaObra)
            {
                MostrarMensaje(TextoSinGente);
                Colocando = null;
                return;
            }
            bool pagado = ColocandoConPlumas && !Banco.PuedePagar(definicion.Costo)
                ? PagarConPlumas(definicion.Costo)
                : Banco.TryGastar(definicion.Costo);
            if (!pagado)
            {
                MostrarMensaje(ColocandoConPlumas ? "No tienes suficientes plumas de quetzal" : "Recursos insuficientes");
                Colocando = null;
                return;
            }
            ColocandoConPlumas = false;
            Construir(definicion, origen, 0, SegundosConstruccion(definicion));
            // Las murallas se siguen colocando una tras otra mientras alcancen los recursos.
            bool otraMuralla = definicion.Id == BuildingId.Muralla && !EnLimite(definicion)
                && Banco.PuedePagar(definicion.Costo);
            if (!otraMuralla) Colocando = null;
            Guardar();
        }

        private Building Construir(BuildingDefinition definicion, Vector2Int origen, int nivel,
            float segundosRestantes, float acumulado = 0f)
        {
            var edificio = new GameObject().AddComponent<Building>();
            edificio.Culto = Culto;
            edificio.Inicializar(definicion, Pueblo, origen, Banco, Mapa, nivel, segundosRestantes, acumulado);
            Mapa.Ocupar(origen, definicion.Tamano, edificio);
            _edificios.Add(edificio);
            if (definicion.Id == BuildingId.Muralla)
            {
                edificio.UnirMuralla(Mapa);
                Mapa.En(origen + new Vector2Int(-1, 0))?.UnirMuralla(Mapa);
                Mapa.En(origen + new Vector2Int(0, -1))?.UnirMuralla(Mapa);
            }
            return edificio;
        }

        // ---------- Mejoras ----------

        /// <summary>Nivel del tecpan; ningún otro edificio puede superarlo.</summary>
        public int NivelTecpan
        {
            get
            {
                foreach (var edificio in _edificios)
                {
                    if (edificio.Definicion.Id == BuildingId.Tecpan) return edificio.Nivel;
                }
                return 1;
            }
        }

        public enum EstadoMejora
        {
            Disponible,
            EnObra,
            NivelMaximo,
            RequiereTecpan,
            FaltanEdificios,   // el tecpan pide antes ciertos edificios
            SinGente,          // todos los macehualtin están en otras obras
            SinRecursos,
        }

        // ---------- Macehualtin (población) ----------

        /// <summary>Cada obra (construcción o mejora) ocupa a esta gente hasta que termina.</summary>
        public const int MacehualtinPorObra = 10;

        /// <summary>20 con el tecpan 1 y 10 más por cada nivel: 2 obras a la vez al empezar, 6 con el tecpan 5.</summary>
        public int Poblacion => 10 + 10 * Mathf.Max(1, NivelTecpan);

        /// <summary>Obras en marcha. Las murallas son rápidas y no ocupan a nadie.</summary>
        public int ObrasEnCurso
        {
            get
            {
                int obras = 0;
                foreach (var edificio in _edificios)
                {
                    if (edificio.EnConstruccion && edificio.Definicion.Id != BuildingId.Muralla) obras++;
                }
                return obras;
            }
        }

        public int MacehualtinLibres => Mathf.Max(0, Poblacion - ObrasEnCurso * MacehualtinPorObra);
        public bool HayGenteParaObra => MacehualtinLibres >= MacehualtinPorObra;

        private bool NecesitaGente(BuildingDefinition definicion) => definicion.Id != BuildingId.Muralla;

        public string TextoSinGente => $"Todos los {Terminos.Gente} están ocupados";

        public EstadoMejora PuedeMejorar(Building edificio)
        {
            if (edificio.EnConstruccion) return EstadoMejora.EnObra;
            if (edificio.Nivel >= edificio.Definicion.NivelMaximo) return EstadoMejora.NivelMaximo;
            if (edificio.Definicion.Id != BuildingId.Tecpan && edificio.Nivel >= NivelTecpan)
                return EstadoMejora.RequiereTecpan;
            if (edificio.Definicion.Id == BuildingId.Tecpan && RequisitosFaltantesTecpan(edificio.Nivel + 1).Count > 0)
                return EstadoMejora.FaltanEdificios;
            if (NecesitaGente(edificio.Definicion) && !HayGenteParaObra) return EstadoMejora.SinGente;
            if (!Banco.PuedePagar(edificio.Definicion.CostoMejora(edificio.Nivel))) return EstadoMejora.SinRecursos;
            return EstadoMejora.Disponible;
        }

        /// <summary>
        /// Lo que pide el tecpan para subir a cada nivel, para que la aldea no se quede atrás
        /// (y vulnerable) por correr a subirlo. Índice = nivel al que se sube.
        /// </summary>
        public static readonly RequisitoTecpan[][] RequisitosTecpan =
        {
            new RequisitoTecpan[0],
            new RequisitoTecpan[0],
            new[]
            {
                new RequisitoTecpan(BuildingId.Granja, 1), new RequisitoTecpan(BuildingId.Lenadores, 1),
                new RequisitoTecpan(BuildingId.Telpochcalli, 1), new RequisitoTecpan(BuildingId.Calpulli, 1),
            },
            new[]
            {
                new RequisitoTecpan(BuildingId.Granja, 2), new RequisitoTecpan(BuildingId.Lenadores, 2),
                new RequisitoTecpan(BuildingId.Obsidiana, 1), new RequisitoTecpan(BuildingId.Petlacalco, 1),
                new RequisitoTecpan(BuildingId.Teocalli, 1), new RequisitoTecpan(BuildingId.Muralla, 1, 10),
            },
            new[]
            {
                new RequisitoTecpan(BuildingId.Granja, 3), new RequisitoTecpan(BuildingId.Lenadores, 3),
                new RequisitoTecpan(BuildingId.Obsidiana, 2), new RequisitoTecpan(BuildingId.Petlacalco, 2),
                new RequisitoTecpan(BuildingId.Telpochcalli, 2), new RequisitoTecpan(BuildingId.Calpulli, 2),
                new RequisitoTecpan(BuildingId.Temazcalli, 1), new RequisitoTecpan(BuildingId.Muralla, 1, 20),
            },
            new[]
            {
                new RequisitoTecpan(BuildingId.Granja, 4), new RequisitoTecpan(BuildingId.Lenadores, 4),
                new RequisitoTecpan(BuildingId.Obsidiana, 3), new RequisitoTecpan(BuildingId.Petlacalco, 3),
                new RequisitoTecpan(BuildingId.Telpochcalli, 3), new RequisitoTecpan(BuildingId.Calpulli, 3),
                new RequisitoTecpan(BuildingId.Temazcalli, 2), new RequisitoTecpan(BuildingId.Muralla, 2, 40),
            },
        };

        /// <summary>Requisitos que aún no se cumplen para subir el tecpan a ese nivel.</summary>
        public List<RequisitoTecpan> RequisitosFaltantesTecpan(int nivel)
        {
            var faltan = new List<RequisitoTecpan>();
            if (nivel < 0 || nivel >= RequisitosTecpan.Length) return faltan;
            foreach (var requisito in RequisitosTecpan[nivel])
            {
                int tiene = 0;
                foreach (var edificio in _edificios)
                {
                    if (edificio.Definicion.Id == requisito.Id && edificio.Nivel >= requisito.Nivel) tiene++;
                }
                if (tiene < requisito.Cantidad) faltan.Add(requisito);
            }
            return faltan;
        }

        public void TryMejorar(Building edificio)
        {
            if (PuedeMejorar(edificio) != EstadoMejora.Disponible) return;
            if (!Banco.TryGastar(edificio.Definicion.CostoMejora(edificio.Nivel))) return;
            edificio.EmpezarMejora();
            Guardar();
        }

        /// <summary>
        /// Plumas de quetzal para comprar lo que falta de un costo: 1 por cada 25 de maíz o madera
        /// y 1 por cada 8 de obsidiana. Devuelve -1 si falta algo que no se compra (mamaltin).
        /// </summary>
        public int PlumasParaCompletar(int[] costo)
        {
            int plumas = 0;
            for (int i = 0; i < costo.Length; i++)
            {
                int falta = costo[i] - Banco.Get((ResourceType)i);
                if (falta <= 0) continue;
                switch ((ResourceType)i)
                {
                    case ResourceType.Maiz:
                    case ResourceType.Madera: plumas += Mathf.CeilToInt(falta / 25f); break;
                    case ResourceType.Obsidiana: plumas += Mathf.CeilToInt(falta / 8f); break;
                    case ResourceType.Plumas: plumas += falta; break;
                    default: return -1;
                }
            }
            return plumas;
        }

        public bool PuedePagarConPlumas(int[] costo)
        {
            int plumas = PlumasParaCompletar(costo);
            return plumas >= 0 && Banco.Get(ResourceType.Plumas) >= plumas + costo[(int)ResourceType.Plumas];
        }

        /// <summary>Paga con plumas lo que falta y gasta lo que sí hay. Falso si no alcanzan las plumas.</summary>
        private bool PagarConPlumas(int[] costo)
        {
            if (!PuedePagarConPlumas(costo)) return false;
            Banco.TryGastar(ResourceInfo.Costo(plumas: PlumasParaCompletar(costo)));
            for (int i = 0; i < costo.Length; i++)
            {
                var tipo = (ResourceType)i;
                Banco.Establecer(tipo, Mathf.Max(0, Banco.Get(tipo) - costo[i]));
            }
            return true;
        }

        /// <summary>Paga con plumas lo que falta, gasta lo que sí hay y empieza la mejora.</summary>
        public bool TryMejorarConPlumas(Building edificio)
        {
            if (PuedeMejorar(edificio) != EstadoMejora.SinRecursos) return false;
            if (!PagarConPlumas(edificio.Definicion.CostoMejora(edificio.Nivel)))
            {
                MostrarMensaje("No tienes suficientes plumas de quetzal");
                return false;
            }
            edificio.EmpezarMejora();
            Guardar();
            return true;
        }

        // ---------- Ejército ----------

        /// <summary>Nivel del mejor telpochcalli terminado (0 si no hay): de él depende si se entrena y qué tan rápido.</summary>
        public int NivelTelpochcalli
        {
            get
            {
                int nivel = 0;
                foreach (var edificio in _edificios)
                {
                    if (edificio.Definicion.Entrena) nivel = Mathf.Max(nivel, edificio.Nivel);
                }
                return nivel;
            }
        }

        /// <summary>Cada nivel del telpochcalli después del 1 entrena un 20 % más rápido.</summary>
        public const float EntrenamientoExtraPorNivel = 0.2f;

        /// <summary>Espacio para tropas: 15 por nivel de cada calpulli terminado.</summary>
        public int CapacidadEjercito
        {
            get
            {
                int capacidad = 0;
                foreach (var edificio in _edificios)
                {
                    capacidad += edificio.Definicion.CapacidadTropas * edificio.Nivel;
                }
                return capacidad;
            }
        }

        /// <summary>Heridos que se curan a la vez: 5 por nivel de cada temazcalli terminado.</summary>
        public int CamasCuracion
        {
            get
            {
                int camas = 0;
                foreach (var edificio in _edificios)
                {
                    camas += edificio.Definicion.CamasCuracion * edificio.Nivel;
                }
                return camas;
            }
        }

        /// <summary>Segundos para construir un edificio nuevo (con el pueblo y la ofrenda a Xiuhtecuhtli).</summary>
        public float SegundosConstruccion(BuildingDefinition definicion)
        {
            return definicion.SegundosParaNivel(1) * Pueblo.MultiplicadorTiempoConstruccion
                   * (1f - Culto.Bono(TipoBono.Construccion));
        }

        /// <summary>Plumas para terminar ya toda la cola: una por cada 10 segundos, como en las obras.</summary>
        public int CostoTerminarEntrenamiento =>
            Mathf.Max(1, Mathf.CeilToInt(Ejercito.SegundosCola(DuracionEntrenamiento) / 10f));

        public bool TryTerminarEntrenamiento()
        {
            if (!Ejercito.Entrenando) return false;
            if (!Banco.TryGastar(ResourceInfo.Costo(plumas: CostoTerminarEntrenamiento)))
            {
                MostrarMensaje("No tienes suficientes plumas de quetzal");
                return false;
            }
            int terminadas = Ejercito.TerminarCola();
            MostrarMensaje(terminadas == 1 ? $"{Terminos.Un} {Terminos.Tropa} terminó su entrenamiento" : $"{terminadas} {Terminos.Tropas} terminaron su entrenamiento");
            Guardar();
            return true;
        }

        public float DuracionEntrenamiento(TroopId id)
        {
            float porNivel = 1f + EntrenamientoExtraPorNivel * Mathf.Max(0, NivelTelpochcalli - 1);
            return TroopCatalog.Get(id).SegundosEntrenamiento * Pueblo.MultiplicadorTiempoEntrenamiento
                   * (1f - Culto.Bono(TipoBono.Entrenamiento)) / porNivel;
        }

        /// <summary>Costo de entrenar, que cambia con el favor de Huitzilopochtli.</summary>
        public int[] CostoEntrenamiento(TroopId id)
        {
            var baseCosto = TroopCatalog.Get(id).Costo;
            var costo = new int[ResourceInfo.Count];
            float mult = Culto.CostoEntrenamientoPorFavor;
            for (int i = 0; i < costo.Length; i++) costo[i] = Mathf.CeilToInt(baseCosto[i] * mult);
            return costo;
        }

        public void TryOfrendar(Deidad deidad)
        {
            if (Banco.Get(ResourceType.Cautivos) < Culto.CostoOfrenda)
            {
                MostrarMensaje($"Necesitas {Culto.CostoOfrenda} mamaltin");
                return;
            }
            if (!Culto.Ofrendar(deidad, Banco)) return;
            MostrarMensaje(deidad.Id == DeidadId.Huitzilopochtli
                ? "Huitzilopochtli recibe tu ofrenda"
                : $"{deidad.Nombre} recibe tu ofrenda", 3f);
            Banco.BonoCapacidad = Culto.Bono(TipoBono.Almacen);
            Guardar();
        }

        /// <summary>Con la ofrenda a Mictlantecuhtli, una tropa caída devuelve parte de su maíz.</summary>
        public void AlCaerTropa(TroopDefinition definicion)
        {
            float reembolso = Culto.Bono(TipoBono.Reembolso);
            if (reembolso <= 0f) return;
            Banco.Add(ResourceType.Maiz, Mathf.Floor(definicion.Costo[(int)ResourceType.Maiz] * reembolso));
        }

        public void TryEntrenar(TroopId id)
        {
            if (NivelTelpochcalli <= 0)
            {
                MostrarMensaje("Construye un telpochcalli");
                return;
            }
            if (CapacidadEjercito <= 0)
            {
                MostrarMensaje("Construye un calpulli para tener espacio");
                return;
            }
            if (Ejercito.Espacio >= CapacidadEjercito)
            {
                MostrarMensaje("Ejército lleno: construye o mejora un calpulli");
                return;
            }
            if (!Banco.TryGastar(CostoEntrenamiento(id)))
            {
                MostrarMensaje("Recursos insuficientes");
                return;
            }
            Ejercito.Encolar(id, DuracionEntrenamiento(id));
            Guardar();
        }

        // ---------- Campaña ----------

        /// <summary>Al armar el ejército, de entrada van todas las tropas sanas y ninguna herida.</summary>
        public SeleccionEjercito SeleccionPorDefecto()
        {
            var seleccion = new SeleccionEjercito();
            foreach (var tropa in TroopCatalog.Todos)
            {
                for (int r = 0; r < Rangos.Count; r++) seleccion.Sanos[Army.Indice(tropa.Id, r)] = Ejercito.Get(tropa.Id, r);
            }
            return seleccion;
        }

        public void EmpezarBatalla(int indice, SeleccionEjercito seleccion)
        {
            if (ModoActual == Modo.Batalla) return;
            if (indice < 0 || indice >= Campana.Length || indice > NivelesCompletados) return;
            if (Asalto.Activo)
            {
                MostrarMensaje($"Primero defiende {Terminos.TuAldea}");
                return;
            }
            if (Ejercito.Total <= 0)
            {
                MostrarMensaje($"Entrena {Terminos.Tropas} en el telpochcalli antes de atacar");
                return;
            }
            if (seleccion == null || seleccion.Total <= 0)
            {
                MostrarMensaje($"Elige al menos {Terminos.Un.ToLowerInvariant()} {Terminos.Tropa}");
                return;
            }

            Seleccionar(null);
            CancelarColocacion();
            Batalla = new GameObject("Batalla").AddComponent<BattleManager>();
            Batalla.Empezar(this, indice, seleccion);
            ModoActual = Modo.Batalla;
            EnfocarCamara(Batalla.Centro, CampaignLevel.TamanoMapa + 4);
            Guardar();
        }

        /// <summary>Lo llama la batalla al terminar: entrega botín y plumas.</summary>
        public void AlTerminarBatalla(BattleManager batalla)
        {
            var resultado = batalla.Resultado;
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                if (resultado.Botin[i] > 0) Banco.Add((ResourceType)i, resultado.Botin[i]);
            }
            var nivel = batalla.Nivel;
            int indice = batalla.IndiceNivel;
            int estrellasAntes = EstrellasDe(indice);
            if (resultado.Estrellas > estrellasAntes)
            {
                while (_estrellas.Count <= indice) _estrellas.Add(0);
                _estrellas[indice] = resultado.Estrellas;
            }
            // Las plumas, la primera vez que se gana (aunque antes se haya perdido, como en Chapultepec).
            if (resultado.Victoria && estrellasAntes == 0)
            {
                resultado.Plumas = nivel.PlumasPrimeraVez;
                Banco.Add(ResourceType.Plumas, resultado.Plumas);
            }
            bool primeraVez = indice == NivelesCompletados;
            // Algunos capítulos siguen aunque se pierdan, como pasó en la historia (Chapultepec).
            if (primeraVez && (resultado.Victoria || nivel.AvanzaAunqueSePierda))
            {
                NivelesCompletados++;
                resultado.AvanzaHistoria = !resultado.Victoria;
                if (!string.IsNullOrEmpty(nivel.Epilogo))
                {
                    TituloHistoriaPendiente = nivel.Nombre;
                    HistoriaPendiente = nivel.Epilogo;
                }
            }
            Guardar();
        }

        public void VolverAAldea()
        {
            if (Batalla != null) Destroy(Batalla.gameObject);
            Batalla = null;
            ModoActual = Modo.Aldea;
            EnfocarCamara(Mapa.Centro, LadoCamaraAldea);
        }

        // ---------- Guardado ----------

        public void Guardar()
        {
            _proximoAutoguardado = Time.time + SegundosEntreAutoguardados;
            if (Pueblo == null) return;

            var datos = new SaveData { pueblo = Pueblo.Id };
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                datos.recursos[i] = Banco.GetExacto((ResourceType)i);
            }
            foreach (var edificio in _edificios)
            {
                datos.edificios.Add(new EdificioGuardado
                {
                    id = edificio.Definicion.Id,
                    nivel = edificio.Nivel,
                    x = edificio.Origen.x,
                    y = edificio.Origen.y,
                    segundosRestantes = edificio.SegundosRestantes,
                    acumulado = edificio.Acumulado,
                });
            }
            Ejercito.Exportar(datos);
            // Si se cierra el juego en plena batalla, las tropas sin desplegar y las que siguen vivas
            // en el campo no se pierden.
            if (Batalla != null && !Batalla.Terminada)
            {
                Batalla.SumarTropasPendientes(datos);
            }
            datos.nivelesCompletados = NivelesCompletados;
            datos.estrellas = new List<int>(_estrellas);
            datos.defensa = (int[])_defensa.Clone();
            Culto.Exportar(datos);
            SaveSystem.Guardar(datos);
        }

        private void Restaurar(SaveData datos)
        {
            Pueblo = Pueblo.Get(datos.pueblo);
            ActualizarSuelo();
            Culto = new Culto(Pueblo.Id);
            Culto.Importar(datos);
            Banco = new ResourceBank();
            Banco.BonoCapacidad = Culto.Bono(TipoBono.Almacen);

            // Primero los edificios, para que los almacenes terminados sumen su capacidad
            // antes de fijar los recursos.
            foreach (var guardado in datos.edificios)
            {
                var definicion = BuildingCatalog.Get(guardado.id);
                var origen = new Vector2Int(guardado.x, guardado.y);
                if (definicion == null || !Mapa.EstaLibre(origen, definicion.Tamano)) continue;
                Construir(definicion, origen, guardado.nivel, guardado.segundosRestantes, guardado.acumulado);
            }
            for (int i = 0; i < ResourceInfo.Count && i < datos.recursos.Length; i++)
            {
                Banco.Establecer((ResourceType)i, datos.recursos[i]);
            }
            Ejercito.Importar(datos);
            NivelesCompletados = Mathf.Clamp(datos.nivelesCompletados, 0, Campana.Length);
            _estrellas.Clear();
            if (datos.estrellas != null) _estrellas.AddRange(datos.estrellas);
            if (datos.defensa != null)
            {
                for (int i = 0; i < _defensa.Length && i < datos.defensa.Length; i++) _defensa[i] = Mathf.Max(0, datos.defensa[i]);
            }

            AplicarTiempoAusente(SaveSystem.SegundosDesde(datos));
        }

        /// <summary>Avanza construcciones y producción por el tiempo que el juego estuvo cerrado.</summary>
        private void AplicarTiempoAusente(float segundos)
        {
            if (segundos <= 0f) return;

            var antes = new int[ResourceInfo.Count];
            for (int i = 0; i < ResourceInfo.Count; i++) antes[i] = Banco.Get((ResourceType)i);

            // Dos pasadas: primero terminan todas las obras (un petlacalco nuevo amplía el almacén)
            // y después cada edificio produce durante el tiempo que le sobró.
            var sobrantes = new float[_edificios.Count];
            for (int i = 0; i < _edificios.Count; i++)
            {
                sobrantes[i] = _edificios[i].AvanzarConstruccion(segundos);
            }
            // La ofrenda activa solo cuenta mientras le quedaba tiempo.
            for (int i = 0; i < _edificios.Count; i++)
            {
                float conOfrenda = Mathf.Min(sobrantes[i], Culto.SegundosRestantes);
                _edificios[i].Producir(conOfrenda, conOfrenda: true);
                _edificios[i].Producir(sobrantes[i] - conOfrenda, conOfrenda: false);
            }
            int tropasAntes = Ejercito.Total;
            if (NivelTelpochcalli > 0) Ejercito.Avanzar(segundos, DuracionEntrenamiento);
            int curados = Ejercito.Curar(segundos, CamasCuracion);

            var ganancias = new List<string>();
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                int ganancia = Banco.Get((ResourceType)i) - antes[i];
                if (ganancia > 0) ganancias.Add($"+{ganancia} {ResourceInfo.Nombre((ResourceType)i).ToLowerInvariant()}");
            }
            Culto.Avanzar(segundos);
            Banco.BonoCapacidad = Culto.Bono(TipoBono.Almacen);
            int tropasNuevas = Ejercito.Total - tropasAntes;
            if (tropasNuevas > 0) ganancias.Add($"+{tropasNuevas} {Terminos.Tropas}");
            if (curados > 0) ganancias.Add($"{curados} {Terminos.Tropas} curad{Terminos.O}s");
            if (ganancias.Count > 0)
            {
                MostrarMensaje("Mientras no estabas: " + string.Join(", ", ganancias), 5f);
            }
        }

        private void OnApplicationPause(bool pausado)
        {
            // En el móvil, el sistema puede cerrar la app mientras está en segundo plano.
            if (pausado) Guardar();
        }

        private void OnApplicationQuit()
        {
            Guardar();
        }

        // ---------- Entrada (ratón en el editor, toque en el móvil) ----------

        /// <summary>abajo: el dedo o el botón izquierdo está presionado en este momento.</summary>
        private static bool LeerPuntero(out Vector2 posicion, out bool abajo, out bool cancelar)
        {
#if ENABLE_INPUT_SYSTEM
            var puntero = Pointer.current;
            posicion = puntero != null ? puntero.position.ReadValue() : Vector2.zero;
            abajo = puntero != null && puntero.press.isPressed;
            var raton = Mouse.current;
            var teclado = Keyboard.current;
            cancelar = (raton != null && raton.rightButton.wasPressedThisFrame)
                       || (teclado != null && teclado.escapeKey.wasPressedThisFrame);
            return puntero != null;
#else
            posicion = Input.mousePosition;
            abajo = Input.GetMouseButton(0);
            cancelar = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);
            return true;
#endif
        }

        /// <summary>Rueda del ratón: positivo para acercar.</summary>
        private static float LeerRueda()
        {
#if ENABLE_INPUT_SYSTEM
            var raton = Mouse.current;
            return raton != null ? raton.scroll.ReadValue().y : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        /// <summary>Dedos en la pantalla (o el botón izquierdo del ratón, con id -1) en este cuadro.</summary>
        private static void LeerDedos(List<KeyValuePair<int, Vector2>> dedos)
        {
            dedos.Clear();
#if ENABLE_INPUT_SYSTEM
            var pantalla = Touchscreen.current;
            if (pantalla != null)
            {
                foreach (var toque in pantalla.touches)
                {
                    if (toque.press.isPressed) dedos.Add(new KeyValuePair<int, Vector2>(toque.touchId.ReadValue(), toque.position.ReadValue()));
                }
            }
            var raton = Mouse.current;
            if (dedos.Count == 0 && raton != null && raton.leftButton.isPressed)
            {
                dedos.Add(new KeyValuePair<int, Vector2>(-1, raton.position.ReadValue()));
            }
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                var toque = Input.GetTouch(i);
                dedos.Add(new KeyValuePair<int, Vector2>(toque.fingerId, toque.position));
            }
            if (dedos.Count == 0 && Input.GetMouseButton(0)) dedos.Add(new KeyValuePair<int, Vector2>(-1, Input.mousePosition));
#endif
        }

        // ---------- Toques: zoom, desplazamiento y despliegue ----------

        private const float ZoomMinimo = 0.35f;   // lo más cerca
        private const float ZoomMaximo = 1.25f;   // lo más lejos
        private const float SegundosParaSoltarSeguido = 0.3f; // dedo quieto este tiempo: suelta tropas seguidas
        private const float SegundosEntreTropas = 0.12f;
        private float _zoom = 1f;
        private Vector3 _desplazamiento;          // cuánto se movió la cámara desde el centro

        private sealed class Dedo
        {
            public int Id;
            public Vector2 Inicio;
            public Vector2 Ultima;
            public Vector2 Actual;
            public float Desde;
            public bool Despliega;      // en batalla empezó donde se puede soltar tropas: solo despliega
            public bool SinTropas;      // empezó donde se podría soltar, pero ya no quedan de ese tipo
            public bool Arrastra;       // se movió: mueve el mapa o hace zoom
            public bool Soltando;       // se quedó quieto en batalla: suelta tropas seguidas
            public float SiguienteTropa;
            public bool EnHud;
            // Murallas: el dedo que empieza en el mapa las va poniendo en línea recta.
            public bool Pinta;
            public Vector2Int PintaInicio;
            public int PintaEje;        // 0 aún sin dirección, 1 a lo largo de x, 2 a lo largo de y
            public int PintaMin, PintaMax;
            public bool EligeMuros;     // empezó sobre una muralla elegida: al arrastrar elige las que va tocando
            public Building MuroTocado;
            public bool Recorto;        // al tocarla se quitaron las que seguían después
            public bool Agrego;
        }

        private readonly Dictionary<int, Dedo> _dedos = new Dictionary<int, Dedo>();
        private readonly List<KeyValuePair<int, Vector2>> _lecturaDedos = new List<KeyValuePair<int, Vector2>>();
        private readonly List<int> _dedosSoltados = new List<int>();

        /// <summary>
        /// Cada dedo por separado (también el ratón):
        /// tocar y soltar sin moverse es un toque (seleccionar, colocar o desplegar una tropa);
        /// en batalla, dejarlo quieto suelta tropas seguidas, y con varios dedos quietos se suelta en todos;
        /// arrastrar un dedo mueve el mapa; abrir o cerrar dos dedos hace zoom. La rueda del ratón también hace zoom.
        /// Devuelve true si este cuadro hubo un toque, y dónde.
        /// </summary>
        private bool ManejarToques(out Vector2 toque)
        {
            toque = Vector2.zero;
            bool huboToque = false;

            LeerPuntero(out Vector2 raton, out _, out _);
            float rueda = LeerRueda();
            if (rueda != 0f && !_hud.PunteroSobreHud(raton)) Zoom(rueda > 0f ? 0.9f : 1f / 0.9f);

            LeerDedos(_lecturaDedos);
            float umbral = Screen.height * 0.02f;
            bool batalla = ModoActual == Modo.Batalla && Batalla != null && !Batalla.Terminada;

            // Dedos nuevos y dedos que siguen.
            foreach (var lectura in _lecturaDedos)
            {
                if (!_dedos.TryGetValue(lectura.Key, out var dedo))
                {
                    dedo = new Dedo
                    {
                        Id = lectura.Key, Inicio = lectura.Value, Ultima = lectura.Value, Actual = lectura.Value,
                        Desde = Time.time, EnHud = _hud.PunteroSobreHud(lectura.Value),
                    };
                    _dedos[lectura.Key] = dedo;
                    // En batalla: donde se puede desplegar, el dedo suelta tropas; sobre los edificios
                    // (o fuera del campo) mueve el mapa o hace zoom, y se marca la zona prohibida.
                    if (batalla && !dedo.EnHud && PunteroEnSuelo(dedo.Inicio, out Vector3 punto))
                    {
                        bool sePuede = Batalla.PuedeDesplegarEn(punto);
                        // Sin tropas del tipo elegido el dedo solo mueve el mapa; un toque suelto avisa.
                        dedo.SinTropas = sePuede && Batalla.Disponibles(Batalla.Seleccionada) == 0;
                        dedo.Despliega = sePuede && !dedo.SinTropas;
                        if (!sePuede) Batalla.MostrarZonaProhibida();
                    }
                    else if (!batalla && !dedo.EnHud && Colocando != null && Colocando.Id == BuildingId.Muralla
                             && PunteroEnSuelo(dedo.Inicio, out Vector3 inicio))
                    {
                        dedo.Pinta = true;
                        dedo.PintaInicio = Mapa.MundoACasilla(inicio);
                        ColocarMurallaEn(dedo.PintaInicio);
                    }
                    else if (!batalla && !dedo.EnHud && EligiendoMuros && PunteroEnSuelo(dedo.Inicio, out Vector3 sobre))
                    {
                        var muro = Mapa.En(Mapa.MundoACasilla(sobre));
                        dedo.EligeMuros = muro != null && _murosElegidos.Contains(muro);
                        if (dedo.EligeMuros)
                        {
                            // Al agarrar una de en medio se sueltan las que siguen después de ella.
                            dedo.MuroTocado = muro;
                            dedo.Recorto = RecortarDesde(muro) > 0;
                        }
                    }
                    continue;
                }
                dedo.Ultima = dedo.Actual;
                dedo.Actual = lectura.Value;
                if (dedo.EnHud || dedo.Soltando) continue;
                if (dedo.Pinta)
                {
                    PintarMurallas(dedo);
                    continue;
                }
                if (dedo.EligeMuros)
                {
                    if (PunteroEnSuelo(dedo.Actual, out Vector3 tocando)) dedo.Agrego |= TryAgregarMuro(Mapa.MundoACasilla(tocando));
                    continue;
                }
                if (dedo.Despliega)
                {
                    // Quieto un momento o deslizándose: suelta tropas seguidas.
                    if (Time.time - dedo.Desde >= SegundosParaSoltarSeguido || Vector2.Distance(dedo.Actual, dedo.Inicio) > umbral)
                    {
                        dedo.Soltando = true;
                        dedo.SiguienteTropa = Time.time;
                    }
                    continue;
                }
                if (!dedo.Arrastra && Vector2.Distance(dedo.Actual, dedo.Inicio) > umbral) dedo.Arrastra = true;
            }

            // Dedos que se levantaron: si no se movieron, cuentan como toque.
            _dedosSoltados.Clear();
            foreach (var par in _dedos)
            {
                bool sigue = false;
                foreach (var lectura in _lecturaDedos) sigue |= lectura.Key == par.Key;
                if (!sigue) _dedosSoltados.Add(par.Key);
            }
            foreach (int id in _dedosSoltados)
            {
                var dedo = _dedos[id];
                _dedos.Remove(id);
                // Un toque en la muralla de la punta (sin nada después) la quita de las elegidas.
                if (dedo.EligeMuros && !dedo.Recorto && !dedo.Agrego && EligiendoMuros
                    && _murosElegidos.Count > 1 && dedo.MuroTocado != _murosElegidos[0])
                {
                    _murosElegidos.Remove(dedo.MuroTocado);
                }
                // En batalla solo cuenta el toque de un dedo que podía desplegar.
                bool quieto = !dedo.Arrastra && !dedo.Soltando && !dedo.EnHud && !dedo.Pinta && !dedo.EligeMuros;
                if (quieto && batalla && dedo.SinTropas && Time.time - dedo.Desde < SegundosParaSoltarSeguido)
                {
                    MostrarMensaje($"No te quedan {Terminos.Tropas} de ese tipo");
                    continue;
                }
                if (quieto && (!batalla || dedo.Despliega))
                {
                    huboToque = true;
                    toque = dedo.Actual;
                }
            }

            // Soltar tropas seguidas con cada dedo quieto (se puede ir deslizando mientras suelta).
            foreach (var dedo in _dedos.Values)
            {
                if (!dedo.Soltando || !batalla || Time.time < dedo.SiguienteTropa) continue;
                dedo.SiguienteTropa = Time.time + SegundosEntreTropas;
                // Al deslizarse sobre un edificio simplemente no suelta ahí.
                if (PunteroEnSuelo(dedo.Actual, out Vector3 punto) && Batalla.PuedeDesplegarEn(punto)) Batalla.Desplegar(punto);
            }

            // Mover el mapa o hacer zoom con los dedos que arrastran.
            Dedo primero = null, segundo = null;
            foreach (var dedo in _dedos.Values)
            {
                if (!dedo.Arrastra || dedo.EnHud) continue;
                if (primero == null) primero = dedo;
                else if (segundo == null) segundo = dedo;
            }
            if (primero != null && segundo != null)
            {
                float antes = Vector2.Distance(primero.Ultima, segundo.Ultima);
                float ahora = Vector2.Distance(primero.Actual, segundo.Actual);
                if (antes > 1f && ahora > 1f) Zoom(antes / ahora);
            }
            else if (primero != null && PunteroEnSuelo(primero.Ultima, out Vector3 desde) && PunteroEnSuelo(primero.Actual, out Vector3 hasta))
            {
                Desplazar(desde - hasta);
            }
            return huboToque;
        }

        /// <summary>Pone murallas desde donde empezó el dedo hasta donde va, en la dirección en que arrancó.</summary>
        private void PintarMurallas(Dedo dedo)
        {
            if (Colocando == null || Colocando.Id != BuildingId.Muralla) return;
            if (!PunteroEnSuelo(dedo.Actual, out Vector3 punto)) return;
            var delta = Mapa.MundoACasilla(punto) - dedo.PintaInicio;
            if (dedo.PintaEje == 0)
            {
                if (delta.x == 0 && delta.y == 0) return;
                dedo.PintaEje = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? 1 : 2;
            }
            var paso = dedo.PintaEje == 1 ? new Vector2Int(1, 0) : new Vector2Int(0, 1);
            int hasta = dedo.PintaEje == 1 ? delta.x : delta.y;
            // Si el dedo va rápido se rellenan las casillas que se saltó.
            for (int k = dedo.PintaMax + 1; k <= hasta && Colocando != null; k++)
            {
                ColocarMurallaEn(dedo.PintaInicio + new Vector2Int(paso.x * k, paso.y * k));
                dedo.PintaMax = k;
            }
            for (int k = dedo.PintaMin - 1; k >= hasta && Colocando != null; k--)
            {
                ColocarMurallaEn(dedo.PintaInicio + new Vector2Int(paso.x * k, paso.y * k));
                dedo.PintaMin = k;
            }
        }

        private void ColocarMurallaEn(Vector2Int casilla)
        {
            // Las casillas ocupadas o fuera del terreno se saltan sin aviso, para no llenar la pantalla de mensajes.
            if (Colocando == null || !Mapa.PuedeConstruir(casilla, 1)) return;
            TryColocar(Colocando, casilla);
        }

        private void Zoom(float factor)
        {
            _zoom = Mathf.Clamp(_zoom * factor, ZoomMinimo, ZoomMaximo);
            AjustarCamara();
        }

        private void Desplazar(Vector3 delta)
        {
            delta.y = 0f;
            float limite = _ladoCamara * 0.6f;
            _desplazamiento += delta;
            _desplazamiento.x = Mathf.Clamp(_desplazamiento.x, -limite, limite);
            _desplazamiento.z = Mathf.Clamp(_desplazamiento.z, -limite, limite);
            AjustarCamara();
        }

        private bool PunteroEnSuelo(Vector2 posicion, out Vector3 punto)
        {
            punto = default;
            if (_camara == null) return false;
            var rayo = _camara.ScreenPointToRay(posicion);
            var suelo = new Plane(Vector3.up, Vector3.zero);
            if (!suelo.Raycast(rayo, out float distancia)) return false;
            punto = rayo.GetPoint(distancia);
            return true;
        }

        // ---------- Escena ----------

        private void PrepararCamara()
        {
            _camara = Camera.main;
            if (_camara == null)
            {
                _camara = new GameObject("Main Camera").AddComponent<Camera>();
                _camara.tag = "MainCamera";
            }
            _camara.orthographic = true;
            _camara.clearFlags = CameraClearFlags.SolidColor;
            _camara.backgroundColor = Deidad.CieloNormal;
            _camara.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            _camara.nearClipPlane = 0.1f;
            _camara.farClipPlane = 100f;
            EnfocarCamara(_centroCamara, _ladoCamara);
        }

        /// <summary>Mueve la cámara para encuadrar un mapa cuadrado de ese lado alrededor del centro.</summary>
        private void EnfocarCamara(Vector3 centro, float lado)
        {
            _centroCamara = centro;
            _ladoCamara = lado;
            _zoom = 1f;
            _desplazamiento = Vector3.zero;
            if (_camara == null) return;
            _camara.transform.position = centro - _camara.transform.forward * 40f;
            AjustarCamara();
        }

        /// <summary>Encuadra todo el mapa, tanto en horizontal como en vertical.</summary>
        private void AjustarCamara()
        {
            if (_camara == null) return;
            _camara.transform.position = _centroCamara + _desplazamiento - _camara.transform.forward * 40f;
            float diagonal = _ladoCamara * Mathf.Sqrt(2f);
            float alturaNecesaria = diagonal * Mathf.Sin(30f * Mathf.Deg2Rad) + 3f;
            float aspecto = Mathf.Max(_camara.aspect, 0.01f);
            float tamano = Mathf.Max(alturaNecesaria * 0.5f, diagonal / (2f * aspecto));
            // Margen extra para que las barras del HUD no tapen la aldea.
            _camara.orthographicSize = tamano * 1.3f * _zoom;
        }

        private void PrepararLuz()
        {
            _sol = FindAnyObjectByType<Light>();
            if (_sol != null) return;
            _sol = new GameObject("Sol").AddComponent<Light>();
            _sol.type = LightType.Directional;
            _sol.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>El cielo y la luz cambian poco a poco con el dios cuya ofrenda está activa.</summary>
        private void ActualizarAmbiente()
        {
            var deidad = Culto?.Activa;
            var cielo = deidad != null ? deidad.Cielo : Deidad.CieloNormal;
            var luz = deidad != null ? deidad.Luz : Color.white;
            float t = Mathf.Clamp01(Time.deltaTime * 1.5f);
            if (_camara != null) _camara.backgroundColor = Color.Lerp(_camara.backgroundColor, cielo, t);
            if (_sol != null) _sol.color = Color.Lerp(_sol.color, luz, t);
        }

        /// <summary>Tierra para todos, salvo los mexicas, que viven sobre el lago.</summary>
        private void ActualizarSuelo()
        {
            if (_suelo == null) return;
            bool lago = Pueblo != null && Pueblo.EnLago;
            // Fuera de la zona abierta el suelo es más oscuro (monte o agua honda).
            _suelo.material.color = lago ? new Color(0.24f, 0.45f, 0.54f) : new Color(0.50f, 0.45f, 0.31f);
            if (_zona != null) _zona.material.color = lago ? new Color(0.33f, 0.56f, 0.62f) : new Color(0.62f, 0.55f, 0.38f);
        }

        private int _ladoZona = -1;

        /// <summary>De entrada la cámara encuadra la zona abierta y un poco de lo que la rodea.</summary>
        private float LadoCamaraAldea => Mathf.Max(_ladoZona, LadoConstruible[0]) + 4f;

        /// <summary>La zona donde se puede construir crece con el tecpan.</summary>
        private void ActualizarZonaConstruible()
        {
            int nivel = Mathf.Clamp(NivelTecpan, 1, LadoConstruible.Length);
            int lado = LadoConstruible[nivel - 1];
            if (lado == _ladoZona) return;
            bool crecio = _ladoZona > 0 && lado > _ladoZona;
            _ladoZona = lado;
            Mapa.FijarArea(lado);
            if (_zona != null)
            {
                _zona.transform.localScale = new Vector3(lado, 0.01f, lado);
                _zona.transform.position = Mapa.Centro + Vector3.up * 0.005f;
            }
            if (ModoActual == Modo.Aldea) _ladoCamara = LadoCamaraAldea;
            if (crecio) MostrarMensaje("¡Se abrió más terreno para construir!");
        }

        private void CrearSuelo()
        {
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo";
            Destroy(suelo.GetComponent<Collider>());
            // Un Plane de Unity mide 10x10 unidades.
            suelo.transform.localScale = new Vector3(TamanoMapa / 10f, 1f, TamanoMapa / 10f);
            suelo.transform.position = Mapa.Centro;
            _suelo = suelo.GetComponent<Renderer>();

            var zona = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zona.name = "Zona construible";
            Destroy(zona.GetComponent<Collider>());
            _zona = zona.GetComponent<Renderer>();
            ActualizarSuelo();
        }

        private static Transform CrearMarcador(string nombre, out Renderer render)
        {
            var marcador = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marcador.name = nombre;
            Destroy(marcador.GetComponent<Collider>());
            render = marcador.GetComponent<Renderer>();
            marcador.SetActive(false);
            return marcador.transform;
        }

        private void ActualizarMarcadores()
        {
            // Vista previa de dónde se colocará el edificio.
            bool mostrarFantasma = false;
            var enMano = Moviendo != null ? Moviendo.Definicion : Colocando;
            if (ModoActual == Modo.Aldea && enMano != null && LeerPuntero(out Vector2 posicion, out _, out _)
                && !_hud.PunteroSobreHud(posicion) && PunteroEnSuelo(posicion, out Vector3 punto))
            {
                int tamano = enMano.Tamano;
                var casilla = Mapa.MundoACasilla(punto);
                var origen = Moviendo != null && _grupo.Count > 1 ? casilla : Mapa.AjustarOrigen(casilla, tamano);
                bool libre = Moviendo != null ? GrupoCabeEn(origen) : Mapa.PuedeConstruir(origen, tamano);
                _fantasma.position = Mapa.CentroDeArea(origen, tamano) + Vector3.up * 0.05f;
                _fantasma.localScale = new Vector3(tamano, 0.1f, tamano);
                // Con un grupo de murallas, una sombra por cada una con la forma del grupo.
                var colorSombra = libre ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.9f, 0.25f, 0.2f);
                int extras = Moviendo != null ? _desfases.Count - 1 : 0;
                while (_sombrasGrupo.Count < extras)
                {
                    _sombrasGrupo.Add(CrearMarcador("Sombra del grupo", out _));
                }
                for (int i = 0; i < _sombrasGrupo.Count; i++)
                {
                    bool activa = i < extras;
                    _sombrasGrupo[i].gameObject.SetActive(activa);
                    if (!activa) continue;
                    var c = origen + _desfases[i + 1];
                    _sombrasGrupo[i].position = new Vector3(c.x + 0.5f, 0.05f, c.y + 0.5f);
                    _sombrasGrupo[i].localScale = new Vector3(1f, 0.1f, 1f);
                    _sombrasGrupo[i].GetComponent<Renderer>().material.color = colorSombra;
                }
                _fantasmaRender.material.color = libre ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.9f, 0.25f, 0.2f);
                mostrarFantasma = true;
            }
            _fantasma.gameObject.SetActive(mostrarFantasma);
            if (!mostrarFantasma)
            {
                foreach (var sombra in _sombrasGrupo) sombra.gameObject.SetActive(false);
            }

            // Marco bajo el edificio seleccionado.
            bool haySeleccion = ModoActual == Modo.Aldea && Seleccionado != null;
            if (haySeleccion)
            {
                int tamano = Seleccionado.Definicion.Tamano;
                // En el lago el marco es más alto para que asome alrededor de la plataforma.
                float alto = Pueblo.EnLago ? 0.18f : 0.04f;
                _marcaSeleccion.position = Mapa.CentroDeArea(Seleccionado.Origen, tamano) + Vector3.up * (alto * 0.5f);
                _marcaSeleccion.localScale = new Vector3(tamano + 0.1f, alto, tamano + 0.1f);
            }
            _marcaSeleccion.gameObject.SetActive(haySeleccion);

            // Marcas sobre las murallas elegidas para mover.
            int marcas = EligiendoMuros && ModoActual == Modo.Aldea ? _murosElegidos.Count : 0;
            while (_marcasMuros.Count < marcas)
            {
                var marca = CrearMarcador("Muralla elegida", out var render);
                render.material.color = new Color(1f, 0.85f, 0.2f);
                _marcasMuros.Add(marca);
            }
            for (int i = 0; i < _marcasMuros.Count; i++)
            {
                bool activa = i < marcas;
                _marcasMuros[i].gameObject.SetActive(activa);
                if (!activa) continue;
                var origen = _murosElegidos[i].Origen;
                _marcasMuros[i].position = new Vector3(origen.x + 0.5f, 0.6f, origen.y + 0.5f);
                _marcasMuros[i].localScale = new Vector3(0.7f, 0.06f, 0.7f);
            }
        }

        private readonly List<Transform> _marcasMuros = new List<Transform>();
        private readonly List<Transform> _sombrasGrupo = new List<Transform>();
    }

    /// <summary>Un edificio (o varios) a cierto nivel que el tecpan pide antes de subir.</summary>
    public struct RequisitoTecpan
    {
        public BuildingId Id;
        public int Nivel;
        public int Cantidad;

        public RequisitoTecpan(BuildingId id, int nivel, int cantidad = 1)
        {
            Id = id;
            Nivel = nivel;
            Cantidad = cantidad;
        }
    }
}
