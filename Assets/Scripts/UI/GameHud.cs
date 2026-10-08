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
        private GUIStyle _textoUnaLinea;
        private GUIStyle _inicial;
        private GUIStyle _boton;
        private GUIStyle _caja;
        private Texture2D _blanco;
        private bool _campanaAbierta;
        private bool _menuAbierto;
        private bool _ofrendasAbierto;
        private int _nivelHistoria = -1;        // capítulo cuya historia se está leyendo
        private int _nivelArmando = -1;         // nivel de campaña para el que se arma el ejército
        private SeleccionEjercito _seleccion;
        private Deidad _deidadInfo;
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
                DibujarInsigniaOfrenda();
                DibujarMensaje(ancho, alto);
                return;
            }

            DibujarRecursos(ancho);
            DibujarInsigniaOfrenda();
            DibujarMenuConstruccion(ancho, alto);
            DibujarBotonAtacar(alto);
            DibujarPanelSeleccion(ancho);
            DibujarCampana(ancho);
            DibujarHistoria(ancho);
            DibujarArmarEjercito(ancho);
            DibujarOfrendas(ancho);
            DibujarMensaje(ancho, alto);
        }

        private void Zona(Rect rect)
        {
            if (Event.current.type == EventType.Layout) _zonasHud.Add(rect);
        }

        private void DibujarEleccionDePueblo(float ancho, float alto)
        {
            GUI.Box(new Rect(0, 0, ancho, alto), GUIContent.none, _caja);
            Titulo(new Rect(0, 30, ancho, 40), "Altepetl — elige tu pueblo");

            float anchoTarjeta = Mathf.Min(280f, (ancho - 80f) / 3f);
            float x = (ancho - anchoTarjeta * 3f - 40f) / 2f;
            foreach (var pueblo in Pueblo.Todos)
            {
                var tarjeta = new Rect(x, 100, anchoTarjeta, 300);
                GUI.Box(tarjeta, GUIContent.none, _caja);
                Titulo(new Rect(tarjeta.x + 10, tarjeta.y + 10, tarjeta.width - 20, 30), pueblo.Nombre);
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
            string pueblo = Manager.Culto.UsaFavor
                ? $"{Manager.Pueblo.Nombre} · Favor {Mathf.FloorToInt(Manager.Culto.Favor)}"
                : Manager.Pueblo.Nombre;
            GUI.Label(new Rect(x, 10, anchoCelda, 24), pueblo, _texto);
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
                    _ofrendasAbierto = false;
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

            Titulo(new Rect(panel.x + 10, panel.y + 8, panel.width - 50, 28), "Construir");

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
            Titulo(new Rect(panel.x + 10, panel.y + 8, panel.width - 50, 28), def.NombrePara(Manager.Pueblo));
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
                case CategoriaEdificio.Militar: return "Militar";
                default: return "Templo";
            }
        }

        /// <summary>Una línea con lo más importante del edificio en nivel 1.</summary>
        private string ResumenEdificio(BuildingDefinition def)
        {
            if (def.Produce) return $"+{ProduccionInicial(def):0.#} {ResourceInfo.Nombre(def.Recurso).ToLowerInvariant()}/min";
            if (def.CapacidadExtra > 0) return $"+{def.CapacidadExtra} de almacén";
            if (def.CapacidadTropas > 0) return $"{def.CapacidadTropas} de espacio para tropas";
            if (def.Entrena) return "Entrena tropas";
            if (def.CamasCuracion > 0) return $"Cura {def.CamasCuracion} heridos a la vez";
            if (def.EsDefensa) return $"{def.DanoDefensaPorSegundo:0.#} de daño por segundo";
            if (def.Id == BuildingId.Teocalli) return "Ofrendas a los dioses";
            return $"Vida: {VidaInicial(def)}";
        }

        private string DetallesEdificio(BuildingDefinition def)
        {
            var lineas = new List<string>
            {
                $"Tamaño: {def.Tamano}x{def.Tamano} casillas",
                $"Construcción: {Mathf.CeilToInt(Manager.SegundosConstruccion(def))} s",
                $"Vida: {VidaInicial(def)}",
            };
            if (def.Produce) lineas.Add($"Produce {ProduccionInicial(def):0.#} de {ResourceInfo.Nombre(def.Recurso).ToLowerInvariant()} por minuto");
            if (def.CapacidadExtra > 0) lineas.Add($"Almacén: +{def.CapacidadExtra} de cada recurso");
            if (def.CapacidadTropas > 0) lineas.Add($"Espacio para tropas: {def.CapacidadTropas} por nivel");
            if (def.Entrena)
            {
                lineas.Add($"Entrena guerreros, arqueros y honderos. Cada nivel entrena {Mathf.RoundToInt(GameManager.EntrenamientoExtraPorNivel * 100)}% más rápido; el espacio lo da el calpulli");
            }
            if (def.CamasCuracion > 0)
            {
                lineas.Add($"Cura a {def.CamasCuracion} heridos a la vez por nivel. Sanar a uno muy herido tarda "
                           + $"{Army.FactorCuracion:0.#} veces lo que entrenarlo, y {Mathf.RoundToInt(Army.CuracionExtraPorRango * 100)}% más por cada rango");
            }
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
            bool entrena = def.Entrena && edificio.Nivel > 0;
            float altoPanel = entrena ? 370 : def.CamasCuracion > 0 ? 310 : 250;
            var panel = new Rect(ancho - 290, AltoBarraSuperior + 10, 280, altoPanel);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);

            Titulo(new Rect(panel.x + 10, panel.y + 8, panel.width - 50, 28), edificio.Nombre);
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
            if (def.Id == BuildingId.Teocalli && edificio.Nivel > 0)
            {
                var culto = Manager.Culto;
                info += culto.HayActiva
                    ? $"\nOfrenda activa: {culto.Activa.Nombre}, quedan {TextoTiempo(culto.SegundosRestantes)}"
                    : "\nSin ofrenda activa";
            }
            if (def.CamasCuracion > 0 && edificio.Nivel > 0)
            {
                int camas = Manager.CamasCuracion;
                int heridos = Manager.Ejercito.Heridos.Count;
                info += heridos == 0
                    ? "\nNo hay heridos"
                    : $"\nCurando {Mathf.Min(camas, heridos)} de {heridos} heridos; el siguiente sana en "
                      + TextoTiempo(Manager.Ejercito.SegundosParaSiguienteCurado(camas));
            }
            if (def.CapacidadTropas > 0 && edificio.Nivel > 0)
            {
                info += $"\nEjército: {Manager.Ejercito.Espacio} / {Manager.CapacidadEjercito}";
            }
            if (def.Entrena && edificio.Nivel > 0)
            {
                if (Manager.Ejercito.Heridos.Count > 0) info += $"\nHeridos en el temazcalli: {Manager.Ejercito.Heridos.Count}";
                string veteranos = TextoRangos(Manager.Ejercito);
                if (veteranos.Length > 0) info += "\nCon rango: " + veteranos;
            }
            GUI.Label(new Rect(panel.x + 10, panel.y + 42, panel.width - 20, entrena ? 120 : panel.height - 100), info, _texto);
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
                    if (def.Id == BuildingId.Teocalli)
                    {
                        if (GUI.Button(botonRect, "Ofrendar mamaltin", _boton))
                        {
                            Manager.Seleccionar(null);
                            _ofrendasAbierto = true;
                            _deidadInfo = null;
                        }
                        break;
                    }
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

        // ---------- Teocalli ----------

        private void DibujarOfrendas(float ancho)
        {
            if (!_ofrendasAbierto) return;
            if (Manager.Seleccionado != null || Manager.Colocando != null || _menuAbierto || _campanaAbierta
                || _nivelArmando >= 0 || _nivelHistoria >= 0 || Manager.HistoriaPendiente != null)
            {
                _ofrendasAbierto = false;
                return;
            }

            var culto = Manager.Culto;
            var panel = new Rect((ancho - 600) / 2, AltoBarraSuperior + 10, 600, 420);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);
            Titulo(new Rect(panel.x + 10, panel.y + 8, panel.width - 50, 28), _deidadInfo != null ? _deidadInfo.Nombre : "Ofrendas");
            if (GUI.Button(new Rect(panel.xMax - 38, panel.y + 6, 32, 28), "X", _boton))
            {
                _ofrendasAbierto = false;
                _deidadInfo = null;
                return;
            }

            if (_deidadInfo != null)
            {
                DibujarLoreDeidad(panel, _deidadInfo);
                return;
            }

            int mamaltin = Manager.Banco.Get(ResourceType.Cautivos);
            string estado = $"Mamaltin: {mamaltin}. Cada ofrenda cuesta {Culto.CostoOfrenda} y su bono dura 2 horas.";
            if (culto.HayActiva)
            {
                var activa = culto.Activa;
                estado += $"\nActiva: {activa.Nombre} ({TextoBono(activa)}), quedan {TextoTiempo(culto.SegundosRestantes)}";
            }
            else
            {
                estado += "\nNinguna ofrenda activa.";
            }
            if (culto.UsaFavor)
            {
                string efecto = culto.FavorAltoActivo ? "alto: +10% ataque, entrenar 10% más barato"
                    : culto.FavorBajoActivo ? "bajo: -10% ataque, entrenar 20% más caro, defensas -10% vida"
                    : "normal";
                estado += $"\nFavor de Huitzilopochtli: {Mathf.FloorToInt(culto.Favor)} / 100 ({efecto}). Baja con el tiempo.";
            }
            GUI.Label(new Rect(panel.x + 15, panel.y + 40, panel.width - 30, 70), estado, _textoChico);

            var deidades = new List<Deidad>();
            foreach (var deidad in Deidad.Todas)
            {
                if (deidad.VeneradaPor(culto.Pueblo)) deidades.Add(deidad);
            }
            float anchoBoton = (panel.width - 30f) / 2f;
            for (int i = 0; i < deidades.Count; i++)
            {
                var deidad = deidades[i];
                var rect = new Rect(panel.x + 10 + (i % 2) * (anchoBoton + 10), panel.y + 116 + (i / 2) * 74, anchoBoton - 44, 66);
                GUI.enabled = culto.PuedeOfrendar(deidad, Manager.Banco);
                if (GUI.Button(rect, $"{deidad.Nombre}: {deidad.Dominio.ToLowerInvariant()}\n{TextoBono(deidad)}", _boton))
                {
                    Manager.TryOfrendar(deidad);
                }
                GUI.enabled = true;
                if (GUI.Button(new Rect(rect.xMax + 4, rect.y, 40, rect.height), "?", _boton))
                {
                    _deidadInfo = deidad;
                }
            }
        }

        /// <summary>Insignia bajo la barra de recursos con el dios cuya ofrenda está activa.</summary>
        private void DibujarInsigniaOfrenda()
        {
            var culto = Manager.Culto;
            if (!culto.HayActiva) return;
            var deidad = culto.Activa;
            var caja = new Rect(10, AltoBarraSuperior + 8, 300, 46);
            Zona(caja);
            GUI.Box(caja, GUIContent.none, _caja);

            // Símbolo provisional (cuadro con el color y la inicial del dios) hasta tener arte.
            var simbolo = new Rect(caja.x + 6, caja.y + 6, 34, 34);
            GUI.color = deidad.Color;
            GUI.DrawTexture(simbolo, _blanco);
            GUI.color = Color.white;
            GUI.Label(simbolo, deidad.Nombre.Substring(0, 1), _inicial);

            GUI.Label(new Rect(caja.x + 48, caja.y + 4, caja.width - 54, 40),
                $"{deidad.Nombre} · {TextoTiempo(culto.SegundosRestantes)}\n{TextoBono(deidad)}", _textoChico);
        }

        private void DibujarLoreDeidad(Rect panel, Deidad deidad)
        {
            string texto = $"{deidad.Dominio}\n\n{deidad.Lore}\n\nOfrenda: {Culto.CostoOfrenda} mamaltin. {TextoBono(deidad)}"
                           + (deidad.Id == DeidadId.Huitzilopochtli ? "." : " durante 2 horas.");
            GUI.Label(new Rect(panel.x + 25, panel.y + 48, panel.width - 50, 280), texto, _texto);

            if (GUI.Button(new Rect(panel.x + 20, panel.yMax - 60, 160, 44), "Volver", _boton))
            {
                _deidadInfo = null;
            }
            GUI.enabled = Manager.Culto.PuedeOfrendar(deidad, Manager.Banco);
            if (GUI.Button(new Rect(panel.xMax - 260, panel.yMax - 60, 240, 44), $"Ofrendar {Culto.CostoOfrenda} mamaltin", _boton))
            {
                Manager.TryOfrendar(deidad);
                _deidadInfo = null;
            }
            GUI.enabled = true;
        }

        private string TextoBono(Deidad deidad)
        {
            if (deidad.Id == DeidadId.Huitzilopochtli) return $"+{Culto.FavorPorOfrenda:0} de favor";
            int porcentaje = Mathf.RoundToInt(Manager.Culto.ValorPara(deidad) * 100f);
            switch (deidad.Bono)
            {
                case TipoBono.Produccion: return $"+{porcentaje}% a toda la producción";
                case TipoBono.Maiz: return $"+{porcentaje}% de maíz";
                case TipoBono.Ataque: return $"+{porcentaje}% de ataque";
                case TipoBono.Entrenamiento: return $"Entrenar {porcentaje}% más rápido";
                case TipoBono.Construccion: return $"Construir {porcentaje}% más rápido";
                case TipoBono.Velocidad: return $"Tropas {porcentaje}% más veloces";
                case TipoBono.DanoDistancia: return $"+{porcentaje}% daño de arqueros y honderos";
                case TipoBono.Almacen: return $"+{porcentaje}% de almacén";
                case TipoBono.Reembolso: return $"Tropas caídas devuelven {porcentaje}% de su maíz";
                default: return "";
            }
        }

        private static string TextoTiempo(float segundos)
        {
            int total = Mathf.CeilToInt(segundos);
            return $"{total / 3600}:{total / 60 % 60:00}:{total % 60:00}";
        }

        private void DibujarEntrenamiento(Rect panel)
        {
            var ejercito = Manager.Ejercito;
            string estado = Manager.CapacidadEjercito > 0
                ? $"Ejército: {ejercito.Espacio} / {Manager.CapacidadEjercito}"
                : "Sin espacio: construye un calpulli";
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
                var costo = Manager.CostoEntrenamiento(tropa.Id);
                GUI.enabled = hayEspacio && Manager.Banco.PuedePagar(costo);
                string texto = $"{tropa.Nombre} ({ejercito.Get(tropa.Id)})\n{TextoCosto(costo)}";
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
                _campanaAbierta = !_campanaAbierta && _nivelArmando < 0 && _nivelHistoria < 0;
                _nivelArmando = -1;
                _nivelHistoria = -1;
                if (_campanaAbierta)
                {
                    _menuAbierto = false;
                    _ofrendasAbierto = false;
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

            var campana = Manager.Campana;
            const float altoFila = 66f;
            float altoPanel = Mathf.Min(470f, 96f + campana.Length * (altoFila + 6f) + 4f);
            var panel = new Rect((ancho - 560) / 2, AltoBarraSuperior + 10, 560, altoPanel);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);
            Titulo(new Rect(panel.x + 10, panel.y + 8, panel.width - 50, 28), Altepetl.Campana.Titulo(Manager.Pueblo));
            if (GUI.Button(new Rect(panel.xMax - 38, panel.y + 6, 32, 28), "X", _boton))
            {
                _campanaAbierta = false;
                return;
            }

            var ejercito = Manager.Ejercito;
            var partes = new List<string>();
            foreach (var tropa in TroopCatalog.Todos)
            {
                partes.Add($"{ejercito.Get(tropa.Id) + ejercito.HeridosDe(tropa.Id)} {tropa.Nombre.ToLowerInvariant()}");
            }
            string veteranosCampana = TextoRangos(ejercito);
            string heridosCampana = ejercito.Heridos.Count > 0 ? $" ({ejercito.Heridos.Count} heridos)" : "";
            GUI.Label(new Rect(panel.x + 10, panel.y + 40, panel.width - 20, 48),
                "Tu ejército: " + string.Join(", ", partes) + heridosCampana
                + (veteranosCampana.Length > 0 ? "\nCon rango: " + veteranosCampana : ""), _texto);

            float y = panel.y + 96;
            for (int i = 0; i < campana.Length; i++)
            {
                var nivel = campana[i];
                bool ganado = i < Manager.NivelesCompletados;
                bool disponible = i <= Manager.NivelesCompletados;
                var fila = new Rect(panel.x + 10, y, panel.width - 20, altoFila);
                GUI.Box(fila, GUIContent.none, _caja);

                int estrellas = Manager.EstrellasDe(i);
                string premio = estrellas > 0 ? "Completado" : $"Primera victoria: {nivel.PlumasPrimeraVez} plumas de quetzal";
                if (ganado) DibujarEstrellas(new Rect(fila.xMax - 220, fila.y + 18, 90, 30), estrellas, 22);
                GUI.Label(new Rect(fila.x + 8, fila.y + 2, fila.width - 230, 24), $"{i + 1}. {nivel.Nombre}", _texto);
                GUI.Label(new Rect(fila.x + 8, fila.y + 24, fila.width - 230, altoFila - 24),
                    $"{nivel.Descripcion}\n{premio}", _textoChico);

                GUI.enabled = disponible && ejercito.Total > 0;
                if (GUI.Button(new Rect(fila.xMax - 120, fila.y + 11, 110, 44), disponible ? "Atacar" : "Bloqueado", _boton))
                {
                    _campanaAbierta = false;
                    if (string.IsNullOrEmpty(nivel.Historia))
                    {
                        _nivelArmando = i;
                        _seleccion = Manager.SeleccionPorDefecto();
                    }
                    else
                    {
                        _nivelHistoria = i;
                    }
                }
                GUI.enabled = true;
                y += altoFila + 6f;
            }
        }

        // ---------- Historia de la campaña ----------

        /// <summary>La historia del capítulo antes de la batalla, o el epílogo al volver a la aldea.</summary>
        private void DibujarHistoria(float ancho)
        {
            string titulo;
            string texto;
            bool epilogo = false;
            if (Manager.HistoriaPendiente != null)
            {
                titulo = Manager.TituloHistoriaPendiente;
                texto = Manager.HistoriaPendiente;
                epilogo = true;
                _nivelHistoria = -1;
            }
            else if (_nivelHistoria >= 0)
            {
                if (Manager.Seleccionado != null || Manager.Colocando != null || _menuAbierto || _campanaAbierta)
                {
                    _nivelHistoria = -1;
                    return;
                }
                var nivel = Manager.Campana[_nivelHistoria];
                titulo = $"{_nivelHistoria + 1}. {nivel.Nombre}";
                texto = nivel.Historia;
            }
            else
            {
                return;
            }

            var panel = new Rect((ancho - 620) / 2, AltoBarraSuperior + 10, 620, 440);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);
            Titulo(new Rect(panel.x + 10, panel.y + 8, panel.width - 20, 28), titulo);
            GUI.Label(new Rect(panel.x + 30, panel.y + 52, panel.width - 60, 310), texto, _texto);

            if (epilogo)
            {
                if (GUI.Button(new Rect(panel.x + (panel.width - 200) / 2, panel.yMax - 60, 200, 44), "Continuar", _boton))
                {
                    Manager.LeerHistoriaPendiente();
                }
                return;
            }
            if (GUI.Button(new Rect(panel.x + 20, panel.yMax - 60, 160, 44), "Volver", _boton))
            {
                _nivelHistoria = -1;
                _campanaAbierta = true;
            }
            if (GUI.Button(new Rect(panel.xMax - 260, panel.yMax - 60, 240, 44), "Preparar el ejército", _boton))
            {
                _nivelArmando = _nivelHistoria;
                _seleccion = Manager.SeleccionPorDefecto();
                _nivelHistoria = -1;
            }
        }

        // ---------- Armar el ejército ----------

        private void DibujarArmarEjercito(float ancho)
        {
            if (_nivelArmando < 0 || _seleccion == null) return;
            if (Manager.Seleccionado != null || Manager.Colocando != null || _menuAbierto || _campanaAbierta)
            {
                _nivelArmando = -1;
                return;
            }

            var ejercito = Manager.Ejercito;
            var nivel = Manager.Campana[_nivelArmando];
            var panel = new Rect((ancho - 620) / 2, AltoBarraSuperior + 10, 620, 470);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);
            Titulo(new Rect(panel.x + 10, panel.y + 8, panel.width - 50, 28), "Arma tu ejército");
            if (GUI.Button(new Rect(panel.xMax - 38, panel.y + 6, 32, 28), "X", _boton))
            {
                _nivelArmando = -1;
                return;
            }
            GUI.Label(new Rect(panel.x + 20, panel.y + 40, panel.width - 40, 40),
                $"Contra: {nivel.Nombre}. Los heridos pelean con la vida que les queda y dejan de curarse mientras estén fuera.",
                _textoChico);

            float y = panel.y + 84;
            GUI.Label(new Rect(panel.x + 20, y, 250, 24), "Tropa", _textoChico);
            GUI.Label(new Rect(panel.x + 280, y, 140, 24), "Sanas", _textoChico);
            GUI.Label(new Rect(panel.x + 435, y, 170, 24), "Heridas", _textoChico);
            y += 26;

            bool hayFilas = false;
            foreach (var tropa in TroopCatalog.Todos)
            {
                for (int r = Rangos.Count - 1; r >= 0; r--)
                {
                    int sanos = ejercito.Get(tropa.Id, r);
                    int heridos = ejercito.HeridosDe(tropa.Id, r);
                    if (sanos + heridos == 0) continue;
                    hayFilas = true;
                    int i = Army.Indice(tropa.Id, r);
                    _seleccion.Sanos[i] = Mathf.Clamp(_seleccion.Sanos[i], 0, sanos);
                    _seleccion.Heridos[i] = Mathf.Clamp(_seleccion.Heridos[i], 0, heridos);

                    GUI.Label(new Rect(panel.x + 20, y + 8, 255, 26),
                        $"{tropa.Nombre} · {Rangos.Nombre(Manager.Pueblo, r).ToLowerInvariant()}", _textoUnaLinea);
                    _seleccion.Sanos[i] = Contador(new Rect(panel.x + 280, y, 140, 34), _seleccion.Sanos[i], sanos, "");
                    string vida = _seleccion.Heridos[i] > 0
                        ? $" {Mathf.RoundToInt(ejercito.VidaPromedioHeridos(tropa.Id, r, _seleccion.Heridos[i]) * 100)}%"
                        : "";
                    _seleccion.Heridos[i] = Contador(new Rect(panel.x + 435, y, 170, 34), _seleccion.Heridos[i], heridos, vida);
                    y += 38;
                }
            }
            if (!hayFilas)
            {
                GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 30), "No tienes tropas.", _texto);
            }

            if (GUI.Button(new Rect(panel.x + 20, panel.yMax - 60, 140, 44), "Volver", _boton))
            {
                _nivelArmando = -1;
                _campanaAbierta = true;
            }
            if (GUI.Button(new Rect(panel.x + 170, panel.yMax - 60, 140, 44), "Llevar todas", _boton))
            {
                foreach (var tropa in TroopCatalog.Todos)
                {
                    for (int r = 0; r < Rangos.Count; r++)
                    {
                        int i = Army.Indice(tropa.Id, r);
                        _seleccion.Sanos[i] = ejercito.Get(tropa.Id, r);
                        _seleccion.Heridos[i] = ejercito.HeridosDe(tropa.Id, r);
                    }
                }
            }
            int total = _seleccion.Total;
            GUI.enabled = total > 0;
            if (GUI.Button(new Rect(panel.xMax - 260, panel.yMax - 60, 240, 44), total == 1 ? "¡A la batalla! (1 tropa)" : $"¡A la batalla! ({total} tropas)", _boton))
            {
                int indice = _nivelArmando;
                _nivelArmando = -1;
                Manager.EmpezarBatalla(indice, _seleccion);
            }
            GUI.enabled = true;
        }

        /// <summary>[-] n / máximo [+]; devuelve el nuevo valor.</summary>
        private int Contador(Rect rect, int valor, int maximo, string extra)
        {
            GUI.enabled = valor > 0;
            if (GUI.Button(new Rect(rect.x, rect.y, 34, rect.height), "-", _boton)) valor--;
            GUI.enabled = true;
            GUI.Label(new Rect(rect.x + 38, rect.y + 6, rect.width - 80, rect.height), $"{valor} / {maximo}{extra}", _texto);
            GUI.enabled = valor < maximo;
            if (GUI.Button(new Rect(rect.xMax - 38, rect.y, 34, rect.height), "+", _boton)) valor++;
            GUI.enabled = true;
            return valor;
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
            DibujarEstrellas(new Rect(ancho - 110, 6, 100, 30), batalla.Estrellas, 22);

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
                int heridos = batalla.HeridosDisponibles(tropa.Id);
                var notas = new List<string>();
                if (conRango > 0) notas.Add($"{conRango} con rango");
                if (heridos > 0) notas.Add($"{heridos} heridos");
                string texto = $"{tropa.Nombre}\nx{cantidad}" + (notas.Count > 0 ? $"\n({string.Join(", ", notas)})" : "");
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
            var panel = new Rect((ancho - 480) / 2, (alto - 440) / 2, 480, 440);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);
            Titulo(new Rect(panel.x + 10, panel.y + 12, panel.width - 20, 34), resultado.Victoria ? "¡Victoria!" : "Derrota");
            DibujarEstrellas(new Rect(panel.x + (panel.width - 150) / 2, panel.y + 46, 150, 40), resultado.Estrellas, 34);

            string botin = TextoCosto(resultado.Botin);
            if (botin.Length > 0 && !string.IsNullOrEmpty(batalla.Nivel.NotaBotin)) botin += $" ({batalla.Nivel.NotaBotin.ToLowerInvariant()})";
            string texto = $"Destrucción: {Mathf.FloorToInt(resultado.Porcentaje * 100)}%"
                           + (resultado.TecpanDestruido ? " · ¡Cayó el tecpan!" : "")
                           + $"\nBotín: {(botin.Length > 0 ? botin : "nada")}";
            if (resultado.Plumas > 0) texto += $"\nPlumas de quetzal: +{resultado.Plumas}";
            if (resultado.Regresan > 0) texto += $"\nRegresan a la aldea: {resultado.Regresan} tropas";
            if (resultado.Heridos > 0)
            {
                texto += Manager.CamasCuracion > 0
                    ? $"\nHeridas: {resultado.Heridos}, se curarán en el temazcalli"
                    : $"\nHeridas: {resultado.Heridos}. Construye un temazcalli para curarlas";
            }
            var ascensos = new List<string>();
            for (int r = Rangos.Count - 1; r > 0; r--)
            {
                if (resultado.Ascensos[r] > 0) ascensos.Add($"{resultado.Ascensos[r]} a {Rangos.Nombre(Manager.Pueblo, r).ToLowerInvariant()}");
            }
            if (ascensos.Count > 0) texto += "\nAscensos: " + string.Join(", ", ascensos);
            if (resultado.AvanzaHistoria) texto += "\n" + batalla.Nivel.TextoDerrota;
            else if (!resultado.Victoria) texto += $"\nPara ganar necesitas un macuahuitl: destruye el {Mathf.RoundToInt(BattleManager.VictoriaMinima * 100)}% o derriba el tecpan.";
            GUI.Label(new Rect(panel.x + 20, panel.y + 92, panel.width - 40, 268), texto, _texto);

            if (GUI.Button(new Rect(panel.x + 100, panel.yMax - 64, panel.width - 200, 48), "Volver a la aldea", _boton))
            {
                Manager.VolverAAldea();
            }
        }

        /// <summary>
        /// Tres macuahuitl, como las estrellas de Clash: uno por el 50%, otro por derribar el tecpan
        /// y otro por el 100%. Los que faltan se ven apagados.
        /// </summary>
        private void DibujarEstrellas(Rect rect, int cuantas, int tamano)
        {
            float paso = rect.width / 3f;
            for (int i = 0; i < 3; i++)
            {
                var lugar = new Rect(rect.x + paso * i + (paso - tamano) * 0.5f, rect.y + (rect.height - tamano) * 0.5f, tamano, tamano);
                DibujarMacuahuitl(lugar, i < cuantas);
            }
        }

        /// <summary>Un macuahuitl inclinado: mango y hoja de madera con navajas de obsidiana a los lados.</summary>
        private void DibujarMacuahuitl(Rect rect, bool ganado)
        {
            var matriz = GUI.matrix;
            // Se gira alrededor de su centro en coordenadas del HUD (después de la escala de pantalla).
            var centro = new Vector3(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f, 0f);
            GUI.matrix = matriz * Matrix4x4.Translate(centro) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 35f))
                         * Matrix4x4.Translate(new Vector3(-centro.x, -centro.y, 0f));

            float h = rect.height;
            float cx = rect.x + rect.width * 0.5f;
            var madera = ganado ? new Color(0.62f, 0.42f, 0.22f) : new Color(0.32f, 0.29f, 0.26f);
            var obsidiana = ganado ? new Color(0.10f, 0.09f, 0.12f) : new Color(0.24f, 0.23f, 0.22f);
            var brillo = ganado ? new Color(0.55f, 0.60f, 0.70f) : new Color(0.30f, 0.29f, 0.28f);

            float anchoHoja = h * 0.26f;
            float anchoMango = h * 0.12f;
            // Navajas: cuadritos a cada lado de la hoja.
            float navaja = h * 0.1f;
            for (int n = 0; n < 4; n++)
            {
                float y = rect.y + h * 0.06f + n * h * 0.15f;
                Rectangulo(new Rect(cx - anchoHoja * 0.5f - navaja * 0.7f, y, navaja, navaja), obsidiana);
                Rectangulo(new Rect(cx + anchoHoja * 0.5f - navaja * 0.3f, y, navaja, navaja), obsidiana);
                Rectangulo(new Rect(cx - anchoHoja * 0.5f - navaja * 0.7f, y, navaja * 0.35f, navaja * 0.35f), brillo);
                Rectangulo(new Rect(cx + anchoHoja * 0.5f - navaja * 0.3f, y, navaja * 0.35f, navaja * 0.35f), brillo);
            }
            Rectangulo(new Rect(cx - anchoHoja * 0.5f, rect.y + h * 0.02f, anchoHoja, h * 0.66f), madera);
            Rectangulo(new Rect(cx - anchoMango * 0.5f, rect.y + h * 0.66f, anchoMango, h * 0.32f), madera);

            GUI.matrix = matriz;
        }

        private void Rectangulo(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, _blanco);
            GUI.color = Color.white;
        }

        private void DibujarMensaje(float ancho, float alto)
        {
            if (string.IsNullOrEmpty(Manager.Mensaje)) return;
            // En la aldea va entre los botones Atacar y Construir, para no tapar los paneles.
            float abajo = Manager.ModoActual == GameManager.Modo.Batalla ? AltoBarraInferior + 50 : 53;
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

            _textoUnaLinea = new GUIStyle(_texto) { fontSize = 14, wordWrap = false, clipping = TextClipping.Clip };

            _inicial = new GUIStyle(_texto) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _inicial.normal.textColor = new Color(0.12f, 0.09f, 0.07f);

            _titulo = new GUIStyle(_texto) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };

            _boton = new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = true };

        }

        // Títulos en una sola línea: si no caben, se achica la letra en vez de salirse del panel.
        private void Titulo(Rect rect, string texto)
        {
            var contenido = new GUIContent(texto);
            _titulo.fontSize = 22;
            while (_titulo.fontSize > 13 && _titulo.CalcSize(contenido).x > rect.width) _titulo.fontSize--;
            GUI.Label(rect, texto, _titulo);
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
