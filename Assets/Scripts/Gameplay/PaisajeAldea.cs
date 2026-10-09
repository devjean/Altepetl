using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Lo que rodea la aldea, distinto para cada pueblo, y una ciudad que crece con el nivel del tecpan.
    /// Es solo decoración: queda fuera de la cuadrícula donde se construye.
    /// - Mexicas: Tenochtitlan sobre el lago, con chinampas, islotes con casas y calzadas.
    /// - Acolhuas: Texcoco en la orilla oriental del lago, con chinampas, embarcadero y las terrazas del Tetzcotzinco.
    /// - Tlaxcaltecas: Tlaxcallan entre cerros, con terrazas, techos de paja y una cabecera nueva en cada nivel,
    ///   bajo la Matlalcuéyetl.
    /// </summary>
    public sealed class PaisajeAldea : MonoBehaviour
    {
        private const int NivelesTecpan = 5;

        private static readonly Color Agua = new Color(0.28f, 0.50f, 0.58f);
        private static readonly Color Orilla = new Color(0.50f, 0.52f, 0.38f);
        private static readonly Color Campo = new Color(0.55f, 0.53f, 0.36f);
        private static readonly Color Lodo = new Color(0.36f, 0.27f, 0.17f);
        private static readonly Color Siembra = new Color(0.35f, 0.55f, 0.25f);
        private static readonly Color Piedra = new Color(0.66f, 0.62f, 0.55f);
        private static readonly Color Cal = new Color(0.92f, 0.88f, 0.80f);    // muros encalados
        private static readonly Color Paja = new Color(0.78f, 0.45f, 0.20f);   // techos de zacate
        private static readonly Color Rojo = new Color(0.70f, 0.22f, 0.15f);
        private static readonly Color Madera = new Color(0.45f, 0.30f, 0.18f);
        private static readonly Color Volcan = new Color(0.48f, 0.42f, 0.55f);
        private static readonly Color Cerro = new Color(0.38f, 0.52f, 0.36f);
        private static readonly Color Terraza = new Color(0.58f, 0.48f, 0.32f);

        public GameManager Manager;

        private Transform _fijo;    // terreno, lago, cerros y volcanes
        private Transform _ciudad;  // lo que crece con el tecpan
        private Pueblo _pueblo;
        private int _nivel = -1;
        private readonly List<Vector3> _casasAfuera = new List<Vector3>();

        /// <summary>Casas de los alrededores a ras de suelo (o sobre islotes), adonde también va la gente del pueblo.</summary>
        public IReadOnlyList<Vector3> CasasAfuera => _casasAfuera;

        private readonly List<Vector3> _chinampas = new List<Vector3>();

        /// <summary>Chinampas de los alrededores, donde la gente va a trabajar un rato.</summary>
        public IReadOnlyList<Vector3> Chinampas => _chinampas;

        private struct CerroEscalonado
        {
            public Vector3 Centro;
            public float Radio;
            public int Terrazas;
        }

        private readonly List<CerroEscalonado> _cerros = new List<CerroEscalonado>();
        private const float AltoTerraza = 0.5f;

        /// <summary>Altura del suelo en ese punto: sube por las terrazas de los cerros escalonados.</summary>
        public float AlturaSuelo(Vector3 p)
        {
            float altura = 0f;
            foreach (var cerro in _cerros)
            {
                float d = new Vector2(p.x - cerro.Centro.x, p.z - cerro.Centro.z).magnitude;
                int escalones = 0;
                for (int i = 0; i < cerro.Terrazas; i++)
                {
                    if (d < cerro.Radio * (1f - i / (float)(cerro.Terrazas + 1))) escalones = i + 1;
                }
                altura = Mathf.Max(altura, escalones * AltoTerraza);
            }
            return altura;
        }

        /// <summary>¿Ese punto está sobre el lago? Ahí la gente va en acalli.</summary>
        public bool EnAgua(Vector3 p)
        {
            if (_pueblo == null) return false;
            switch (_pueblo.Id)
            {
                case PuebloId.Mexicas: return true;
                // El lago de Texcoco queda al oeste de la aldea (ver FijoAcolhua).
                case PuebloId.Acolhuas: return p.x < -1f && Mathf.Abs(p.z - Lado * 0.5f) < 45f;
                default: return false;
            }
        }

        private float Lado => GameManager.TamanoMapa;
        private Vector3 CentroMapa => new Vector3(Lado * 0.5f, 0f, Lado * 0.5f);

        private void Update()
        {
            if (Manager == null) return;
            bool visible = Manager.Pueblo != null && Manager.ModoActual == GameManager.Modo.Aldea;
            if (_fijo != null) _fijo.gameObject.SetActive(visible);
            if (_ciudad != null) _ciudad.gameObject.SetActive(visible);
            if (!visible) return;

            if (Manager.Pueblo != _pueblo)
            {
                _pueblo = Manager.Pueblo;
                _nivel = -1;
                if (_fijo != null) Destroy(_fijo.gameObject);
                _fijo = Grupo("Alrededores");
                CrearFijo();
            }
            int nivel = Mathf.Clamp(Manager.NivelTecpan, 1, NivelesTecpan);
            if (nivel != _nivel)
            {
                _nivel = nivel;
                if (_ciudad != null) Destroy(_ciudad.gameObject);
                _casasAfuera.Clear();
                _chinampas.Clear();
                _cerros.Clear();
                _ciudad = Grupo("Ciudad");
                CrearCiudad(nivel);
            }
        }

        private void CrearFijo()
        {
            switch (_pueblo.Id)
            {
                case PuebloId.Mexicas: FijoMexica(); break;
                case PuebloId.Acolhuas: FijoAcolhua(); break;
                default: FijoTlaxcalteca(); break;
            }
        }

        private void CrearCiudad(int nivel)
        {
            switch (_pueblo.Id)
            {
                case PuebloId.Mexicas: CiudadMexica(nivel); break;
                case PuebloId.Acolhuas: CiudadAcolhua(nivel); break;
                default: CiudadTlaxcalteca(nivel); break;
            }
        }

        // ---------- Mexicas: Tenochtitlan sobre el lago ----------

        private static readonly int[] ChinampasMexica = { 4, 10, 18, 28, 40 };
        private static readonly int[] IslotesMexica = { 0, 1, 2, 3, 4 };
        private static readonly int[] CalzadasMexica = { 0, 0, 1, 2, 3 };

        private void FijoMexica()
        {
            var centro = CentroMapa;
            Pieza(_fijo, PrimitiveType.Cube, centro + new Vector3(0f, -0.08f, 0f), new Vector3(200f, 0.1f, 200f), Orilla);
            Pieza(_fijo, PrimitiveType.Cube, centro + new Vector3(0f, -0.03f, 0f), new Vector3(70f, 0.02f, 70f), Agua);
            Volcanes(centro);
        }

        private void CiudadMexica(int nivel)
        {
            var azar = new System.Random(1325);
            var islotes = new List<Vector3>
            {
                new Vector3(-6f, 0f, Lado * 0.6f),
                new Vector3(Lado * 0.55f, 0f, Lado + 6f),
                new Vector3(Lado + 6f, 0f, Lado * 0.35f),
                new Vector3(Lado + 7f, 0f, Lado + 7f),
            };
            var chinampas = Lugares(1325, ChinampasMexica[NivelesTecpan - 1], -12f, Lado + 12f, -6f, Lado + 14f, 2.2f,
                p => SobreCalzadaMexica(p) || Cerca(p, islotes, 4.5f));

            for (int i = 0; i < ChinampasMexica[nivel - 1] && i < chinampas.Count; i++) Chinampa(chinampas[i], i % 2 == 0);
            for (int i = 0; i < IslotesMexica[nivel - 1]; i++)
            {
                Pieza(_ciudad, PrimitiveType.Cube, islotes[i] + new Vector3(0f, 0.05f, 0f), new Vector3(7f, 0.1f, 7f), Piedra);
                Casas(islotes[i], 2.8f, 4 + nivel * 2, azar, Cal, null);
                if (nivel == NivelesTecpan && i == 0) Piramide(islotes[i], 2.6f, 4, Cal);
            }

            // Calzadas hacia tierra firme, como las de Tepeyac, Tlacopan e Iztapalapa.
            var centro = CentroMapa;
            if (CalzadasMexica[nivel - 1] >= 1) Calzada(new Vector3(centro.x, 0f, Lado), new Vector3(centro.x, 0f, Lado + 30f));
            if (CalzadasMexica[nivel - 1] >= 2) Calzada(new Vector3(Lado, 0f, centro.z), new Vector3(Lado + 30f, 0f, centro.z));
            if (CalzadasMexica[nivel - 1] >= 3) Calzada(new Vector3(0f, 0f, centro.z), new Vector3(-30f, 0f, centro.z));
        }

        private bool SobreCalzadaMexica(Vector3 p)
        {
            float medio = Lado * 0.5f;
            const float margen = 2f;
            return (Mathf.Abs(p.x - medio) < margen && p.z > Lado)
                   || (Mathf.Abs(p.z - medio) < margen && (p.x > Lado || p.x < 0f));
        }

        // ---------- Acolhuas: Texcoco en la orilla del lago ----------

        private static readonly int[] ChinampasAcolhua = { 3, 6, 10, 14, 18 };
        private static readonly int[] CasasAcolhua = { 4, 10, 18, 26, 34 };

        /// <summary>Texcoco estaba en la orilla oriental del lago: el agua queda del lado izquierdo (al oeste).</summary>
        private void FijoAcolhua()
        {
            var centro = CentroMapa;
            Pieza(_fijo, PrimitiveType.Cube, centro + new Vector3(0f, -0.08f, 0f), new Vector3(200f, 0.1f, 200f), Campo);
            Pieza(_fijo, PrimitiveType.Cube, new Vector3(-20f, -0.03f, Lado * 0.5f), new Vector3(38f, 0.02f, 90f), Agua);
            Volcanes(centro);
        }

        private void CiudadAcolhua(int nivel)
        {
            var azar = new System.Random(1431);
            // Chinampas en el lago, junto a la orilla.
            var chinampas = Lugares(1431, ChinampasAcolhua[NivelesTecpan - 1], -14f, -2.5f, -4f, Lado + 12f, 2.2f, null);
            for (int i = 0; i < ChinampasAcolhua[nivel - 1] && i < chinampas.Count; i++) Chinampa(chinampas[i], i % 2 == 0);

            // Casas en tierra firme, alrededor de la aldea.
            var tetzcotzinco = new Vector3(Lado + 10f, 0f, Lado + 10f);
            var templo = new Vector3(Lado + 6f, 0f, Lado * 0.3f);
            var casas = Lugares(1520, CasasAcolhua[NivelesTecpan - 1], 0f, Lado + 12f, -6f, Lado + 12f, 1.4f,
                p => (p - tetzcotzinco).sqrMagnitude < 64f || (p - templo).sqrMagnitude < 9f);
            for (int i = 0; i < CasasAcolhua[nivel - 1] && i < casas.Count; i++) Casa(casas[i], azar, Cal, null);

            // Embarcadero con acalli.
            if (nivel >= 3)
            {
                var muelle = new Vector3(-3f, 0f, Lado * 0.5f);
                Pieza(_ciudad, PrimitiveType.Cube, muelle + new Vector3(0f, 0.06f, 0f), new Vector3(6f, 0.12f, 1.2f), Madera);
                for (int i = 0; i < 4; i++)
                {
                    var canoa = muelle + new Vector3(-1.5f + i * 1.1f, 0.03f, i % 2 == 0 ? 1.1f : -1.1f);
                    Pieza(_ciudad, PrimitiveType.Cube, canoa, new Vector3(0.38f, 0.06f, 0.13f), Madera);
                }
            }
            // Las terrazas y jardines del Tetzcotzinco, el cerro de Nezahualcóyotl.
            if (nivel >= 4) CerroConTerrazas(tetzcotzinco, 7f, 5, azar, 6, Cal, null);
            if (nivel >= NivelesTecpan) Piramide(templo, 3f, 5, Cal);
        }

        // ---------- Tlaxcaltecas: Tlaxcallan entre cerros ----------

        private static readonly int[] CasasTlaxcalteca = { 4, 8, 12, 16, 20 };

        // Las cuatro cabeceras, cada una en su cerro; aparecen en orden con el tecpan (nivel 2 a 5).
        private Vector3[] Cabeceras => new[]
        {
            new Vector3(-9f, 0f, Lado + 4f),    // Tepeticpac, la primera, en lo alto
            new Vector3(Lado * 0.5f, 0f, Lado + 11f), // Ocotelulco
            new Vector3(Lado + 11f, 0f, Lado * 0.6f), // Tizatlán
            new Vector3(Lado + 9f, 0f, -6f),    // Quiahuiztlán
        };

        /// <summary>Tierra de cerros, con la Matlalcuéyetl (la Malinche) al fondo.</summary>
        private void FijoTlaxcalteca()
        {
            var centro = CentroMapa;
            Pieza(_fijo, PrimitiveType.Cube, centro + new Vector3(0f, -0.08f, 0f), new Vector3(200f, 0.1f, 200f), Campo);
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(42f, -4f, 42f), new Vector3(34f, 26f, 34f), new Color(0.32f, 0.50f, 0.48f));
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(-26f, -3f, 36f), new Vector3(22f, 10f, 18f), Cerro);
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(38f, -3f, -20f), new Vector3(18f, 9f, 22f), Cerro);
        }

        private void CiudadTlaxcalteca(int nivel)
        {
            var azar = new System.Random(1250);
            var cabeceras = Cabeceras;
            var casas = Lugares(1250, CasasTlaxcalteca[NivelesTecpan - 1], -6f, Lado + 6f, -5f, Lado + 6f, 1.4f,
                p => Cerca(p, cabeceras, 6f));
            for (int i = 0; i < CasasTlaxcalteca[nivel - 1] && i < casas.Count; i++) Casa(casas[i], azar, Cal, Paja);

            for (int i = 0; i < nivel - 1 && i < cabeceras.Length; i++)
            {
                bool principal = nivel == NivelesTecpan && i == 0;
                CerroConTerrazas(cabeceras[i], 5.5f, 4, azar, 5, Cal, Paja);
                if (principal) TemploDePaja(cabeceras[i] + new Vector3(0f, 2f, 0f));
            }
        }

        /// <summary>Templo con techo cónico de paja, como los que pintó Xochitiotzin en Tlaxcala.</summary>
        private void TemploDePaja(Vector3 baseTemplo)
        {
            Piramide(baseTemplo, 2.2f, 3, Cal);
            var arriba = baseTemplo + new Vector3(0f, 1.5f, 0f);
            Pieza(_ciudad, PrimitiveType.Cylinder, arriba + new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.3f, 0.9f), Rojo);
            // Techo cónico con discos cada vez más chicos.
            for (int i = 0; i < 6; i++)
            {
                float r = 1.3f - i * 0.2f;
                Pieza(_ciudad, PrimitiveType.Cylinder, arriba + new Vector3(0f, 0.7f + i * 0.28f, 0f), new Vector3(r, 0.14f, r), Paja);
            }
        }

        // ---------- Piezas comunes ----------

        private void Volcanes(Vector3 centro)
        {
            // La cámara mira hacia +x +z: lo de ese lado queda al fondo.
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(30f, -2f, 52f), new Vector3(26f, 18f, 18f), Volcan);
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(52f, -2f, 30f), new Vector3(18f, 22f, 26f), Volcan);
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(46f, -3f, 46f), new Vector3(20f, 14f, 20f), Volcan * 0.9f);
        }

        /// <summary>Parcela de lodo con siembra y un ahuejote en la esquina.</summary>
        private void Chinampa(Vector3 p, bool larga)
        {
            var tam = larga ? new Vector3(1.2f, 0.08f, 2.4f) : new Vector3(2.4f, 0.08f, 1.2f);
            _chinampas.Add(new Vector3(p.x, 0f, p.z));
            Pieza(_ciudad, PrimitiveType.Cube, p + new Vector3(0f, 0.04f, 0f), tam, Lodo);
            Pieza(_ciudad, PrimitiveType.Cube, p + new Vector3(0f, 0.1f, 0f), new Vector3(tam.x * 0.8f, 0.04f, tam.z * 0.8f), Siembra);
            var esquina = new Vector3(tam.x * 0.45f, 0f, tam.z * 0.45f);
            Pieza(_ciudad, PrimitiveType.Cylinder, p + esquina + new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.45f, 0.12f), Siembra * 0.85f);
        }

        private void Calzada(Vector3 desde, Vector3 hasta)
        {
            var dif = hasta - desde;
            bool enX = Mathf.Abs(dif.x) > Mathf.Abs(dif.z);
            float largo = dif.magnitude;
            var tam = enX ? new Vector3(largo, 0.12f, 1.2f) : new Vector3(1.2f, 0.12f, largo);
            Pieza(_ciudad, PrimitiveType.Cube, (desde + hasta) * 0.5f + new Vector3(0f, 0.06f, 0f), tam, Piedra);
        }

        /// <summary>Una casa: muros y, si se da, un techo de paja.</summary>
        private void Casa(Vector3 p, System.Random azar, Color muro, Color? techo)
        {
            float ancho = Rango(azar, 0.5f, 0.9f);
            float alto = Rango(azar, 0.3f, 0.5f);
            // La puerta, a ras de suelo; en los cerros la gente sube por las terrazas (ver AlturaSuelo).
            _casasAfuera.Add(new Vector3(p.x, 0f, p.z) + new Vector3(0f, 0f, -ancho * 0.5f - 0.15f));
            Pieza(_ciudad, PrimitiveType.Cube, p + new Vector3(0f, alto * 0.5f, 0f), new Vector3(ancho, alto, ancho), muro);
            if (techo.HasValue)
            {
                Pieza(_ciudad, PrimitiveType.Cube, p + new Vector3(0f, alto + 0.08f, 0f),
                    new Vector3(ancho * 1.15f, 0.16f, ancho * 1.15f), techo.Value);
            }
        }

        private void Casas(Vector3 centro, float radio, int cantidad, System.Random azar, Color muro, Color? techo)
        {
            for (int i = 0; i < cantidad; i++)
            {
                var p = centro + new Vector3(Rango(azar, -radio, radio), 0.1f, Rango(azar, -radio, radio));
                Casa(p, azar, muro, techo);
            }
        }

        /// <summary>Cerro escalonado en terrazas, con casas en la cima.</summary>
        private void CerroConTerrazas(Vector3 centro, float radio, int terrazas, System.Random azar, int casas, Color muro, Color? techo)
        {
            const float altoTerraza = AltoTerraza;
            _cerros.Add(new CerroEscalonado { Centro = centro, Radio = radio, Terrazas = terrazas });
            for (int i = 0; i < terrazas; i++)
            {
                float r = radio * (1f - i / (float)(terrazas + 1));
                var color = i % 2 == 0 ? Terraza : Cerro;
                // Un Cylinder mide 2 de alto: escala y = mitad del alto.
                Pieza(_ciudad, PrimitiveType.Cylinder, centro + new Vector3(0f, altoTerraza * (i + 0.5f), 0f),
                    new Vector3(r * 2f, altoTerraza * 0.5f, r * 2f), color);
            }
            float cima = altoTerraza * terrazas;
            float radioCima = radio / (terrazas + 1) * 0.9f;
            for (int i = 0; i < casas; i++)
            {
                float angulo = (float)azar.NextDouble() * Mathf.PI * 2f;
                float d = radioCima + Rango(azar, 0f, radio * 0.4f);
                int terraza = Mathf.Clamp((int)((1f - d / radio) * (terrazas + 1)), 0, terrazas - 1);
                var p = centro + new Vector3(Mathf.Cos(angulo) * d, altoTerraza * (terraza + 1), Mathf.Sin(angulo) * d);
                Casa(p, azar, muro, techo);
            }
        }

        private void Piramide(Vector3 centro, float lado, int escalones, Color color)
        {
            for (int i = 0; i < escalones; i++)
            {
                float t = lado * (1f - i * 0.2f);
                Pieza(_ciudad, PrimitiveType.Cube, centro + new Vector3(0f, 0.1f + 0.25f + i * 0.5f, 0f), new Vector3(t, 0.5f, t), color);
            }
        }

        /// <summary>Reparte lugares fuera de la cuadrícula, al azar pero siempre igual (misma semilla).</summary>
        private List<Vector3> Lugares(int semilla, int cantidad, float minX, float maxX, float minZ, float maxZ,
            float separacion, System.Predicate<Vector3> prohibido)
        {
            var azar = new System.Random(semilla);
            var lugares = new List<Vector3>();
            int intentos = 0;
            while (lugares.Count < cantidad && intentos++ < 5000)
            {
                var p = new Vector3(Rango(azar, minX, maxX), 0f, Rango(azar, minZ, maxZ));
                if (DentroDeLaCuadricula(p, 1.5f) || Cerca(p, lugares, separacion)) continue;
                if (prohibido != null && prohibido(p)) continue;
                lugares.Add(p);
            }
            return lugares;
        }

        private bool DentroDeLaCuadricula(Vector3 p, float margen)
        {
            return p.x > -margen && p.x < Lado + margen && p.z > -margen && p.z < Lado + margen;
        }

        private static bool Cerca(Vector3 p, IList<Vector3> otros, float distancia)
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

        private Transform Grupo(string nombre)
        {
            var grupo = new GameObject(nombre).transform;
            grupo.SetParent(transform, false);
            return grupo;
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
