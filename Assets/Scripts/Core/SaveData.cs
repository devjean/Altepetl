using System;
using System.Collections.Generic;

namespace Altepetl
{
    /// <summary>Todo lo que se guarda de la aldea. JsonUtility solo serializa campos públicos.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int VersionActual = 4; // 2: nivel de los edificios. 3: ejército y campaña. 4: rangos y ofrendas

        public int version = VersionActual;
        public PuebloId pueblo;
        public float[] recursos = new float[ResourceInfo.Count];
        public List<EdificioGuardado> edificios = new List<EdificioGuardado>();
        public long guardadoUtcTicks; // momento del guardado, para calcular la producción sin conexión

        public int[] tropas = new int[TroopCatalog.Count];          // total por tipo (versión 3)
        public int[] tropasPorRango = new int[0];                    // por tipo y rango (versión 4)
        public List<int> colaEntrenamiento = new List<int>();
        public float entrenamientoRestante;
        public int nivelesCompletados; // niveles de campaña ganados, en orden

        public int deidadActiva;               // ofrenda activa en el teocalli
        public float ofrendaRestante;          // segundos que le quedan
        public float favorHuitzilopochtli = 50f; // solo mexicas
    }

    [Serializable]
    public sealed class EdificioGuardado
    {
        public BuildingId id;
        public int x;
        public int y;
        public int nivel;              // nivel terminado; 0 si aún se está construyendo
        public float segundosRestantes;
        public float acumulado;
    }
}
