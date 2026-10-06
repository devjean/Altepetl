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

    /// <summary>Una aldea enemiga de la campaña contra la IA.</summary>
    public sealed class CampaignLevel
    {
        public const int TamanoMapa = 12;

        public string Nombre;
        public string Descripcion;
        public EdificioEnemigo[] Edificios;
        public int[] Botin;          // botín máximo, se gana en proporción a lo destruido
        public int PlumasPrimeraVez; // solo la primera victoria

        public static readonly CampaignLevel[] Todos =
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
}
