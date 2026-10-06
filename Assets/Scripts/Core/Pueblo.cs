namespace Altepetl
{
    public enum PuebloId
    {
        Mexicas,
        Tlaxcaltecas,
        Acolhuas,
    }

    /// <summary>
    /// Pueblo elegido al crear la aldea. Cada uno modifica algunas estadísticas.
    /// Los multiplicadores de ataque y entrenamiento se aplicarán cuando exista el combate.
    /// </summary>
    public sealed class Pueblo
    {
        public PuebloId Id;
        public string Nombre;
        public string Ciudad;
        public string Estilo;
        public string Descripcion;
        public string NombreGranja; // Chinampa o Cultivos

        public float MultiplicadorMaiz = 1f;
        public float MultiplicadorTiempoConstruccion = 1f;
        public float MultiplicadorVidaMurallas = 1f;
        public float MultiplicadorAtaqueTropas = 1f;
        public float MultiplicadorVidaTropas = 1f;
        public float MultiplicadorTiempoEntrenamiento = 1f;

        public static readonly Pueblo[] Todos =
        {
            new Pueblo
            {
                Id = PuebloId.Mexicas,
                Nombre = "Mexicas",
                Ciudad = "Tenochtitlan",
                Estilo = "Ofensivo",
                Descripcion = "+15 % ataque de tropas, +10 % maíz de las chinampas.\nMurallas y torres −10 % vida.",
                NombreGranja = "Chinampa",
                MultiplicadorMaiz = 1.10f,
                MultiplicadorAtaqueTropas = 1.15f,
                MultiplicadorVidaMurallas = 0.90f,
            },
            new Pueblo
            {
                Id = PuebloId.Tlaxcaltecas,
                Nombre = "Tlaxcaltecas",
                Ciudad = "Tlaxcala",
                Estilo = "Defensivo",
                Descripcion = "Murallas y torres +20 % vida.\nLas tropas se entrenan 10 % más lento.",
                NombreGranja = "Cultivos",
                MultiplicadorVidaMurallas = 1.20f,
                MultiplicadorTiempoEntrenamiento = 1.10f,
            },
            new Pueblo
            {
                Id = PuebloId.Acolhuas,
                Nombre = "Acolhuas",
                Ciudad = "Texcoco",
                Estilo = "Economía y conocimiento",
                Descripcion = "Construcciones y mejoras 15 % más rápidas.\nTropas −10 % vida.",
                NombreGranja = "Chinampa",
                MultiplicadorTiempoConstruccion = 0.85f,
                MultiplicadorVidaTropas = 0.90f,
            },
        };

        public static Pueblo Get(PuebloId id)
        {
            foreach (var pueblo in Todos)
            {
                if (pueblo.Id == id) return pueblo;
            }
            return Todos[0];
        }
    }
}
