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
        public const int TamanoMapa = 20;
        private const float SegundosEntreAutoguardados = 30f;

        public Pueblo Pueblo { get; private set; }
        public ResourceBank Banco { get; private set; }
        public GridMap Mapa { get; private set; }
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
            var tropasEnAldea = new GameObject("Tropas en la aldea").AddComponent<TropasEnAldea>();
            tropasEnAldea.Manager = this;
            _tropasEnAldea = tropasEnAldea;
            var paisaje = new GameObject("Paisaje").AddComponent<PaisajeAldea>();
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

        public void EmpezarColocacion(BuildingDefinition definicion)
        {
            Seleccionar(null);
            if (EnLimite(definicion))
            {
                MostrarMensaje("Mejora el tecpan para construir más");
                return;
            }
            if (!Banco.PuedePagar(definicion.Costo))
            {
                MostrarMensaje("Recursos insuficientes");
                return;
            }
            Colocando = definicion;
        }

        public void CancelarColocacion()
        {
            Colocando = null;
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

        private void Update()
        {
            if (Mensaje != null && Time.time > _mensajeHasta) Mensaje = null;
            AjustarCamara();
            ActualizarMarcadores();
            ActualizarAmbiente();

            if (Pueblo == null) return;
            if (NivelTelpochcalli > 0) Ejercito.Avanzar(Time.deltaTime, DuracionEntrenamiento);
            // Los heridos empiezan a sanar ya de vuelta en la aldea, cuando llegan al temazcalli.
            bool curando = ModoActual == Modo.Aldea && !_tropasEnAldea.HeridosLlegando;
            int curados = curando ? Ejercito.Curar(Time.deltaTime, CamasCuracion) : 0;
            if (curados > 0) MostrarMensaje(curados == 1 ? "Una tropa sanó en el temazcalli" : $"{curados} tropas sanaron en el temazcalli");
            Culto.Avanzar(Time.deltaTime);
            Banco.BonoCapacidad = Culto.Bono(TipoBono.Almacen);
            if (Time.time >= _proximoAutoguardado) Guardar();
            if (!LeerPuntero(out Vector2 posicion, out bool presionado, out bool cancelar)) return;

            if (cancelar) CancelarColocacion();
            if (!presionado || _hud.PunteroSobreHud(posicion)) return;
            if (!PunteroEnSuelo(posicion, out Vector3 punto)) return;

            if (ModoActual == Modo.Batalla)
            {
                if (Batalla != null) Batalla.Desplegar(punto);
                return;
            }

            var casilla = Mapa.MundoACasilla(punto);
            if (Colocando != null)
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
            if (!Mapa.EstaLibre(origen, definicion.Tamano))
            {
                MostrarMensaje("Ese lugar está ocupado");
                return;
            }
            if (!Banco.TryGastar(definicion.Costo))
            {
                MostrarMensaje("Recursos insuficientes");
                Colocando = null;
                return;
            }
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
            SinRecursos,
        }

        public EstadoMejora PuedeMejorar(Building edificio)
        {
            if (edificio.EnConstruccion) return EstadoMejora.EnObra;
            if (edificio.Nivel >= edificio.Definicion.NivelMaximo) return EstadoMejora.NivelMaximo;
            if (edificio.Definicion.Id != BuildingId.Tecpan && edificio.Nivel >= NivelTecpan)
                return EstadoMejora.RequiereTecpan;
            if (!Banco.PuedePagar(edificio.Definicion.CostoMejora(edificio.Nivel))) return EstadoMejora.SinRecursos;
            return EstadoMejora.Disponible;
        }

        public void TryMejorar(Building edificio)
        {
            if (PuedeMejorar(edificio) != EstadoMejora.Disponible) return;
            if (!Banco.TryGastar(edificio.Definicion.CostoMejora(edificio.Nivel))) return;
            edificio.EmpezarMejora();
            Guardar();
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
            MostrarMensaje(terminadas == 1 ? "Una tropa terminó su entrenamiento" : $"{terminadas} tropas terminaron su entrenamiento");
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
            if (Ejercito.Total <= 0)
            {
                MostrarMensaje("Entrena tropas en el telpochcalli antes de atacar");
                return;
            }
            if (seleccion == null || seleccion.Total <= 0)
            {
                MostrarMensaje("Elige al menos una tropa");
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
            EnfocarCamara(Mapa.Centro, TamanoMapa);
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
            if (tropasNuevas > 0) ganancias.Add($"+{tropasNuevas} tropas");
            if (curados > 0) ganancias.Add($"{curados} tropas curadas");
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

        private static bool LeerPuntero(out Vector2 posicion, out bool presionado, out bool cancelar)
        {
#if ENABLE_INPUT_SYSTEM
            var puntero = Pointer.current;
            posicion = puntero != null ? puntero.position.ReadValue() : Vector2.zero;
            presionado = puntero != null && puntero.press.wasPressedThisFrame;
            var raton = Mouse.current;
            var teclado = Keyboard.current;
            cancelar = (raton != null && raton.rightButton.wasPressedThisFrame)
                       || (teclado != null && teclado.escapeKey.wasPressedThisFrame);
            return puntero != null;
#else
            posicion = Input.mousePosition;
            presionado = Input.GetMouseButtonDown(0);
            cancelar = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);
            return true;
#endif
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
            if (_camara == null) return;
            _camara.transform.position = centro - _camara.transform.forward * 40f;
            AjustarCamara();
        }

        /// <summary>Encuadra todo el mapa, tanto en horizontal como en vertical.</summary>
        private void AjustarCamara()
        {
            if (_camara == null) return;
            float diagonal = _ladoCamara * Mathf.Sqrt(2f);
            float alturaNecesaria = diagonal * Mathf.Sin(30f * Mathf.Deg2Rad) + 3f;
            float aspecto = Mathf.Max(_camara.aspect, 0.01f);
            float tamano = Mathf.Max(alturaNecesaria * 0.5f, diagonal / (2f * aspecto));
            // Margen extra para que las barras del HUD no tapen la aldea.
            _camara.orthographicSize = tamano * 1.3f;
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
            _suelo.material.color = Pueblo != null && Pueblo.EnLago
                ? new Color(0.28f, 0.50f, 0.58f)
                : new Color(0.62f, 0.55f, 0.38f);
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
            if (ModoActual == Modo.Aldea && Colocando != null && LeerPuntero(out Vector2 posicion, out _, out _)
                && !_hud.PunteroSobreHud(posicion) && PunteroEnSuelo(posicion, out Vector3 punto))
            {
                var origen = Mapa.AjustarOrigen(Mapa.MundoACasilla(punto), Colocando.Tamano);
                bool libre = Mapa.EstaLibre(origen, Colocando.Tamano);
                _fantasma.position = Mapa.CentroDeArea(origen, Colocando.Tamano) + Vector3.up * 0.05f;
                _fantasma.localScale = new Vector3(Colocando.Tamano, 0.1f, Colocando.Tamano);
                _fantasmaRender.material.color = libre ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.9f, 0.25f, 0.2f);
                mostrarFantasma = true;
            }
            _fantasma.gameObject.SetActive(mostrarFantasma);

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
        }
    }
}
