using UnityEditor;
using UnityEngine;

namespace Altepetl.Herramientas
{
    /// <summary>Atajos del menú "Altepetl" en el editor para probar el guardado.</summary>
    public static class AltepetlMenu
    {
        [MenuItem("Altepetl/Borrar partida guardada")]
        private static void BorrarPartida()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Detén el juego antes de borrar la partida; al salir de Play se vuelve a guardar.");
                return;
            }
            SaveSystem.Borrar();
            Debug.Log($"Partida borrada: {SaveSystem.Ruta}");
        }

        [MenuItem("Altepetl/Abrir carpeta de guardado")]
        private static void AbrirCarpeta()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}
