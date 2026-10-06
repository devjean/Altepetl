using UnityEngine;

namespace Altepetl
{
    /// <summary>Un edificio colocado en la aldea: se construye con un temporizador y luego produce.</summary>
    public sealed class Building : MonoBehaviour
    {
        public BuildingDefinition Definicion { get; private set; }
        public Vector2Int Origen { get; private set; }
        public bool EnConstruccion => _segundosRestantes > 0f;
        public float SegundosRestantes => _segundosRestantes;
        public string Nombre => Definicion.NombrePara(_pueblo);

        private Pueblo _pueblo;
        private ResourceBank _banco;
        private float _segundosRestantes;
        private float _acumulado;
        private Transform _modelo;
        private Renderer _render;

        public void Inicializar(BuildingDefinition definicion, Pueblo pueblo, Vector2Int origen,
            ResourceBank banco, GridMap mapa, bool instantaneo)
        {
            Definicion = definicion;
            Origen = origen;
            _pueblo = pueblo;
            _banco = banco;
            _segundosRestantes = instantaneo ? 0f : definicion.SegundosConstruccion * pueblo.MultiplicadorTiempoConstruccion;

            name = Nombre;
            transform.position = mapa.CentroDeArea(origen, definicion.Tamano);

            var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cubo.GetComponent<Collider>());
            cubo.transform.SetParent(transform, false);
            _modelo = cubo.transform;
            _render = cubo.GetComponent<Renderer>();

            ActualizarVisual();
            if (!EnConstruccion) AlTerminar();
        }

        /// <summary>Vida actual; las murallas dependen del pueblo.</summary>
        public int Vida
        {
            get
            {
                float mult = Definicion.Id == BuildingId.Muralla ? _pueblo.MultiplicadorVidaMurallas : 1f;
                return Mathf.RoundToInt(Definicion.VidaBase * mult);
            }
        }

        public float ProduccionPorMinuto
        {
            get
            {
                if (!Definicion.Produce) return 0f;
                float mult = Definicion.Recurso == ResourceType.Maiz ? _pueblo.MultiplicadorMaiz : 1f;
                return Definicion.ProduccionPorMinuto * mult;
            }
        }

        /// <summary>Plumas de quetzal necesarias para terminar ya: 1 por cada 10 s restantes.</summary>
        public int CostoAcelerar => Mathf.Max(1, Mathf.CeilToInt(_segundosRestantes / 10f));

        public bool TryAcelerar()
        {
            if (!EnConstruccion) return false;
            if (!_banco.TryGastar(ResourceInfo.Costo(plumas: CostoAcelerar))) return false;
            _segundosRestantes = 0f;
            AlTerminar();
            return true;
        }

        private void Update()
        {
            if (EnConstruccion)
            {
                _segundosRestantes -= Time.deltaTime;
                if (_segundosRestantes <= 0f)
                {
                    _segundosRestantes = 0f;
                    AlTerminar();
                }
                ActualizarVisual();
                return;
            }

            if (Definicion.Produce)
            {
                _acumulado += ProduccionPorMinuto / 60f * Time.deltaTime;
                if (_acumulado >= 1f)
                {
                    float entero = Mathf.Floor(_acumulado);
                    _banco.Add(Definicion.Recurso, entero);
                    _acumulado -= entero;
                }
            }
        }

        private void AlTerminar()
        {
            if (Definicion.CapacidadExtra > 0) _banco.AgregarCapacidad(Definicion.CapacidadExtra);
            ActualizarVisual();
        }

        private void ActualizarVisual()
        {
            float lado = Definicion.Tamano * 0.9f;
            float progreso = 1f;
            if (EnConstruccion)
            {
                float total = Definicion.SegundosConstruccion * _pueblo.MultiplicadorTiempoConstruccion;
                progreso = Mathf.Clamp01(1f - _segundosRestantes / Mathf.Max(total, 0.01f));
            }

            float altura = Mathf.Max(0.05f, Definicion.Altura * Mathf.Lerp(0.2f, 1f, progreso));
            _modelo.localScale = new Vector3(lado, altura, lado);
            _modelo.localPosition = new Vector3(0f, altura * 0.5f, 0f);
            _render.material.color = EnConstruccion
                ? Color.Lerp(Color.gray, Definicion.Color, 0.4f)
                : Definicion.Color;
        }
    }
}
