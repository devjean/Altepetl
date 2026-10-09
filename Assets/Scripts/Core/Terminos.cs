using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Ajuste de vocabulario: con términos nahuas (por defecto) o en español sencillo.
    /// Los edificios, dioses y rangos siempre llevan su nombre náhuatl; esto solo cambia detalles
    /// como cómo se les llama a las tropas. Se guarda en el dispositivo, no en la partida.
    /// </summary>
    public static class Terminos
    {
        private const string Clave = "terminos_nahuatl";
        private static int _nahuatl = -1;

        public static bool Nahuatl
        {
            get
            {
                if (_nahuatl < 0) _nahuatl = PlayerPrefs.GetInt(Clave, 1);
                return _nahuatl == 1;
            }
            set
            {
                _nahuatl = value ? 1 : 0;
                PlayerPrefs.SetInt(Clave, _nahuatl);
                PlayerPrefs.Save();
            }
        }

        public static string Tropa => Nahuatl ? "yaoquizqui" : "tropa";
        public static string Tropas => Nahuatl ? "yaoquizqueh" : "tropas";
        public static string TropasMayuscula => Nahuatl ? "Yaoquizqueh" : "Tropas";
        public static string TropasCuenta(int n) => n == 1 ? $"1 {Tropa}" : $"{n} {Tropas}";

        // Calquetza: "levantar casa, edificar" (calli + quetza). Molina lo da como edificar.
        public static string BotonConstruir => Nahuatl ? "Calquetza\n(construir)" : "Construir";
        public static string TituloConstruir => Nahuatl ? "Calquetza" : "Construir";

        // Yaoyotl: "guerra" (sustantivo). Altepetl: el pueblo o señorío, en vez de "aldea".
        public static string Atacar => Nahuatl ? "Yaoyotl (atacar)" : "Atacar";
        public static string AtacarCorto => Nahuatl ? "Yaoyotl" : "Atacar";
        public static string AlAldea => Nahuatl ? "al altepetl" : "a la aldea";

        // Concordancia: yaoquizqui es masculino; tropa, femenino.
        public static string Un => Nahuatl ? "Un" : "Una";
        public static string Los => Nahuatl ? "los" : "las";
        public static string O => Nahuatl ? "o" : "a";

        /// <summary>Cambia las marcas [tropa], [tropas], [Tropas], [los], [o] y [glosa] de un texto fijo.</summary>
        public static string Aplicar(string texto)
        {
            if (string.IsNullOrEmpty(texto) || texto.IndexOf('[') < 0) return texto;
            return texto.Replace("[tropas]", Tropas).Replace("[Tropas]", TropasMayuscula).Replace("[tropa]", Tropa)
                .Replace("[los]", Los).Replace("[o]", O)
                .Replace("[glosa]", Nahuatl ? ", «los que salen a la guerra»" : "");
        }
    }
}
