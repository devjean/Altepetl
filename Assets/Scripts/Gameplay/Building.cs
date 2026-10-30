using System.Collections.Generic;
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

        /// <summary>Daño recibido en el ataque en curso a la aldea. Al terminar el ataque se repara solo.</summary>
        public float DanoAsalto { get; private set; }
        public bool Derribado => DanoAsalto > 0f && DanoAsalto >= Vida;
        public float FraccionVidaAsalto => Mathf.Clamp01(1f - DanoAsalto / Mathf.Max(1, Vida));
        /// <summary>Radio aproximado, para saber cuándo una tropa ya lo alcanza.</summary>
        public float Radio => Definicion.Id == BuildingId.Muralla ? 0.35f : Definicion.Tamano * 0.45f;
        public float AlturaModelo => _base + _modelo.localScale.y;
        /// <summary>Se está levantando de nuevo después de un ataque (solo se ve; ya produce).</summary>
        public bool Reconstruyendo => _reconstruirHasta > Time.time;

        /// <summary>Muralla convertida en puerta: tu gente la cruza y se abre a su paso; los enemigos no.</summary>
        public bool EsPuerta { get; private set; }
        private const float SegundosAbierta = 0.8f;
        private const float AnguloAbierta = 80f;
        private float _abiertaHasta;
        private float _angulo;          // qué tan abiertas están las hojas
        private bool _puertaEnX = true; // la muralla corre de este a oeste (si no, de norte a sur)
        private Transform _hojaA, _hojaB; // pivotes de las dos hojas

        public const float SegundosReconstruccion = 8f;
        private float _reconstruirDesde;
        private float _reconstruirHasta;
        private bool _seLevanta; // estaba derribado: sube desde las ruinas

        private Pueblo _pueblo;
        private ResourceBank _banco;

        /// <summary>Ofrendas activas; lo asigna GameManager al construir.</summary>
        public Culto Culto { get; set; }
        private float _segundosRestantes;
        private float _segundosTotales;
        private float _acumulado;
        private Transform _modelo;
        private Renderer _render;
        private Material _materialModelo;
        private Material _materialTransparente;
        private bool _vistaTransparente;
        private static Material _plantillaTransparente;
        private bool _faltaPlantillaTransparente;
        private Color _ultimoColorTransparente;
        private bool _colorTransparenteAsignado;
        private float _base; // alto de la plataforma sobre el lago (aldea mexica)
        private bool _chinampaEnOrilla; // chinampa acolhua: parcela más chica para que se vean los canales
        private Transform _uneEste, _uneNorte; // tramos que unen una muralla con la de al lado

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
            _materialModelo = _render.material;
            if (pueblo.EnLago) CrearPlataforma();
            else if (definicion.Id == BuildingId.Granja && pueblo.NombreGranja == "Chinampa") CrearChinampaEnLaOrilla();

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

        /// <summary>Recibe daño de un invasor. Devuelve true si con este golpe quedó derribado.</summary>
        public bool RecibirDanoAsalto(float cantidad)
        {
            if (Derribado) return false;
            DanoAsalto += cantidad;
            ActualizarVisual();
            return Derribado;
        }

        /// <summary>
        /// Terminado el ataque, el edificio vuelve a quedar como estaba: los derribados se levantan
        /// poco a poco desde las ruinas (empiezan después de unos segundos de espera).
        /// </summary>
        public void Reparar(float espera = 0f)
        {
            if (DanoAsalto <= 0f) return;
            _seLevanta = Derribado;
            DanoAsalto = 0f;
            _reconstruirDesde = Time.time + espera;
            _reconstruirHasta = _reconstruirDesde + SegundosReconstruccion;
            ActualizarVisual();
        }

        /// <summary>Alguien de la aldea la está cruzando: se abre un momento.</summary>
        public void AbrirPuerta()
        {
            if (EsPuerta) _abiertaHasta = Time.time + SegundosAbierta;
        }

        public void HacerPuerta(bool puerta, GridMap mapa)
        {
            if (!EsMuralla || EsPuerta == puerta) return;
            EsPuerta = puerta;
            _abiertaHasta = 0f;
            _angulo = 0f;
            if (puerta)
            {
                _hojaA = Hoja("Hoja izquierda", -1);
                _hojaB = Hoja("Hoja derecha", 1);
            }
            else
            {
                if (_hojaA != null) Destroy(_hojaA.gameObject);
                if (_hojaB != null) Destroy(_hojaB.gameObject);
                _hojaA = _hojaB = null;
            }
            UnirMuralla(mapa);
        }

        private Transform Hoja(string nombre, int lado)
        {
            var pivote = new GameObject(nombre).transform;
            pivote.SetParent(transform, false);
            var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cubo.GetComponent<Collider>());
            cubo.transform.SetParent(pivote, false);
            return pivote;
        }

        private void MoverHojas()
        {
            if (!EsPuerta || _hojaA == null) return;
            bool abierta = !Derribado && Time.time < _abiertaHasta;
            float objetivo = abierta ? AnguloAbierta : 0f;
            if (Mathf.Abs(_angulo - objetivo) < 0.01f) return;
            _angulo = Mathf.MoveTowards(_angulo, objetivo, 240f * Time.deltaTime);
            // Las dos hojas giran hacia el mismo lado del muro, cada una sobre su gozne.
            _hojaA.localRotation = Quaternion.Euler(0f, _puertaEnX ? _angulo : -_angulo, 0f);
            _hojaB.localRotation = Quaternion.Euler(0f, _puertaEnX ? -_angulo : _angulo, 0f);
        }

        private void Update()
        {
            MoverHojas();
            AvanzarConstruccion(Time.deltaTime);
            if (Derribado) return; // un edificio derribado no produce hasta que termine el ataque
            if (Reconstruyendo || _reconstruirHasta > 0f)
            {
                ActualizarVisual();
                if (!Reconstruyendo) _reconstruirHasta = 0f;
            }
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

        /// <summary>
        /// Fuera del lago (Texcoco, en la orilla) la chinampa se ve rodeada de sus canales:
        /// se hacían en las zonas pantanosas de la ribera, abriendo zanjas y apilando lodo.
        /// </summary>
        private void CrearChinampaEnLaOrilla()
        {
            var canal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            canal.name = "Canal";
            Destroy(canal.GetComponent<Collider>());
            canal.transform.SetParent(transform, false);
            canal.transform.localScale = new Vector3(0.98f, 0.03f, 0.98f);
            canal.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            canal.GetComponent<Renderer>().material.color = new Color(0.28f, 0.50f, 0.58f);

            _base = 0.07f;
            _chinampaEnOrilla = true;
            float lado = Definicion.Tamano * 0.74f;
            var parcela = GameObject.CreatePrimitive(PrimitiveType.Cube);
            parcela.name = "Chinampa";
            Destroy(parcela.GetComponent<Collider>());
            parcela.transform.SetParent(transform, false);
            parcela.transform.localScale = new Vector3(lado, _base, lado);
            parcela.transform.localPosition = new Vector3(0f, _base * 0.5f, 0f);
            parcela.GetComponent<Renderer>().material.color = new Color(0.36f, 0.27f, 0.17f);

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

        /// <summary>Atenúa solo el cuerpo de los edificios altos; terreno, plataformas y murallas quedan sólidos.</summary>
        public void FijarOpacidadVista(float opacidad)
        {
            if (_materialModelo == null) return;
            bool transparente = opacidad < 0.999f && !EsMuralla && !Derribado && Definicion.Altura >= 1.2f;
            if (!transparente)
            {
                if (_vistaTransparente) _render.sharedMaterial = _materialModelo;
                _vistaTransparente = false;
                return;
            }
            if (_materialTransparente == null)
            {
                if (_faltaPlantillaTransparente) return;
                if (_plantillaTransparente == null)
                    _plantillaTransparente = Resources.Load<Material>("Materials/EdificioTransparente");
                if (_plantillaTransparente == null)
                {
                    // Si falta el recurso, conservar el material opaco en vez de mostrar uno inválido.
                    _faltaPlantillaTransparente = true;
                    Debug.LogError("Falta Resources/Materials/EdificioTransparente.mat; se mantiene el edificio opaco.", this);
                    return;
                }
                // El recurso incluye la variante transparente en el build; cada edificio tiene su copia.
                _materialTransparente = new Material(_plantillaTransparente);
                // El inspector de URP desactiva este pase al validar materiales transparentes.
                _materialTransparente.SetShaderPassEnabled("ShadowCaster", true);
            }
            var color = _materialModelo.color;
            color.a *= Mathf.Clamp01(opacidad);
            if (!_colorTransparenteAsignado || !_ultimoColorTransparente.Equals(color))
            {
                _materialTransparente.color = color;
                _ultimoColorTransparente = color;
                _colorTransparenteAsignado = true;
            }
            if (!_vistaTransparente) _render.sharedMaterial = _materialTransparente;
            _vistaTransparente = true;
        }

        private void OnDestroy()
        {
            if (_materialTransparente != null) Destroy(_materialTransparente);
            if (_materialModelo != null) Destroy(_materialModelo);
        }

        private bool EsMuralla => Definicion.Id == BuildingId.Muralla;

        /// <summary>
        /// Une esta muralla con las murallas vecinas al este y al norte (como en Clash, cada tramo
        /// se pega solo al de al lado). La de al oeste y la del sur se encargan del otro lado.
        /// </summary>
        public void UnirMuralla(GridMap mapa)
        {
            if (!EsMuralla) return;
            _uneEste = Union(_uneEste, mapa.En(Origen + new Vector2Int(1, 0)), "Muralla este");
            _uneNorte = Union(_uneNorte, mapa.En(Origen + new Vector2Int(0, 1)), "Muralla norte");
            // Una puerta se abre a lo largo del muro: de este a oeste si hay murallas a los lados.
            bool ladoX = _uneEste != null || VecinaMuralla(mapa, new Vector2Int(-1, 0));
            bool ladoZ = _uneNorte != null || VecinaMuralla(mapa, new Vector2Int(0, -1));
            _puertaEnX = ladoX || !ladoZ;
            ActualizarVisual();
        }

        /// <summary>Quita el edificio del mundo (al moverlo se vuelve a crear en otro lugar).</summary>
        public void Retirar()
        {
            _banco.AgregarCapacidad(-Definicion.CapacidadExtra * Nivel);
            Destroy(gameObject);
        }

        private bool VecinaMuralla(GridMap mapa, Vector2Int desfase)
        {
            var vecina = mapa.En(Origen + desfase);
            return vecina != null && vecina.EsMuralla;
        }

        private Transform Union(Transform actual, Building vecino, string nombre)
        {
            bool unir = vecino != null && vecino.EsMuralla;
            if (!unir)
            {
                if (actual != null) Destroy(actual.gameObject);
                return null;
            }
            if (actual != null) return actual;
            var tramo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tramo.name = nombre;
            Destroy(tramo.GetComponent<Collider>());
            tramo.transform.SetParent(transform, false);
            return tramo.transform;
        }

        private readonly List<Renderer> _partesSeleccion = new List<Renderer>();

        /// <summary>Comprueba el toque contra las piezas visibles, sin añadir colisiones al juego.</summary>
        public bool TryTocarFigura(Ray rayo, out float distancia)
        {
            distancia = float.PositiveInfinity;
            GetComponentsInChildren(false, _partesSeleccion);
            foreach (var parte in _partesSeleccion)
            {
                if (!parte.enabled) continue;
                var matriz = parte.transform.worldToLocalMatrix;
                var local = new Ray(matriz.MultiplyPoint3x4(rayo.origin), matriz.MultiplyVector(rayo.direction));
                if (!parte.localBounds.IntersectRay(local, out float entrada)) continue;
                var punto = parte.transform.TransformPoint(local.GetPoint(entrada));
                distancia = Mathf.Min(distancia, Vector3.Distance(rayo.origin, punto));
            }
            return !float.IsPositiveInfinity(distancia);
        }

        /// <summary>Volumen visible para señalar el edificio incluso cuando está detrás de otro.</summary>
        public Bounds LimitesSeleccion()
        {
            GetComponentsInChildren(false, _partesSeleccion);
            var limites = new Bounds(transform.position, Vector3.zero);
            foreach (var parte in _partesSeleccion)
                if (parte.enabled) limites.Encapsulate(parte.bounds);
            return limites;
        }

        private void ActualizarVisual()
        {
            // La muralla es un poste; los tramos hacia sus vecinas completan el muro.
            float lado = EsMuralla ? 0.55f : Definicion.Tamano * (_chinampaEnOrilla ? 0.66f : 0.9f);
            // Cada nivel hace el edificio un poco más alto.
            float alturaNivel = Definicion.Altura * (1f + 0.15f * (Mathf.Max(1, Nivel) - 1));

            float altura = alturaNivel;
            if (EnConstruccion && Nivel == 0)
            {
                float progreso = Mathf.Clamp01(1f - _segundosRestantes / Mathf.Max(_segundosTotales, 0.01f));
                altura = alturaNivel * Mathf.Lerp(0.2f, 1f, progreso);
            }
            altura = Mathf.Max(0.05f, altura);
            if (Derribado) altura = 0.08f; // quedan ruinas hasta que termine el ataque
            float levantado = 1f;
            if (_reconstruirHasta > 0f)
            {
                levantado = Mathf.Clamp01((Time.time - _reconstruirDesde) / SegundosReconstruccion);
                if (_seLevanta) altura = Mathf.Lerp(0.08f, altura, levantado);
            }

            _modelo.localScale = new Vector3(lado, altura, lado);
            _modelo.localPosition = new Vector3(0f, _base + altura * 0.5f, 0f);
            _materialModelo.color = EnConstruccion
                ? Color.Lerp(Color.gray, Definicion.Color, 0.4f)
                : Definicion.Color;
            if (Derribado) _materialModelo.color = new Color(0.25f, 0.22f, 0.2f);
            else if (levantado < 1f) _materialModelo.color = Color.Lerp(new Color(0.25f, 0.22f, 0.2f), _materialModelo.color, levantado);
            else if (DanoAsalto > 0f) _materialModelo.color = Color.Lerp(new Color(0.3f, 0.1f, 0.1f), _materialModelo.color, FraccionVidaAsalto);

            // Tramos: del borde de este poste al del vecino, un poco más bajos y delgados.
            float alturaTramo = altura * 0.85f;
            if (EsPuerta)
            {
                // En vez del poste, un dintel sobre el hueco y dos hojas de madera que giran sobre sus goznes
                // (los tramos de las vecinas llegan justo hasta los goznes).
                float gozne = lado * 0.5f;
                float grueso = 0.12f;
                _modelo.localScale = _puertaEnX ? new Vector3(lado + 0.2f, 0.1f, 0.3f) : new Vector3(0.3f, 0.1f, lado + 0.2f);
                _modelo.localPosition = new Vector3(0f, _base + alturaTramo + 0.05f, 0f);
                for (int i = 0; i < 2; i++)
                {
                    var pivote = i == 0 ? _hojaA : _hojaB;
                    if (pivote == null) continue;
                    int signo = i == 0 ? -1 : 1;
                    pivote.localPosition = _puertaEnX ? new Vector3(signo * gozne, _base, 0f) : new Vector3(0f, _base, signo * gozne);
                    var hoja = pivote.GetChild(0);
                    hoja.localScale = _puertaEnX ? new Vector3(gozne, alturaTramo * 0.9f, grueso) : new Vector3(grueso, alturaTramo * 0.9f, gozne);
                    hoja.localPosition = _puertaEnX ? new Vector3(-signo * gozne * 0.5f, alturaTramo * 0.45f, 0f) : new Vector3(0f, alturaTramo * 0.45f, -signo * gozne * 0.5f);
                    hoja.GetComponent<Renderer>().material.color = Derribado ? new Color(0.25f, 0.22f, 0.2f) : new Color(0.45f, 0.30f, 0.18f);
                }
            }
            if (_uneEste != null)
            {
                _uneEste.localScale = new Vector3(1f - lado, alturaTramo, 0.4f);
                _uneEste.localPosition = new Vector3(0.5f, _base + alturaTramo * 0.5f, 0f);
                _uneEste.GetComponent<Renderer>().material.color = _materialModelo.color;
            }
            if (_uneNorte != null)
            {
                _uneNorte.localScale = new Vector3(0.4f, alturaTramo, 1f - lado);
                _uneNorte.localPosition = new Vector3(0f, _base + alturaTramo * 0.5f, 0.5f);
                _uneNorte.GetComponent<Renderer>().material.color = _materialModelo.color;
            }
        }
    }
}
