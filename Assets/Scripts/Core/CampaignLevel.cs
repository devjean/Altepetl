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
                    "Los mexicas contaban que venían de Aztlán, un lugar rodeado de agua. No sabemos si existió como lugar real o "
                    + "si es sobre todo un origen sagrado.\n\n"
                    + "La Tira de la peregrinación empieza ahí: en el año 1 Pedernal cruzan el agua en una canoa y llegan a "
                    + "Colhuacan, donde Huitzilopochtli, en su bulto sagrado, les habla. Los teomamaque, los cargadores del dios, lo "
                    + "llevarían a cuestas durante todo el camino.\n\n"
                    + "Los pueblos del camino que aquí se enfrentan no están en la Tira: son una recreación de un viaje largo por "
                    + "tierras ajenas.",
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
                    "La peregrinación no fue un solo viaje: duró generaciones. La Tira muestra parada tras parada, con los años que "
                    + "pasaron en cada una; sembraban, levantaban un templo y partían otra vez cuando el dios lo ordenaba.\n\n"
                    + "En Coatepec encendieron su primer fuego nuevo, la ceremonia que cerraba un ciclo de 52 años. Otras "
                    + "tradiciones, recogidas por Durán y en la Crónica Mexicáyotl, cuentan que en el camino quedó atrás "
                    + "Malinalxóchitl, hermana de Huitzilopochtli, con su gente.\n\n"
                    + "La ciudad que aquí se ataca es una recreación: la Tira no registra una batalla en este tramo.",
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
                    "Ya en la cuenca de México, los mexicas se establecieron en Chapultepec. Eran recién llegados, sin tierra "
                    + "propia, y los pueblos del lago no los querían ahí.\n\n"
                    + "Las fuentes no cuentan igual la derrota. La Tira muestra a los colhuas llevándose prisioneros a Huitzilíhuitl, "
                    + "el jefe mexica, y a su hija; las crónicas posteriores hablan de una alianza de Culhuacan, Azcapotzalco, "
                    + "Xochimilco y Xaltocan, y añaden la historia de Cópil, el hijo de Malinalxóchitl, cuyo corazón arrojado al lago "
                    + "daría origen al nopal de la señal.\n\n"
                    + "El juego sigue la versión de la Tira: los mexicas de Chapultepec son derrotados y dispersados.\n\n"
                    + "Esta batalla es muy difícil: pelea lo mejor que puedas, que la historia sigue aunque pierdas.",
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
                    "Tras la derrota, Coxcoxtli, señor de Culhuacan, dejó vivir a los mexicas en Tizaapan, una tierra pedregosa. "
                    + "Sobrevivieron y, como vasallos de Culhuacan, fueron llamados a su guerra contra Xochimilco.\n\n"
                    + "La Tira termina en esa guerra: las últimas láminas muestran a los mexicas peleando por Culhuacan y la tira "
                    + "queda inconclusa. Las crónicas cuentan que volvieron con muchos cautivos, prueba de que peleaban por otro "
                    + "señor sin tierra propia.\n\n"
                    + "La mitad del botín es para Culhuacan.",
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
                    "La Tira se corta antes del final de la historia. Las crónicas cuentan lo que sigue: en un islote del lago los "
                    + "mexicas vieron la señal prometida, un águila sobre un nopal, y ahí fundaron México-Tenochtitlan, "
                    + "tradicionalmente en el año 1325.\n\n"
                    + "La ciudad nació débil. Durante casi un siglo los mexicas fueron tributarios de los tepanecas de Azcapotzalco, "
                    + "el poder más grande del valle, y pelearon sus guerras como vasallos: así crecieron, hasta que en 1428 "
                    + "cambiaron de bando.\n\n"
                    + "La ciudad que aquí se ataca no es una batalla concreta: representa esas guerras al servicio de Azcapotzalco. "
                    + "La mitad del botín es para los tepanecas.",
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
                    "Después de la caída de Tula, grupos del norte entraron en la cuenca de México. El Códice Xólotl cuenta la "
                    + "llegada de Xólotl, un gran jefe chichimeca, con su hijo Nopaltzin: pasaron junto a las ruinas de Tula "
                    + "cubiertas de hierba, recorrieron el valle y tomaron posesión de la tierra con una ceremonia en el cerro "
                    + "Xocotl, disparando flechas a los cuatro rumbos.\n\n"
                    + "Los chichimecas cazaban con arco y flecha y vivían en cuevas. En las orillas de los lagos vivían pueblos que "
                    + "sembraban maíz y guardaban la herencia tolteca. Según el códice, al principio no hubo guerra: cada quien "
                    + "respetó las tierras del otro.\n\n"
                    + "La aldea que aquí se ataca es una recreación para el juego.",
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
                    "Xólotl hizo de Tenayuca su primera capital y desde ahí repartió tierras y señoríos entre sus jefes y entre los "
                    + "grupos que fueron llegando.\n\n"
                    + "Según el Códice Xólotl, Xólotl exigió a Nauhyotl, señor tolteca de Culhuacan, que lo reconociera como su "
                    + "señor. Nauhyotl se negó: los reyes toltecas no reconocían más superior que sus dioses. Xólotl envió a su hijo "
                    + "Nopaltzin, y la batalla se dio a orillas de la laguna, en el año 13 Casa. Fue muy sangrienta. Después, Xólotl "
                    + "fue a Culhuacan y puso como señor a Achitómetl.",
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
                    "Poco a poco los chichimecas adoptaron la agricultura y las costumbres de los pueblos del lago. Según Alva "
                    + "Ixtlilxóchitl, Tlotzin aprendió a sembrar del chalca Tecpoyo Achcauhtli; algunos chichimecas aceptaron, y "
                    + "otros, apegados a la vida de sus antepasados, se fueron a las sierras.\n\n"
                    + "El Códice Xólotl cuenta otra disputa. Yacanex, jefe de los chichimecas cazadores de Tepetlaóztoc, pidió a "
                    + "Achitómetl la mano de su hija Atotoztli, que ya estaba prometida a Huetzin, señor de Coatlinchan. Yacanex se "
                    + "alzó contra Huetzin, y Huetzin y Quinatzin lo derrotaron en una batalla sangrienta. Muerto Tlotzin, Yacanex "
                    + "volvió a rebelarse con apoyo de otros señoríos.\n\n"
                    + "Sus defensores son en su mayoría arqueros.",
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
                Descripcion = "Los tepanecas atacan Texcoco. Aquí se perdió.",
                Historia =
                    "Con el tiempo el centro del poder acolhua pasó de Tenayuca a Texcoco. Pero Tezozómoc, señor de los tepanecas "
                    + "de Azcapotzalco, se volvió el poder más grande del valle.\n\n"
                    + "Techotlalatzin murió hacia 1409 y lo sucedió su hijo Ixtlilxóchitl. Tezozómoc le hizo la guerra. Según el "
                    + "Códice Xólotl, Ixtlilxóchitl cayó en una trampa de Tezozómoc y fue asesinado; su hijo Nezahualcóyotl escapó "
                    + "escondido en un árbol. Las fuentes ponen esa muerte hacia 1418.\n\n"
                    + "Tezozómoc se repartió el Acolhuacan con sus aliados, y Texcoco quedó bajo señores impuestos por Azcapotzalco.\n\n"
                    + "Esta batalla es muy difícil: pelea lo mejor que puedas, que la historia sigue aunque pierdas.",
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
                    "Nezahualcóyotl pasó años huyendo. Cuando murió Tezozómoc, su hijo Maxtla tomó el poder en Azcapotzalco y "
                    + "siguió persiguiéndolo. El Códice Xólotl termina aquí, en 1429, con Nezahualcóyotl tejiendo alianzas: Chalco, "
                    + "Huexotla, Coatlinchan, Huexotzinco, Tlaxcallan y los mexicas.\n\n"
                    + "Lo que sigue lo cuentan Alva Ixtlilxóchitl y las crónicas mexicas, y no todas en el mismo orden: con guerreros "
                    + "de Huexotzinco y Tlaxcallan recuperó Texcoco; junto a los mexicas de Itzcóatl derrotó a Azcapotzalco, en 1428; "
                    + "y de esa victoria nació la Triple Alianza de Tenochtitlan, Texcoco y Tlacopan, que gobernaría el valle durante "
                    + "casi un siglo.\n\n"
                    + "Esta batalla representa la caída de Azcapotzalco.",
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
                    "Según la tradición que recogió Diego Muñoz Camargo, los teochichimecas que serían los tlaxcaltecas salieron de "
                    + "Chicomóztoc, las Siete Cuevas, guiados por su dios Camaxtli, y se asentaron en los llanos de Poyauhtlan, en la "
                    + "orilla oriental del lago, en tierras acolhuas.\n\n"
                    + "Ahí, en el año 1 Conejo, se dio una de las dos batallas más recordadas de su historia: la de Poyauhtlan, \"a "
                    + "orillas de la laguna, desde Coatlinchan hasta Chimalhuacan\". Después de esa guerra, siguiendo a Camaxtli, "
                    + "dejaron el valle y partieron hacia el oriente.\n\n"
                    + "La tradición dice que pelearon contra los pueblos del lago que no los querían como vecinos.",
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
                    "Partieron al oriente, al valle de Puebla-Tlaxcala. Según Muñoz Camargo llegaron a la sierra de Tepeticpac en "
                    + "el año 5 Pedernal; al lugar lo llamaron Texcalticpac, \"sobre los riscos\", y de ahí vendría el nombre "
                    + "Tlaxcallan.\n\n"
                    + "La región no estaba vacía: los olmeca-xicalancas la habitaban desde antes. Muñoz Camargo cuenta que los "
                    + "teochichimecas los expulsaron y ocuparon sus tierras, y que Culhua Quanez, primer señor de los teochichimecas, "
                    + "fundó en Tepeticpac la primera cabecera.",
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
                Nombre = "Texcalticpac",
                Descripcion = "Una segunda batalla en tierras nuevas.",
                Historia =
                    "Desde Tepeticpac crecieron otras cabeceras: Ocotelulco, Tizatlan y Quiahuiztlan. Muñoz Camargo cuenta que las "
                    + "tierras se repartieron en paz; cada cabecera tenía su propio señor, y entre las cuatro gobernaron Tlaxcallan. "
                    + "A diferencia de Tenochtitlan, aquí no mandaba un solo gobernante.\n\n"
                    + "No todo fue paz. Muñoz Camargo recuerda, junto a Poyauhtlan, una segunda batalla \"famosa y cruel\": la de "
                    + "Texcalticpac, en el año 9 Pedernal, en el corazón del nuevo territorio. No dice contra quién se peleó; las "
                    + "tierras se disputaban con los vecinos, entre ellos Cholollan.",
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
                    "Con los años, los mexicas y sus aliados fueron sometiendo a los pueblos de alrededor y Tlaxcallan quedó "
                    + "rodeada. Muñoz Camargo cuenta que, cuando los tlaxcaltecas preguntaron por qué se les hacía la guerra, el señor "
                    + "de México exigió tributo; ellos contestaron que nunca lo habían pagado y que preferían morir.\n\n"
                    + "No fue una batalla sino un cerco de \"más de sesenta años\": les faltaron el algodón, la sal, el cacao, el oro "
                    + "y las plumas. Hubo escaramuzas constantes en las fronteras, que servían para ejercitar a los guerreros. "
                    + "Pueblos enteros, otomíes, xaltocamecas y chalcas, se refugiaron en Tlaxcallan y recibieron tierras a cambio de "
                    + "defender la frontera.\n\n"
                    + "La guarnición que aquí se ataca es una recreación de esas escaramuzas de frontera.",
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
                    "A principios del siglo XVI Tlaxcallan estaba en guerra con Huexotzinco, su vecino. Los huexotzincas, agotados, "
                    + "pidieron ayuda a Motecuhzoma Xocoyotzin, y los mexicas entraron en la guerra.\n\n"
                    + "Las crónicas mexicas cuentan combates de muchos días, con refuerzos de Texcoco y Tlacopan, y la captura del "
                    + "guerrero tlaxcalteca Tlahuicole, que prefirió morir en la piedra de sacrificio antes que servir a México. "
                    + "Muñoz Camargo lo recuerda como el más valiente de los suyos.\n\n"
                    + "Tlaxcallan no fue conquistada. Pocos años después, en 1519, llegarían los españoles, y los tlaxcaltecas "
                    + "decidirían su propio destino.\n\n"
                    + "El campamento enemigo está lleno de guerreros: lleva a tu ejército completo.",
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
