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

        /// <summary>Crea una figura sin collider, colgada de padre, con el material compartido y su color.</summary>
        public static GameObject Crear(PrimitiveType tipo, Transform padre, Color color, string nombre = null)
        {
            var figura = GameObject.CreatePrimitive(tipo);
            Object.Destroy(figura.GetComponent<Collider>()); // que no estorbe al tocar los edificios
            if (nombre != null) figura.name = nombre;
            figura.transform.SetParent(padre, false);
            var render = figura.GetComponent<Renderer>();
            render.sharedMaterial = Material;
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
