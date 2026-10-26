using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Un guerrero en un ataque a tu aldea. Los invasores van por el edificio más cercano
    /// (los de alcance prefieren las torres) rodeando lo que estorba; si la muralla les cierra el paso
    /// o el rodeo es muy largo, van derecho y la derriban (las puertas, para ellos, son muralla).
    /// Tus defensores salen a buscar al invasor más cercano, cruzan la muralla por sus puertas y,
    /// cuando ya no queda ninguno, vuelven a su puesto.
    /// </summary>
    public sealed class Combatiente : MonoBehaviour
    {
        private const float MargenAlcance = 0.05f;
        private const float DistanciaParaPelear = 1.2f; // un rival a esta distancia (más el alcance) lo distrae
        private const float RodeoMaximo = 2.5f;         // un rodeo más largo que esto por la recta (más unas casillas) no vale la pena
        private const float RodeoExtra = 3f;
        private const float CadaCuantoRevisaRuta = 2f;  // caen murallas y edificios: el camino cambia

        public TroopDefinition Definicion { get; private set; }
        public int Rango { get; private set; }
        public bool Invasor { get; private set; }
        public float Vida { get; private set; }
        public float VidaMaxima { get; private set; }
        public float FraccionVida => VidaMaxima > 0f ? Vida / VidaMaxima : 0f;
        public bool Muerto => Vida <= 0f;
        /// <summary>Peleó contra algún invasor (para subir de rango al terminar).</summary>
        public bool Peleo { get; private set; }
        /// <summary>Invasores que capturó en este ataque.</summary>
        public int Capturas { get; set; }

        private Asalto _asalto;
        private float _danoPorSegundo;
        private float _velocidad;
        private Vector3 _puesto;
        private Building _objetivo;
        private Building _muro;
        private Renderer _render;
        private Color _color;
        private float _destelloHasta;
        private readonly Caminos.Andante _andar = new Caminos.Andante();
        private float _revisarRuta;

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
                _andar.Olvidar();
            }
            Golpear(_objetivo);
        }

        /// <summary>Para un invasor solo se pasa por las ruinas y por el edificio al que va.</summary>
        private bool PasaInvasor(Building edificio) => edificio.Derribado || edificio == _objetivo;

        /// <summary>Un defensor pasa por las ruinas y por las puertas de la muralla.</summary>
        private static bool PasaDefensor(Building edificio)
        {
            return edificio.Derribado || (edificio.Definicion.Id == BuildingId.Muralla && edificio.EsPuerta);
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
            Peleo = true;
            rival.RecibirDano(_danoPorSegundo * Time.deltaTime, this);
        }

        /// <summary>El siguiente punto hacia el destino, rodeando lo que estorbe según quién camina.</summary>
        private Vector3 Siguiente(Vector3 destino, float tolerancia)
        {
            Vector3 plano = transform.position.WithY0();
            if (Invasor)
            {
                if (Time.time >= _revisarRuta)
                {
                    _revisarRuta = Time.time + CadaCuantoRevisaRuta;
                    _andar.Olvidar();
                }
                bool nueva = _andar.Ruta == null;
                Vector3 siguiente = _andar.Siguiente(_asalto.Manager.Caminos, plano, destino, PasaInvasor, tolerancia);
                var ruta = _andar.Ruta;
                if (nueva && ruta != null)
                {
                    // Si el rodeo es muy largo, mejor derecho: lo que estorbe se derriba.
                    float recta = Vector3.Distance(plano, destino);
                    if (Caminos.Largo(plano, ruta) > recta * RodeoMaximo + RodeoExtra)
                    {
                        _andar.Descartar();
                        return destino;
                    }
                }
                return siguiente;
            }
            Vector3 paso = _andar.Siguiente(_asalto.Manager.Caminos, plano, destino, PasaDefensor, tolerancia);
            _asalto.Manager.CruzarPuerta(plano, (paso - plano).normalized);
            return paso;
        }

        /// <summary>Camina hasta el edificio y, ya a su alcance, lo golpea.</summary>
        private void Golpear(Building edificio)
        {
            Vector3 hacia = edificio.transform.position - transform.position;
            hacia.y = 0f;
            float distanciaAlBorde = hacia.magnitude - edificio.Radio;
            if (distanciaAlBorde > Definicion.Alcance + MargenAlcance)
            {
                Vector3 siguiente = Siguiente(edificio.transform.position.WithY0(), 0f);
                Vector3 rumbo = (siguiente - transform.position.WithY0()).normalized;
                Vector3 paso = rumbo * Mathf.Min(_velocidad * Time.deltaTime, distanciaAlBorde - Definicion.Alcance);
                // Sin rodeo (va derecho), una muralla en el camino se derriba primero.
                if (_andar.Ruta == null)
                {
                    var muro = _asalto.MuroEn(transform.position + rumbo * 0.45f);
                    if (muro != null && muro != edificio)
                    {
                        _muro = muro;
                        return;
                    }
                }
                transform.position += paso;
                return;
            }
            _asalto.DanarEdificio(edificio, _danoPorSegundo * Time.deltaTime);
        }

        /// <summary>Camina hacia un punto (un rival o el puesto) rodeando lo que estorbe, hasta quedar a "parar".</summary>
        private void Caminar(Vector3 destino, float parar)
        {
            Vector3 hacia = destino - transform.position;
            hacia.y = 0f;
            float distancia = hacia.magnitude - parar;
            if (distancia <= 0.01f) return;
            // Un rival se mueve: la ruta se vuelve a buscar cuando se aleja de donde estaba.
            Vector3 siguiente = Siguiente(destino.WithY0(), 0.75f);
            Vector3 rumbo = (siguiente - transform.position.WithY0()).normalized;
            transform.position += rumbo * Mathf.Min(_velocidad * Time.deltaTime, distancia);
        }
    }
}
