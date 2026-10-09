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
                // Con términos nahuas: tlaolli (maíz desgranado), cuahuitl (árbol, madera),
                // itztli (obsidiana), quetzalli (pluma preciosa).
                case ResourceType.Maiz: return Terminos.Nahuatl ? "Tlaolli" : "Maíz";
                case ResourceType.Madera: return Terminos.Nahuatl ? "Cuahuitl" : "Madera";
                case ResourceType.Obsidiana: return Terminos.Nahuatl ? "Itztli" : "Obsidiana";
                case ResourceType.Plumas: return Terminos.Nahuatl ? "Quetzalli" : "Plumas de quetzal";
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
