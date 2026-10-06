using System;
using System.Collections.Generic;

namespace Altepetl
{
    /// <summary>Todo lo que se guarda de la aldea. JsonUtility solo serializa campos públicos.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int VersionActual = 1;

        public int version = VersionActual;
        public PuebloId pueblo;
        public float[] recursos = new float[ResourceInfo.Count];
        public List<EdificioGuardado> edificios = new List<EdificioGuardado>();
        public long guardadoUtcTicks; // momento del guardado, para calcular la producción sin conexión
    }

    [Serializable]
    public sealed class EdificioGuardado
    {
        public BuildingId id;
        public int x;
        public int y;
        public float segundosRestantes;
        public float acumulado;
    }
}
