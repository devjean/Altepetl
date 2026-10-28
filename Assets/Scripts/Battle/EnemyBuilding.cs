using UnityEngine;

namespace Altepetl
{
    /// <summary>Edificio de una aldea enemiga. Las torres disparan a la tropa más cercana.</summary>
    public sealed class EnemyBuilding : MonoBehaviour
    {
        private const float SegundosEntreDisparos = 1f;

        public BuildingDefinition Definicion { get; private set; }
        public float VidaMaxima { get; private set; }
        public float Vida { get; private set; }
        public bool Destruido => Vida <= 0f;
        public bool EsDefensa => Definicion.EsDefensa;
        /// <summary>Radio aproximado del edificio, para saber cuándo una tropa ya lo alcanza.</summary>
        public float Radio => Definicion.Tamano * 0.45f;
        /// <summary>Altura a la que se dibuja su barra de vida.</summary>
        public float AlturaBarra => _altura + 0.4f;

        private BattleManager _batalla;
        private float _dano;
        private float _proximoDisparo;
        private Transform _modelo;
        private Renderer _render;
        private float _altura;

        public void Inicializar(BuildingDefinition definicion, int nivel, Vector3 centro, BattleManager batalla, bool plataforma = false)
        {
            Definicion = definicion;
            _batalla = batalla;
            float mult = BuildingDefinition.Multiplicador(Mathf.Max(1, nivel));
            VidaMaxima = definicion.VidaBase * mult;
            Vida = VidaMaxima;
            _dano = definicion.DanoDefensaPorSegundo * mult * SegundosEntreDisparos;

            name = $"Enemigo {definicion.Nombre}";
            transform.position = centro;

            var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cubo.GetComponent<Collider>());
            cubo.transform.SetParent(transform, false);
            _modelo = cubo.transform;
            _render = cubo.GetComponent<Renderer>();
            _altura = definicion.Altura * (1f + 0.15f * (Mathf.Max(1, nivel) - 1));
            float lado = definicion.Tamano * 0.9f;
            float base_ = 0f;
            if (plataforma)
            {
                // Sobre el lago, cada edificio va en su plataforma de piedra, como en Tenochtitlan.
                base_ = 0.14f;
                var piedra = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piedra.name = "Plataforma";
                Destroy(piedra.GetComponent<Collider>());
                piedra.transform.SetParent(transform, false);
                float ladoPlataforma = definicion.Tamano * 0.96f;
                piedra.transform.localScale = new Vector3(ladoPlataforma, base_, ladoPlataforma);
                piedra.transform.localPosition = new Vector3(0f, base_ * 0.5f, 0f);
                piedra.GetComponent<Renderer>().material.color = new Color(0.62f, 0.60f, 0.55f);
                _altura += base_;
            }
            _modelo.localScale = new Vector3(lado, _altura - base_, lado);
            _modelo.localPosition = new Vector3(0f, base_ + (_altura - base_) * 0.5f, 0f);
            ActualizarColor();
        }

        public void RecibirDano(float cantidad, TroopUnit atacante)
        {
            if (Destruido) return;
            Vida = Mathf.Max(0f, Vida - cantidad);
            if (Destruido)
            {
                // Quedan ruinas planas (sobre su plataforma, si la hay).
                float base_ = _modelo.localPosition.y - _modelo.localScale.y * 0.5f;
                _modelo.localScale = new Vector3(_modelo.localScale.x, 0.08f, _modelo.localScale.z);
                _modelo.localPosition = new Vector3(0f, base_ + 0.04f, 0f);
                _batalla.AlDestruirEdificio(this, atacante);
            }
            ActualizarColor();
        }

        private void Update()
        {
            if (Destruido || !EsDefensa || _batalla.Terminada) return;
            if (Time.time < _proximoDisparo) return;

            var objetivo = _batalla.TropaMasCercana(transform.position, Definicion.AlcanceDefensa + Radio);
            if (objetivo == null) return;
            objetivo.RecibirDano(_dano);
            _proximoDisparo = Time.time + SegundosEntreDisparos;
        }

        private void ActualizarColor()
        {
            if (Destruido)
            {
                _render.material.color = new Color(0.25f, 0.22f, 0.2f);
                return;
            }
            // Se oscurece conforme pierde vida.
            float salud = Vida / VidaMaxima;
            _render.material.color = Color.Lerp(new Color(0.3f, 0.1f, 0.1f), Definicion.Color, salud);
        }
    }
}
