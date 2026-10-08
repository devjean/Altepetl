using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Alrededor de la aldea mexica: el lago, los volcanes al fondo y una ciudad que crece con el tecpan.
    /// Con cada nivel aparecen más chinampas, islotes con casas y, desde el nivel 3, las calzadas que
    /// unían Tenochtitlan con tierra firme. Es solo decoración: queda fuera de la cuadrícula donde se construye.
    /// </summary>
    public sealed class PaisajeLago : MonoBehaviour
    {
        private const float LadoLago = 70f;

        // Cuántas piezas se ven con el tecpan en nivel 1..5.
        private static readonly int[] Chinampas = { 4, 10, 18, 28, 40 };
        private static readonly int[] Islotes = { 0, 1, 2, 3, 4 };
        private static readonly int[] Calzadas = { 0, 0, 1, 2, 3 };

        private static readonly Color Agua = new Color(0.28f, 0.50f, 0.58f);
        private static readonly Color Orilla = new Color(0.50f, 0.52f, 0.38f);
        private static readonly Color Lodo = new Color(0.36f, 0.27f, 0.17f);
        private static readonly Color Siembra = new Color(0.35f, 0.55f, 0.25f);
        private static readonly Color Piedra = new Color(0.66f, 0.62f, 0.55f);
        private static readonly Color Cal = new Color(0.92f, 0.88f, 0.80f);   // casas encaladas
        private static readonly Color Volcan = new Color(0.48f, 0.42f, 0.55f);

        public GameManager Manager;

        private Transform _fijo;    // lago y volcanes
        private Transform _ciudad;  // lo que crece con el tecpan
        private int _nivel = -1;

        // Lugares calculados una sola vez, para que la ciudad crezca sin que nada cambie de sitio.
        private readonly List<Vector3> _lugaresChinampa = new List<Vector3>();
        private readonly List<Vector3> _lugaresIslote = new List<Vector3>();

        private void Update()
        {
            if (Manager == null) return;
            bool visible = Manager.Pueblo != null && Manager.Pueblo.EnLago
                           && Manager.ModoActual == GameManager.Modo.Aldea;
            if (_fijo != null) _fijo.gameObject.SetActive(visible);
            if (_ciudad != null) _ciudad.gameObject.SetActive(visible);
            if (!visible) return;

            if (_fijo == null) CrearFijo();
            int nivel = Mathf.Clamp(Manager.NivelTecpan, 1, Chinampas.Length);
            if (nivel != _nivel)
            {
                _nivel = nivel;
                CrearCiudad(nivel);
            }
        }

        private Vector3 CentroMapa => new Vector3(GameManager.TamanoMapa * 0.5f, 0f, GameManager.TamanoMapa * 0.5f);

        private void CrearFijo()
        {
            _fijo = new GameObject("Lago").transform;
            _fijo.SetParent(transform, false);
            var centro = CentroMapa;

            Pieza(_fijo, PrimitiveType.Cube, centro + new Vector3(0f, -0.08f, 0f), new Vector3(200f, 0.1f, 200f), Orilla);
            Pieza(_fijo, PrimitiveType.Cube, centro + new Vector3(0f, -0.03f, 0f), new Vector3(LadoLago, 0.02f, LadoLago), Agua);

            // Volcanes al fondo (la cámara mira hacia +x +z).
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(30f, -2f, 52f), new Vector3(26f, 18f, 18f), Volcan);
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(52f, -2f, 30f), new Vector3(18f, 22f, 26f), Volcan);
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(46f, -3f, 46f), new Vector3(20f, 14f, 20f), Volcan * 0.9f);

            CalcularLugares();
        }

        /// <summary>Reparte lugares alrededor de la cuadrícula (nunca dentro), al azar pero siempre igual.</summary>
        private void CalcularLugares()
        {
            var azar = new System.Random(1325);
            float lado = GameManager.TamanoMapa;
            int intentos = 0;
            while (_lugaresChinampa.Count < Chinampas[Chinampas.Length - 1] && intentos++ < 5000)
            {
                var p = new Vector3(Rango(azar, -12f, lado + 12f), 0f, Rango(azar, -6f, lado + 14f));
                if (CercaDeLaCuadricula(p, 1.5f) || SobreCalzada(p) || Ocupado(p, _lugaresChinampa, 2.2f)) continue;
                _lugaresChinampa.Add(p);
            }
            // Islotes con casas: a los lados y al fondo, donde se ven bien.
            _lugaresIslote.Add(new Vector3(-6f, 0f, lado * 0.6f));
            _lugaresIslote.Add(new Vector3(lado * 0.55f, 0f, lado + 6f));
            _lugaresIslote.Add(new Vector3(lado + 6f, 0f, lado * 0.35f));
            _lugaresIslote.Add(new Vector3(lado + 7f, 0f, lado + 7f));
            // Que las chinampas no queden encima de los islotes.
            _lugaresChinampa.RemoveAll(c => Ocupado(c, _lugaresIslote, 4.5f));
        }

        private void CrearCiudad(int nivel)
        {
            if (_ciudad != null) Destroy(_ciudad.gameObject);
            _ciudad = new GameObject("Ciudad del lago").transform;
            _ciudad.SetParent(transform, false);
            float lado = GameManager.TamanoMapa;
            var azar = new System.Random(1428);

            int chinampas = System.Math.Min(Chinampas[nivel - 1], _lugaresChinampa.Count);
            for (int i = 0; i < chinampas; i++)
            {
                var p = _lugaresChinampa[i];
                bool larga = i % 2 == 0;
                var tam = larga ? new Vector3(1.2f, 0.08f, 2.4f) : new Vector3(2.4f, 0.08f, 1.2f);
                Pieza(_ciudad, PrimitiveType.Cube, p + new Vector3(0f, 0.04f, 0f), tam, Lodo);
                Pieza(_ciudad, PrimitiveType.Cube, p + new Vector3(0f, 0.1f, 0f),
                    new Vector3(tam.x * 0.8f, 0.04f, tam.z * 0.8f), Siembra);
                // Un ahuejote en una esquina.
                var esquina = new Vector3(tam.x * 0.45f, 0f, tam.z * 0.45f);
                Pieza(_ciudad, PrimitiveType.Cylinder, p + esquina + new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.45f, 0.12f), Siembra * 0.85f);
            }

            for (int i = 0; i < Islotes[nivel - 1] && i < _lugaresIslote.Count; i++)
            {
                CrearIslote(_lugaresIslote[i], azar, casas: 4 + nivel * 2, templo: nivel == 5 && i == 0);
            }

            // Calzadas hacia tierra firme, como las de Tepeyac, Tlacopan e Iztapalapa.
            var centro = CentroMapa;
            if (Calzadas[nivel - 1] >= 1) Calzada(new Vector3(centro.x, 0f, lado), new Vector3(centro.x, 0f, lado + 30f));
            if (Calzadas[nivel - 1] >= 2) Calzada(new Vector3(lado, 0f, centro.z), new Vector3(lado + 30f, 0f, centro.z));
            if (Calzadas[nivel - 1] >= 3) Calzada(new Vector3(0f, 0f, centro.z), new Vector3(-30f, 0f, centro.z));
        }

        private void CrearIslote(Vector3 centro, System.Random azar, int casas, bool templo)
        {
            Pieza(_ciudad, PrimitiveType.Cube, centro + new Vector3(0f, 0.05f, 0f), new Vector3(7f, 0.1f, 7f), Piedra);
            for (int i = 0; i < casas; i++)
            {
                var p = centro + new Vector3(Rango(azar, -2.8f, 2.8f), 0f, Rango(azar, -2.8f, 2.8f));
                float ancho = Rango(azar, 0.5f, 0.9f);
                float alto = Rango(azar, 0.3f, 0.5f);
                Pieza(_ciudad, PrimitiveType.Cube, p + new Vector3(0f, 0.1f + alto * 0.5f, 0f), new Vector3(ancho, alto, ancho), Cal);
            }
            if (!templo) return;
            // Un templo escalonado en el último nivel.
            for (int escalon = 0; escalon < 4; escalon++)
            {
                float t = 2.6f - escalon * 0.55f;
                Pieza(_ciudad, PrimitiveType.Cube, centro + new Vector3(0f, 0.1f + 0.25f + escalon * 0.5f, 0f), new Vector3(t, 0.5f, t), Cal);
            }
        }

        private void Calzada(Vector3 desde, Vector3 hasta)
        {
            var centro = (desde + hasta) * 0.5f;
            var dif = hasta - desde;
            bool enX = Mathf.Abs(dif.x) > Mathf.Abs(dif.z);
            float largo = dif.magnitude;
            var tam = enX ? new Vector3(largo, 0.12f, 1.2f) : new Vector3(1.2f, 0.12f, largo);
            Pieza(_ciudad, PrimitiveType.Cube, centro + new Vector3(0f, 0.06f, 0f), tam, Piedra);
        }

        /// <summary>Por donde pasan las calzadas (ver CrearCiudad) no va ninguna chinampa.</summary>
        private bool SobreCalzada(Vector3 p)
        {
            float lado = GameManager.TamanoMapa;
            float medio = lado * 0.5f;
            const float margen = 2f;
            return (Mathf.Abs(p.x - medio) < margen && p.z > lado)
                   || (Mathf.Abs(p.z - medio) < margen && (p.x > lado || p.x < 0f));
        }

        private bool CercaDeLaCuadricula(Vector3 p, float margen)
        {
            float lado = GameManager.TamanoMapa;
            return p.x > -margen && p.x < lado + margen && p.z > -margen && p.z < lado + margen;
        }

        private static bool Ocupado(Vector3 p, List<Vector3> otros, float distancia)
        {
            foreach (var o in otros)
            {
                if ((o - p).sqrMagnitude < distancia * distancia) return true;
            }
            return false;
        }

        private static float Rango(System.Random azar, float min, float max)
        {
            return min + (float)azar.NextDouble() * (max - min);
        }

        private static void Pieza(Transform padre, PrimitiveType tipo, Vector3 posicion, Vector3 escala, Color color)
        {
            var pieza = GameObject.CreatePrimitive(tipo);
            Destroy(pieza.GetComponent<Collider>());
            pieza.transform.SetParent(padre, false);
            pieza.transform.localPosition = posicion;
            pieza.transform.localScale = escala;
            pieza.GetComponent<Renderer>().material.color = color;
        }
    }
}
