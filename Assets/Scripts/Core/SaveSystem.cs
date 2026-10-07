using System;
using System.IO;
using UnityEngine;

namespace Altepetl
{
    /// <summary>Guarda y carga la partida como JSON en la carpeta de datos persistentes del dispositivo.</summary>
    public static class SaveSystem
    {
        private const string NombreArchivo = "aldea.json";

        public static string Ruta => Path.Combine(Application.persistentDataPath, NombreArchivo);

        public static bool Existe => File.Exists(Ruta);

        public static void Guardar(SaveData datos)
        {
            datos.version = SaveData.VersionActual;
            datos.guardadoUtcTicks = DateTime.UtcNow.Ticks;
            string json = JsonUtility.ToJson(datos, prettyPrint: true);

            // Se escribe a un archivo temporal y luego se reemplaza, para no corromper
            // la partida si el juego se cierra a mitad de la escritura.
            string temporal = Ruta + ".tmp";
            File.WriteAllText(temporal, json);
            if (File.Exists(Ruta)) File.Delete(Ruta);
            File.Move(temporal, Ruta);
        }

        public static SaveData Cargar()
        {
            if (!Existe) return null;
            try
            {
                var datos = JsonUtility.FromJson<SaveData>(File.ReadAllText(Ruta));
                if (datos == null || datos.version < 1 || datos.version > SaveData.VersionActual) return null;
                Migrar(datos);
                return datos;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"No se pudo leer la partida guardada: {e.Message}");
                return null;
            }
        }

        /// <summary>Actualiza partidas de versiones anteriores al formato actual.</summary>
        private static void Migrar(SaveData datos)
        {
            if (datos.version < 2)
            {
                // La versión 1 no tenía niveles: lo terminado era nivel 1 y lo que estaba en obra, nivel 0.
                foreach (var edificio in datos.edificios)
                {
                    edificio.nivel = edificio.segundosRestantes > 0f ? 0 : 1;
                }
            }
            // La versión 3 solo añadió campos (ejército y campaña) que empiezan vacíos.
            // La versión 4 añadió los rangos (Army.Importar pasa las tropas viejas a jóvenes guerreros)
            // y las ofrendas, que empiezan sin ofrenda activa y con el favor a la mitad.
            if (datos.version < 4)
            {
                datos.deidadActiva = 0;
                datos.ofrendaRestante = 0f;
                datos.favorHuitzilopochtli = 50f;
            }
            datos.version = SaveData.VersionActual;
        }

        public static void Borrar()
        {
            if (Existe) File.Delete(Ruta);
        }

        /// <summary>Segundos reales desde el guardado. Si el reloj del dispositivo retrocedió, devuelve 0.</summary>
        public static float SegundosDesde(SaveData datos)
        {
            long diferencia = DateTime.UtcNow.Ticks - datos.guardadoUtcTicks;
            return diferencia > 0 ? (float)TimeSpan.FromTicks(diferencia).TotalSeconds : 0f;
        }
    }
}
