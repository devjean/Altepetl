using System;
using System.Collections.Generic;

namespace Altepetl
{
    /// <summary>Una tropa que regresó herida de batalla. Vida va de 0 a 1 (fracción de su vida máxima).</summary>
    [Serializable]
    public sealed class Herido
    {
        public TroopId tipo;
        public int rango;
        public float vida;

        public Herido() { }

        public Herido(TroopId tipo, int rango, float vida)
        {
            this.tipo = tipo;
            this.rango = rango;
            this.vida = vida;
        }
    }

    /// <summary>Cuántas tropas sanas y heridas de cada tipo y rango se llevan a una batalla.</summary>
    public sealed class SeleccionEjercito
    {
        public readonly int[] Sanos = new int[TroopCatalog.Count * Rangos.Count];
        public readonly int[] Heridos = new int[TroopCatalog.Count * Rangos.Count];

        public int Total
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Sanos.Length; i++) total += Sanos[i] + Heridos[i];
                return total;
            }
        }
    }

    /// <summary>
    /// Tropas listas para atacar, separadas por tipo y rango, las heridas que se curan en el
    /// temazcalli y la cola de entrenamiento. Cada tropa (sana o herida) ocupa 1 de espacio.
    /// Las recién entrenadas son jóvenes guerreros.
    /// </summary>
    public sealed class Army
    {
        /// <summary>
        /// Curar a una tropa desde casi muerta tarda 1.5 veces lo que tarda entrenar una nueva,
        /// y un 25 % más por cada rango.
        /// </summary>
        public const float FactorCuracion = 1.5f;
        public const float CuracionExtraPorRango = 0.25f;

        private readonly int[] _tropas = new int[TroopCatalog.Count * Rangos.Count];
        private readonly List<Herido> _heridos = new List<Herido>();
        private readonly List<TroopId> _cola = new List<TroopId>();
        private float _restante; // segundos que le faltan a la primera de la cola

        public static int Indice(TroopId id, int rango) => (int)id * Rangos.Count + rango;

        /// <summary>Segundos para curar a una tropa de ese tipo y rango desde 0 hasta toda su vida.</summary>
        public static float SegundosCuracionCompleta(TroopId id, int rango)
        {
            return TroopCatalog.Get(id).SegundosEntrenamiento * FactorCuracion * (1f + CuracionExtraPorRango * rango);
        }

        public int Get(TroopId id, int rango) => _tropas[Indice(id, rango)];

        /// <summary>Todas las tropas de ese tipo, de cualquier rango.</summary>
        public int Get(TroopId id)
        {
            int total = 0;
            for (int r = 0; r < Rangos.Count; r++) total += Get(id, r);
            return total;
        }

        /// <summary>Tropas de ese rango, de cualquier tipo.</summary>
        public int ConRango(int rango)
        {
            int total = 0;
            for (int t = 0; t < TroopCatalog.Count; t++) total += Get((TroopId)t, rango);
            return total;
        }

        /// <summary>Tropas sanas.</summary>
        public int Sanos
        {
            get
            {
                int total = 0;
                foreach (int n in _tropas) total += n;
                return total;
            }
        }

        /// <summary>Tropas sanas y heridas.</summary>
        public int Total => Sanos + _heridos.Count;
        public IReadOnlyList<Herido> Heridos => _heridos;

        public int HeridosDe(TroopId id, int rango)
        {
            int total = 0;
            foreach (var herido in _heridos)
            {
                if (herido.tipo == id && herido.rango == rango) total++;
            }
            return total;
        }

        public int HeridosDe(TroopId id)
        {
            int total = 0;
            foreach (var herido in _heridos)
            {
                if (herido.tipo == id) total++;
            }
            return total;
        }
        public int EnCola => _cola.Count;
        public int Espacio => Total + EnCola;
        public bool Entrenando => _cola.Count > 0;
        public TroopId Actual => _cola[0];
        public float SegundosRestantes => _restante;
        public IReadOnlyList<TroopId> Cola => _cola;

        public void Encolar(TroopId id, float segundos)
        {
            if (_cola.Count == 0) _restante = segundos;
            _cola.Add(id);
        }

        public void Agregar(TroopId id, int rango, int cantidad)
        {
            if (cantidad > 0) _tropas[Indice(id, rango)] += cantidad;
        }

        /// <summary>Saca una tropa de ese tipo y rango. Devuelve false si no había.</summary>
        public bool Quitar(TroopId id, int rango)
        {
            int i = Indice(id, rango);
            if (_tropas[i] <= 0) return false;
            _tropas[i]--;
            return true;
        }

        /// <summary>Una tropa que vuelve de batalla: sana si trae toda su vida, si no, herida.</summary>
        public void Regresar(TroopId id, int rango, float vida)
        {
            if (vida >= 0.999f) Agregar(id, rango, 1);
            else _heridos.Add(new Herido(id, rango, Math.Max(0.01f, vida)));
        }

        /// <summary>Saca hasta cantidad heridos de ese tipo y rango, los de más vida primero.</summary>
        public List<Herido> SacarHeridos(TroopId id, int rango, int cantidad)
        {
            var sacados = new List<Herido>();
            while (sacados.Count < cantidad)
            {
                Herido mejor = null;
                foreach (var herido in _heridos)
                {
                    if (herido.tipo != id || herido.rango != rango) continue;
                    if (mejor == null || herido.vida > mejor.vida) mejor = herido;
                }
                if (mejor == null) break;
                _heridos.Remove(mejor);
                sacados.Add(mejor);
            }
            return sacados;
        }

        /// <summary>Vida promedio (0..1) de los cantidad heridos más sanos de ese tipo y rango.</summary>
        public float VidaPromedioHeridos(TroopId id, int rango, int cantidad)
        {
            var vidas = new List<float>();
            foreach (var herido in _heridos)
            {
                if (herido.tipo == id && herido.rango == rango) vidas.Add(herido.vida);
            }
            if (vidas.Count == 0 || cantidad <= 0) return 0f;
            vidas.Sort((a, b) => b.CompareTo(a));
            int n = Math.Min(cantidad, vidas.Count);
            float suma = 0f;
            for (int i = 0; i < n; i++) suma += vidas[i];
            return suma / n;
        }

        /// <summary>
        /// Cura a los primeros heridos, tantos como camas haya en los temazcallis.
        /// Devuelve cuántos quedaron sanos.
        /// </summary>
        public int Curar(float segundos, int camas)
        {
            int curados = 0;
            while (segundos > 0f && camas > 0 && _heridos.Count > 0)
            {
                int atendidos = Math.Min(camas, _heridos.Count);
                // Avanza hasta que el primero de los atendidos sane (o se acabe el tiempo).
                float paso = segundos;
                for (int i = 0; i < atendidos; i++)
                {
                    var h = _heridos[i];
                    paso = Math.Min(paso, (1f - h.vida) * SegundosCuracionCompleta(h.tipo, h.rango));
                }
                paso = Math.Max(paso, 0f);
                int antes = curados;
                for (int i = atendidos - 1; i >= 0; i--)
                {
                    var h = _heridos[i];
                    h.vida += paso / SegundosCuracionCompleta(h.tipo, h.rango);
                    if (h.vida >= 0.999f)
                    {
                        _heridos.RemoveAt(i);
                        Agregar(h.tipo, h.rango, 1);
                        curados++;
                    }
                }
                segundos -= paso;
                if (paso <= 0f && curados == antes) break;
            }
            return curados;
        }

        /// <summary>Segundos para que sane el siguiente herido que está en una cama (0 si no hay).</summary>
        public float SegundosParaSiguienteCurado(int camas)
        {
            float menor = 0f;
            int atendidos = Math.Min(camas, _heridos.Count);
            for (int i = 0; i < atendidos; i++)
            {
                var h = _heridos[i];
                float s = (1f - h.vida) * SegundosCuracionCompleta(h.tipo, h.rango);
                if (i == 0 || s < menor) menor = s;
            }
            return menor;
        }

        /// <summary>Avanza el entrenamiento. duracion da los segundos de cada tipo de tropa.</summary>
        public void Avanzar(float segundos, Func<TroopId, float> duracion)
        {
            while (segundos > 0f && _cola.Count > 0)
            {
                if (segundos < _restante)
                {
                    _restante -= segundos;
                    return;
                }
                segundos -= _restante;
                _tropas[Indice(_cola[0], Rangos.Joven)]++;
                _cola.RemoveAt(0);
                _restante = _cola.Count > 0 ? duracion(_cola[0]) : 0f;
            }
        }

        // ---------- Guardado ----------

        public void Exportar(SaveData datos)
        {
            datos.tropasPorRango = (int[])_tropas.Clone();
            datos.tropas = new int[TroopCatalog.Count];
            for (int t = 0; t < TroopCatalog.Count; t++) datos.tropas[t] = Get((TroopId)t);
            datos.colaEntrenamiento = new List<int>();
            foreach (var id in _cola) datos.colaEntrenamiento.Add((int)id);
            datos.entrenamientoRestante = _restante;
            datos.heridos = new List<Herido>();
            foreach (var h in _heridos) datos.heridos.Add(new Herido(h.tipo, h.rango, h.vida));
        }

        public void Importar(SaveData datos)
        {
            Array.Clear(_tropas, 0, _tropas.Length);
            if (datos.tropasPorRango != null && datos.tropasPorRango.Length == _tropas.Length)
            {
                for (int i = 0; i < _tropas.Length; i++) _tropas[i] = Math.Max(0, datos.tropasPorRango[i]);
            }
            else if (datos.tropas != null)
            {
                // Partidas anteriores a los rangos: todas las tropas empiezan como jóvenes guerreros.
                for (int t = 0; t < TroopCatalog.Count && t < datos.tropas.Length; t++)
                {
                    _tropas[Indice((TroopId)t, Rangos.Joven)] = Math.Max(0, datos.tropas[t]);
                }
            }

            _heridos.Clear();
            if (datos.heridos != null)
            {
                foreach (var h in datos.heridos)
                {
                    if (h == null || (int)h.tipo < 0 || (int)h.tipo >= TroopCatalog.Count) continue;
                    int rango = Math.Max(0, Math.Min(Rangos.Count - 1, h.rango));
                    float vida = Math.Max(0.01f, Math.Min(1f, h.vida));
                    if (vida >= 0.999f) Agregar(h.tipo, rango, 1);
                    else _heridos.Add(new Herido(h.tipo, rango, vida));
                }
            }

            _cola.Clear();
            if (datos.colaEntrenamiento != null)
            {
                foreach (int id in datos.colaEntrenamiento)
                {
                    if (id >= 0 && id < TroopCatalog.Count) _cola.Add((TroopId)id);
                }
            }
            _restante = _cola.Count > 0 ? Math.Max(0f, datos.entrenamientoRestante) : 0f;
        }
    }
}
