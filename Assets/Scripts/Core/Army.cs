using System;
using System.Collections.Generic;

namespace Altepetl
{
    /// <summary>Tropas listas para atacar y la cola de entrenamiento. Cada tropa ocupa 1 de espacio.</summary>
    public sealed class Army
    {
        private readonly int[] _tropas = new int[TroopCatalog.Count];
        private readonly List<TroopId> _cola = new List<TroopId>();
        private float _restante; // segundos que le faltan a la primera de la cola

        public int Get(TroopId id) => _tropas[(int)id];
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

        public void Agregar(TroopId id, int cantidad)
        {
            if (cantidad > 0) _tropas[(int)id] += cantidad;
        }

        public bool Quitar(TroopId id)
        {
            if (_tropas[(int)id] <= 0) return false;
            _tropas[(int)id]--;
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
                _tropas[(int)_cola[0]]++;
                _cola.RemoveAt(0);
                _restante = _cola.Count > 0 ? duracion(_cola[0]) : 0f;
            }
        }

        // ---------- Guardado ----------

        public void Exportar(SaveData datos)
        {
            datos.tropas = (int[])_tropas.Clone();
            datos.colaEntrenamiento = new List<int>();
            foreach (var id in _cola) datos.colaEntrenamiento.Add((int)id);
            datos.entrenamientoRestante = _restante;
        }

        public void Importar(SaveData datos)
        {
            if (datos.tropas != null)
            {
                for (int i = 0; i < _tropas.Length && i < datos.tropas.Length; i++) _tropas[i] = Math.Max(0, datos.tropas[i]);
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
