using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Una tropa en batalla: busca el objetivo más cercano (o la defensa más cercana),
    /// camina en línea recta hasta tenerlo a su alcance y lo ataca. Si un defensor enemigo
    /// se le acerca, primero pelea con él.
    /// </summary>
    public sealed class TroopUnit : MonoBehaviour
    {
        private const float MargenAlcance = 0.05f;
        private const float DistanciaParaPelear = 1.2f; // defensores a esta distancia (más el alcance) la distraen

        public TroopDefinition Definicion { get; private set; }
        public float Vida { get; private set; }
        public float VidaMaxima => _vidaMaxima;
        public float FraccionVida => _vidaMaxima > 0f ? Vida / _vidaMaxima : 0f;
        public bool Muerta => Vida <= 0f;
        public int Rango { get; private set; }
        /// <summary>Edificios que derribó en esta batalla.</summary>
        public int Capturas { get; set; }

        private BattleManager _batalla;
        private float _vidaMaxima;
        private float _danoPorSegundo;
        private float _velocidad;
        private EnemyBuilding _objetivo;
        private Renderer _render;
        private float _destelloHasta;
        private Transform _cuerpo;
        private Transform _acalli;   // canoa, en los campos con agua
        private float _tamano;
        private float _fase;

        /// <summary>vidaInicial: fracción de la vida máxima con la que entra (las heridas entran incompletas).</summary>
        public void Inicializar(TroopDefinition definicion, int rango, float vidaInicial, Pueblo pueblo, Culto culto,
            Vector3 posicion, BattleManager batalla)
        {
            Definicion = definicion;
            Rango = rango;
            _batalla = batalla;
            float bonoRango = Rangos.Multiplicador(rango);
            _vidaMaxima = definicion.Vida * pueblo.MultiplicadorVidaTropas * bonoRango;
            Vida = _vidaMaxima * Mathf.Clamp(vidaInicial, 0.01f, 1f);
            _danoPorSegundo = definicion.DanoPorSegundo * pueblo.MultiplicadorAtaqueTropas * bonoRango;
            _velocidad = definicion.Velocidad;
            if (culto != null)
            {
                float ataque = 1f + culto.Bono(TipoBono.Ataque);
                if (definicion.Alcance > 1f) ataque += culto.Bono(TipoBono.DanoDistancia);
                _danoPorSegundo *= ataque * culto.AtaquePorFavor;
                _velocidad *= 1f + culto.Bono(TipoBono.Velocidad);
            }

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
            _cuerpo = cuerpo.transform;
            _tamano = tamano;
            _fase = Random.value * Mathf.PI * 2f;

            if (batalla.HayAgua)
            {
                // Llegan por el agua en acalli; la canoa se ve mientras van sobre el agua.
                var acalli = GameObject.CreatePrimitive(PrimitiveType.Cube);
                acalli.name = "Acalli";
                Destroy(acalli.GetComponent<Collider>());
                acalli.transform.SetParent(transform, false);
                acalli.transform.localScale = new Vector3(0.2f, 0.08f, 0.6f);
                acalli.GetComponent<Renderer>().material.color = new Color(0.45f, 0.30f, 0.18f);
                _acalli = acalli.transform;
                Remar(Vector3.forward);
            }
        }

        /// <summary>Sobre el agua, el monito va en su canoa y se mece; en tierra, la canoa se queda atrás.</summary>
        private void Remar(Vector3 rumbo)
        {
            if (_acalli == null) return;
            bool enAgua = _batalla.EnAgua(transform.position);
            if (_acalli.gameObject.activeSelf != enAgua) _acalli.gameObject.SetActive(enAgua);
            if (!enAgua)
            {
                _cuerpo.localPosition = new Vector3(0f, _tamano, 0f);
                return;
            }
            float vaiven = Mathf.Sin(Time.time * 1.8f + _fase) * 0.015f;
            _acalli.localPosition = new Vector3(0f, 0.04f + vaiven, 0f);
            if (rumbo.sqrMagnitude > 0.0001f) _acalli.localRotation = Quaternion.Euler(0f, Mathf.Atan2(rumbo.x, rumbo.z) * Mathf.Rad2Deg, 0f);
            _cuerpo.localPosition = new Vector3(0f, 0.08f + vaiven + _tamano, 0f);
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

            var defensor = _batalla.DefensorCercano(transform.position, Definicion.Alcance + DistanciaParaPelear);
            if (defensor != null)
            {
                Vector3 haciaDefensor = defensor.transform.position - transform.position;
                haciaDefensor.y = 0f;
                float distancia = haciaDefensor.magnitude - 0.2f;
                if (distancia > Definicion.Alcance + MargenAlcance)
                {
                    transform.position += haciaDefensor.normalized * Mathf.Min(_velocidad * Time.deltaTime, distancia - Definicion.Alcance);
                    Remar(haciaDefensor);
                    return;
                }
                Remar(Vector3.zero);
                defensor.RecibirDano(_danoPorSegundo * Time.deltaTime, this);
                return;
            }

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
                float paso = Mathf.Min(_velocidad * Time.deltaTime, distanciaAlBorde - Definicion.Alcance);
                transform.position += haciaObjetivo.normalized * paso;
                Remar(haciaObjetivo);
                return;
            }
            Remar(Vector3.zero);
            _objetivo.RecibirDano(_danoPorSegundo * Time.deltaTime, this);
        }
    }
}
