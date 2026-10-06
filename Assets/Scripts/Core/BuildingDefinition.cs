using UnityEngine;

namespace Altepetl
{
    public enum BuildingId
    {
        Tecpan,
        Granja,
        Lenadores,
        Obsidiana,
        Petlacalco,
        Muralla,
    }

    /// <summary>Datos fijos de un tipo de edificio. Más adelante pasarán a ScriptableObjects.</summary>
    public sealed class BuildingDefinition
    {
        public BuildingId Id;
        public string Nombre;
        public string Descripcion;
        public int Tamano = 1;            // lado en casillas
        public float Altura = 1f;
        public Color Color = Color.white;
        public int[] Costo = ResourceInfo.Costo();
        public float SegundosConstruccion = 5f;
        public bool Construible = true;   // el tecpan se coloca solo al inicio

        public bool Produce;
        public ResourceType Recurso;
        public float ProduccionPorMinuto;

        public int CapacidadExtra;        // almacenamiento extra por recurso
        public int VidaBase;              // solo relevante para defensas, de momento

        public string NombrePara(Pueblo pueblo)
        {
            return Id == BuildingId.Granja && pueblo != null ? pueblo.NombreGranja : Nombre;
        }
    }

    public static class BuildingCatalog
    {
        public static readonly BuildingDefinition[] Todos =
        {
            new BuildingDefinition
            {
                Id = BuildingId.Tecpan,
                Nombre = "Tecpan",
                Descripcion = "Palacio del gobernante, corazón del altepetl.",
                Tamano = 2,
                Altura = 2f,
                Color = new Color(0.85f, 0.75f, 0.55f),
                Construible = false,
                CapacidadExtra = 0,
                VidaBase = 2000,
            },
            new BuildingDefinition
            {
                Id = BuildingId.Granja,
                Nombre = "Granja",
                Descripcion = "Produce maíz.",
                Altura = 0.3f,
                Color = new Color(0.45f, 0.70f, 0.30f),
                Costo = ResourceInfo.Costo(madera: 60),
                SegundosConstruccion = 8f,
                Produce = true,
                Recurso = ResourceType.Maiz,
                ProduccionPorMinuto = 30f,
                VidaBase = 300,
            },
            new BuildingDefinition
            {
                Id = BuildingId.Lenadores,
                Nombre = "Leñadores",
                Descripcion = "Produce madera.",
                Altura = 0.8f,
                Color = new Color(0.50f, 0.35f, 0.20f),
                Costo = ResourceInfo.Costo(maiz: 60),
                SegundosConstruccion = 8f,
                Produce = true,
                Recurso = ResourceType.Madera,
                ProduccionPorMinuto = 30f,
                VidaBase = 300,
            },
            new BuildingDefinition
            {
                Id = BuildingId.Obsidiana,
                Nombre = "Yacimiento de obsidiana",
                Descripcion = "Produce obsidiana.",
                Altura = 0.6f,
                Color = new Color(0.15f, 0.15f, 0.20f),
                Costo = ResourceInfo.Costo(maiz: 80, madera: 80),
                SegundosConstruccion = 15f,
                Produce = true,
                Recurso = ResourceType.Obsidiana,
                ProduccionPorMinuto = 15f,
                VidaBase = 400,
            },
            new BuildingDefinition
            {
                Id = BuildingId.Petlacalco,
                Nombre = "Petlacalco",
                Descripcion = "Almacén: +1000 de capacidad para cada recurso.",
                Tamano = 2,
                Altura = 1.2f,
                Color = new Color(0.80f, 0.55f, 0.35f),
                Costo = ResourceInfo.Costo(maiz: 100, madera: 150),
                SegundosConstruccion = 20f,
                CapacidadExtra = 1000,
                VidaBase = 800,
            },
            new BuildingDefinition
            {
                Id = BuildingId.Muralla,
                Nombre = "Muralla",
                Descripcion = "Defensa. Su vida depende del pueblo.",
                Altura = 0.9f,
                Color = new Color(0.70f, 0.68f, 0.62f),
                Costo = ResourceInfo.Costo(madera: 10, obsidiana: 5),
                SegundosConstruccion = 2f,
                VidaBase = 500,
            },
        };

        public static BuildingDefinition Get(BuildingId id)
        {
            foreach (var def in Todos)
            {
                if (def.Id == id) return def;
            }
            return null;
        }
    }
}
