using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Interfaz provisional con IMGUI: elegir pueblo, recursos, menú de construcción
    /// y panel del edificio seleccionado. Se reemplazará por UI Toolkit o uGUI con arte real.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        // Resolución de referencia; todo se escala para verse igual en cualquier pantalla.
        private const float AnchoReferencia = 960f;
        private const float AltoReferencia = 540f;
        private const float AltoBarraSuperior = 44f;
        private const float AltoBarraInferior = 110f;

        public GameManager Manager;

        private readonly List<Rect> _zonasHud = new List<Rect>();
        private float _escala = 1f;
        private GUIStyle _titulo;
        private GUIStyle _texto;
        private GUIStyle _textoChico;
        private GUIStyle _boton;
        private GUIStyle _caja;
        private Texture2D _blanco;
        private bool _campanaAbierta;
        private bool _menuAbierto;
        private CategoriaEdificio _pestana = CategoriaEdificio.Suministros;
        private BuildingDefinition _info;

        /// <summary>¿La posición (en píxeles de pantalla, origen abajo) está sobre algún panel?</summary>
        public bool PunteroSobreHud(Vector2 posicionPantalla)
        {
            if (Manager.Pueblo == null) return true;
            var gui = new Vector2(posicionPantalla.x / _escala, (Screen.height - posicionPantalla.y) / _escala);
            foreach (var zona in _zonasHud)
            {
                if (zona.Contains(gui)) return true;
            }
            return false;
        }

        private void OnGUI()
        {
            _escala = Mathf.Min(Screen.width / AnchoReferencia, Screen.height / AltoReferencia);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_escala, _escala, 1f));
            float ancho = Screen.width / _escala;
            float alto = Screen.height / _escala;
            PrepararEstilos();

            if (Event.current.type == EventType.Layout) _zonasHud.Clear();

            if (Manager.Pueblo == null)
            {
                DibujarEleccionDePueblo(ancho, alto);
                return;
            }

            if (Manager.ModoActual == GameManager.Modo.Batalla)
            {
                DibujarBatalla(ancho, alto);
                DibujarMensaje(ancho, alto);
                return;
            }

            DibujarRecursos(ancho);
            DibujarMenuConstruccion(ancho, alto);
            DibujarBotonAtacar(alto);
            DibujarPanelSeleccion(ancho);
            DibujarCampana(ancho);
            DibujarMensaje(ancho, alto);
        }

        private void Zona(Rect rect)
        {
            if (Event.current.type == EventType.Layout) _zonasHud.Add(rect);
        }

        private void DibujarEleccionDePueblo(float ancho, float alto)
        {
            GUI.Box(new Rect(0, 0, ancho, alto), GUIContent.none, _caja);
            GUI.Label(new Rect(0, 30, ancho, 40), "Altepetl — elige tu pueblo", _titulo);

            float anchoTarjeta = Mathf.Min(280f, (ancho - 80f) / 3f);
            float x = (ancho - anchoTarjeta * 3f - 40f) / 2f;
            foreach (var pueblo in Pueblo.Todos)
            {
                var tarjeta = new Rect(x, 100, anchoTarjeta, 300);
                GUI.Box(tarjeta, GUIContent.none, _caja);
                GUI.Label(new Rect(tarjeta.x + 10, tarjeta.y + 10, tarjeta.width - 20, 30), pueblo.Nombre, _titulo);
                GUI.Label(new Rect(tarjeta.x + 10, tarjeta.y + 50, tarjeta.width - 20, 160),
                    $"{pueblo.Ciudad}\nEstilo: {pueblo.Estilo}\n\n{pueblo.Descripcion}", _texto);
                if (GUI.Button(new Rect(tarjeta.x + 20, tarjeta.yMax - 60, tarjeta.width - 40, 44), "Elegir", _boton))
                {
                    Manager.ElegirPueblo(pueblo);
                }
                x += anchoTarjeta + 20f;
            }
        }

        private void DibujarRecursos(float ancho)
        {
            var barra = new Rect(0, 0, ancho, AltoBarraSuperior);
            Zona(barra);
            GUI.Box(barra, GUIContent.none, _caja);

            var banco = Manager.Banco;
            float x = 10f;
            float anchoCelda = (ancho - 20f) / (ResourceInfo.Count + 1);
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                var tipo = (ResourceType)i;
                int maximo = banco.Capacidad(tipo);
                string capacidad = maximo == int.MaxValue ? "" : $" / {maximo}";
                GUI.Label(new Rect(x, 10, anchoCelda, 24), $"{ResourceInfo.Nombre(tipo)}: {banco.Get(tipo)}{capacidad}", _texto);
                x += anchoCelda;
            }
            GUI.Label(new Rect(x, 10, anchoCelda, 24), Manager.Pueblo.Nombre, _texto);
        }

        private void DibujarMenuConstruccion(float ancho, float alto)
        {
            if (Manager.Colocando != null)
            {
                var barra = new Rect(0, alto - 64, ancho, 64);
                Zona(barra);
                GUI.Box(barra, GUIContent.none, _caja);
                GUI.Label(new Rect(20, barra.y + 18, ancho - 200, 30),
                    $"Toca el mapa para colocar: {Manager.Colocando.NombrePara(Manager.Pueblo)}", _texto);
                if (GUI.Button(new Rect(ancho - 170, barra.y + 8, 150, 48), "Cancelar", _boton))
                {
                    Manager.CancelarColocacion();
                }
                _menuAbierto = false;
                return;
            }

            var boton = new Rect(ancho - 160, alto - 60, 150, 50);
            Zona(boton);
            if (GUI.Button(boton, "Construir", _boton))
            {
                _menuAbierto = !_menuAbierto;
                _info = null;
                if (_menuAbierto)
                {
                    _campanaAbierta = false;
                    Manager.Seleccionar(null);
                }
            }
            if (!_menuAbierto) return;
            if (Manager.Seleccionado != null)
            {
                _menuAbierto = false;
                return;
            }

            var panel = new Rect((ancho - 600) / 2, AltoBarraSuperior + 10, 600, 400);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);
            if (GUI.Button(new Rect(panel.xMax - 38, panel.y + 6, 32, 28), "X", _boton))
            {
                _menuAbierto = false;
                return;
            }

            if (_info != null)
            {
                DibujarInfoEdificio(panel, _info);
                return;
            }

            GUI.Label(new Rect(panel.x + 10, panel.y + 8, panel.width - 50, 28), "Construir", _titulo);

            // Pestañas
            var categorias = (CategoriaEdificio[])System.Enum.GetValues(typeof(CategoriaEdificio));
            float anchoPestana = (panel.width - 20f) / categorias.Length;
            for (int i = 0; i < categorias.Length; i++)
            {
                GUI.backgroundColor = categorias[i] == _pestana ? new Color(1f, 0.85f, 0.2f) : Color.white;
                if (GUI.Button(new Rect(panel.x + 10 + i * anchoPestana, panel.y + 44, anchoPestana - 6, 36),
                        NombreCategoria(categorias[i]), _boton))
                {
                    _pestana = categorias[i];
                }
            }
            GUI.backgroundColor = Color.white;

            // Tarjetas de la pestaña elegida
            const int PorFila = 4;
            float anchoTarjeta = (panel.width - 20f) / PorFila;
            int indice = 0;
            foreach (var def in BuildingCatalog.Todos)
            {
                if (def.Id == BuildingId.Tecpan || def.Categoria != _pestana) continue;
                var tarjeta = new Rect(panel.x + 10 + (indice % PorFila) * anchoTarjeta, panel.y + 92 + (indice / PorFila) * 150,
                    anchoTarjeta - 8, 296);
                DibujarTarjeta(tarjeta, def);
                indice++;
            }
        }

        private void DibujarTarjeta(Rect tarjeta, BuildingDefinition def)
        {
            GUI.Box(tarjeta, GUIContent.none, _caja);
            string resumen = def.Construible ? TextoCosto(def.Costo) : "Próximamente";
            GUI.Label(new Rect(tarjeta.x + 8, tarjeta.y + 6, tarjeta.width - 16, 44), def.NombrePara(Manager.Pueblo), _texto);
            GUI.Label(new Rect(tarjeta.x + 8, tarjeta.y + 52, tarjeta.width - 16, 80),
                $"{resumen}\n{ResumenEdificio(def)}", _textoChico);
            if (def.Construible)
            {
                GUI.Label(new Rect(tarjeta.x + 8, tarjeta.yMax - 162, tarjeta.width - 16, 64), TextoLimite(def), _textoChico);
            }

            GUI.enabled = PuedeConstruir(def);
            if (GUI.Button(new Rect(tarjeta.x + 8, tarjeta.yMax - 96, tarjeta.width - 16, 44), "Construir", _boton))
            {
                _menuAbierto = false;
                Manager.EmpezarColocacion(def);
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(tarjeta.x + 8, tarjeta.yMax - 46, tarjeta.width - 16, 36), "Información", _boton))
            {
                _info = def;
            }
        }

        private void DibujarInfoEdificio(Rect panel, BuildingDefinition def)
        {
            GUI.Label(new Rect(panel.x + 10, panel.y + 8, panel.width - 50, 28), def.NombrePara(Manager.Pueblo), _titulo);
            GUI.Label(new Rect(panel.x + 20, panel.y + 48, panel.width - 40, 260),
                def.Descripcion + "\n\n" + DetallesEdificio(def), _texto);

            if (GUI.Button(new Rect(panel.x + 20, panel.yMax - 60, 160, 44), "Volver", _boton))
            {
                _info = null;
            }
            GUI.enabled = PuedeConstruir(def);
            string texto = def.Construible ? $"Construir\n{TextoCosto(def.Costo)}" : "Próximamente";
            if (GUI.Button(new Rect(panel.xMax - 260, panel.yMax - 60, 240, 44), texto, _boton))
            {
                _menuAbierto = false;
                _info = null;
                Manager.EmpezarColocacion(def);
            }
            GUI.enabled = true;
        }

        /// <summary>"2 tlamani, 3 guerrero experimentado": tropas con rango, sin contar a los jóvenes.</summary>
        private string TextoRangos(Army ejercito)
        {
            var partes = new List<string>();
            for (int r = Rangos.Count - 1; r > Rangos.Joven; r--)
            {
                int n = ejercito.ConRango(r);
                if (n > 0) partes.Add($"{n} {Rangos.Nombre(Manager.Pueblo, r).ToLowerInvariant()}");
            }
            return string.Join(", ", partes);
        }

        private bool PuedeConstruir(BuildingDefinition def)
        {
            return def.Construible && !Manager.EnLimite(def) && Manager.Banco.PuedePagar(def.Costo);
        }

        private string TextoLimite(BuildingDefinition def)
        {
            int maximo = Manager.Maximo(def);
            int tienes = Manager.Cantidad(def.Id);
            if (maximo == int.MaxValue) return $"Tienes: {tienes}";
            string texto = $"Tienes: {tienes} / {maximo}";
            if (tienes >= maximo)
            {
                texto += Manager.NivelTecpan < BuildingCatalog.Get(BuildingId.Tecpan).NivelMaximo
                    ? "\nSube el tecpan para más"
                    : "\nMáximo alcanzado";
            }
            return texto;
        }

        private static string NombreCategoria(CategoriaEdificio categoria)
        {
            switch (categoria)
            {
                case CategoriaEdificio.Suministros: return "Suministros";
                case CategoriaEdificio.Defensas: return "Defensas";
                default: return "Militar";
            }
        }

        /// <summary>Una línea con lo más importante del edificio en nivel 1.</summary>
        private string ResumenEdificio(BuildingDefinition def)
        {
            if (def.Produce) return $"+{ProduccionInicial(def):0.#} {ResourceInfo.Nombre(def.Recurso).ToLowerInvariant()}/min";
            if (def.CapacidadExtra > 0) return $"+{def.CapacidadExtra} de almacén";
            if (def.CapacidadTropas > 0) return $"{def.CapacidadTropas} de espacio para tropas";
            if (def.EsDefensa) return $"{def.DanoDefensaPorSegundo:0.#} de daño por segundo";
            return $"Vida: {VidaInicial(def)}";
        }

        private string DetallesEdificio(BuildingDefinition def)
        {
            var lineas = new List<string>
            {
                $"Tamaño: {def.Tamano}x{def.Tamano} casillas",
                $"Construcción: {Mathf.CeilToInt(def.SegundosParaNivel(1) * Manager.Pueblo.MultiplicadorTiempoConstruccion)} s",
                $"Vida: {VidaInicial(def)}",
            };
            if (def.Produce) lineas.Add($"Produce {ProduccionInicial(def):0.#} de {ResourceInfo.Nombre(def.Recurso).ToLowerInvariant()} por minuto");
            if (def.CapacidadExtra > 0) lineas.Add($"Almacén: +{def.CapacidadExtra} de cada recurso");
            if (def.CapacidadTropas > 0) lineas.Add($"Espacio para tropas: {def.CapacidadTropas}");
            if (def.EsDefensa) lineas.Add($"Alcance: {def.AlcanceDefensa:0.#} casillas, daño: {def.DanoDefensaPorSegundo:0.#} por segundo");
            if (def.NivelMaximo > 1) lineas.Add($"Se mejora hasta nivel {def.NivelMaximo}: +50% por nivel, sin pasar el nivel del tecpan");

            lineas.Add(TextoLimite(def));
            return string.Join("\n", lineas);
        }

        private float ProduccionInicial(BuildingDefinition def)
        {
            float mult = def.Recurso == ResourceType.Maiz ? Manager.Pueblo.MultiplicadorMaiz : 1f;
            return def.ProduccionPorMinuto * mult;
        }

        private int VidaInicial(BuildingDefinition def)
        {
            float mult = def.Id == BuildingId.Muralla ? Manager.Pueblo.MultiplicadorVidaMurallas : 1f;
            return Mathf.RoundToInt(def.VidaBase * mult);
        }

        private void DibujarPanelSeleccion(float ancho)
        {
            var edificio = Manager.Seleccionado;
            if (edificio == null) return;

            var def = edificio.Definicion;
            bool entrena = def.CapacidadTropas > 0 && edificio.Nivel > 0;
            var panel = new Rect(ancho - 290, AltoBarraSuperior + 10, 280, entrena ? 370 : 250);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);

            GUI.Label(new Rect(panel.x + 10, panel.y + 8, panel.width - 50, 28), edificio.Nombre, _titulo);
            if (GUI.Button(new Rect(panel.xMax - 38, panel.y + 6, 32, 28), "X", _boton))
            {
                Manager.Seleccionar(null);
                return;
            }

            string info = (edificio.Nivel > 0 ? $"Nivel {edificio.Nivel}. " : "") + def.Descripcion + "\n";
            if (edificio.Mejorando)
            {
                info += $"\nMejorando a nivel {edificio.Nivel + 1}: {Mathf.CeilToInt(edificio.SegundosRestantes)} s";
            }
            else if (edificio.EnConstruccion)
            {
                info += $"\nEn construcción: {Mathf.CeilToInt(edificio.SegundosRestantes)} s";
            }
            else if (def.Produce)
            {
                info += $"\nProduce {edificio.ProduccionPorMinuto:0.#} de {ResourceInfo.Nombre(def.Recurso)} por minuto";
                if (Manager.Banco.EstaLleno(def.Recurso)) info += "\nAlmacén lleno: construye o mejora un petlacalco";
            }
            info += $"\nVida: {edificio.Vida}";
            if (def.CapacidadTropas > 0 && edificio.Nivel > 0)
            {
                string veteranos = TextoRangos(Manager.Ejercito);
                if (veteranos.Length > 0) info += "\nCon rango: " + veteranos;
            }
            GUI.Label(new Rect(panel.x + 10, panel.y + 42, panel.width - 20, 120), info, _texto);
            if (entrena) DibujarEntrenamiento(panel);

            var botonRect = new Rect(panel.x + 10, panel.yMax - 54, panel.width - 20, 44);
            if (edificio.EnConstruccion)
            {
                int costo = edificio.CostoAcelerar;
                GUI.enabled = Manager.Banco.Get(ResourceType.Plumas) >= costo;
                if (GUI.Button(botonRect, $"Terminar ya ({costo} plumas de quetzal)", _boton))
                {
                    edificio.TryAcelerar();
                }
                GUI.enabled = true;
                return;
            }

            var estado = Manager.PuedeMejorar(edificio);
            switch (estado)
            {
                case GameManager.EstadoMejora.NivelMaximo:
                    GUI.Label(botonRect, "Nivel máximo", _texto);
                    break;
                case GameManager.EstadoMejora.RequiereTecpan:
                    GUI.Label(botonRect, $"Mejora el tecpan a nivel {edificio.Nivel + 1} para seguir", _texto);
                    break;
                default:
                    GUI.enabled = estado == GameManager.EstadoMejora.Disponible;
                    string extra = def.Produce
                        ? $" → {edificio.ProduccionEnNivel(edificio.Nivel + 1):0.#}/min"
                        : "";
                    string texto = $"Mejorar a nivel {edificio.Nivel + 1}{extra}\n{TextoCosto(def.CostoMejora(edificio.Nivel))}";
                    if (GUI.Button(botonRect, texto, _boton))
                    {
                        Manager.TryMejorar(edificio);
                    }
                    GUI.enabled = true;
                    break;
            }
        }

        private void DibujarEntrenamiento(Rect panel)
        {
            var ejercito = Manager.Ejercito;
            string estado = $"Ejército: {ejercito.Espacio} / {Manager.CapacidadEjercito}";
            if (ejercito.Entrenando)
            {
                var actual = TroopCatalog.Get(ejercito.Actual);
                estado += $"\nEntrenando {actual.Nombre.ToLowerInvariant()}: {Mathf.CeilToInt(ejercito.SegundosRestantes)} s"
                          + (ejercito.EnCola > 1 ? $" (+{ejercito.EnCola - 1} en cola)" : "");
            }
            GUI.Label(new Rect(panel.x + 10, panel.y + 160, panel.width - 20, 48), estado, _texto);

            float anchoBoton = (panel.width - 30f) / TroopCatalog.Count;
            float x = panel.x + 10f;
            bool hayEspacio = ejercito.Espacio < Manager.CapacidadEjercito;
            foreach (var tropa in TroopCatalog.Todos)
            {
                GUI.enabled = hayEspacio && Manager.Banco.PuedePagar(tropa.Costo);
                string texto = $"{tropa.Nombre} ({ejercito.Get(tropa.Id)})\n{TextoCosto(tropa.Costo)}";
                if (GUI.Button(new Rect(x, panel.y + 212, anchoBoton - 5f, 88), texto, _boton))
                {
                    Manager.TryEntrenar(tropa.Id);
                }
                GUI.enabled = true;
                x += anchoBoton + 5f;
            }
        }

        private void DibujarBotonAtacar(float alto)
        {
            if (Manager.Colocando != null) return;
            var rect = new Rect(10, alto - 60, 150, 50);
            Zona(rect);
            if (GUI.Button(rect, $"Atacar\n({Manager.Ejercito.Total} tropas)", _boton))
            {
                _campanaAbierta = !_campanaAbierta;
                if (_campanaAbierta)
                {
                    _menuAbierto = false;
                    Manager.Seleccionar(null);
                }
            }
        }

        private void DibujarCampana(float ancho)
        {
            if (!_campanaAbierta) return;
            if (Manager.Seleccionado != null || Manager.Colocando != null)
            {
                _campanaAbierta = false;
                return;
            }

            var panel = new Rect((ancho - 480) / 2, AltoBarraSuperior + 10, 480, 394);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);
            GUI.Label(new Rect(panel.x + 10, panel.y + 8, panel.width - 50, 28), "Campaña", _titulo);
            if (GUI.Button(new Rect(panel.xMax - 38, panel.y + 6, 32, 28), "X", _boton))
            {
                _campanaAbierta = false;
                return;
            }

            var ejercito = Manager.Ejercito;
            var partes = new List<string>();
            foreach (var tropa in TroopCatalog.Todos)
            {
                partes.Add($"{ejercito.Get(tropa.Id)} {tropa.Nombre.ToLowerInvariant()}");
            }
            string veteranosCampana = TextoRangos(ejercito);
            GUI.Label(new Rect(panel.x + 10, panel.y + 40, panel.width - 20, 48),
                "Tu ejército: " + string.Join(", ", partes)
                + (veteranosCampana.Length > 0 ? "\nCon rango: " + veteranosCampana : ""), _texto);

            float y = panel.y + 96;
            for (int i = 0; i < CampaignLevel.Todos.Length; i++)
            {
                var nivel = CampaignLevel.Todos[i];
                bool ganado = i < Manager.NivelesCompletados;
                bool disponible = i <= Manager.NivelesCompletados;
                var fila = new Rect(panel.x + 10, y, panel.width - 20, 88);
                GUI.Box(fila, GUIContent.none, _caja);

                string premio = ganado ? "Ganado" : $"Primera victoria: {nivel.PlumasPrimeraVez} plumas de quetzal";
                GUI.Label(new Rect(fila.x + 8, fila.y + 4, fila.width - 140, 84),
                    $"{i + 1}. {nivel.Nombre}\n{nivel.Descripcion}\n{premio}", _texto);

                GUI.enabled = disponible && ejercito.Total > 0;
                if (GUI.Button(new Rect(fila.xMax - 120, fila.y + 20, 110, 48), disponible ? "Atacar" : "Bloqueado", _boton))
                {
                    _campanaAbierta = false;
                    Manager.EmpezarBatalla(i);
                }
                GUI.enabled = true;
                y += 96;
            }
        }

        // ---------- Batalla ----------

        private void DibujarBatalla(float ancho, float alto)
        {
            var batalla = Manager.Batalla;
            if (batalla == null) return;

            var barra = new Rect(0, 0, ancho, AltoBarraSuperior);
            Zona(barra);
            GUI.Box(barra, GUIContent.none, _caja);
            int segundos = Mathf.CeilToInt(batalla.TiempoRestante);
            GUI.Label(new Rect(10, 10, ancho - 20, 24),
                $"{batalla.Nivel.Nombre}    Destrucción: {Mathf.FloorToInt(batalla.Destruccion * 100)}%    Tiempo: {segundos / 60}:{segundos % 60:00}",
                _texto);

            DibujarBarrasDeVida(batalla);

            if (batalla.Terminada)
            {
                DibujarResultado(batalla, ancho, alto);
                return;
            }

            var abajo = new Rect(0, alto - AltoBarraInferior, ancho, AltoBarraInferior);
            Zona(abajo);
            GUI.Box(abajo, GUIContent.none, _caja);

            float x = 15f;
            foreach (var tropa in TroopCatalog.Todos)
            {
                int cantidad = batalla.Disponibles(tropa.Id);
                bool elegida = batalla.Seleccionada == tropa.Id && cantidad > 0;
                GUI.enabled = cantidad > 0;
                GUI.backgroundColor = elegida ? new Color(1f, 0.85f, 0.2f) : Color.white;
                int conRango = cantidad - batalla.Disponibles(tropa.Id, Rangos.Joven);
                string texto = $"{tropa.Nombre}\nx{cantidad}" + (conRango > 0 ? $"\n({conRango} con rango)" : "");
                if (GUI.Button(new Rect(x, abajo.y + 10, 150, AltoBarraInferior - 20), texto, _boton))
                {
                    batalla.Seleccionada = tropa.Id;
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
                x += 160f;
            }
            GUI.Label(new Rect(x + 10, abajo.y + 20, ancho - x - 200, 70),
                "Toca el campo, fuera de los edificios, para desplegar la tropa elegida.", _texto);
            if (GUI.Button(new Rect(ancho - 170, abajo.y + 30, 150, 50), "Retirarse", _boton))
            {
                batalla.Terminar();
            }
        }

        private void DibujarBarrasDeVida(BattleManager batalla)
        {
            var camara = Camera.main;
            if (camara == null || Event.current.type != EventType.Repaint) return;

            foreach (var edificio in batalla.Edificios)
            {
                if (edificio.Destruido || edificio.Vida >= edificio.VidaMaxima) continue;
                var arriba = edificio.transform.position + Vector3.up * edificio.AlturaBarra;
                BarraDeVida(camara, arriba, edificio.Vida / edificio.VidaMaxima, 44f, new Color(0.85f, 0.2f, 0.15f));
            }
            foreach (var tropa in batalla.Tropas)
            {
                if (tropa == null || tropa.Muerta || tropa.Vida >= tropa.VidaMaxima) continue;
                var arriba = tropa.transform.position + Vector3.up * 0.9f;
                BarraDeVida(camara, arriba, tropa.Vida / tropa.VidaMaxima, 22f, new Color(0.3f, 0.85f, 0.3f));
            }
        }

        private void BarraDeVida(Camera camara, Vector3 mundo, float fraccion, float anchoBarra, Color color)
        {
            Vector3 pantalla = camara.WorldToScreenPoint(mundo);
            if (pantalla.z < 0f) return;
            float x = pantalla.x / _escala - anchoBarra * 0.5f;
            float y = (Screen.height - pantalla.y) / _escala;
            var fondo = new Rect(x, y, anchoBarra, 5f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(fondo, _blanco);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, anchoBarra * Mathf.Clamp01(fraccion), 5f), _blanco);
            GUI.color = Color.white;
        }

        private void DibujarResultado(BattleManager batalla, float ancho, float alto)
        {
            var resultado = batalla.Resultado;
            var panel = new Rect((ancho - 420) / 2, (alto - 340) / 2, 420, 340);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);
            GUI.Label(new Rect(panel.x + 10, panel.y + 15, panel.width - 20, 34),
                resultado.Victoria ? "¡Victoria!" : "Derrota", _titulo);

            string botin = TextoCosto(resultado.Botin);
            string texto = (resultado.TecpanDestruido ? "¡Cayó el tecpan! La ciudad se rinde.\n" : "")
                           + $"Destrucción: {Mathf.FloorToInt(resultado.Porcentaje * 100)}%"
                           + $"\nBotín: {(botin.Length > 0 ? botin : "nada")}";
            if (resultado.Plumas > 0) texto += $"\nPlumas de quetzal: +{resultado.Plumas}";
            if (resultado.Regresan > 0) texto += $"\nRegresan a la aldea: {resultado.Regresan} tropas";
            var ascensos = new List<string>();
            for (int r = Rangos.Count - 1; r > 0; r--)
            {
                if (resultado.Ascensos[r] > 0) ascensos.Add($"{resultado.Ascensos[r]} a {Rangos.Nombre(Manager.Pueblo, r).ToLowerInvariant()}");
            }
            if (ascensos.Count > 0) texto += "\nAscensos: " + string.Join(", ", ascensos);
            if (!resultado.Victoria) texto += $"\nNecesitas destruir al menos {Mathf.RoundToInt(BattleManager.VictoriaMinima * 100)}% para ganar.";
            GUI.Label(new Rect(panel.x + 20, panel.y + 60, panel.width - 40, 200), texto, _texto);

            if (GUI.Button(new Rect(panel.x + 100, panel.yMax - 64, panel.width - 200, 48), "Volver a la aldea", _boton))
            {
                Manager.VolverAAldea();
            }
        }

        private void DibujarMensaje(float ancho, float alto)
        {
            if (string.IsNullOrEmpty(Manager.Mensaje)) return;
            float abajo = Manager.ModoActual == GameManager.Modo.Batalla ? AltoBarraInferior + 50 : 110;
            var rect = new Rect((ancho - 360) / 2, alto - abajo, 360, 36);
            GUI.Box(rect, Manager.Mensaje, _boton);
        }

        private static string TextoCosto(int[] costo)
        {
            var partes = new List<string>();
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                if (costo[i] > 0) partes.Add($"{costo[i]} {ResourceInfo.Nombre((ResourceType)i).ToLowerInvariant()}");
            }
            return string.Join(", ", partes);
        }

        private void PrepararEstilos()
        {
            if (_titulo != null) return;

            _blanco = Textura(Color.white);
            _caja = new GUIStyle(GUI.skin.box);
            _caja.normal.background = Textura(new Color(0.12f, 0.09f, 0.07f, 0.85f));

            _texto = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _texto.normal.textColor = new Color(0.96f, 0.92f, 0.82f);

            _textoChico = new GUIStyle(_texto) { fontSize = 13 };
            _textoChico.normal.textColor = new Color(0.85f, 0.80f, 0.70f);

            _titulo = new GUIStyle(_texto) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            _boton = new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = true };
        }

        private static Texture2D Textura(Color color)
        {
            var textura = new Texture2D(1, 1);
            textura.SetPixel(0, 0, color);
            textura.Apply();
            return textura;
        }
    }
}
