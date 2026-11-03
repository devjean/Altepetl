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

        [MenuItem("Altepetl/Volver a preguntar los términos")]
        private static void OlvidarTerminos()
        {
            Terminos.Olvidar();
            Debug.Log("Al empezar partida nueva se volverá a preguntar náhuatl o español.");
        }

        [MenuItem("Altepetl/Provocar un ataque a la aldea")]
        private static void ProvocarAtaque()
        {
            var manager = Object.FindAnyObjectByType<GameManager>();
            if (!EditorApplication.isPlaying || manager == null || manager.Asalto == null)
            {
                Debug.LogWarning("Dale Play y entra a tu aldea para provocar un ataque.");
                return;
            }
            manager.Asalto.ProvocarAhora();
        }

        [MenuItem("Altepetl/Simular un ataque sin conexión")]
        private static void SimularAtaqueAusente()
        {
            var manager = Object.FindAnyObjectByType<GameManager>();
            if (!EditorApplication.isPlaying || manager == null || manager.Asalto == null)
            {
                Debug.LogWarning("Dale Play y entra a tu aldea para simular un ataque sin conexión.");
                return;
            }
            // Como si hubieras vuelto tras horas fuera: aparece el panel para verlo o saltarlo.
            if (!manager.Asalto.PrepararAusente(0f, forzar: true))
            {
                Debug.LogWarning("Ya hay un ataque avisado o en curso; espera a que termine.");
            }
        }

        [MenuItem("Altepetl/Abrir carpeta de guardado")]
        private static void AbrirCarpeta()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}
