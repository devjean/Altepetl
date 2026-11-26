using System;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Altepetl
{
    /// <summary>Conserva legibles las fachadas pequeñas al variar el encuadre de las sombras.</summary>
    internal sealed class ImportarMaterialesCasas : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        // URP traduce primero los materiales FBX a Lit (orden -980).
        public override int GetPostprocessOrder() => -900;

        private void OnPreprocessMaterialDescription(MaterialDescription descripcion, Material material, AnimationClip[] clips)
        {
            if (!assetPath.StartsWith("Assets/Resources/ModelosCasas/", StringComparison.Ordinal)
                || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return;
            string modelo = Path.GetFileNameWithoutExtension(assetPath);
            if (!modelo.StartsWith("Casa_", StringComparison.Ordinal)
                && !modelo.StartsWith("Modelo_Mexica_", StringComparison.Ordinal))
                return;

            switch (material.name)
            {
                case "Madera cálida":
                case "Bajareque ocre":
                case "Cal":
                case "Rojo":
                case "Sombra interior":
                case "Cal crema":
                case "Zócalo rojo":
                case "Adobe ocre":
                case "Terracota roja":
                case "Interior":
                    // En piezas tan pequeñas la sombra móvil produce manchas que saltan con el zoom.
                    // Lit conserva su iluminación y el pase ShadowCaster sigue proyectando al terreno.
                    material.SetFloat("_ReceiveShadows", 0f);
                    material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                    break;
            }
        }
    }
}
