using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Una tropa en batalla: busca el objetivo más cercano (o la defensa más cercana),
    /// camina en línea recta hasta tenerlo a su alcance y lo ataca.
    /// </summary>
    public sealed class TroopUnit : MonoBehaviour
    {
        private const float MargenAlcance = 0.05f;

        public TroopDefinition Definicion { get; private set; }
        public float Vida { get; private set; }
        public float VidaMaxima => _vidaMaxima;
        public bool Muerta => Vida <= 0f;
        public int Rango { get; private set; }
        /// <summary>Edificios que derribó en esta batalla.</summary>
        public int Capturas { get; set; }

        private BattleManager _batalla;
        private float _vidaMaxima;
        private float _danoPorSegundo;
        private EnemyBuilding _objetivo;
        private Renderer _render;
        private float _destelloHasta;

        public void Inicializar(TroopDefinition definicion, int rango, Pueblo pueblo, Vector3 posicion, BattleManager batalla)
        {
            Definicion = definicion;
            Rango = rango;
            _batalla = batalla;
            float bonoRango = Rangos.Multiplicador(rango);
            _vidaMaxima = definicion.Vida * pueblo.MultiplicadorVidaTropas * bonoRango;
            Vida = _vidaMaxima;
            _danoPorSegundo = definicion.DanoPorSegundo * pueblo.MultiplicadorAtaqueTropas * bonoRango;

            name = definicion.Nombre;
            transform.position = posicion;

            var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(cuerpo.GetComponent<Collider>());
            cuerpo.transform.SetParent(transform, false);
            // Los de más rango se ven un poco más grandes.
            float tamano = 0.3f * (1f + 0.12f * rango);
            cuerpo.transform.localScale = new Vector3(tamano, tamano, tamano);
            cuerpo.transform.localPosition = new Vector3(0f, tamano, 0f);
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

            // Margen pequeño: lejos del origen los float pierden precisión y, sin él, la tropa
            // podía quedarse a una milésima del alcance sin moverse ni atacar.
            if (distanciaAlBorde > Definicion.Alcance + MargenAlcance)
            {
                float paso = Mathf.Min(Definicion.Velocidad * Time.deltaTime, distanciaAlBorde - Definicion.Alcance);
                transform.position += haciaObjetivo.normalized * paso;
                return;
            }
            _objetivo.RecibirDano(_danoPorSegundo * Time.deltaTime, this);
        }
    }
}
