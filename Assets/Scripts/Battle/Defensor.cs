using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Un guerrero que defiende la aldea enemiga. Espera junto al tecpan y, cuando tus tropas se
    /// acercan (o lo atacan), sale a pelear contra la más cercana. Si no queda nadie cerca, vuelve a su puesto.
    /// </summary>
    public sealed class Defensor : MonoBehaviour
    {
        private const float RadioAlerta = 4.5f;      // si una tropa tuya llega a esta distancia del puesto, salen
        private const float RadioPersecucion = 7f;   // hasta dónde persiguen
        private const float MargenAlcance = 0.05f;

        public TroopDefinition Definicion { get; private set; }
        public int Rango { get; private set; }
        public float Vida { get; private set; }
        public float VidaMaxima { get; private set; }
        public bool Muerto => Vida <= 0f;

        private BattleManager _batalla;
        private float _danoPorSegundo;
        private Vector3 _puesto;
        private bool _activo;
        private TroopUnit _objetivo;
        private Renderer _render;
        private Color _color;
        private float _destelloHasta;

        public void Inicializar(TroopDefinition definicion, int rango, Vector3 puesto, BattleManager batalla)
        {
            Definicion = definicion;
            Rango = rango;
            _batalla = batalla;
            _puesto = puesto;
            float bono = Rangos.Multiplicador(rango);
            VidaMaxima = definicion.Vida * bono;
            Vida = VidaMaxima;
            _danoPorSegundo = definicion.DanoPorSegundo * bono;

            name = $"Defensor {definicion.Nombre}";
            transform.position = puesto;

            var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(cuerpo.GetComponent<Collider>());
            cuerpo.transform.SetParent(transform, false);
            float tamano = 0.3f * (1f + 0.12f * rango);
            cuerpo.transform.localScale = new Vector3(tamano, tamano, tamano);
            cuerpo.transform.localPosition = new Vector3(0f, tamano, 0f);
            _render = cuerpo.GetComponent<Renderer>();
            // Más oscuros que los tuyos, con un tinte rojizo, para distinguirlos.
            _color = Color.Lerp(definicion.Color, new Color(0.35f, 0.08f, 0.08f), 0.55f);
            _render.material.color = _color;
        }

        /// <summary>Recibe daño de una de tus tropas; si estaba esperando, sale a pelear.</summary>
        public void RecibirDano(float cantidad, TroopUnit atacante)
        {
            if (Muerto) return;
            Vida -= cantidad;
            _destelloHasta = Time.time + 0.15f;
            _activo = true;
            if (_objetivo == null && atacante != null) _objetivo = atacante;
            if (Muerto)
            {
                _batalla.AlMorirDefensor(this, atacante);
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (Muerto || _batalla.Terminada) return;

            _render.material.color = Time.time < _destelloHasta ? Color.white : _color;

            if (!_activo && _batalla.TropaMasCercana(_puesto, RadioAlerta) != null) _activo = true;
            if (_objetivo == null || _objetivo.Muerta)
            {
                _objetivo = _activo ? _batalla.TropaMasCercana(transform.position, RadioPersecucion) : null;
            }

            if (_objetivo == null)
            {
                // Nadie cerca: vuelve a su puesto y espera.
                _activo = false;
                Caminar(_puesto, 0f);
                return;
            }

            Vector3 hacia = _objetivo.transform.position - transform.position;
            hacia.y = 0f;
            if (hacia.magnitude > Definicion.Alcance + MargenAlcance + 0.2f)
            {
                Caminar(_objetivo.transform.position, Definicion.Alcance + 0.2f);
                return;
            }
            _objetivo.RecibirDano(_danoPorSegundo * Time.deltaTime);
        }

        private void Caminar(Vector3 destino, float parar)
        {
            Vector3 hacia = destino - transform.position;
            hacia.y = 0f;
            float distancia = hacia.magnitude - parar;
            if (distancia <= 0.01f) return;
            transform.position += hacia.normalized * Mathf.Min(Definicion.Velocidad * Time.deltaTime, distancia);
        }
    }
}
