using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Figuras de relleno (cápsulas, cubos, cilindros) que comparten un solo material con GPU instancing:
    /// el shader FiguraInstanciada admite color por instancia sin copiar el material. Al integrar los
    /// modelos definitivos hay que comprobar sus mallas, materiales y pases en Frame Debugger.
    /// </summary>
    public static class Figuras
    {
        private const string RutaMaterial = "Materials/Figura";
        private static readonly int IdColorFigura = Shader.PropertyToID("_ColorFigura");
        private static Material _material;
        private static MaterialPropertyBlock _bloque;

        /// <summary>El material compartido; sale de Resources para que llegue entero al build del celular.</summary>
        public static Material Material
        {
            get
            {
                if (_material != null) return _material;
                _material = Resources.Load<Material>(RutaMaterial);
                if (_material == null)
                {
                    var shader = Shader.Find("Altepetl/FiguraInstanciada");
                    _material = new Material(shader) { name = "Figura (sin recurso)" };
                    Debug.LogWarning($"Falta Resources/{RutaMaterial}.mat; se usa un material creado al vuelo.");
                }
                _material.enableInstancing = true;
                return _material;
            }
        }

        /// <summary>
        /// Sustituto de GameObject.CreatePrimitive para todo el juego: la misma malla de Unity, sin collider
        /// (no se usan: las figuras se tocan por sus límites) y con el material compartido de URP. CreatePrimitive
        /// trae el material estándar de Unity, que no entra en las builds con URP (todo sale rosa en el celular),
        /// y además intenta añadir un collider, que el recorte de motor quita de la build (errores en consola).
        /// El material queda compartido: para un color propio, usar Colorear o pedir render.material.
        /// </summary>
        public static GameObject Primitiva(PrimitiveType tipo, string nombre = null)
        {
            var figura = new GameObject(nombre ?? tipo.ToString());
            figura.AddComponent<MeshFilter>().sharedMesh = Malla(tipo);
            figura.AddComponent<MeshRenderer>().sharedMaterial = Material;
            return figura;
        }

        private static readonly Dictionary<PrimitiveType, Mesh> _mallas = new Dictionary<PrimitiveType, Mesh>();

        private static Mesh Malla(PrimitiveType tipo)
        {
            if (_mallas.TryGetValue(tipo, out var malla) && malla != null) return malla;
            // Las mallas que usa CreatePrimitive; vienen con el motor en cualquier plataforma.
            string[] nombres;
            switch (tipo)
            {
                case PrimitiveType.Sphere: nombres = new[] { "New-Sphere.fbx", "Sphere.fbx" }; break;
                case PrimitiveType.Capsule: nombres = new[] { "New-Capsule.fbx", "Capsule.fbx" }; break;
                case PrimitiveType.Cylinder: nombres = new[] { "New-Cylinder.fbx", "Cylinder.fbx" }; break;
                case PrimitiveType.Plane: nombres = new[] { "New-Plane.fbx", "Plane.fbx" }; break;
                case PrimitiveType.Quad: nombres = new[] { "Quad.fbx" }; break;
                default: nombres = new[] { "Cube.fbx" }; break;
            }
            foreach (var nombre in nombres)
            {
                malla = Resources.GetBuiltinResource<Mesh>(nombre);
                if (malla != null) break;
            }
            if (malla == null)
            {
                // Por si cambia el nombre del recurso: se saca la malla de una primitiva de Unity, una sola vez.
                var primitiva = GameObject.CreatePrimitive(tipo);
                malla = primitiva.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(primitiva);
            }
            _mallas[tipo] = malla;
            return malla;
        }

        /// <summary>Crea una figura sin collider, colgada de padre, con el material compartido y su color.</summary>
        public static GameObject Crear(PrimitiveType tipo, Transform padre, Color color, string nombre = null)
        {
            var figura = Primitiva(tipo, nombre);
            figura.transform.SetParent(padre, false);
            var render = figura.GetComponent<Renderer>();
            Colorear(render, color);
            return figura;
        }

        /// <summary>Cambia el color de una figura sin duplicar su material.</summary>
        public static void Colorear(Renderer render, Color color)
        {
            if (render == null) return;
            if (_bloque == null) _bloque = new MaterialPropertyBlock();
            // Este bloque solo contiene propiedades instanciadas; las de material romperían el lote.
            _bloque.Clear();
            _bloque.SetColor(IdColorFigura, color);
            render.SetPropertyBlock(_bloque);
        }

        public static void Colorear(GameObject figura, Color color) => Colorear(figura.GetComponent<Renderer>(), color);
    }
}
