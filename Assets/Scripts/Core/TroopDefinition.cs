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
        public const int Count = 3;
        public const int Joven = 0;
        public const int Experimentado = 1;
        public const int Capturador = 2;
        public const float BonoPorRango = 0.15f; // +15 % de vida y ataque por rango

        /// <summary>
        /// Probabilidad de hacer una captura al derribar un edificio. Los mexicas, que necesitan
        /// cautivos para Huitzilopochtli, buscan capturar más que los demás.
        /// </summary>
        public static float ProbabilidadCaptura(Pueblo pueblo)
        {
            return pueblo != null && pueblo.Id == PuebloId.Mexicas ? 0.26f : 0.2f;
        }

        private static readonly string[] NombresMexicas = { "Joven guerrero", "Guerrero experimentado", "Tlamani" };
        // De acolhuas y tlaxcaltecas no hay una lista de rangos tan detallada; se usan nombres generales.
        private static readonly string[] NombresGenerales = { "Joven guerrero", "Guerrero experimentado", "Veterano" };

        public static string Nombre(Pueblo pueblo, int rango)
        {
            var nombres = pueblo != null && pueblo.Id == PuebloId.Mexicas ? NombresMexicas : NombresGenerales;
            return nombres[UnityEngine.Mathf.Clamp(rango, 0, Count - 1)];
        }

        public static float Multiplicador(int rango) => 1f + BonoPorRango * rango;

        /// <summary>Rango con el que regresa una tropa que sobrevivió a la batalla.</summary>
        public static int AlRegresar(int rango, int capturas)
        {
            if (capturas > 0) return Capturador;
            return UnityEngine.Mathf.Max(rango, Experimentado);
        }
    }
}
