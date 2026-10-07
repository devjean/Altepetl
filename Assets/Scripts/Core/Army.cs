using System;
using System.Collections.Generic;

namespace Altepetl
{
    /// <summary>
    /// Tropas listas para atacar, separadas por tipo y rango, y la cola de entrenamiento.
    /// Cada tropa ocupa 1 de espacio. Las recién entrenadas son jóvenes guerreros.
    /// </summary>
    public sealed class Army
    {
        private readonly int[] _tropas = new int[TroopCatalog.Count * Rangos.Count];
        private readonly List<TroopId> _cola = new List<TroopId>();
        private float _restante; // segundos que le faltan a la primera de la cola

        private static int Indice(TroopId id, int rango) => (int)id * Rangos.Count + rango;

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

        public int Total
        {
            get
            {
                int total = 0;
                foreach (int n in _tropas) total += n;
                return total;
            }
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
