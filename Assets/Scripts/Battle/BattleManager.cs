using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    public sealed class ResultadoBatalla
    {
        public float Porcentaje;   // 0..1 de edificios destruidos
        public bool Victoria;      // al menos la mitad destruida
        public int[] Botin = ResourceInfo.Costo();
        public int Plumas;         // solo en la primera victoria de cada nivel
        public bool TecpanDestruido;
        public int Cautivos;       // mamaltin capturados (ya incluidos en Botin)
        public int Regresan;       // tropas desplegadas que sobrevivieron
        public int[] Ascensos = new int[Rangos.Count]; // cuántas subieron a cada rango
    }

    /// <summary>
    /// Una batalla contra una aldea de la campaña. Se arma lejos de la aldea del jugador
    /// (que sigue produciendo mientras tanto) y la cámara se mueve hasta aquí.
    /// </summary>
    public sealed class BattleManager : MonoBehaviour
    {
        public static readonly Vector3 Origen = new Vector3(100f, 0f, 0f);
        public const float SegundosLimite = 120f;
        public const float VictoriaMinima = 0.5f;
        private const int MargenDespliegue = 3; // casillas alrededor del mapa donde también se puede desplegar

        public int IndiceNivel { get; private set; }
        public CampaignLevel Nivel { get; private set; }
        public bool Terminada { get; private set; }
        public ResultadoBatalla Resultado { get; private set; }
        public float TiempoRestante { get; private set; }
        public TroopId Seleccionada { get; set; }

        public Vector3 Centro => Origen + new Vector3(CampaignLevel.TamanoMapa * 0.5f, 0f, CampaignLevel.TamanoMapa * 0.5f);

        /// <summary>Si cae el tecpan, la ciudad se rinde.</summary>
        public bool TecpanDestruido { get; private set; }
        public IReadOnlyList<EnemyBuilding> Edificios => _edificios;
        public IReadOnlyList<TroopUnit> Tropas => _tropas;

        public float Destruccion
        {
            get
            {
                if (TecpanDestruido || _edificios.Count == 0) return 1f;
                int destruidos = 0;
                foreach (var edificio in _edificios)
                {
                    if (edificio.Destruido) destruidos++;
                }
                return (float)destruidos / _edificios.Count;
            }
        }

        private readonly List<EnemyBuilding> _edificios = new List<EnemyBuilding>();
        private readonly List<TroopUnit> _tropas = new List<TroopUnit>();
        private bool[,] _ocupado;
        private GameManager _manager;
        private Army _ejercito;
        // Tropas que se llevaron a esta batalla, por tipo y rango; lo que se entrene mientras tanto
        // se queda en la aldea.
        private readonly int[,] _reserva = new int[TroopCatalog.Count, Rangos.Count];
        private int _cautivos;

        public int Cautivos => _cautivos;
        public int Disponibles(TroopId id, int rango) => _reserva[(int)id, rango];

        public int Disponibles(TroopId id)
        {
            int total = 0;
            for (int r = 0; r < Rangos.Count; r++) total += _reserva[(int)id, r];
            return total;
        }

        public int TotalDisponibles
        {
            get
            {
                int total = 0;
                for (int t = 0; t < TroopCatalog.Count; t++) total += Disponibles((TroopId)t);
                return total;
            }
        }
        private bool _algunaDesplegada;

        public void Empezar(GameManager manager, int indiceNivel)
        {
            _manager = manager;
            _ejercito = manager.Ejercito;
            foreach (var definicion in TroopCatalog.Todos)
            {
                for (int r = 0; r < Rangos.Count; r++)
                {
                    while (_ejercito.Quitar(definicion.Id, r)) _reserva[(int)definicion.Id, r]++;
                }
            }
            IndiceNivel = indiceNivel;
            Nivel = CampaignLevel.Todos[indiceNivel];
            TiempoRestante = SegundosLimite;
            Seleccionada = PrimeraTropaDisponible();

            int tamano = CampaignLevel.TamanoMapa;
            _ocupado = new bool[tamano, tamano];

            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo enemigo";
            Destroy(suelo.GetComponent<Collider>());
            float lado = tamano + MargenDespliegue * 2;
            suelo.transform.SetParent(transform, false);
            suelo.transform.localScale = new Vector3(lado / 10f, 1f, lado / 10f);
            suelo.transform.position = Centro;
            suelo.GetComponent<Renderer>().material.color = new Color(0.55f, 0.50f, 0.36f);

            foreach (var datos in Nivel.Edificios)
            {
                var definicion = BuildingCatalog.Get(datos.Id);
                var edificio = new GameObject().AddComponent<EnemyBuilding>();
                edificio.transform.SetParent(transform, false);
                var centro = Origen + new Vector3(datos.X + definicion.Tamano * 0.5f, 0f, datos.Y + definicion.Tamano * 0.5f);
                edificio.Inicializar(definicion, datos.Nivel, centro, this);
                _edificios.Add(edificio);

                for (int x = datos.X; x < datos.X + definicion.Tamano; x++)
                {
                    for (int y = datos.Y; y < datos.Y + definicion.Tamano; y++)
                    {
                        if (x >= 0 && y >= 0 && x < tamano && y < tamano) _ocupado[x, y] = true;
                    }
                }
            }
        }

        public TroopId PrimeraTropaDisponible()
        {
            foreach (var definicion in TroopCatalog.Todos)
            {
                if (Disponibles(definicion.Id) > 0) return definicion.Id;
            }
            return TroopId.Macuahuitl;
        }

        /// <summary>Despliega la tropa seleccionada en ese punto del mundo, si se puede.</summary>
        public void Desplegar(Vector3 punto)
        {
            if (Terminada) return;

            int x = Mathf.FloorToInt(punto.x - Origen.x);
            int y = Mathf.FloorToInt(punto.z - Origen.z);
            int tamano = CampaignLevel.TamanoMapa;
            if (x < -MargenDespliegue || y < -MargenDespliegue
                || x >= tamano + MargenDespliegue || y >= tamano + MargenDespliegue) return;
            if (x >= 0 && y >= 0 && x < tamano && y < tamano && _ocupado[x, y])
            {
                _manager.MostrarMensaje("No puedes desplegar encima de un edificio");
                return;
            }
            // Salen primero los de mayor rango.
            int rango = -1;
            for (int r = Rangos.Count - 1; r >= 0; r--)
            {
                if (_reserva[(int)Seleccionada, r] > 0)
                {
                    rango = r;
                    break;
                }
            }
            if (rango < 0)
            {
                _manager.MostrarMensaje("No te quedan tropas de ese tipo");
                return;
            }

            _reserva[(int)Seleccionada, rango]--;
            var tropa = new GameObject().AddComponent<TroopUnit>();
            tropa.transform.SetParent(transform, false);
            tropa.Inicializar(TroopCatalog.Get(Seleccionada), rango, _manager.Pueblo, _manager.Culto, new Vector3(punto.x, 0f, punto.z), this);
            _tropas.Add(tropa);
            _algunaDesplegada = true;

            if (Disponibles(Seleccionada) == 0) Seleccionada = PrimeraTropaDisponible();
        }

        private void Update()
        {
            if (Terminada) return;

            TiempoRestante -= Time.deltaTime;
            bool todoDestruido = Destruccion >= 1f;
            bool sinTropas = _algunaDesplegada && _tropas.Count == 0 && TotalDisponibles == 0;
            if (TiempoRestante <= 0f || todoDestruido || sinTropas) Terminar();
        }

        /// <summary>Termina la batalla (también al retirarse) y entrega el resultado.</summary>
        public void Terminar()
        {
            if (Terminada) return;
            Terminada = true;
            TiempoRestante = Mathf.Max(0f, TiempoRestante);

            float porcentaje = Destruccion;
            var resultado = new ResultadoBatalla
            {
                Porcentaje = porcentaje,
                Victoria = porcentaje >= VictoriaMinima,
                TecpanDestruido = TecpanDestruido,
                Cautivos = _cautivos,
            };
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                resultado.Botin[i] = Mathf.FloorToInt(Nivel.Botin[i] * porcentaje);
            }
            resultado.Botin[(int)ResourceType.Cautivos] = _cautivos;

            // Las tropas que no se desplegaron regresan tal cual; las que sobrevivieron, con su nuevo rango.
            DevolverReserva();
            foreach (var tropa in _tropas)
            {
                if (tropa == null || tropa.Muerta) continue;
                int nuevo = Rangos.AlRegresar(tropa.Rango, tropa.Capturas);
                if (nuevo > tropa.Rango) resultado.Ascensos[nuevo]++;
                _ejercito.Agregar(tropa.Definicion.Id, nuevo, 1);
                resultado.Regresan++;
            }
            Resultado = resultado;
            _manager.AlTerminarBatalla(this);
        }

        /// <summary>Suma (en el arreglo por tipo y rango del guardado) las tropas que aún volverían.</summary>
        public void SumarTropasPendientes(int[] porRango)
        {
            for (int t = 0; t < TroopCatalog.Count; t++)
            {
                for (int r = 0; r < Rangos.Count; r++) porRango[t * Rangos.Count + r] += _reserva[t, r];
            }
            foreach (var tropa in _tropas)
            {
                if (tropa == null || tropa.Muerta) continue;
                porRango[(int)tropa.Definicion.Id * Rangos.Count + tropa.Rango]++;
            }
        }

        private void DevolverReserva()
        {
            for (int t = 0; t < TroopCatalog.Count; t++)
            {
                for (int r = 0; r < Rangos.Count; r++)
                {
                    _ejercito.Agregar((TroopId)t, r, _reserva[t, r]);
                    _reserva[t, r] = 0;
                }
            }
        }

        public EnemyBuilding ObjetivoPara(Vector3 desde, bool prefiereDefensas)
        {
            if (prefiereDefensas)
            {
                var defensa = MasCercano(desde, soloDefensas: true);
                if (defensa != null) return defensa;
            }
            return MasCercano(desde, soloDefensas: false);
        }

        private EnemyBuilding MasCercano(Vector3 desde, bool soloDefensas)
        {
            EnemyBuilding mejor = null;
            float mejorDistancia = float.MaxValue;
            foreach (var edificio in _edificios)
            {
                if (edificio.Destruido || (soloDefensas && !edificio.EsDefensa)) continue;
                float distancia = (edificio.transform.position - desde).sqrMagnitude;
                if (distancia < mejorDistancia)
                {
                    mejorDistancia = distancia;
                    mejor = edificio;
                }
            }
            return mejor;
        }

        public TroopUnit TropaMasCercana(Vector3 desde, float alcance)
        {
            TroopUnit mejor = null;
            float mejorDistancia = alcance * alcance;
            foreach (var tropa in _tropas)
            {
                if (tropa == null || tropa.Muerta) continue;
                float distancia = (tropa.transform.position - desde).sqrMagnitude;
                if (distancia <= mejorDistancia)
                {
                    mejorDistancia = distancia;
                    mejor = tropa;
                }
            }
            return mejor;
        }

        /// <summary>Quien derriba un edificio puede hacer una captura y traer un malli (cautivo).</summary>
        public void AlDestruirEdificio(EnemyBuilding edificio, TroopUnit atacante)
        {
            if (atacante != null && !atacante.Muerta && Random.value < Rangos.ProbabilidadCaptura(_manager.Pueblo))
            {
                atacante.Capturas++;
                _cautivos++;
            }

            if (edificio.Definicion.Id != BuildingId.Tecpan) return;
            // Al caer el tecpan la ciudad se rinde: victoria con todo el botín.
            TecpanDestruido = true;
            Terminar();
        }

        public void AlMorirTropa(TroopUnit tropa)
        {
            _tropas.Remove(tropa);
            _manager.AlCaerTropa(tropa.Definicion);
        }
    }
}
