using UnityEngine;

namespace Altepetl
{
    public enum TroopId
    {
        Macuahuitl = 0,
        Arquero = 1,
        Hondero = 2,
    }

    /// <summary>Datos fijos de un tipo de tropa.</summary>
    public sealed class TroopDefinition
    {
        public TroopId Id;
        public string NombreEspanol;
        public string NombreNahuatl;     // por su arma: macuahuitl, tlahuitolli (arco), tematlatl (honda)
        public string Nombre => Terminos.Nahuatl ? NombreNahuatl : NombreEspanol;
        public string Descripcion;
        public int[] Costo = ResourceInfo.Costo();
        public float SegundosEntrenamiento = 5f;
        public float Vida = 100f;
        public float DanoPorSegundo = 10f;
        public float Alcance = 0.6f;       // distancia al borde del objetivo para poder atacar
        public float Velocidad = 1.5f;     // casillas por segundo
        public bool PrefiereDefensas;      // va primero por las torres
        public Color Color = Color.white;
    }

    public static class TroopCatalog
    {
        public const int Count = 3;

        public static readonly TroopDefinition[] Todos =
        {
            new TroopDefinition
            {
                Id = TroopId.Macuahuitl,
                NombreEspanol = "Guerrero",
                NombreNahuatl = "Macuahuitl",
                Descripcion = "Cuerpo a cuerpo con macuahuitl. Resistente.",
                Costo = ResourceInfo.Costo(maiz: 25),
                SegundosEntrenamiento = 6f,
                Vida = 120f,
                DanoPorSegundo = 12f,
                Alcance = 0.4f,
                Velocidad = 1.6f,
                Color = new Color(0.75f, 0.20f, 0.15f),
            },
            new TroopDefinition
            {
                Id = TroopId.Arquero,
                NombreEspanol = "Arquero",
                NombreNahuatl = "Tlahuitolli",
                Descripcion = "Ataca de lejos, pero aguanta poco.",
                Costo = ResourceInfo.Costo(maiz: 35),
                SegundosEntrenamiento = 8f,
                Vida = 50f,
                DanoPorSegundo = 9f,
                Alcance = 3f,
                Velocidad = 1.8f,
                Color = new Color(0.95f, 0.80f, 0.25f),
            },
            new TroopDefinition
            {
                Id = TroopId.Hondero,
                NombreEspanol = "Hondero",
                NombreNahuatl = "Tematlatl",
                Descripcion = "Va primero contra las torres.",
                Costo = ResourceInfo.Costo(maiz: 45),
                SegundosEntrenamiento = 10f,
                Vida = 70f,
                DanoPorSegundo = 14f,
                Alcance = 2.2f,
                Velocidad = 1.4f,
                PrefiereDefensas = true,
                Color = new Color(0.25f, 0.55f, 0.85f),
            },
        };

        public static TroopDefinition Get(TroopId id)
        {
            return Todos[(int)id];
        }
    }

    /// <summary>
    /// Rangos que se ganan en batalla: sobrevivir hace al guerrero experimentado y
    /// hacer una captura lo convierte en tlamani ("el que ha capturado").
    /// </summary>
    public static class Rangos
    {
        public const int Count = 5;
        public const int Joven = 0;
        public const int Experimentado = 1;
        public const int Capturador = 2;
        /// <summary>Cuarto rango, el que se gana en el calmécac. Para los mexicas es la orden cuauhtli (águila).</summary>
        public const int Elite = 3;
        /// <summary>Solo mexicas: la orden ocelotl (jaguar). Mismas fuerzas que cuauhtli, distinto atavío.</summary>
        public const int Ocelotl = 4;
        public const float BonoPorRango = 0.15f; // +15 % de vida y ataque por rango
        public const float BonoElite = 0.4f;     // la élite, +40 % sobre el tlamani
        /// <summary>Un yaotequihua (tlaxcaltecas y acolhuas) hace pelear mejor a los que tiene cerca.</summary>
        public const float BonoLider = 0.10f;
        public const float RadioLider = 2.5f;
        /// <summary>Un cuauhtli u ocelotl que vence a un guerrero tiene más probabilidad de capturarlo.</summary>
        public const float CapturaExtraElite = 0.30f;

        public static bool EsElite(int rango) => rango >= Elite;

        /// <summary>El rango ocelotl solo existe entre los mexicas.</summary>
        public static bool Existe(Pueblo pueblo, int rango)
        {
            return rango != Ocelotl || (pueblo != null && pueblo.Id == PuebloId.Mexicas);
        }

        /// <summary>
        /// Probabilidad de hacer una captura al derribar un edificio. Los mexicas, que necesitan
        /// cautivos para Huitzilopochtli, buscan capturar más que los demás.
        /// </summary>
        public static float ProbabilidadCaptura(Pueblo pueblo)
        {
            return pueblo != null && pueblo.Id == PuebloId.Mexicas ? 0.26f : 0.2f;
        }

        /// <summary>Probabilidad de capturar a un guerrero vencido; la élite mexica tiene más.</summary>
        public static float ProbabilidadCapturaGuerrero(Pueblo pueblo, int rango)
        {
            float probabilidad = ProbabilidadCaptura(pueblo);
            if (EsElite(rango) && pueblo != null && pueblo.Id == PuebloId.Mexicas) probabilidad += CapturaExtraElite;
            return probabilidad;
        }

        /// <summary>¿Este rango hace de líder (yaotequihua) para los de alrededor? Los mexicas no: su élite es prestigio propio.</summary>
        public static bool EsLider(Pueblo pueblo, int rango)
        {
            return EsElite(rango) && pueblo != null && pueblo.Id != PuebloId.Mexicas;
        }

        private static readonly string[] NombresMexicas =
            { "Joven guerrero", "Guerrero experimentado", "Tlamani", "Cuauhtli", "Ocelotl" };
        // De acolhuas y tlaxcaltecas no hay una lista de rangos tan detallada; se usan nombres generales.
        // Yaotequihua: capitán de guerra por méritos (en Tlaxcala documentado; en Texcoco se usa como palabra general).
        private static readonly string[] NombresGenerales =
            { "Joven guerrero", "Guerrero experimentado", "Veterano", "Yaotequihua", "Yaotequihua" };

        public static string Nombre(Pueblo pueblo, int rango)
        {
            var nombres = pueblo != null && pueblo.Id == PuebloId.Mexicas ? NombresMexicas : NombresGenerales;
            return nombres[UnityEngine.Mathf.Clamp(rango, 0, Count - 1)];
        }

        /// <summary>Lo que es la élite de cada pueblo, para las fichas.</summary>
        public static string DescripcionElite(Pueblo pueblo)
        {
            if (pueblo == null) return "";
            switch (pueblo.Id)
            {
                case PuebloId.Mexicas:
                    return "Las órdenes cuauhtli (águila) y ocelotl (jaguar): un honor que se ganaba con capturas. "
                           + "Mismas fuerzas, distinto atavío. Al vencer a un guerrero tienen más probabilidad de capturarlo.";
                case PuebloId.Tlaxcaltecas:
                    return "Los yaotequihuaque: capitanes de guerra por sus hazañas, sin necesidad de ser nobles. "
                           + "Los que pelean cerca de uno atacan 10% más, y cada uno suma 2 macehualtin al altepetl, "
                           + "porque en Tlaxcallan el mérito de guerra abría el poder político.";
                default:
                    return "Yaotequihua, palabra náhuatl general para capitán de guerra (de los acolhuas no hay un nombre propio documentado). "
                           + "Los que pelean cerca de uno atacan 10% más.";
            }
        }

        public static float Multiplicador(int rango)
        {
            if (EsElite(rango)) return (1f + BonoPorRango * Capturador) * (1f + BonoElite);
            return 1f + BonoPorRango * rango;
        }

        /// <summary>Tamaño del monito: crece con el rango; cuauhtli y ocelotl son iguales.</summary>
        public static float Tamano(int rango) => 1f + 0.12f * UnityEngine.Mathf.Min(rango, Elite);

        /// <summary>Color del monito: el de su tipo, y la élite con su atavío encima.</summary>
        public static Color ColorDe(Pueblo pueblo, Color tipo, int rango)
        {
            if (!EsElite(rango)) return tipo;
            bool mexica = pueblo != null && pueblo.Id == PuebloId.Mexicas;
            Color atavio;
            if (!mexica) atavio = new Color(0.95f, 0.93f, 0.85f);          // manta blanca con divisa
            else if (rango == Ocelotl) atavio = new Color(0.95f, 0.78f, 0.30f); // piel de jaguar
            else atavio = new Color(0.50f, 0.33f, 0.14f);                    // plumaje de águila
            return Color.Lerp(tipo, atavio, 0.6f);
        }

        /// <summary>Rango con el que regresa una tropa que sobrevivió a la batalla. La élite no se pierde.</summary>
        public static int AlRegresar(int rango, int capturas)
        {
            if (EsElite(rango)) return rango;
            if (capturas > 0) return Capturador;
            return UnityEngine.Mathf.Max(rango, Experimentado);
        }
    }
}
