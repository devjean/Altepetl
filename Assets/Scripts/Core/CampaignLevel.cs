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

    /// <summary>Guerreros que defienden la aldea enemiga; esperan junto al tecpan.</summary>
    public struct GrupoDefensor
    {
        public TroopId Tipo;
        public int Rango;
        public int Cantidad;

        public GrupoDefensor(TroopId tipo, int cantidad, int rango = 0)
        {
            Tipo = tipo;
            Cantidad = cantidad;
            Rango = rango;
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
        public GrupoDefensor[] Defensores = new GrupoDefensor[0];
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
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 2),
                },
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
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 3),
                    new GrupoDefensor(TroopId.Arquero, 2),
                },
                PlumasPrimeraVez = 25,
            },
        };
    }

    /// <summary>Cada pueblo recorre su propia historia; los que aún no tienen la suya usan la general.</summary>
    public static class Campana
    {
        public static string Titulo(Pueblo pueblo)
        {
            if (pueblo == null) return "Campaña";
            switch (pueblo.Id)
            {
                case PuebloId.Mexicas: return "La peregrinación mexica";
                case PuebloId.Acolhuas: return "Los chichimecas de Xólotl";
                case PuebloId.Tlaxcaltecas: return "La fundación de Tlaxcallan";
                default: return "Campaña";
            }
        }

        public static CampaignLevel[] Para(Pueblo pueblo)
        {
            if (pueblo == null) return CampaignLevel.General;
            switch (pueblo.Id)
            {
                case PuebloId.Mexicas: return Mexica;
                case PuebloId.Acolhuas: return Acolhua;
                case PuebloId.Tlaxcaltecas: return Tlaxcalteca;
                default: return CampaignLevel.General;
            }
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
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 2),
                },
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
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 3),
                    new GrupoDefensor(TroopId.Arquero, 1),
                },
                PlumasPrimeraVez = 15,
            },
            new CampaignLevel
            {
                Nombre = "Chapultepec",
                Descripcion = "Un cerro muy defendido. Aquí se perdió.",
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
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5, 6),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 4, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 5, 4, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 6, 4, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 4, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 5, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 6, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 5, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 6, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 5, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 6, 5),
                    new EdificioEnemigo(BuildingId.Teocalli, 5, 9, 5),
                    // Ocho torres alrededor del cerro. Los niveles pasan del 5: son los pueblos del lago aliados.
                    new EdificioEnemigo(BuildingId.Torre, 2, 2, 7),
                    new EdificioEnemigo(BuildingId.Torre, 9, 2, 7),
                    new EdificioEnemigo(BuildingId.Torre, 2, 9, 7),
                    new EdificioEnemigo(BuildingId.Torre, 9, 9, 7),
                    new EdificioEnemigo(BuildingId.Torre, 1, 5, 7),
                    new EdificioEnemigo(BuildingId.Torre, 10, 6, 7),
                    new EdificioEnemigo(BuildingId.Torre, 6, 2, 7),
                    new EdificioEnemigo(BuildingId.Torre, 3, 11, 7),
                    new EdificioEnemigo(BuildingId.Granja, 0, 0, 5),
                    new EdificioEnemigo(BuildingId.Granja, 5, 1, 5),
                    new EdificioEnemigo(BuildingId.Obsidiana, 11, 0, 5),
                    new EdificioEnemigo(BuildingId.Lenadores, 11, 11, 5),
                    new EdificioEnemigo(BuildingId.Petlacalco, 0, 10, 5),
                },
                Botin = ResourceInfo.Costo(maiz: 900, madera: 900, obsidiana: 250),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 6, 1),
                    new GrupoDefensor(TroopId.Arquero, 4, 1),
                },
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
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 5),
                    new GrupoDefensor(TroopId.Arquero, 3),
                },
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
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 6, 1),
                    new GrupoDefensor(TroopId.Arquero, 3),
                    new GrupoDefensor(TroopId.Hondero, 3),
                },
                PlumasPrimeraVez = 40,
                ParteBotin = 0.5f,
                NotaBotin = "La mitad es para Azcapotzalco",
                Epilogo =
                    "México-Tenochtitlan crece sobre el lago. Pasarán casi cien años como tributarios de Azcapotzalco, "
                    + "hasta que en 1428 los mexicas, junto con Nezahualcóyotl de Texcoco, derroten a los tepanecas.\n\n"
                    + "De esa victoria nacerá la Triple Alianza. Esa historia continuará.",
            },
        };

        public static readonly CampaignLevel[] Acolhua =
        {
            new CampaignLevel
            {
                Nombre = "La llegada de Xólotl",
                Descripcion = "Un pueblo del valle, con una sola torre.",
                Historia =
                    "Después de la caída de Tula y del mundo tolteca, muchos grupos del norte entraron en la Cuenca de México. "
                    + "Uno de los más importantes era el de Xólotl, un gran jefe chichimeca.\n\n"
                    + "Los chichimecas de Xólotl no eran agricultores: cazaban con arco y flecha, recolectaban, vivían en cuevas "
                    + "y recorrían territorios muy amplios. Alrededor de los lagos, en cambio, vivían pueblos sedentarios que sembraban maíz.\n\n"
                    + "No todos los pueblos del valle recibieron en paz a los recién llegados.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5),
                    new EdificioEnemigo(BuildingId.Granja, 2, 2),
                    new EdificioEnemigo(BuildingId.Granja, 9, 9),
                    new EdificioEnemigo(BuildingId.Lenadores, 9, 2),
                    new EdificioEnemigo(BuildingId.Torre, 3, 8),
                },
                Botin = ResourceInfo.Costo(maiz: 300, madera: 300, obsidiana: 50),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 2),
                },
                PlumasPrimeraVez = 10,
            },
            new CampaignLevel
            {
                Nombre = "Tenayuca",
                Descripcion = "Culhuacan se niega a pagar tributo.",
                Historia =
                    "Xólotl hizo de Tenayuca su primera capital. Desde ahí organizó el territorio: "
                    + "repartió tierras y señoríos entre sus jefes y entre los grupos que fueron llegando después.\n\n"
                    + "Según la Historia chichimeca de Fernando de Alva Ixtlilxóchitl, Xólotl reclamó tributo a los señores toltecas de Culhuacan. "
                    + "Nauhyotzin respondió que no reconocían a ningún señor extranjero y que querían conservar su libertad. "
                    + "Xólotl envió entonces a su hijo Nopaltzin con un ejército, y la batalla se dio en la laguna y los carrizales de Culhuacan.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5),
                    new EdificioEnemigo(BuildingId.Petlacalco, 2, 7),
                    new EdificioEnemigo(BuildingId.Granja, 9, 9),
                    new EdificioEnemigo(BuildingId.Granja, 1, 1),
                    new EdificioEnemigo(BuildingId.Lenadores, 4, 1),
                    new EdificioEnemigo(BuildingId.Obsidiana, 9, 2),
                    new EdificioEnemigo(BuildingId.Torre, 4, 3),
                    new EdificioEnemigo(BuildingId.Torre, 8, 7),
                },
                Botin = ResourceInfo.Costo(maiz: 500, madera: 500, obsidiana: 120),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 3),
                    new GrupoDefensor(TroopId.Arquero, 1),
                },
                PlumasPrimeraVez = 15,
            },
            new CampaignLevel
            {
                Nombre = "La rebelión de Yacanex",
                Descripcion = "Señores rebeldes: muchos arqueros.",
                Historia =
                    "Poco a poco, los chichimecas adoptaron la agricultura, la vida sedentaria y costumbres de los pueblos que ya estaban ahí. "
                    + "Según Ixtlilxóchitl, Tlotzin impulsó el cultivo de la tierra después de aprenderlo de Tecpoyo Achcauhtli. "
                    + "Algunos chichimecas aceptaron sembrar; otros, apegados a las costumbres de sus antepasados, "
                    + "se marcharon a las sierras de Metztitlan y Totépec.\n\n"
                    + "Más tarde, durante el gobierno de Quinatzin, Yacanex y otros señores se rebelaron, "
                    + "y las tropas de Quinatzin salieron a enfrentarlos.\n\n"
                    + "Sus defensores son casi todos arqueros: entra con guerreros que aguanten.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5, 2),
                    new EdificioEnemigo(BuildingId.Lenadores, 1, 1, 2),
                    new EdificioEnemigo(BuildingId.Lenadores, 10, 10, 2),
                    new EdificioEnemigo(BuildingId.Lenadores, 10, 1),
                    new EdificioEnemigo(BuildingId.Obsidiana, 1, 10, 2),
                    new EdificioEnemigo(BuildingId.Petlacalco, 8, 9),
                    new EdificioEnemigo(BuildingId.Torre, 3, 3, 2),
                    new EdificioEnemigo(BuildingId.Torre, 8, 3, 2),
                    new EdificioEnemigo(BuildingId.Torre, 3, 8, 2),
                },
                Botin = ResourceInfo.Costo(maiz: 700, madera: 1000, obsidiana: 220),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Arquero, 5),
                    new GrupoDefensor(TroopId.Macuahuitl, 2),
                },
                PlumasPrimeraVez = 25,
            },
            new CampaignLevel
            {
                Nombre = "La guerra tepaneca",
                Descripcion = "Los tepanecas cercan Texcoco. Aquí se perdió.",
                Historia =
                    "Con el tiempo, el centro del poder acolhua pasó de Tenayuca a Texcoco, junto al lago. "
                    + "Pero los tepanecas de Azcapotzalco, gobernados por Tezozómoc, se volvieron el poder más grande del valle.\n\n"
                    + "En 1418 los tepanecas atacaron Texcoco. El señor Ixtlilxóchitl salió a pelear por su ciudad.\n\n"
                    + "Esta batalla es muy difícil: pelea lo mejor que puedas, que la historia sigue aunque pierdas. "
                    + "Destruirla por completo es un reto para un ejército grande.",
                Edificios = new[]
                {
                    // El campamento tepaneca: tecpan amurallado y torres reforzadas.
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5, 6),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 4, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 5, 4, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 6, 4, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 4, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 5, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 6, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 5, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 6, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 5, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 6, 5),
                    new EdificioEnemigo(BuildingId.Teocalli, 9, 5, 5),
                    new EdificioEnemigo(BuildingId.Torre, 2, 2, 7),
                    new EdificioEnemigo(BuildingId.Torre, 9, 2, 7),
                    new EdificioEnemigo(BuildingId.Torre, 2, 9, 7),
                    new EdificioEnemigo(BuildingId.Torre, 9, 9, 7),
                    new EdificioEnemigo(BuildingId.Torre, 5, 1, 7),
                    new EdificioEnemigo(BuildingId.Torre, 6, 10, 7),
                    new EdificioEnemigo(BuildingId.Torre, 1, 6, 7),
                    new EdificioEnemigo(BuildingId.Granja, 0, 0, 5),
                    new EdificioEnemigo(BuildingId.Granja, 11, 11, 5),
                    new EdificioEnemigo(BuildingId.Obsidiana, 11, 0, 5),
                    new EdificioEnemigo(BuildingId.Lenadores, 0, 11, 5),
                    new EdificioEnemigo(BuildingId.Petlacalco, 2, 4, 5),
                },
                Botin = ResourceInfo.Costo(maiz: 900, madera: 900, obsidiana: 250),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 5, 1),
                    new GrupoDefensor(TroopId.Hondero, 3, 1),
                    new GrupoDefensor(TroopId.Arquero, 2, 1),
                },
                PlumasPrimeraVez = 40,
                AvanzaAunqueSePierda = true,
                TextoDerrota = "Como cuenta la historia, Texcoco cayó e Ixtlilxóchitl murió peleando. "
                               + "Su hijo Nezahualcóyotl, de unos dieciséis años, lo vio todo escondido entre las ramas de un árbol. "
                               + "Comienza su exilio.",
            },
            new CampaignLevel
            {
                Nombre = "Nezahualcóyotl",
                Descripcion = "Contra Azcapotzalco, junto a los mexicas.",
                Historia =
                    "Nezahualcóyotl pasó años huyendo y escondiéndose de los tepanecas. Cuando murió Tezozómoc, "
                    + "su hijo Maxtla tomó el poder en Azcapotzalco y siguió persiguiéndolo.\n\n"
                    + "Con la ayuda de Huexotzinco y de Tlaxcallan, Nezahualcóyotl recuperó Texcoco. "
                    + "Después se unió a los mexicas de Itzcóatl, y juntos marcharon contra Azcapotzalco en 1428.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5, 3),
                    new EdificioEnemigo(BuildingId.Teocalli, 1, 5),
                    new EdificioEnemigo(BuildingId.Petlacalco, 9, 1),
                    new EdificioEnemigo(BuildingId.Petlacalco, 9, 9),
                    new EdificioEnemigo(BuildingId.Granja, 1, 1),
                    new EdificioEnemigo(BuildingId.Granja, 1, 10),
                    new EdificioEnemigo(BuildingId.Lenadores, 5, 1),
                    new EdificioEnemigo(BuildingId.Obsidiana, 5, 10),
                    new EdificioEnemigo(BuildingId.Torre, 4, 3, 3),
                    new EdificioEnemigo(BuildingId.Torre, 8, 5, 3),
                    new EdificioEnemigo(BuildingId.Torre, 4, 8, 3),
                    new EdificioEnemigo(BuildingId.Torre, 7, 8, 2),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 6),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 6),
                },
                Botin = ResourceInfo.Costo(maiz: 1300, madera: 1300, obsidiana: 320),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 6, 1),
                    new GrupoDefensor(TroopId.Arquero, 3),
                    new GrupoDefensor(TroopId.Hondero, 3),
                },
                PlumasPrimeraVez = 40,
                Epilogo =
                    "Azcapotzalco cayó y Maxtla huyó. De esa victoria nació la Triple Alianza: Tenochtitlan, Texcoco y Tlacopan.\n\n"
                    + "Nezahualcóyotl gobernó Texcoco y la convirtió en un gran centro de leyes, obras y poesía. "
                    + "Esa historia continuará.",
            },
        };

        public static readonly CampaignLevel[] Tlaxcalteca =
        {
            new CampaignLevel
            {
                Nombre = "Poyauhtlan",
                Descripcion = "Pueblos vecinos, con una sola torre.",
                Historia =
                    "Hace unos 800 o 900 años, varios grupos chichimecas llegaron del norte. Entre ellos venían los primeros tlaxcaltecas, "
                    + "guiados por Camaxtli, su dios tutelar.\n\n"
                    + "Según la tradición que recogió Diego Muñoz Camargo en su Historia de Tlaxcala, se asentaron en los llanos de Poyauhtlan, "
                    + "en tierras de Texcoco, y vivían de la caza. Ahí se enfrentaron con los pueblos vecinos, "
                    + "y después de esa guerra decidieron seguir su camino.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5),
                    new EdificioEnemigo(BuildingId.Granja, 2, 2),
                    new EdificioEnemigo(BuildingId.Lenadores, 9, 3),
                    new EdificioEnemigo(BuildingId.Lenadores, 2, 9),
                    new EdificioEnemigo(BuildingId.Torre, 8, 8),
                },
                Botin = ResourceInfo.Costo(maiz: 300, madera: 300, obsidiana: 50),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 2),
                },
                PlumasPrimeraVez = 10,
            },
            new CampaignLevel
            {
                Nombre = "Tepeticpac",
                Descripcion = "Los olmeca-xicalancas defienden la región.",
                Historia =
                    "Después partieron hacia el oriente, al valle de Puebla-Tlaxcala. Se establecieron en Tepeticpac, "
                    + "en lo alto de un cerro fácil de defender, y ahí fundaron su primer altepetl.\n\n"
                    + "Según Muñoz Camargo, los olmeca-xicalancas tenían asentamientos y fortificaciones en partes de la región, "
                    + "como Xochitécatl, y los recién llegados se enfrentaron a ellos.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5),
                    new EdificioEnemigo(BuildingId.Teocalli, 1, 1),
                    new EdificioEnemigo(BuildingId.Petlacalco, 9, 8),
                    new EdificioEnemigo(BuildingId.Granja, 2, 9),
                    new EdificioEnemigo(BuildingId.Granja, 10, 1),
                    new EdificioEnemigo(BuildingId.Obsidiana, 5, 10),
                    new EdificioEnemigo(BuildingId.Torre, 4, 3),
                    new EdificioEnemigo(BuildingId.Torre, 8, 5),
                },
                Botin = ResourceInfo.Costo(maiz: 500, madera: 500, obsidiana: 120),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 2),
                    new GrupoDefensor(TroopId.Hondero, 2),
                },
                PlumasPrimeraVez = 15,
            },
            new CampaignLevel
            {
                Nombre = "Las cuatro cabeceras",
                Descripcion = "Un señorío vecino disputa las tierras.",
                Historia =
                    "Desde Tepeticpac crecieron otras cabeceras: Ocotelulco, Tizatlán y Quiahuiztlán. "
                    + "Cada una tenía su propio señor, y entre las cuatro formaron el núcleo de Tlaxcallan.\n\n"
                    + "A diferencia de Tenochtitlan, aquí no mandaba un solo gobernante: Tlaxcallan fue una confederación de más de veinte altepemeh "
                    + "y el poder se repartía entre los principales señores.\n\n"
                    + "Mientras crecían, tuvieron que defender sus tierras de los señoríos vecinos.",
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
                    new EdificioEnemigo(BuildingId.Muralla, 4, 6),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 5),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 6),
                },
                Botin = ResourceInfo.Costo(maiz: 900, madera: 900, obsidiana: 220),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 5),
                    new GrupoDefensor(TroopId.Arquero, 3),
                },
                PlumasPrimeraVez = 25,
            },
            new CampaignLevel
            {
                Nombre = "El cerco",
                Descripcion = "Una guarnición de la Triple Alianza.",
                Historia =
                    "La Triple Alianza fue conquistando los pueblos de alrededor y Tlaxcallan quedó rodeada. "
                    + "Sin comercio con el exterior, por muchos años les faltaron la sal y el algodón.\n\n"
                    + "Mexicas y tlaxcaltecas peleaban además guerras floridas, la xochiyaoyotl, para tomar cautivos y probar a sus guerreros. "
                    + "Tlaxcallan nunca se rindió.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5, 3),
                    new EdificioEnemigo(BuildingId.Telpochcalli, 1, 4, 2),
                    new EdificioEnemigo(BuildingId.Petlacalco, 9, 1),
                    new EdificioEnemigo(BuildingId.Granja, 1, 1),
                    new EdificioEnemigo(BuildingId.Granja, 10, 10),
                    new EdificioEnemigo(BuildingId.Obsidiana, 1, 10),
                    new EdificioEnemigo(BuildingId.Lenadores, 5, 10),
                    new EdificioEnemigo(BuildingId.Torre, 4, 3, 3),
                    new EdificioEnemigo(BuildingId.Torre, 8, 4, 3),
                    new EdificioEnemigo(BuildingId.Torre, 4, 8, 3),
                    new EdificioEnemigo(BuildingId.Torre, 8, 8, 2),
                },
                Botin = ResourceInfo.Costo(maiz: 1100, madera: 1100, obsidiana: 260),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 6, 1),
                    new GrupoDefensor(TroopId.Arquero, 3),
                    new GrupoDefensor(TroopId.Hondero, 2),
                },
                PlumasPrimeraVez = 30,
            },
            new CampaignLevel
            {
                Nombre = "Tlaxcallan no se rinde",
                Descripcion = "Un gran ejército mexica. Muchos defensores.",
                Historia =
                    "A principios del siglo XVI, los mexicas de Motecuhzoma Xocoyotzin enviaron grandes ejércitos contra Tlaxcallan.\n\n"
                    + "Los tlaxcaltecas resistieron desde sus cerros. Esta vez el campamento enemigo está lleno de guerreros: "
                    + "lleva a tu ejército completo.",
                Edificios = new[]
                {
                    new EdificioEnemigo(BuildingId.Tecpan, 5, 5, 4),
                    new EdificioEnemigo(BuildingId.Teocalli, 1, 5, 3),
                    new EdificioEnemigo(BuildingId.Telpochcalli, 9, 5, 3),
                    new EdificioEnemigo(BuildingId.Petlacalco, 1, 1),
                    new EdificioEnemigo(BuildingId.Petlacalco, 9, 9),
                    new EdificioEnemigo(BuildingId.Granja, 1, 10),
                    new EdificioEnemigo(BuildingId.Obsidiana, 10, 1),
                    new EdificioEnemigo(BuildingId.Torre, 4, 2, 4),
                    new EdificioEnemigo(BuildingId.Torre, 7, 2, 4),
                    new EdificioEnemigo(BuildingId.Torre, 4, 9, 4),
                    new EdificioEnemigo(BuildingId.Torre, 7, 9, 4),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 4, 2),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 5, 2),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 6, 2),
                    new EdificioEnemigo(BuildingId.Muralla, 4, 7, 2),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 4, 2),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 5, 2),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 6, 2),
                    new EdificioEnemigo(BuildingId.Muralla, 7, 7, 2),
                },
                Botin = ResourceInfo.Costo(maiz: 1400, madera: 1300, obsidiana: 340),
                Defensores = new[]
                {
                    new GrupoDefensor(TroopId.Macuahuitl, 8, 1),
                    new GrupoDefensor(TroopId.Arquero, 4, 1),
                    new GrupoDefensor(TroopId.Hondero, 4),
                },
                PlumasPrimeraVez = 40,
                Epilogo =
                    "La Triple Alianza nunca pudo conquistar Tlaxcallan. Las cuatro cabeceras siguieron libres "
                    + "hasta 1519, cuando llegaron los españoles. Primero los enfrentaron y después se aliaron con ellos contra Tenochtitlan.\n\n"
                    + "Esa historia continuará.",
            },
        };
    }
}
