namespace Altepetl
{
    public struct EdificioEnemigo
    {
        public BuildingId Id;
        public int X;
        public int Y;
        public int Nivel;

        public EdificioEnemigo(BuildingId id, int x, int y, int nivel = 1)
        {
            Id = id;
            X = x;
            Y = y;
            Nivel = nivel;
        }
    }

    /// <summary>
    /// Un capítulo de campaña: una aldea enemiga contra la IA y la historia que lo acompaña.
    /// </summary>
    public sealed class CampaignLevel
    {
        public const int TamanoMapa = 12;

        public string Nombre;
        public string Descripcion;
        public EdificioEnemigo[] Edificios;
        public int[] Botin;          // botín máximo, se gana en proporción a lo destruido
        public int PlumasPrimeraVez; // solo la primera victoria

        /// <summary>Se lee antes de la batalla. Vacío en la campaña general.</summary>
        public string Historia = "";
        /// <summary>Se lee al completar el capítulo por primera vez.</summary>
        public string Epilogo = "";
        /// <summary>Parte del botín que te quedas (al pelear para otro señor, el resto es suyo).</summary>
        public float ParteBotin = 1f;
        public string NotaBotin = "";
        /// <summary>La historia sigue aunque pierdas: se cuenta como derrota, pero el capítulo avanza.</summary>
        public bool AvanzaAunqueSePierda;
        public string TextoDerrota = "";

        /// <summary>Campaña general, para los pueblos que aún no tienen la suya.</summary>
        public static readonly CampaignLevel[] General =
        {
            new CampaignLevel
            {
                Nombre = "Aldea rebelde",
                Descripcion = "Un pueblo pequeño con una sola torre.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5),
                    new EdificioEnemigo(BuildingId.Granja, 2, 2),
                    new EdificioEnemigo(BuildingId.Lenadores, 9, 3),
                    new EdificioEnemigo(BuildingId.Torre, 3, 8),
                },
                Botin = ResourceInfo.Costo(maiz: 300, madera: 300, obsidiana: 50),
                PlumasPrimeraVez = 10,
            },
            new CampaignLevel
            {
                Nombre = "Puesto fronterizo",
                Descripcion = "Dos torres vigilan los almacenes.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5),
                    new EdificioEnemigo(BuildingId.Petlacalco, 2, 7),
                    new EdificioEnemigo(BuildingId.Granja, 9, 9),
                    new EdificioEnemigo(BuildingId.Lenadores, 2, 2),
                    new EdificioEnemigo(BuildingId.Obsidiana, 9, 2),
                    new EdificioEnemigo(BuildingId.Torre, 4, 3),
                    new EdificioEnemigo(BuildingId.Torre, 8, 7),
                },
                Botin = ResourceInfo.Costo(maiz: 500, madera: 500, obsidiana: 120),
                PlumasPrimeraVez = 15,
            },
            new CampaignLevel
            {
                Nombre = "Ciudad tributaria",
                Descripcion = "Tres torres reforzadas protegen un tecpan de nivel 2.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5, 2),
                    new EdificioEnemigo(BuildingId.Petlacalco, 1, 1),
                    new EdificioEnemigo(BuildingId.Petlacalco, 9, 9),
                    new EdificioEnemigo(BuildingId.Granja, 1, 9),
                    new EdificioEnemigo(BuildingId.Lenadores, 10, 1),
                    new EdificioEnemigo(BuildingId.Obsidiana, 6, 9),
                    new EdificioEnemigo(BuildingId.Torre, 4, 4, 2),
                    new EdificioEnemigo(BuildingId.Torre, 8, 4, 2),
                    new EdificioEnemigo(BuildingId.Torre, 5, 8, 2),
                },
                Botin = ResourceInfo.Costo(maiz: 800, madera: 800, obsidiana: 200),
                PlumasPrimeraVez = 25,
            },
        };
    }

    /// <summary>Cada pueblo recorre su propia historia; los que aún no tienen la suya usan la general.</summary>
    public static class Campana
    {
        public static string Titulo(Pueblo pueblo)
        {
            return pueblo != null && pueblo.Id == PuebloId.Mexicas ? "La peregrinación mexica" : "Campaña";
        }

        public static CampaignLevel[] Para(Pueblo pueblo)
        {
            return pueblo != null && pueblo.Id == PuebloId.Mexicas ? Mexica : CampaignLevel.General;
        }

        public static readonly CampaignLevel[] Mexica =
        {
            new CampaignLevel
            {
                Nombre = "Salida de Aztlán",
                Descripcion = "Pueblos del camino, con una sola torre.",
                Historia =
                    "Los mexicas contaban que venían de Aztlán, un lugar rodeado de agua en algún punto del norte. "
                    + "No sabemos si existió como un lugar real o si es sobre todo un origen sagrado.\n\n"
                    + "Huitzilopochtli les habló: había una tierra que les correspondía y debían buscarla. "
                    + "Los teomamaque, los cargadores del dios, llevaban a cuestas su bulto sagrado, y el pueblo los seguía.\n\n"
                    + "En el camino no todos los pueblos los recibían en paz.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5),
                    new EdificioEnemigo(BuildingId.Granja, 2, 2),
                    new EdificioEnemigo(BuildingId.Lenadores, 9, 3),
                    new EdificioEnemigo(BuildingId.Torre, 3, 8),
                },
                Botin = ResourceInfo.Costo(maiz: 300, madera: 300, obsidiana: 50),
                PlumasPrimeraVez = 10,
            },
            new CampaignLevel
            {
                Nombre = "La peregrinación",
                Descripcion = "Dos torres vigilan los almacenes.",
                Historia =
                    "La peregrinación no fue un solo viaje: duró muchas generaciones, más o menos entre los siglos XII y XIV. "
                    + "Se asentaban un tiempo, sembraban y volvían a partir cuando el dios lo ordenaba.\n\n"
                    + "Hubo separaciones: Malinalxóchitl, hermana de Huitzilopochtli, fue abandonada con su gente. "
                    + "Para mantener el favor del dios, los mexicas le ofrecían su propia sangre, punzándose con espinas de maguey.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5),
                    new EdificioEnemigo(BuildingId.Petlacalco, 2, 7),
                    new EdificioEnemigo(BuildingId.Granja, 9, 9),
                    new EdificioEnemigo(BuildingId.Lenadores, 2, 2),
                    new EdificioEnemigo(BuildingId.Obsidiana, 9, 2),
                    new EdificioEnemigo(BuildingId.Torre, 4, 3),
                    new EdificioEnemigo(BuildingId.Torre, 8, 7),
                },
                Botin = ResourceInfo.Costo(maiz: 500, madera: 500, obsidiana: 120),
                PlumasPrimeraVez = 15,
            },
            new CampaignLevel
            {
                Nombre = "Chapultepec",
                Descripcion = "Un cerro muy defendido. En la historia, aquí se perdió.",
                Historia =
                    "Ya en el Valle de México, los mexicas se establecieron en Chapultepec. Eran recién llegados, "
                    + "sin tierra propia, y los pueblos del lago no los querían ahí.\n\n"
                    + "Cópil, hijo de Malinalxóchitl, llegó a vengar a su madre. Los mexicas lo mataron y arrojaron su corazón al lago; "
                    + "según el mito, de ahí nacería la piedra donde crecería el nopal de la señal.\n\n"
                    + "Después, una alianza de pueblos del lago cayó sobre Chapultepec y los mexicas fueron derrotados. "
                    + "Esta batalla es muy difícil: pelea lo mejor que puedas, que la historia sigue aunque pierdas. "
                    + "Destruirla por completo es un reto para un ejército grande.",
                Edificios = new[]
                {
                    // El tecpan en lo alto, rodeado de murallas.
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5, 4),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 4, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 5, 4, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 6, 4, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 4, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 7, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 5, 7, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 6, 7, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 7, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 5, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 6, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 5, 3),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 6, 3),
                    new EdificioEnemigo(BuildingId.Teocalli, 5, 9, 3),
                    // Seis torres alrededor del cerro.
                    new EdificioEnemigo(BuildingId.Torre, 2, 2, 4),
                    new EdificioEnemigo(BuildingId.Torre, 9, 2, 4),
                    new EdificioEnemigo(BuildingId.Torre, 2, 9, 4),
                    new EdificioEnemigo(BuildingId.Torre, 9, 9, 4),
                    new EdificioEnemigo(BuildingId.Torre, 1, 5, 4),
                    new EdificioEnemigo(BuildingId.Torre, 10, 6, 4),
                    new EdificioEnemigo(BuildingId.Granja, 0, 0, 3),
                    new EdificioEnemigo(BuildingId.Granja, 5, 1, 3),
                    new EdificioEnemigo(BuildingId.Obsidiana, 11, 0, 3),
                    new EdificioEnemigo(BuildingId.Lenadores, 11, 11, 3),
                    new EdificioEnemigo(BuildingId.Petlacalco, 0, 10, 3),
                },
                Botin = ResourceInfo.Costo(maiz: 900, madera: 900, obsidiana: 250),
                PlumasPrimeraVez = 40,
                AvanzaAunqueSePierda = true,
                TextoDerrota = "Como cuenta la historia, los mexicas fueron derrotados en Chapultepec y quedaron sometidos a Culhuacan. "
                               + "La peregrinación continúa.",
            },
            new CampaignLevel
            {
                Nombre = "Al servicio de Culhuacan",
                Descripcion = "Guerra contra Xochimilco. Mitad del botín.",
                Historia =
                    "Tras la derrota, el señor de Culhuacan dejó a los mexicas vivir en Tizapan, una tierra pedregosa y llena de serpientes. "
                    + "Sobrevivieron, y Culhuacan los llamó como guerreros en su guerra contra Xochimilco.\n\n"
                    + "Sin tierra propia, los mexicas pelean para otro señor. La mitad del botín es para Culhuacan.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5, 2),
                    new EdificioEnemigo(BuildingId.Petlacalco, 1, 1),
                    new EdificioEnemigo(BuildingId.Petlacalco, 9, 9),
                    new EdificioEnemigo(BuildingId.Granja, 1, 9),
                    new EdificioEnemigo(BuildingId.Granja, 3, 10),
                    new EdificioEnemigo(BuildingId.Lenadores, 10, 1),
                    new EdificioEnemigo(BuildingId.Obsidiana, 6, 9),
                    new EdificioEnemigo(BuildingId.Torre, 4, 4, 2),
                    new EdificioEnemigo(BuildingId.Torre, 8, 4, 2),
                    new EdificioEnemigo(BuildingId.Torre, 5, 8, 2),
                },
                Botin = ResourceInfo.Costo(maiz: 900, madera: 900, obsidiana: 220),
                PlumasPrimeraVez = 25,
                ParteBotin = 0.5f,
                NotaBotin = "La mitad es para Culhuacan",
            },
            new CampaignLevel
            {
                Nombre = "Tributarios de Azcapotzalco",
                Descripcion = "Guerra para los tepanecas. Mitad del botín.",
                Historia =
                    "Por fin, en un islote del lago, los mexicas vieron la señal que Huitzilopochtli había prometido: "
                    + "un águila sobre un nopal que crecía de una piedra. Ahí fundaron México-Tenochtitlan, tradicionalmente en 1325.\n\n"
                    + "Pero la ciudad nacía débil. Los mexicas tributaban a los tepanecas de Azcapotzalco, el poder más grande del valle, "
                    + "y peleaban sus guerras. La mitad del botín es para Azcapotzalco.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5, 3),
                    new EdificioEnemigo(BuildingId.Teocalli, 1, 5),
                    new EdificioEnemigo(BuildingId.Petlacalco, 9, 1),
                    new EdificioEnemigo(BuildingId.Petlacalco, 9, 9),
                    new EdificioEnemigo(BuildingId.Granja, 1, 1),
                    new EdificioEnemigo(BuildingId.Granja, 1, 10),
                    new EdificioEnemigo(BuildingId.Obsidiana, 5, 10),
                    new EdificioEnemigo(BuildingId.Torre, 4, 3, 3),
                    new EdificioEnemigo(BuildingId.Torre, 8, 5, 3),
                    new EdificioEnemigo(BuildingId.Torre, 4, 8, 2),
                    new EdificioEnemigo(BuildingId.Torre, 7, 8, 2),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 6),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 6),
                },
                Botin = ResourceInfo.Costo(maiz: 1300, madera: 1300, obsidiana: 300),
                PlumasPrimeraVez = 40,
                ParteBotin = 0.5f,
                NotaBotin = "La mitad es para Azcapotzalco",
                Epilogo =
                    "México-Tenochtitlan crece sobre el lago. Pasarán casi cien años como tributarios de Azcapotzalco, "
                    + "hasta que en 1428 los mexicas, junto con Nezahualcóyotl de Texcoco, derroten a los tepanecas.\n\n"
                    + "De esa victoria nacerá la Triple Alianza. Esa historia continuará.",
            },
        };
    }
}
