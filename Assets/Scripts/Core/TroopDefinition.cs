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
        public string Nombre;
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
                Nombre = "Guerrero",
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
                Nombre = "Arquero",
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
                Nombre = "Hondero",
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
}
