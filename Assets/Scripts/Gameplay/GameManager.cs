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

        public string Mensaje { get; private set; }

        private readonly List<Building> _edificios = new List<Building>();
        private Camera _camara;
        private GameHud _hud;
        private Transform _fantasma;
        private Renderer _fantasmaRender;
        private Transform _marcaSeleccion;
        private float _mensajeHasta;
        private float _proximoAutoguardado;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CrearAutomaticamente()
        {
            if (FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("Altepetl").AddComponent<GameManager>();
        }

        private void Awake()
        {
            Mapa = new GridMap(TamanoMapa, TamanoMapa);
            _hud = gameObject.AddComponent<GameHud>();
            _hud.Manager = this;

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

        public void EmpezarColocacion(BuildingDefinition definicion)
        {
            Seleccionar(null);
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

        private void Update()
        {
            if (Mensaje != null && Time.time > _mensajeHasta) Mensaje = null;
            AjustarCamara();
            ActualizarMarcadores();

            if (Pueblo == null) return;
            if (Time.time >= _proximoAutoguardado) Guardar();
            if (!LeerPuntero(out Vector2 posicion, out bool presionado, out bool cancelar)) return;

            if (cancelar) CancelarColocacion();
            if (!presionado || _hud.PunteroSobreHud(posicion)) return;
            if (!PunteroEnSuelo(posicion, out Vector3 punto)) return;

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
            Construir(definicion, origen, 0, definicion.SegundosParaNivel(1) * Pueblo.MultiplicadorTiempoConstruccion);
            Colocando = null;
            Guardar();
        }

        private Building Construir(BuildingDefinition definicion, Vector2Int origen, int nivel,
            float segundosRestantes, float acumulado = 0f)
        {
            var edificio = new GameObject().AddComponent<Building>();
            edificio.Inicializar(definicion, Pueblo, origen, Banco, Mapa, nivel, segundosRestantes, acumulado);
            Mapa.Ocupar(origen, definicion.Tamano, edificio);
            _edificios.Add(edificio);
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
            SaveSystem.Guardar(datos);
        }

        private void Restaurar(SaveData datos)
        {
            Pueblo = Pueblo.Get(datos.pueblo);
            Banco = new ResourceBank();

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
            for (int i = 0; i < _edificios.Count; i++)
            {
                _edificios[i].Producir(sobrantes[i]);
            }

            var ganancias = new List<string>();
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                int ganancia = Banco.Get((ResourceType)i) - antes[i];
                if (ganancia > 0) ganancias.Add($"+{ganancia} {ResourceInfo.Nombre((ResourceType)i).ToLowerInvariant()}");
            }
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
            _camara.backgroundColor = new Color(0.55f, 0.75f, 0.85f);
            _camara.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            _camara.transform.position = Mapa.Centro - _camara.transform.forward * 40f;
            _camara.nearClipPlane = 0.1f;
            _camara.farClipPlane = 100f;
            AjustarCamara();
        }

        /// <summary>Encuadra todo el mapa, tanto en horizontal como en vertical.</summary>
        private void AjustarCamara()
        {
            if (_camara == null) return;
            float diagonal = TamanoMapa * Mathf.Sqrt(2f);
            float alturaNecesaria = diagonal * Mathf.Sin(30f * Mathf.Deg2Rad) + 3f;
            float aspecto = Mathf.Max(_camara.aspect, 0.01f);
            float tamano = Mathf.Max(alturaNecesaria * 0.5f, diagonal / (2f * aspecto));
            // Margen extra para que las barras del HUD no tapen la aldea.
            _camara.orthographicSize = tamano * 1.3f;
        }

        private static void PrepararLuz()
        {
            if (FindAnyObjectByType<Light>() != null) return;
            var luz = new GameObject("Sol").AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private void CrearSuelo()
        {
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo";
            Destroy(suelo.GetComponent<Collider>());
            // Un Plane de Unity mide 10x10 unidades.
            suelo.transform.localScale = new Vector3(TamanoMapa / 10f, 1f, TamanoMapa / 10f);
            suelo.transform.position = Mapa.Centro;
            suelo.GetComponent<Renderer>().material.color = new Color(0.62f, 0.55f, 0.38f);
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
            if (Colocando != null && LeerPuntero(out Vector2 posicion, out _, out _)
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
            bool haySeleccion = Seleccionado != null;
            if (haySeleccion)
            {
                int tamano = Seleccionado.Definicion.Tamano;
                _marcaSeleccion.position = Mapa.CentroDeArea(Seleccionado.Origen, tamano) + Vector3.up * 0.02f;
                _marcaSeleccion.localScale = new Vector3(tamano + 0.1f, 0.04f, tamano + 0.1f);
            }
            _marcaSeleccion.gameObject.SetActive(haySeleccion);
        }
    }
}
