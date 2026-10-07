using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Muestra el ejército en la aldea: las tropas sanas esperan alrededor de los calpulli
    /// y las heridas descansan acostadas junto al temazcalli (o en el calpulli si no hay).
    /// Cada monito tiene el color de su tipo y es más grande según su rango.
    /// Las recién entrenadas salen del telpochcalli, las que vuelven de batalla entran por la
    /// orilla de la aldea y las que sanan se levantan y caminan del temazcalli al calpulli.
    /// </summary>
    public sealed class TropasEnAldea : MonoBehaviour
    {
        private const float Separacion = 0.4f;   // distancia entre monitos
        private const float Margen = 0.3f;       // distancia del borde del edificio a la primera fila
        private const int Filas = 3;             // filas alrededor de cada edificio
        private const float Velocidad = 1.4f;    // casillas por segundo al caminar

        private sealed class Monito
        {
            public TroopId Tipo;
            public int Rango;
            public bool Herido;
            public Transform Cuerpo;
            public Vector3 Destino;
            public float Fase;
            public float Tamano;
        }

        public GameManager Manager;

        private readonly List<Monito> _monitos = new List<Monito>();
        private string _firma = "";
        private bool _volviendoDeBatalla;
        private bool _colocados; // al abrir el juego aparecen ya en su lugar, sin caminar

        private void Update()
        {
            if (Manager == null || Manager.Pueblo == null) return;

            // Durante la batalla la aldea no se ve; al volver, las tropas entran caminando.
            if (Manager.ModoActual == GameManager.Modo.Batalla)
            {
                _volviendoDeBatalla = true;
            }
            else
            {
                string firma = Firma();
                if (firma != _firma)
                {
                    _firma = firma;
                    Reacomodar();
                }
                _volviendoDeBatalla = false;
            }

            foreach (var monito in _monitos) Mover(monito);
        }

        private void Mover(Monito monito)
        {
            var cuerpo = monito.Cuerpo;
            Vector3 actual = cuerpo.localPosition;
            Vector3 plano = new Vector3(actual.x, 0f, actual.z);
            Vector3 hacia = monito.Destino - plano;
            bool caminando = hacia.sqrMagnitude > 0.0004f;

            if (caminando)
            {
                float paso = Velocidad * Time.deltaTime;
                plano = hacia.magnitude <= paso ? monito.Destino : plano + hacia.normalized * paso;
            }

            // Los heridos se acuestan al llegar; los sanos se balancean un poco (más al caminar).
            bool acostado = monito.Herido && !caminando;
            cuerpo.localRotation = acostado ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(0f, 0f, 0f);
            float altura = acostado ? monito.Tamano * 0.5f : monito.Tamano;
            if (!acostado)
            {
                float ritmo = caminando ? 9f : 2.5f;
                float salto = caminando ? 0.06f : (monito.Herido ? 0f : 0.04f);
                altura += Mathf.Abs(Mathf.Sin(Time.time * ritmo + monito.Fase)) * salto;
            }
            cuerpo.localPosition = new Vector3(plano.x, altura, plano.z);
        }

        /// <summary>Cambia cuando cambian las tropas o los edificios donde se paran.</summary>
        private string Firma()
        {
            var texto = new StringBuilder();
            var ejercito = Manager.Ejercito;
            foreach (var tropa in TroopCatalog.Todos)
            {
                for (int r = 0; r < Rangos.Count; r++)
                {
                    texto.Append(ejercito.Get(tropa.Id, r)).Append(',').Append(ejercito.HeridosDe(tropa.Id, r)).Append(';');
                }
            }
            foreach (var edificio in Manager.Edificios)
            {
                if (edificio.Nivel <= 0) continue;
                var def = edificio.Definicion;
                if (def.CapacidadTropas > 0 || def.CamasCuracion > 0 || def.Entrena)
                {
                    texto.Append(edificio.Origen.x).Append('.').Append(edificio.Origen.y).Append('|');
                }
            }
            return texto.ToString();
        }

        /// <summary>Asigna a cada tropa su lugar, reutilizando los monitos que ya están en la aldea.</summary>
        private void Reacomodar()
        {
            var calpullis = new List<Building>();
            var temazcallis = new List<Building>();
            Building telpochcalli = null;
            foreach (var edificio in Manager.Edificios)
            {
                if (edificio.Nivel <= 0) continue;
                if (edificio.Definicion.CapacidadTropas > 0) calpullis.Add(edificio);
                if (edificio.Definicion.CamasCuracion > 0) temazcallis.Add(edificio);
                if (edificio.Definicion.Entrena && telpochcalli == null) telpochcalli = edificio;
            }

            var lugaresCalpulli = Lugares(calpullis);
            var lugaresTemazcalli = temazcallis.Count > 0 ? Lugares(temazcallis) : null;
            int siguienteCalpulli = 0;
            int siguienteTemazcalli = 0;

            // Lo que debería haber: tipo, rango, si está herido y dónde se para.
            var deseados = new List<Monito>();
            var ejercito = Manager.Ejercito;
            // Primero los de mayor rango, para que siempre se vean si no caben todos.
            for (int r = Rangos.Count - 1; r >= 0; r--)
            {
                foreach (var tropa in TroopCatalog.Todos)
                {
                    int sanos = ejercito.Get(tropa.Id, r);
                    for (int n = 0; n < sanos && siguienteCalpulli < lugaresCalpulli.Count; n++)
                    {
                        deseados.Add(new Monito { Tipo = tropa.Id, Rango = r, Destino = lugaresCalpulli[siguienteCalpulli++] });
                    }
                }
            }
            foreach (var herido in ejercito.Heridos)
            {
                Vector3 lugar;
                if (lugaresTemazcalli != null)
                {
                    if (siguienteTemazcalli >= lugaresTemazcalli.Count) continue;
                    lugar = lugaresTemazcalli[siguienteTemazcalli++];
                }
                else
                {
                    if (siguienteCalpulli >= lugaresCalpulli.Count) continue;
                    lugar = lugaresCalpulli[siguienteCalpulli++];
                }
                deseados.Add(new Monito { Tipo = herido.tipo, Rango = herido.rango, Herido = true, Destino = lugar });
            }

            var libres = new List<Monito>(_monitos);
            var nuevos = new List<Monito>();
            var faltan = new List<Monito>();

            // 1. El mismo monito sigue igual (quizá cambia de lugar).
            foreach (var deseado in deseados)
            {
                var existente = Tomar(libres, deseado.Tipo, deseado.Rango, deseado.Herido);
                if (existente == null)
                {
                    faltan.Add(deseado);
                    continue;
                }
                existente.Destino = deseado.Destino;
                nuevos.Add(existente);
            }
            // 2. Un herido que sanó se levanta y camina al calpulli.
            for (int i = faltan.Count - 1; i >= 0; i--)
            {
                var deseado = faltan[i];
                var existente = Tomar(libres, deseado.Tipo, deseado.Rango, !deseado.Herido);
                if (existente == null) continue;
                existente.Herido = deseado.Herido;
                existente.Destino = deseado.Destino;
                existente.Cuerpo.GetComponent<Renderer>().material.color = ColorDe(existente);
                nuevos.Add(existente);
                faltan.RemoveAt(i);
            }
            // 3. Los que llegan: de la batalla por la orilla, los recién entrenados del telpochcalli.
            int llegada = 0;
            foreach (var deseado in faltan)
            {
                Vector3 inicio;
                if (!_colocados)
                {
                    inicio = deseado.Destino;
                }
                else if (_volviendoDeBatalla || deseado.Herido || telpochcalli == null)
                {
                    inicio = Entrada(llegada++);
                }
                else
                {
                    float frente = telpochcalli.Definicion.Tamano * 0.5f + 0.2f;
                    inicio = telpochcalli.transform.position + new Vector3(0f, 0f, -frente);
                }
                Crear(deseado, inicio);
                nuevos.Add(deseado);
            }
            // Los que ya no están (se fueron a batalla) desaparecen.
            foreach (var sobra in libres)
            {
                if (sobra.Cuerpo != null) Destroy(sobra.Cuerpo.gameObject);
            }

            _monitos.Clear();
            _monitos.AddRange(nuevos);
            _colocados = true;
        }

        private static Monito Tomar(List<Monito> libres, TroopId tipo, int rango, bool herido)
        {
            for (int i = 0; i < libres.Count; i++)
            {
                var m = libres[i];
                if (m.Tipo != tipo || m.Rango != rango || m.Herido != herido) continue;
                libres.RemoveAt(i);
                return m;
            }
            return null;
        }

        /// <summary>Punto de entrada a la aldea: la esquina más cercana a la cámara, en fila.</summary>
        private static Vector3 Entrada(int orden)
        {
            float atras = 0.35f * (orden / 3);
            float lado = 0.35f * (orden % 3);
            return new Vector3(-0.3f - atras + lado, 0f, -0.3f - atras - lado);
        }

        /// <summary>Puntos alrededor de los edificios, fila por fila.</summary>
        private static List<Vector3> Lugares(List<Building> edificios)
        {
            var lugares = new List<Vector3>();
            foreach (var edificio in edificios)
            {
                Vector3 centro = edificio.transform.position;
                centro.y = 0f;
                for (int fila = 0; fila < Filas; fila++)
                {
                    float mitad = edificio.Definicion.Tamano * 0.5f + Margen + Separacion * fila;
                    int porLado = Mathf.Max(1, Mathf.RoundToInt(mitad * 2f / Separacion));
                    float paso = mitad * 2f / porLado;
                    // Empieza por el frente (el lado que mira a la cámara) y sigue alrededor.
                    for (int i = 0; i < porLado; i++) lugares.Add(centro + new Vector3(-mitad + paso * i, 0f, -mitad));
                    for (int i = 0; i < porLado; i++) lugares.Add(centro + new Vector3(mitad, 0f, -mitad + paso * i));
                    for (int i = 0; i < porLado; i++) lugares.Add(centro + new Vector3(mitad - paso * i, 0f, mitad));
                    for (int i = 0; i < porLado; i++) lugares.Add(centro + new Vector3(-mitad, 0f, mitad - paso * i));
                }
            }
            return lugares;
        }

        /// <summary>Los heridos se ven más apagados.</summary>
        private static Color ColorDe(Monito monito)
        {
            var color = TroopCatalog.Get(monito.Tipo).Color;
            return monito.Herido ? Color.Lerp(color, new Color(0.45f, 0.42f, 0.40f), 0.5f) : color;
        }

        private void Crear(Monito monito, Vector3 inicio)
        {
            var tropa = TroopCatalog.Get(monito.Tipo);
            var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cuerpo.name = tropa.Nombre;
            Destroy(cuerpo.GetComponent<Collider>()); // que no estorbe al tocar los edificios
            cuerpo.transform.SetParent(transform, false);

            monito.Tamano = 0.18f * (1f + 0.12f * monito.Rango);
            cuerpo.transform.localScale = new Vector3(monito.Tamano, monito.Tamano, monito.Tamano);
            cuerpo.transform.localPosition = new Vector3(inicio.x, monito.Tamano, inicio.z);

            cuerpo.GetComponent<Renderer>().material.color = ColorDe(monito);

            monito.Cuerpo = cuerpo.transform;
            monito.Fase = Random.value * Mathf.PI * 2f;
        }
    }
}
