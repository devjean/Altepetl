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
        private GameObject _modeloCasa;
        private Bounds? _limitesCasa;
        private readonly List<Bounds> _parcelasMexicas = new List<Bounds>();
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
            if (_pueblo?.Id == PuebloId.Mexicas) return AlturaParcelaMexica(p);
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
                case PuebloId.Mexicas: return AlturaParcelaMexica(p) <= 0f;
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
                _modeloCasa = _pueblo.Id switch
                {
                    PuebloId.Tlaxcaltecas => Resources.Load<GameObject>($"ModelosCasas/Tlaxcaltecas/Casa_Tlaxcalteca_{nivel}")
                        ?? Resources.Load<GameObject>("ModelosCasas/Casa_Tlaxcaltecas"),
                    PuebloId.Acolhuas => Resources.Load<GameObject>($"ModelosCasas/Acolhuas/Casa_Acolhua_{nivel}")
                        ?? Resources.Load<GameObject>("ModelosCasas/Casa_Acolhuas"),
                    _ => null,
                };
                _limitesCasa = null;
                if (_ciudad != null) Destroy(_ciudad.gameObject);
                _casasAfuera.Clear();
                _parcelasMexicas.Clear();
                _chinampas.Clear();
                _cerros.Clear();
                _ciudad = Grupo("Ciudad");
                CrearCiudad(nivel);
                Manager.ActualizarEntradasDelPaisaje(_casasAfuera);
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
        private static readonly int[] CalzadasMexica = { 0, 0, 1, 2, 3 };
        private const float AnchoViviendaBarrio = 1.35f;
        private const float AnchoCultivoMexica = 0.55f;
        private const string RecursosMexicas = "ModelosCasas/Mexicas/";

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
            var piramide = new Vector3(-8f, 0f, Lado * 0.75f + 5f);
            var casa = Resources.Load<GameObject>(RecursosMexicas + $"Casa_Mexica_{nivel}");
            var cultivo = Resources.Load<GameObject>(RecursosMexicas + "Chinampa_Maiz");
            float escalaCasa = 0f;
            float escalaCultivo = 0f;
            var islotesOriginales = new[]
            {
                new Vector3(-6f, 0f, Lado * 0.6f),
                new Vector3(Lado * 0.55f, 0f, Lado + 6f),
                new Vector3(Lado + 6f, 0f, Lado * 0.35f),
                new Vector3(Lado + 7f, 0f, Lado + 7f),
            };
            int cantidadMaxima = ChinampasMexica[NivelesTecpan - 1];
            int cantidad = ChinampasMexica[nivel - 1];
            var huertos = Lugares(1325, cantidadMaxima, -12f, Lado + 12f, -6f, Lado + 14f, 2.2f,
                p => SobreCalzadaMexica(p) || Cerca(p, islotesOriginales, 4.5f));
            for (int i = 0; i < cantidad && i < huertos.Count; i++) Chinampa(huertos[i], i % 2 == 0);

            var barrios = Barrios(nivel, cantidad,
                p => SobreCalzadaMexica(p) || Cerca(p, huertos, 3f) || (p - piramide).sqrMagnitude < 9f);
            foreach (var barrio in barrios)
            foreach (var centroPar in barrio)
            {
                // Los FBX importados tienen el frente en +Z. Estos giros lo dejan hacia -Z o -X.
                int vueltas = 2 + azar.Next(2);
                var giro = Quaternion.Euler(0f, vueltas * 90f, 0f);
                var pCasa = centroPar + giro * new Vector3(-0.73f, -0.04f, 0f);
                var pCultivo = centroPar + giro * new Vector3(0.73f, -0.04f, 0f);
                if (casa != null)
                {
                    var vivienda = ColocarParcelaMexica(casa, pCasa, AnchoViviendaBarrio, ref escalaCasa);
                    vivienda.localRotation = giro * vivienda.localRotation;
                    var entrada = vivienda.Find("Entrada");
                    var llegada = entrada != null ? entrada.position : pCasa;
                    _casasAfuera.Add(new Vector3(llegada.x, 0f, llegada.z));
                    if (entrada != null && vivienda.Find("Base de tierra") is Transform tierra)
                    {
                        var limites = tierra.GetComponent<MeshRenderer>().bounds;
                        limites.center = new Vector3(limites.center.x, entrada.position.y, limites.center.z);
                        _parcelasMexicas.Add(limites);
                    }
                }
                else Casa(pCasa + Vector3.up * 0.04f, azar, Cal, null, giroCasa: giro);

                if (cultivo != null)
                {
                    var chinampa = ColocarParcelaMexica(cultivo, pCultivo, AnchoCultivoMexica, ref escalaCultivo);
                    chinampa.localRotation = giro * chinampa.localRotation;
                }
                else Chinampa(pCultivo, vueltas % 2 != 0, 0.24f, registrar: false);
            }
            if (nivel == NivelesTecpan) Piramide(piramide, 2.6f, 4, Cal);

            // Calzadas hacia tierra firme, como las de Tepeyac, Tlacopan e Iztapalapa.
            var centro = CentroMapa;
            if (CalzadasMexica[nivel - 1] >= 1) Calzada(new Vector3(centro.x, 0f, Lado), new Vector3(centro.x, 0f, Lado + 30f));
            if (CalzadasMexica[nivel - 1] >= 2) Calzada(new Vector3(Lado, 0f, centro.z), new Vector3(Lado + 30f, 0f, centro.z));
            if (CalzadasMexica[nivel - 1] >= 3) Calzada(new Vector3(0f, 0f, centro.z), new Vector3(-30f, 0f, centro.z));
        }

        private List<Vector3[]> Barrios(int nivel, int cantidad, System.Predicate<Vector3> fueraDelBarrio)
        {
            int ladoClaro = GameManager.LadoConstruible[nivel - 1];
            float borde = ladoClaro * 0.5f;
            float altoVista = (ladoClaro + 4f) * Mathf.Sqrt(2f) * 0.5f + 3f;
            float semialto = altoVista * 0.65f;
            var barrios = new List<Vector3[]>();
            var ocupadas = new List<Vector3>(cantidad);
            var azar = new System.Random(2718);
            int restantes = cantidad;
            while (restantes > 0)
            {
                int tamano = restantes == 2 || restantes == 4 ? 2 : 3;
                var barrio = new Vector3[tamano];
                bool encontrado = false;
                for (int intento = 0; intento < 1200 && !encontrado; intento++)
                {
                    int lado = (barrios.Count + intento) % 4;
                    var normal = Quaternion.Euler(0f, lado * 90f, 0f) * Vector3.right;
                    var eje = new Vector3(-normal.z, 0f, normal.x);
                    float distancia = borde + Rango(azar, 1.7f, intento < 400 ? 5f : 9f);
                    var centro = CentroMapa + normal * distancia + eje * Rango(azar, -borde - 2f, borde + 2f);
                    encontrado = true;
                    for (int i = 0; i < tamano; i++)
                    {
                        var p = centro + eje * ((i - (tamano - 1) * 0.5f) * 2.9f);
                        var relativo = p - CentroMapa;
                        // Reserva espacio en las esquinas del encuadre horizontal inicial para los controles.
                        float x = (relativo.x - relativo.z) * 0.7071f / (semialto * (16f / 9f));
                        float y = (relativo.x + relativo.z) * 0.3536f / semialto;
                        bool bajoHud = (y > 0.35f && Mathf.Abs(x) > 0.45f) || (y < -0.5f && Mathf.Abs(x) > 0.32f);
                        if ((intento < 800 && bajoHud) || fueraDelBarrio(p) || Cerca(p, ocupadas, 3.2f))
                        {
                            encontrado = false;
                            break;
                        }
                        barrio[i] = p;
                    }
                }
                if (!encontrado) break;
                barrios.Add(barrio);
                ocupadas.AddRange(barrio);
                restantes -= tamano;
            }
            return barrios;
        }

        private Transform ColocarParcelaMexica(GameObject prefab, Vector3 punto, float ancho, ref float escala)
        {
            var parcela = Instantiate(prefab, _ciudad).transform;
            parcela.localPosition = punto;
            // Mide la geometría ya importada una vez por modelo. El factor de unidades del FBX
            // puede cambiar, pero la huella debe caber en el espacio reservado por el paisaje.
            if (escala <= 0f)
            {
                var dibujos = parcela.GetComponentsInChildren<MeshRenderer>();
                var limites = new Bounds(punto, Vector3.zero);
                foreach (var dibujo in dibujos) limites.Encapsulate(dibujo.bounds);
                float lado = Mathf.Max(limites.size.x, limites.size.z);
                escala = lado > 0f ? ancho / lado : 1f;
            }
            parcela.localScale *= escala;
            return parcela;
        }

        private float AlturaParcelaMexica(Vector3 p)
        {
            foreach (var parcela in _parcelasMexicas)
                if (Mathf.Abs(p.x - parcela.center.x) <= parcela.extents.x
                    && Mathf.Abs(p.z - parcela.center.z) <= parcela.extents.z)
                    return parcela.center.y;
            return 0f;
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
            var barrios = Barrios(nivel, CasasAcolhua[nivel - 1],
                p => p.x < 0f || (p - tetzcotzinco).sqrMagnitude < 64f || (p - templo).sqrMagnitude < 9f);
            ColocarCasasDeBarrio(barrios, azar, null);

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
            var barrios = Barrios(nivel, CasasTlaxcalteca[nivel - 1], p => Cerca(p, cabeceras, 7.1f));
            ColocarCasasDeBarrio(barrios, azar, Paja);

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

        private void ColocarCasasDeBarrio(List<Vector3[]> barrios, System.Random azar, Color? techo)
        {
            foreach (var barrio in barrios)
            foreach (var posicion in barrio)
            {
                var giro = Quaternion.Euler(0f, 180f + azar.Next(2) * 90f, 0f);
                Casa(posicion, azar, Cal, techo, AnchoViviendaBarrio, giro);
            }
        }

        private void Volcanes(Vector3 centro)
        {
            // La cámara mira hacia +x +z: lo de ese lado queda al fondo.
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(30f, -2f, 52f), new Vector3(26f, 18f, 18f), Volcan);
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(52f, -2f, 30f), new Vector3(18f, 22f, 26f), Volcan);
            Pieza(_fijo, PrimitiveType.Sphere, centro + new Vector3(46f, -3f, 46f), new Vector3(20f, 14f, 20f), Volcan * 0.9f);
        }

        /// <summary>Parcela de lodo con siembra y un ahuejote en la esquina.</summary>
        private void Chinampa(Vector3 p, bool larga, float escala = 1f, bool registrar = true)
        {
            var tam = (larga ? new Vector3(1.2f, 0.08f, 2.4f) : new Vector3(2.4f, 0.08f, 1.2f)) * escala;
            if (registrar) _chinampas.Add(new Vector3(p.x, 0f, p.z));
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

        /// <summary>Coloca una casa del pueblo y registra su punto de llegada.</summary>
        private void Casa(Vector3 p, System.Random azar, Color muro, Color? techo,
            float? anchoFijo = null, Quaternion? giroCasa = null)
        {
            float ancho = anchoFijo ?? Rango(azar, 0.5f, 0.9f);
            float alto = Rango(azar, 0.3f, 0.5f);
            var giro = giroCasa ?? Quaternion.identity;
            if (_modeloCasa != null)
            {
                var casa = Instantiate(_modeloCasa, _ciudad);
                casa.transform.localPosition = Vector3.zero;
                if (!_limitesCasa.HasValue)
                {
                    var dibujos = casa.GetComponentsInChildren<MeshRenderer>();
                    if (dibujos.Length > 0)
                    {
                        var limites = dibujos[0].bounds;
                        for (int i = 1; i < dibujos.Length; i++) limites.Encapsulate(dibujos[i].bounds);
                        limites.center -= _ciudad.position;
                        if (Mathf.Max(limites.size.x, limites.size.z) > 0f) _limitesCasa = limites;
                    }
                    if (!_limitesCasa.HasValue) _modeloCasa = null;
                }
                if (_limitesCasa.HasValue)
                {
                    // Normaliza la huella y apoya la base en el terreno, aunque el FBX tenga otro pivote.
                    var limites = _limitesCasa.Value;
                    float escala = ancho / Mathf.Max(limites.size.x, limites.size.z);
                    casa.transform.localScale *= escala;
                    casa.transform.localRotation = giro * casa.transform.localRotation;
                    casa.transform.position = p - giro * new Vector3(limites.center.x, limites.min.y, limites.center.z) * escala;
                    _casasAfuera.Add(new Vector3(p.x, 0f, p.z)
                        + giro * (Vector3.forward * (limites.extents.z * escala + 0.15f)));
                    return;
                }
                Destroy(casa);
            }
            // La puerta, a ras de suelo; en los cerros la gente sube por las terrazas (ver AlturaSuelo).
            _casasAfuera.Add(new Vector3(p.x, 0f, p.z) + giro * (Vector3.forward * (ancho * 0.5f + 0.15f)));
            Pieza(_ciudad, PrimitiveType.Cube, p + new Vector3(0f, alto * 0.5f, 0f), new Vector3(ancho, alto, ancho), muro);
            if (techo.HasValue)
            {
                Pieza(_ciudad, PrimitiveType.Cube, p + new Vector3(0f, alto + 0.08f, 0f),
                    new Vector3(ancho * 1.15f, 0.16f, ancho * 1.15f), techo.Value);
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
            // Coloca las viviendas en la ladera que mira a la cámara, donde el cerro no oculta las puertas.
            float d = radio * 2.5f / (terrazas + 1);
            float altura = altoTerraza * (terrazas - 1);
            for (int i = 0; i < casas; i++)
            {
                float t = casas > 1 ? i / (float)(casas - 1) : 0.5f;
                float angulo = Mathf.Lerp(160f, 290f, t) * Mathf.Deg2Rad;
                var p = centro + new Vector3(Mathf.Cos(angulo) * d, altura, Mathf.Sin(angulo) * d);
                var giro = Quaternion.Euler(0f, t < 0.5f ? 270f : 180f, 0f);
                Casa(p, azar, muro, techo, giroCasa: giro);
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
            var pieza = Figuras.Crear(tipo, padre, color);
            pieza.transform.localPosition = posicion;
            pieza.transform.localScale = escala;
        }
    }
}
