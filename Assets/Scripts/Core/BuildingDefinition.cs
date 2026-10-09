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
        Telpochcalli,
        Torre,
        Teocalli, // nuevos al final: el número se guarda en la partida
        Temazcalli,
        Calpulli,
    }

    /// <summary>Pestañas del menú de construcción.</summary>
    public enum CategoriaEdificio
    {
        Suministros,
        Defensas,
        Militar,
        Templo,
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
        public CategoriaEdificio Categoria = CategoriaEdificio.Suministros;
        public int[] MaximoPorTecpan;     // cuántos se pueden tener con el tecpan en nivel 1..5; null = sin límite
        public int NivelMaximo = 5;
        public int[] CostoMejoraBase;     // si es null, se usa Costo (el tecpan no tiene costo de construcción)

        public bool Produce;
        public ResourceType Recurso;
        public float ProduccionPorMinuto;

        public int CapacidadExtra;        // almacenamiento extra por recurso
        public int VidaBase;
        public int CapacidadTropas;       // espacio para tropas por nivel (calpulli)
        public bool Entrena;              // entrena tropas (telpochcalli)
        public int CamasCuracion;         // heridos que cura a la vez, por nivel (temazcalli)

        public bool EsDefensa;            // ataca a las tropas enemigas en batalla
        public float AlcanceDefensa;
        public float DanoDefensaPorSegundo;

        /// <summary>Cuántos se pueden tener con el tecpan en ese nivel (int.MaxValue si no hay límite).</summary>
        public int Maximo(int nivelTecpan)
        {
            if (MaximoPorTecpan == null || MaximoPorTecpan.Length == 0) return int.MaxValue;
            return MaximoPorTecpan[Mathf.Clamp(nivelTecpan, 1, MaximoPorTecpan.Length) - 1];
        }

        public string NombrePara(Pueblo pueblo)
        {
            return Id == BuildingId.Granja && pueblo != null ? pueblo.NombreGranja : Nombre;
        }

        /// <summary>Producción, almacenamiento y vida: +50 % por cada nivel después del 1.</summary>
        public static float Multiplicador(int nivel)
        {
            return 1f + 0.5f * (nivel - 1);
        }

        /// <summary>Costo para subir de nivelActual a nivelActual + 1: se duplica en cada nivel.</summary>
        public int[] CostoMejora(int nivelActual)
        {
            var baseCosto = CostoMejoraBase ?? Costo;
            int factor = 1 << Mathf.Clamp(nivelActual, 0, 20);
            var costo = new int[ResourceInfo.Count];
            for (int i = 0; i < costo.Length; i++) costo[i] = baseCosto[i] * factor;
            return costo;
        }

        /// <summary>Segundos base para llegar a ese nivel: el doble que el anterior.</summary>
        public float SegundosParaNivel(int nivel)
        {
            return SegundosConstruccion * (1 << Mathf.Clamp(nivel - 1, 0, 20));
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
                Descripcion = "Palacio del gobernante. Su nivel limita el de los demás edificios.",
                Tamano = 2,
                Altura = 2f,
                Color = new Color(0.85f, 0.75f, 0.55f),
                Construible = false,
                CostoMejoraBase = ResourceInfo.Costo(maiz: 110, madera: 85, obsidiana: 25),
                SegundosConstruccion = 15f,
                CapacidadExtra = 0,
                VidaBase = 2000,
            },
            new BuildingDefinition
            {
                Id = BuildingId.Granja,
                Nombre = "Granja",
                Descripcion = "Campos de maíz: el alimento de tu pueblo y el pago para entrenar [tropas].",
                Altura = 0.3f,
                Color = new Color(0.45f, 0.70f, 0.30f),
                Costo = ResourceInfo.Costo(madera: 50),
                SegundosConstruccion = 8f,
                Produce = true,
                Recurso = ResourceType.Maiz,
                ProduccionPorMinuto = 30f,
                VidaBase = 300,
                MaximoPorTecpan = new[] { 2, 3, 4, 5, 6 },
            },
            new BuildingDefinition
            {
                Id = BuildingId.Lenadores,
                Nombre = "Leñadores",
                Descripcion = "Cortan madera en los bosques cercanos. Casi todos los edificios la necesitan.",
                Altura = 0.8f,
                Color = new Color(0.50f, 0.35f, 0.20f),
                Costo = ResourceInfo.Costo(maiz: 60),
                SegundosConstruccion = 8f,
                Produce = true,
                Recurso = ResourceType.Madera,
                ProduccionPorMinuto = 36f,
                VidaBase = 300,
                MaximoPorTecpan = new[] { 2, 3, 4, 5, 6 },
            },
            new BuildingDefinition
            {
                Id = BuildingId.Obsidiana,
                Nombre = "Yacimiento de obsidiana",
                Descripcion = "Aquí se extrae el itztli, el vidrio volcánico de las navajas del macuahuitl y las puntas de flecha.",
                Altura = 0.6f,
                Color = new Color(0.15f, 0.15f, 0.20f),
                Costo = ResourceInfo.Costo(maiz: 85, madera: 70),
                SegundosConstruccion = 15f,
                Produce = true,
                Recurso = ResourceType.Obsidiana,
                ProduccionPorMinuto = 15f,
                VidaBase = 400,
                MaximoPorTecpan = new[] { 1, 1, 2, 2, 3 },
            },
            new BuildingDefinition
            {
                Id = BuildingId.Petlacalco,
                Nombre = "Petlacalco",
                Descripcion = "Almacén de los tributos. Cada nivel guarda +1000 de cada recurso.",
                Tamano = 2,
                Altura = 1.2f,
                Color = new Color(0.80f, 0.55f, 0.35f),
                Costo = ResourceInfo.Costo(maiz: 110, madera: 130),
                SegundosConstruccion = 20f,
                CapacidadExtra = 1000,
                VidaBase = 800,
                MaximoPorTecpan = new[] { 1, 1, 2, 2, 3 },
            },
            new BuildingDefinition
            {
                Id = BuildingId.Telpochcalli,
                Nombre = "Telpochcalli",
                Descripcion = "Casa de los jóvenes, donde aprenden el manejo de las armas. Entrena [tropas][glosa].",
                Tamano = 2,
                Altura = 1.1f,
                Color = new Color(0.60f, 0.25f, 0.20f),
                Costo = ResourceInfo.Costo(maiz: 120, madera: 100),
                SegundosConstruccion = 20f,
                Entrena = true,
                Categoria = CategoriaEdificio.Militar,
                VidaBase = 700,
                MaximoPorTecpan = new[] { 1, 1, 1, 1, 1 },
            },
            new BuildingDefinition
            {
                Id = BuildingId.Calpulli,
                Nombre = "Calpulli",
                Descripcion = "Barrio de familias. Los guerreros eran hombres del calpulli que se movilizaban "
                              + "para cada campaña. Da espacio para [tropas].",
                Tamano = 2,
                Altura = 0.8f,
                Color = new Color(0.72f, 0.58f, 0.40f),
                Costo = ResourceInfo.Costo(maiz: 80, madera: 120),
                SegundosConstruccion = 15f,
                CapacidadTropas = 15,
                Categoria = CategoriaEdificio.Militar,
                VidaBase = 600,
                MaximoPorTecpan = new[] { 1, 2, 2, 3, 3 },
            },
            new BuildingDefinition
            {
                Id = BuildingId.Temazcalli,
                Nombre = "Temazcalli",
                Descripcion = "Baño de vapor donde el ticitl cura a [los] [tropas] herid[o]s con plantas medicinales, "
                              + "suturas y férulas.",
                Tamano = 2,
                Altura = 0.9f,
                Color = new Color(0.62f, 0.45f, 0.32f),
                Costo = ResourceInfo.Costo(maiz: 100, madera: 120),
                SegundosConstruccion = 20f,
                CamasCuracion = 5,
                Categoria = CategoriaEdificio.Militar,
                VidaBase = 600,
                MaximoPorTecpan = new[] { 1, 1, 1, 2, 2 },
            },
            new BuildingDefinition
            {
                Id = BuildingId.Teocalli,
                Nombre = "Teocalli",
                Descripcion = "Casa de los dioses. Aquí se ofrendan los mamaltin para obtener el favor de los dioses de tu pueblo.",
                Tamano = 2,
                Altura = 2.4f,
                Color = new Color(0.85f, 0.80f, 0.70f),
                Costo = ResourceInfo.Costo(maiz: 150, madera: 150, obsidiana: 50),
                SegundosConstruccion = 30f,
                NivelMaximo = 1,
                VidaBase = 1500,
                Categoria = CategoriaEdificio.Templo,
                MaximoPorTecpan = new[] { 1, 1, 1, 1, 1 },
            },
            new BuildingDefinition
            {
                Id = BuildingId.Torre,
                Nombre = "Torre de vigía",
                Descripcion = "Vigila los alrededores y dispara a [los] [tropas] enemig[o]s que se acercan. "
                              + "Si van a atacar, avisa antes y marca por dónde vienen.",
                Altura = 1.8f,
                Color = new Color(0.55f, 0.50f, 0.45f),
                Costo = ResourceInfo.Costo(madera: 120, obsidiana: 60),
                SegundosConstruccion = 25f,
                Categoria = CategoriaEdificio.Defensas,
                MaximoPorTecpan = new[] { 1, 2, 3, 4, 5 },
                VidaBase = 400,
                EsDefensa = true,
                AlcanceDefensa = 3.5f,
                DanoDefensaPorSegundo = 10f,
            },
            new BuildingDefinition
            {
                Id = BuildingId.Muralla,
                Nombre = "Muralla",
                Descripcion = "Frena a [los] [tropas] enemig[o]s. Los tlaxcaltecas, famosos por sus murallas, las hacen más resistentes.",
                Altura = 0.9f,
                Color = new Color(0.70f, 0.68f, 0.62f),
                Costo = ResourceInfo.Costo(madera: 10, obsidiana: 5),
                SegundosConstruccion = 2f,
                VidaBase = 500,
                Categoria = CategoriaEdificio.Defensas,
                MaximoPorTecpan = new[] { 20, 40, 60, 80, 100 },
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
