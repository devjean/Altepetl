namespace Altepetl
{
    public enum ResourceType
    {
        Maiz = 0,
        Madera = 1,
        Obsidiana = 2,
        Plumas = 3, // Plumas de quetzal: moneda premium
        Cautivos = 4, // Mamaltin: cautivos capturados en batalla
    }

    public static class ResourceInfo
    {
        public const int Count = 5;

        public static string Nombre(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Maiz: return "Maíz";
                case ResourceType.Madera: return "Madera";
                case ResourceType.Obsidiana: return "Obsidiana";
                case ResourceType.Plumas: return "Plumas de quetzal";
                case ResourceType.Cautivos: return "Mamaltin";
                default: return type.ToString();
            }
        }

        /// <summary>Crea un arreglo de costos indexado por ResourceType.</summary>
        public static int[] Costo(int maiz = 0, int madera = 0, int obsidiana = 0, int plumas = 0, int cautivos = 0)
        {
            return new[] { maiz, madera, obsidiana, plumas, cautivos };
        }
    }
}
