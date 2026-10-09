using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Un guerrero en un ataque a tu aldea. Los invasores van por el edificio más cercano
    /// (los de alcance prefieren las torres) y, si una muralla les cierra el paso, la derriban.
    /// Tus defensores salen a buscar al invasor más cercano y, cuando ya no queda ninguno, vuelven a su puesto.
    /// </summary>
    public sealed class Combatiente : MonoBehaviour
    {
        private const float MargenAlcance = 0.05f;
        private const float DistanciaParaPelear = 1.2f; // un rival a esta distancia (más el alcance) lo distrae

        public TroopDefinition Definicion { get; private set; }
        public int Rango { get; private set; }
        public bool Invasor { get; private set; }
        public float Vida { get; private set; }
        public float VidaMaxima { get; private set; }
        public float FraccionVida => VidaMaxima > 0f ? Vida / VidaMaxima : 0f;
        public bool Muerto => Vida <= 0f;

        private Asalto _asalto;
        private float _danoPorSegundo;
        private float _velocidad;
        private Vector3 _puesto;
        private Building _objetivo;
        private Building _muro;
        private Renderer _render;
        private Color _color;
        private float _destelloHasta;

        /// <summary>vidaInicial: fracción de la vida máxima. Con pueblo, es tuyo y usa sus bonos y los de la ofrenda.</summary>
        public void Inicializar(TroopDefinition definicion, int rango, bool invasor, Pueblo pueblo, Culto culto,
            Vector3 posicion, Asalto asalto)
        {
            Definicion = definicion;
            Rango = rango;
            Invasor = invasor;
            _asalto = asalto;
            _puesto = posicion;
            float bono = Rangos.Multiplicador(rango);
            VidaMaxima = definicion.Vida * bono;
            _danoPorSegundo = definicion.DanoPorSegundo * bono;
            _velocidad = definicion.Velocidad;
            if (!invasor && pueblo != null)
            {
                VidaMaxima *= pueblo.MultiplicadorVidaTropas;
                _danoPorSegundo *= pueblo.MultiplicadorAtaqueTropas;
                if (culto != null)
                {
                    float ataque = 1f + culto.Bono(TipoBono.Ataque);
                    if (definicion.Alcance > 1f) ataque += culto.Bono(TipoBono.DanoDistancia);
                    _danoPorSegundo *= ataque * culto.AtaquePorFavor;
                    _velocidad *= 1f + culto.Bono(TipoBono.Velocidad);
                }
            }
            Vida = VidaMaxima;

            name = invasor ? $"Invasor {definicion.Nombre}" : $"Defensor {definicion.Nombre}";
            transform.position = posicion;

            var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(cuerpo.GetComponent<Collider>());
            cuerpo.transform.SetParent(transform, false);
            float tamano = 0.3f * (1f + 0.12f * rango);
            cuerpo.transform.localScale = new Vector3(tamano, tamano, tamano);
            cuerpo.transform.localPosition = new Vector3(0f, tamano, 0f);
            _render = cuerpo.GetComponent<Renderer>();
            // Los invasores, más oscuros y rojizos, como los defensores de las aldeas enemigas.
            _color = invasor ? Color.Lerp(definicion.Color, new Color(0.35f, 0.08f, 0.08f), 0.55f) : definicion.Color;
            _render.material.color = _color;
        }

        public void RecibirDano(float cantidad, Combatiente atacante)
        {
            if (Muerto) return;
            Vida -= cantidad;
            _destelloHasta = Time.time + 0.15f;
            if (Muerto)
            {
                _asalto.AlCaer(this, atacante);
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (Muerto || !_asalto.EnCurso) return;
            _render.material.color = Time.time < _destelloHasta ? Color.white : _color;

            if (Invasor) ActuarInvasor();
            else ActuarDefensor();
        }

        private void ActuarInvasor()
        {
            var rival = _asalto.MasCercano(transform.position, invasores: false, Definicion.Alcance + DistanciaParaPelear);
            if (rival != null)
            {
                Pelear(rival);
                return;
            }

            // Una muralla en el camino: primero hay que derribarla.
            if (_muro != null && !_muro.Derribado)
            {
                Golpear(_muro);
                return;
            }
            _muro = null;

            if (_objetivo == null || _objetivo.Derribado)
            {
                _objetivo = _asalto.ObjetivoPara(transform.position, Definicion.PrefiereDefensas);
                if (_objetivo == null) return;
            }
            Golpear(_objetivo);
        }

        private void ActuarDefensor()
        {
            var rival = _asalto.MasCercano(transform.position, invasores: true, float.MaxValue);
            if (rival != null)
            {
                Pelear(rival);
                return;
            }
            Caminar(_puesto, 0f);
        }

        private void Pelear(Combatiente rival)
        {
            Vector3 hacia = rival.transform.position - transform.position;
            hacia.y = 0f;
            float distancia = hacia.magnitude - 0.2f;
            if (distancia > Definicion.Alcance + MargenAlcance)
            {
                Caminar(rival.transform.position, Definicion.Alcance + 0.2f);
                return;
            }
            rival.RecibirDano(_danoPorSegundo * Time.deltaTime, this);
        }

        /// <summary>Camina hasta el edificio y, ya a su alcance, lo golpea.</summary>
        private void Golpear(Building edificio)
        {
            Vector3 hacia = edificio.transform.position - transform.position;
            hacia.y = 0f;
            float distanciaAlBorde = hacia.magnitude - edificio.Radio;
            if (distanciaAlBorde > Definicion.Alcance + MargenAlcance)
            {
                Vector3 paso = hacia.normalized * Mathf.Min(_velocidad * Time.deltaTime, distanciaAlBorde - Definicion.Alcance);
                var muro = _asalto.MuroEn(transform.position + hacia.normalized * 0.45f);
                if (muro != null && muro != edificio)
                {
                    _muro = muro;
                    return;
                }
                transform.position += paso;
                return;
            }
            _asalto.DanarEdificio(edificio, _danoPorSegundo * Time.deltaTime);
        }

        private void Caminar(Vector3 destino, float parar)
        {
            Vector3 hacia = destino - transform.position;
            hacia.y = 0f;
            float distancia = hacia.magnitude - parar;
            if (distancia <= 0.01f) return;
            transform.position += hacia.normalized * Mathf.Min(_velocidad * Time.deltaTime, distancia);
        }
    }
}
