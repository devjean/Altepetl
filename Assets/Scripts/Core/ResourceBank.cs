using UnityEngine;

namespace Altepetl
{
    /// <summary>Recursos del jugador con su capacidad máxima de almacenamiento.</summary>
    public sealed class ResourceBank
    {
        public const int CapacidadBase = 1000;

        private readonly float[] _cantidades = new float[ResourceInfo.Count];
        private int _capacidadExtra;

        public int Get(ResourceType type)
        {
            return Mathf.FloorToInt(_cantidades[(int)type]);
        }

        /// <summary>Las plumas de quetzal no tienen límite.</summary>
        public int Capacidad(ResourceType type)
        {
            return type == ResourceType.Plumas ? int.MaxValue : CapacidadBase + _capacidadExtra;
        }

        public void AgregarCapacidad(int extra)
        {
            _capacidadExtra += extra;
        }

        /// <summary>Suma recursos respetando la capacidad. Devuelve lo que realmente entró.</summary>
        public float Add(ResourceType type, float cantidad)
        {
            int i = (int)type;
            float antes = _cantidades[i];
            _cantidades[i] = Mathf.Min(antes + cantidad, Capacidad(type));
            return _cantidades[i] - antes;
        }

        public bool EstaLleno(ResourceType type)
        {
            return _cantidades[(int)type] >= Capacidad(type);
        }

        public bool PuedePagar(int[] costo)
        {
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                if (Get((ResourceType)i) < costo[i]) return false;
            }
            return true;
        }

        public bool TryGastar(int[] costo)
        {
            if (!PuedePagar(costo)) return false;
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                _cantidades[i] -= costo[i];
            }
            return true;
        }
    }
}
