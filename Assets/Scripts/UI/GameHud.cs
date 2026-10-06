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
        private GUIStyle _boton;
        private GUIStyle _caja;

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

            DibujarRecursos(ancho);
            DibujarMenuConstruccion(ancho, alto);
            DibujarPanelSeleccion(ancho);
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
                string capacidad = tipo == ResourceType.Plumas ? "" : $" / {banco.Capacidad(tipo)}";
                GUI.Label(new Rect(x, 10, anchoCelda, 24), $"{ResourceInfo.Nombre(tipo)}: {banco.Get(tipo)}{capacidad}", _texto);
                x += anchoCelda;
            }
            GUI.Label(new Rect(x, 10, anchoCelda, 24), Manager.Pueblo.Nombre, _texto);
        }

        private void DibujarMenuConstruccion(float ancho, float alto)
        {
            var barra = new Rect(0, alto - AltoBarraInferior, ancho, AltoBarraInferior);
            Zona(barra);
            GUI.Box(barra, GUIContent.none, _caja);

            if (Manager.Colocando != null)
            {
                GUI.Label(new Rect(20, barra.y + 15, ancho - 200, 30),
                    $"Toca el mapa para colocar: {Manager.Colocando.NombrePara(Manager.Pueblo)}", _texto);
                if (GUI.Button(new Rect(ancho - 170, barra.y + 30, 150, 50), "Cancelar", _boton))
                {
                    Manager.CancelarColocacion();
                }
                return;
            }

            var construibles = new List<BuildingDefinition>();
            foreach (var def in BuildingCatalog.Todos)
            {
                if (def.Construible) construibles.Add(def);
            }

            float anchoBoton = (ancho - 20f) / construibles.Count - 10f;
            float x = 15f;
            foreach (var def in construibles)
            {
                bool puede = Manager.Banco.PuedePagar(def.Costo);
                GUI.enabled = puede;
                string texto = $"{def.NombrePara(Manager.Pueblo)}\n{TextoCosto(def.Costo)}";
                if (GUI.Button(new Rect(x, barra.y + 10, anchoBoton, AltoBarraInferior - 20), texto, _boton))
                {
                    Manager.EmpezarColocacion(def);
                }
                GUI.enabled = true;
                x += anchoBoton + 10f;
            }
        }

        private void DibujarPanelSeleccion(float ancho)
        {
            var edificio = Manager.Seleccionado;
            if (edificio == null) return;

            var panel = new Rect(ancho - 290, AltoBarraSuperior + 10, 280, 250);
            Zona(panel);
            GUI.Box(panel, GUIContent.none, _caja);

            var def = edificio.Definicion;
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
            GUI.Label(new Rect(panel.x + 10, panel.y + 42, panel.width - 20, 120), info, _texto);

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

        private void DibujarMensaje(float ancho, float alto)
        {
            if (string.IsNullOrEmpty(Manager.Mensaje)) return;
            var rect = new Rect((ancho - 360) / 2, alto - AltoBarraInferior - 50, 360, 36);
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

            _caja = new GUIStyle(GUI.skin.box);
            _caja.normal.background = Textura(new Color(0.12f, 0.09f, 0.07f, 0.85f));

            _texto = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _texto.normal.textColor = new Color(0.96f, 0.92f, 0.82f);

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
