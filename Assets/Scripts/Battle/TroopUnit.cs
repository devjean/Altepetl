using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Una tropa en batalla: busca el objetivo más cercano (o la defensa más cercana),
    /// camina en línea recta hasta tenerlo a su alcance y lo ataca.
    /// </summary>
    public sealed class TroopUnit : MonoBehaviour
    {
        public TroopDefinition Definicion { get; private set; }
        public float Vida { get; private set; }
        public float VidaMaxima => _vidaMaxima;
        public bool Muerta => Vida <= 0f;

        private BattleManager _batalla;
        private float _vidaMaxima;
        private float _danoPorSegundo;
        private EnemyBuilding _objetivo;
        private Renderer _render;
        private float _destelloHasta;

        public void Inicializar(TroopDefinition definicion, Pueblo pueblo, Vector3 posicion, BattleManager batalla)
        {
            Definicion = definicion;
            _batalla = batalla;
            _vidaMaxima = definicion.Vida * pueblo.MultiplicadorVidaTropas;
            Vida = _vidaMaxima;
            _danoPorSegundo = definicion.DanoPorSegundo * pueblo.MultiplicadorAtaqueTropas;

            name = definicion.Nombre;
            transform.position = posicion;

            var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(cuerpo.GetComponent<Collider>());
            cuerpo.transform.SetParent(transform, false);
            cuerpo.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            cuerpo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            _render = cuerpo.GetComponent<Renderer>();
            _render.material.color = definicion.Color;
        }

        public void RecibirDano(float cantidad)
        {
            if (Muerta) return;
            Vida -= cantidad;
            _destelloHasta = Time.time + 0.15f;
            if (Muerta)
            {
                _batalla.AlMorirTropa(this);
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (Muerta || _batalla.Terminada) return;

            _render.material.color = Time.time < _destelloHasta ? Color.white : Definicion.Color;

            if (_objetivo == null || _objetivo.Destruido)
            {
                _objetivo = _batalla.ObjetivoPara(transform.position, Definicion.PrefiereDefensas);
                if (_objetivo == null) return;
            }

            Vector3 haciaObjetivo = _objetivo.transform.position - transform.position;
            haciaObjetivo.y = 0f;
            float distanciaAlBorde = haciaObjetivo.magnitude - _objetivo.Radio;

            if (distanciaAlBorde > Definicion.Alcance)
            {
                float paso = Mathf.Min(Definicion.Velocidad * Time.deltaTime, distanciaAlBorde - Definicion.Alcance);
                transform.position += haciaObjetivo.normalized * paso;
                return;
            }
            _objetivo.RecibirDano(_danoPorSegundo * Time.deltaTime);
        }
    }
}
