using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Busca caminos por la cuadrícula de la aldea dándole la vuelta a los edificios (A*).
    /// La cuadrícula se extiende unas casillas fuera del mapa, donde no hay nada que estorbe,
    /// para poder llegar a las casitas y chinampas de los alrededores. Quien llama dice por
    /// qué edificios se puede pasar (la gente del pueblo cruza las puertas; los invasores, no).
    /// </summary>
    public sealed class Caminos
    {
        /// <summary>Lo que recuerda alguien que sigue una ruta: hacia dónde iba y por qué punto va.</summary>
        public sealed class Andante
        {
            private List<Vector3> _ruta;
            private Vector3 _hacia;
            private int _paso;
            private bool _calculada;

            /// <summary>La ruta en uso; null si va en línea recta (no había camino o se descartó).</summary>
            public List<Vector3> Ruta => _calculada ? _ruta : null;

            /// <summary>
            /// El siguiente punto al que caminar para llegar al destino desde donde está. La ruta se
            /// vuelve a buscar si el destino se movió más de la tolerancia.
            /// </summary>
            public Vector3 Siguiente(Caminos caminos, Vector3 desde, Vector3 destino, Pasa pasa, float tolerancia = 0f)
            {
                if (!_calculada || (_hacia - destino).sqrMagnitude > tolerancia * tolerancia)
                {
                    _hacia = destino;
                    _ruta = caminos.Buscar(desde, destino, pasa);
                    _paso = 0;
                    _calculada = true;
                }
                if (_ruta == null || _ruta.Count == 0) return destino;
                while (_paso < _ruta.Count - 1 && (_ruta[_paso] - desde).sqrMagnitude < 0.0025f) _paso++;
                return _paso == _ruta.Count - 1 ? destino : _ruta[_paso];
            }

            /// <summary>Sigue en línea recta hasta que cambie el destino (o se olvide la ruta).</summary>
            public void Descartar() => _ruta = null;

            /// <summary>Se volverá a buscar la ruta (después de un salto de lugar, o para revisar el camino).</summary>
            public void Olvidar() => _calculada = false;
        }

        public const int Margen = 8;
        private const float PasoDeMuestra = 0.2f; // cada cuánto se revisa una recta
        private const float Diagonal = 1.4142f;

        /// <summary>true si por ese edificio se puede caminar.</summary>
        public delegate bool Pasa(Building edificio);

        private readonly GridMap _mapa;
        private readonly int _ancho, _alto;
        private readonly float[] _costo;
        private readonly int[] _desde;
        private readonly int[] _visita;      // en qué búsqueda se tocó cada casilla
        private readonly bool[] _cerrado;
        private readonly List<int> _abiertos = new List<int>();
        private readonly List<float> _prioridad = new List<float>();
        private int _busqueda;

        public Caminos(GridMap mapa)
        {
            _mapa = mapa;
            _ancho = mapa.Ancho + Margen * 2;
            _alto = mapa.Alto + Margen * 2;
            int casillas = _ancho * _alto;
            _costo = new float[casillas];
            _desde = new int[casillas];
            _visita = new int[casillas];
            _cerrado = new bool[casillas];
        }

        /// <summary>
        /// Los puntos por donde pasar para llegar de un lugar a otro, el último es el destino exacto.
        /// null si no hay camino (por ejemplo, encerrados por una muralla sin puerta).
        /// Una recta despejada devuelve solo el destino.
        /// </summary>
        public List<Vector3> Buscar(Vector3 desde, Vector3 hasta, Pasa pasa)
        {
            var inicio = _mapa.MundoACasilla(desde);
            var fin = _mapa.MundoACasilla(hasta);
            if (Despejado(desde, hasta, pasa)) return new List<Vector3> { hasta };
            if (!EnCuadricula(inicio) || !EnCuadricula(fin)) return null;

            int origen = Indice(inicio), destino = Indice(fin);
            _busqueda++;
            _abiertos.Clear();
            _prioridad.Clear();
            Tocar(origen);
            _costo[origen] = 0f;
            _desde[origen] = -1;
            Meter(origen, Heuristica(inicio, fin));

            bool encontrado = false;
            while (_abiertos.Count > 0)
            {
                int actual = Sacar();
                if (actual == destino)
                {
                    encontrado = true;
                    break;
                }
                if (_cerrado[actual]) continue;
                _cerrado[actual] = true;
                var casilla = Casilla(actual);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var vecina = new Vector2Int(casilla.x + dx, casilla.y + dy);
                        if (!EnCuadricula(vecina)) continue;
                        if (!Libre(vecina, pasa, inicio, fin)) continue;
                        // En diagonal solo si no se roza una esquina ocupada.
                        if (dx != 0 && dy != 0
                            && (!Libre(new Vector2Int(casilla.x + dx, casilla.y), pasa, inicio, fin)
                                || !Libre(new Vector2Int(casilla.x, casilla.y + dy), pasa, inicio, fin))) continue;
                        int indice = Indice(vecina);
                        float costo = _costo[actual] + (dx != 0 && dy != 0 ? Diagonal : 1f);
                        if (_visita[indice] == _busqueda && costo >= _costo[indice]) continue;
                        Tocar(indice);
                        _costo[indice] = costo;
                        _desde[indice] = actual;
                        Meter(indice, costo + Heuristica(vecina, fin));
                    }
                }
            }
            if (!encontrado) return null;

            // Del destino al origen, casilla por casilla; luego se enderezan los tramos que ya están despejados.
            var casillas = new List<Vector3>();
            for (int i = destino; i != origen && i >= 0; i = _desde[i])
            {
                var c = Casilla(i);
                casillas.Add(new Vector3(c.x + 0.5f, 0f, c.y + 0.5f));
            }
            casillas.Reverse();
            if (casillas.Count > 0) casillas[casillas.Count - 1] = hasta;
            else casillas.Add(hasta);
            return Enderezar(desde, casillas, pasa);
        }

        /// <summary>Largo de la ruta en casillas, empezando desde un punto.</summary>
        public static float Largo(Vector3 desde, List<Vector3> ruta)
        {
            float largo = 0f;
            Vector3 anterior = desde;
            foreach (var punto in ruta)
            {
                largo += Vector3.Distance(anterior, punto);
                anterior = punto;
            }
            return largo;
        }

        /// <summary>¿Se puede ir en línea recta? Las casillas de salida y de llegada no cuentan.</summary>
        public bool Despejado(Vector3 desde, Vector3 hasta, Pasa pasa)
        {
            var inicio = _mapa.MundoACasilla(desde);
            var fin = _mapa.MundoACasilla(hasta);
            Vector3 hacia = hasta - desde;
            hacia.y = 0f;
            float largo = hacia.magnitude;
            if (largo < 0.001f) return true;
            Vector3 direccion = hacia / largo;
            for (float t = 0f; t < largo; t += PasoDeMuestra)
            {
                if (!Libre(_mapa.MundoACasilla(desde + direccion * t), pasa, inicio, fin)) return false;
            }
            return true;
        }

        private List<Vector3> Enderezar(Vector3 desde, List<Vector3> casillas, Pasa pasa)
        {
            // Desde cada punto se avanza hasta la última casilla que todavía se ve en recta.
            var ruta = new List<Vector3>();
            Vector3 actual = desde;
            int i = 0;
            while (i < casillas.Count)
            {
                int alcanza = i;
                while (alcanza + 1 < casillas.Count && Despejado(actual, casillas[alcanza + 1], pasa)) alcanza++;
                actual = casillas[alcanza];
                ruta.Add(actual);
                i = alcanza + 1;
            }
            return ruta;
        }

        private bool Libre(Vector2Int casilla, Pasa pasa, Vector2Int inicio, Vector2Int fin)
        {
            if (casilla == inicio || casilla == fin) return true;
            if (!_mapa.DentroDelMapa(casilla)) return EnCuadricula(casilla);
            var edificio = _mapa.En(casilla);
            return edificio == null || pasa(edificio);
        }

        private bool EnCuadricula(Vector2Int casilla)
        {
            return casilla.x >= -Margen && casilla.y >= -Margen
                   && casilla.x < _mapa.Ancho + Margen && casilla.y < _mapa.Alto + Margen;
        }

        private int Indice(Vector2Int casilla) => (casilla.x + Margen) + (casilla.y + Margen) * _ancho;

        private Vector2Int Casilla(int indice) => new Vector2Int(indice % _ancho - Margen, indice / _ancho - Margen);

        private static float Heuristica(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return Mathf.Max(dx, dy) + (Diagonal - 1f) * Mathf.Min(dx, dy);
        }

        private void Tocar(int indice)
        {
            if (_visita[indice] == _busqueda) return;
            _visita[indice] = _busqueda;
            _cerrado[indice] = false;
            _costo[indice] = float.MaxValue;
        }

        // Montón binario de casillas abiertas, la de menor prioridad al frente.
        private void Meter(int indice, float prioridad)
        {
            _abiertos.Add(indice);
            _prioridad.Add(prioridad);
            int i = _abiertos.Count - 1;
            while (i > 0)
            {
                int padre = (i - 1) / 2;
                if (_prioridad[padre] <= _prioridad[i]) break;
                Intercambiar(i, padre);
                i = padre;
            }
        }

        private int Sacar()
        {
            int primero = _abiertos[0];
            int ultimo = _abiertos.Count - 1;
            _abiertos[0] = _abiertos[ultimo];
            _prioridad[0] = _prioridad[ultimo];
            _abiertos.RemoveAt(ultimo);
            _prioridad.RemoveAt(ultimo);
            int i = 0;
            while (true)
            {
                int izq = i * 2 + 1, der = izq + 1, menor = i;
                if (izq < _abiertos.Count && _prioridad[izq] < _prioridad[menor]) menor = izq;
                if (der < _abiertos.Count && _prioridad[der] < _prioridad[menor]) menor = der;
                if (menor == i) break;
                Intercambiar(i, menor);
                i = menor;
            }
            return primero;
        }

        private void Intercambiar(int a, int b)
        {
            int indice = _abiertos[a];
            _abiertos[a] = _abiertos[b];
            _abiertos[b] = indice;
            float prioridad = _prioridad[a];
            _prioridad[a] = _prioridad[b];
            _prioridad[b] = prioridad;
        }
    }
}
