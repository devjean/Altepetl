using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Un edificio colocado en la aldea. Se construye con un temporizador, luego produce
    /// y se puede mejorar de nivel. Mientras se construye o mejora, no produce.
    /// </summary>
    public sealed class Building : MonoBehaviour
    {
        public BuildingDefinition Definicion { get; private set; }
        public Vector2Int Origen { get; private set; }

        /// <summary>Nivel terminado. 0 mientras se construye por primera vez.</summary>
        public int Nivel { get; private set; }
        public bool EnConstruccion => _segundosRestantes > 0f;
        public bool Mejorando => EnConstruccion && Nivel > 0;
        public float SegundosRestantes => _segundosRestantes;
        public string Nombre => Definicion.NombrePara(_pueblo);
        public float Acumulado => _acumulado;

        private Pueblo _pueblo;
        private ResourceBank _banco;

        /// <summary>Ofrendas activas; lo asigna GameManager al construir.</summary>
        public Culto Culto { get; set; }
        private float _segundosRestantes;
        private float _segundosTotales;
        private float _acumulado;
        private Transform _modelo;
        private Renderer _render;
        private float _base; // alto de la plataforma sobre el lago (aldea mexica)

        public void Inicializar(BuildingDefinition definicion, Pueblo pueblo, Vector2Int origen,
            ResourceBank banco, GridMap mapa, int nivel, float segundosRestantes, float acumulado = 0f)
        {
            Definicion = definicion;
            Origen = origen;
            Nivel = Mathf.Clamp(nivel, 0, definicion.NivelMaximo);
            _pueblo = pueblo;
            _banco = banco;
            _segundosRestantes = Mathf.Max(0f, segundosRestantes);
            _segundosTotales = Mathf.Max(_segundosRestantes, SegundosParaNivel(Nivel + 1));
            _acumulado = acumulado;

            name = Nombre;
            transform.position = mapa.CentroDeArea(origen, definicion.Tamano);

            var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cubo.GetComponent<Collider>());
            cubo.transform.SetParent(transform, false);
            _modelo = cubo.transform;
            _render = cubo.GetComponent<Renderer>();
            if (pueblo.EnLago) CrearPlataforma();

            // Capacidad de los niveles ya terminados (al cargar una partida o colocar el tecpan).
            _banco.AgregarCapacidad(Definicion.CapacidadExtra * Nivel);
            ActualizarVisual();
        }

        /// <summary>Segundos que tarda en llegar a ese nivel desde el anterior.</summary>
        public float SegundosParaNivel(int nivel)
        {
            float ofrenda = Culto != null ? 1f - Culto.Bono(TipoBono.Construccion) : 1f;
            return Definicion.SegundosParaNivel(nivel) * _pueblo.MultiplicadorTiempoConstruccion * ofrenda;
        }

        /// <summary>Vida actual; crece con el nivel y las murallas dependen del pueblo.</summary>
        public int Vida
        {
            get
            {
                float mult = Definicion.Id == BuildingId.Muralla ? _pueblo.MultiplicadorVidaMurallas : 1f;
                if ((Definicion.Id == BuildingId.Muralla || Definicion.EsDefensa) && Culto != null) mult *= Culto.DefensasPorFavor;
                return Mathf.RoundToInt(Definicion.VidaBase * BuildingDefinition.Multiplicador(Mathf.Max(1, Nivel)) * mult);
            }
        }

        public float ProduccionPorMinuto => ProduccionEnNivel(Nivel);

        public float ProduccionEnNivel(int nivel, bool conOfrenda = true)
        {
            if (!Definicion.Produce || nivel <= 0) return 0f;
            float mult = Definicion.Recurso == ResourceType.Maiz ? _pueblo.MultiplicadorMaiz : 1f;
            if (conOfrenda && Culto != null)
            {
                float bono = Culto.Bono(TipoBono.Produccion);
                if (Definicion.Recurso == ResourceType.Maiz) bono += Culto.Bono(TipoBono.Maiz);
                mult *= 1f + bono;
            }
            return Definicion.ProduccionPorMinuto * BuildingDefinition.Multiplicador(nivel) * mult;
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

        /// <summary>Arranca el temporizador hacia el siguiente nivel. El costo lo cobra quien llama.</summary>
        public void EmpezarMejora()
        {
            if (EnConstruccion || Nivel >= Definicion.NivelMaximo) return;
            _segundosTotales = SegundosParaNivel(Nivel + 1);
            _segundosRestantes = _segundosTotales;
            ActualizarVisual();
        }

        private void Update()
        {
            AvanzarConstruccion(Time.deltaTime);
            if (EnConstruccion)
            {
                ActualizarVisual();
                return;
            }
            Producir(Time.deltaTime);
        }

        /// <summary>
        /// Avanza el temporizador de construcción o mejora. Devuelve los segundos que sobran
        /// después de terminar (0 si sigue en obra).
        /// </summary>
        public float AvanzarConstruccion(float segundos)
        {
            if (!EnConstruccion) return segundos;
            if (segundos < _segundosRestantes)
            {
                _segundosRestantes -= segundos;
                return 0f;
            }
            float sobrante = segundos - _segundosRestantes;
            _segundosRestantes = 0f;
            AlTerminar();
            return sobrante;
        }

        /// <summary>Produce lo correspondiente a ese tiempo. Lo que no cabe en el almacén se pierde.</summary>
        public void Producir(float segundos, bool conOfrenda = true)
        {
            if (EnConstruccion || !Definicion.Produce || segundos <= 0f) return;
            _acumulado += ProduccionEnNivel(Nivel, conOfrenda) / 60f * segundos;
            if (_acumulado >= 1f)
            {
                float entero = Mathf.Floor(_acumulado);
                _banco.Add(Definicion.Recurso, entero);
                _acumulado -= entero;
            }
        }

        private void AlTerminar()
        {
            Nivel++;
            _banco.AgregarCapacidad(Definicion.CapacidadExtra);
            ActualizarVisual();
        }

        /// <summary>
        /// En el lago, cada edificio se levanta sobre una plataforma de piedra y tierra. La chinampa es
        /// una parcela de lodo y vegetación sujeta con estacas y ahuejotes, los árboles que la anclan.
        /// </summary>
        private void CrearPlataforma()
        {
            bool chinampa = Definicion.Id == BuildingId.Granja;
            _base = chinampa ? 0.08f : 0.14f;
            float lado = Definicion.Tamano * (chinampa ? 0.98f : 0.96f);

            var plataforma = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plataforma.name = chinampa ? "Chinampa" : "Plataforma";
            Destroy(plataforma.GetComponent<Collider>());
            plataforma.transform.SetParent(transform, false);
            plataforma.transform.localScale = new Vector3(lado, _base, lado);
            plataforma.transform.localPosition = new Vector3(0f, _base * 0.5f, 0f);
            plataforma.GetComponent<Renderer>().material.color = chinampa
                ? new Color(0.36f, 0.27f, 0.17f)
                : new Color(0.66f, 0.62f, 0.55f);

            if (!chinampa) return;
            // Dos ahuejotes en esquinas opuestas.
            float esquina = lado * 0.45f;
            CrearAhuejote(new Vector3(-esquina, 0f, -esquina));
            CrearAhuejote(new Vector3(esquina, 0f, esquina));
        }

        private void CrearAhuejote(Vector3 posicion)
        {
            var arbol = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arbol.name = "Ahuejote";
            Destroy(arbol.GetComponent<Collider>());
            arbol.transform.SetParent(transform, false);
            arbol.transform.localScale = new Vector3(0.12f, 0.45f, 0.12f); // un Cylinder mide 2 de alto
            arbol.transform.localPosition = posicion + new Vector3(0f, 0.45f, 0f);
            arbol.GetComponent<Renderer>().material.color = new Color(0.30f, 0.50f, 0.25f);
        }

        private void ActualizarVisual()
        {
            float lado = Definicion.Tamano * 0.9f;
            // Cada nivel hace el edificio un poco más alto.
            float alturaNivel = Definicion.Altura * (1f + 0.15f * (Mathf.Max(1, Nivel) - 1));

            float altura = alturaNivel;
            if (EnConstruccion && Nivel == 0)
            {
                float progreso = Mathf.Clamp01(1f - _segundosRestantes / Mathf.Max(_segundosTotales, 0.01f));
                altura = alturaNivel * Mathf.Lerp(0.2f, 1f, progreso);
            }
            altura = Mathf.Max(0.05f, altura);

            _modelo.localScale = new Vector3(lado, altura, lado);
            _modelo.localPosition = new Vector3(0f, _base + altura * 0.5f, 0f);
            _render.material.color = EnConstruccion
                ? Color.Lerp(Color.gray, Definicion.Color, 0.4f)
                : Definicion.Color;
        }
    }
}
