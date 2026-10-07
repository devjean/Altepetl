using UnityEngine;

namespace Altepetl
{
    public enum DeidadId
    {
        Huitzilopochtli,
        Tlaloc,
        Cinteotl,
        Tezcatlipoca,
        XipeTotec,
        Xiuhtecuhtli,
        Ehecatl,
        Mixcoatl,
        Camaxtli,
        Mayahuel,
        Mictlantecuhtli,
    }

    /// <summary>Qué mejora una ofrenda.</summary>
    public enum TipoBono
    {
        Ninguno,
        Produccion,        // toda la producción
        Maiz,              // solo el maíz
        Ataque,
        Entrenamiento,     // tropas se entrenan más rápido
        Construccion,      // obras y mejoras más rápidas
        Velocidad,         // tropas caminan más rápido
        DanoDistancia,     // arqueros y honderos
        Almacen,
        Reembolso,         // tropas caídas devuelven parte de su costo
    }

    public sealed class Deidad
    {
        public DeidadId Id;
        public string Nombre;
        public string Dominio;
        public TipoBono Bono;
        public float Valor;           // bono completo (acolhuas y tlaxcaltecas)
        public PuebloId[] Pueblos;    // pueblos que le rinden culto según las fuentes

        public bool VeneradaPor(PuebloId pueblo)
        {
            foreach (var p in Pueblos)
            {
                if (p == pueblo) return true;
            }
            return false;
        }

        public static readonly Deidad[] Todas =
        {
            new Deidad { Id = DeidadId.Huitzilopochtli, Nombre = "Huitzilopochtli", Dominio = "Sol y guerra",
                Pueblos = new[] { PuebloId.Mexicas } },
            new Deidad { Id = DeidadId.Tlaloc, Nombre = "Tláloc", Dominio = "Lluvia y fertilidad",
                Bono = TipoBono.Produccion, Valor = 0.20f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Acolhuas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Cinteotl, Nombre = "Cintéotl", Dominio = "Maíz",
                Bono = TipoBono.Maiz, Valor = 0.30f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Tezcatlipoca, Nombre = "Tezcatlipoca", Dominio = "Poder y guerra",
                Bono = TipoBono.Ataque, Valor = 0.15f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Acolhuas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.XipeTotec, Nombre = "Xipe Tótec", Dominio = "Renovación",
                Bono = TipoBono.Entrenamiento, Valor = 0.25f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Xiuhtecuhtli, Nombre = "Xiuhtecuhtli", Dominio = "Fuego",
                Bono = TipoBono.Construccion, Valor = 0.25f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Acolhuas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Ehecatl, Nombre = "Ehécatl-Quetzalcóatl", Dominio = "Viento",
                Bono = TipoBono.Velocidad, Valor = 0.20f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Acolhuas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Mixcoatl, Nombre = "Mixcóatl", Dominio = "Caza y guerra",
                Bono = TipoBono.DanoDistancia, Valor = 0.20f,
                Pueblos = new[] { PuebloId.Mexicas } },
            new Deidad { Id = DeidadId.Camaxtli, Nombre = "Camaxtli", Dominio = "Caza y guerra",
                Bono = TipoBono.DanoDistancia, Valor = 0.20f,
                Pueblos = new[] { PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Mayahuel, Nombre = "Mayahuel", Dominio = "Maguey",
                Bono = TipoBono.Almacen, Valor = 0.25f,
                Pueblos = new[] { PuebloId.Acolhuas } },
            new Deidad { Id = DeidadId.Mictlantecuhtli, Nombre = "Mictlantecuhtli", Dominio = "Muerte e inframundo",
                Bono = TipoBono.Reembolso, Valor = 0.50f,
                Pueblos = new[] { PuebloId.Acolhuas } },
        };

        public static Deidad Get(DeidadId id) => Todas[(int)id];
    }

    /// <summary>
    /// Ofrendas de mamaltin en el teocalli. Cada ofrenda activa el bono de un dios por un tiempo.
    /// Los mexicas además mantienen el favor de Huitzilopochtli, que baja solo con el tiempo.
    /// </summary>
    public sealed class Culto
    {
        public const int CostoOfrenda = 3;               // mamaltin
        public const float SegundosOfrenda = 2f * 3600f;  // 2 horas
        public const float MultiplicadorMexicas = 0.5f;   // los dioses menores dan la mitad a los mexicas
        public const float FavorPorOfrenda = 30f;
        public const float FavorQuePierdePorHora = 5f;
        public const float FavorAlto = 70f;
        public const float FavorBajo = 30f;

        public PuebloId Pueblo { get; private set; }
        public bool HayActiva => _restante > 0f;
        public Deidad Activa => HayActiva ? Deidad.Get(_activa) : null;
        public float SegundosRestantes => _restante;
        /// <summary>Favor de Huitzilopochtli, de 0 a 100. Solo cuenta para los mexicas.</summary>
        public float Favor { get; private set; } = 50f;
        public bool UsaFavor => Pueblo == PuebloId.Mexicas;

        private DeidadId _activa;
        private float _restante;

        public Culto(PuebloId pueblo)
        {
            Pueblo = pueblo;
        }

        /// <summary>Bono que da ese dios a este pueblo (0.10 = +10 %).</summary>
        public float ValorPara(Deidad deidad)
        {
            return Pueblo == PuebloId.Mexicas ? deidad.Valor * MultiplicadorMexicas : deidad.Valor;
        }

        public bool PuedeOfrendar(Deidad deidad, ResourceBank banco)
        {
            if (!deidad.VeneradaPor(Pueblo)) return false;
            if (banco.Get(ResourceType.Cautivos) < CostoOfrenda) return false;
            if (deidad.Id == DeidadId.Huitzilopochtli) return Favor < 100f;
            return !HayActiva;
        }

        public bool Ofrendar(Deidad deidad, ResourceBank banco)
        {
            if (!PuedeOfrendar(deidad, banco)) return false;
            if (!banco.TryGastar(ResourceInfo.Costo(cautivos: CostoOfrenda))) return false;
            if (deidad.Id == DeidadId.Huitzilopochtli)
            {
                Favor = Mathf.Min(100f, Favor + FavorPorOfrenda);
            }
            else
            {
                _activa = deidad.Id;
                _restante = SegundosOfrenda;
            }
            return true;
        }

        public void Avanzar(float segundos)
        {
            if (segundos <= 0f) return;
            _restante = Mathf.Max(0f, _restante - segundos);
            if (UsaFavor) Favor = Mathf.Max(0f, Favor - FavorQuePierdePorHora * segundos / 3600f);
        }

        /// <summary>Bono de la ofrenda activa para ese tipo (0 si no aplica).</summary>
        public float Bono(TipoBono tipo)
        {
            var activa = Activa;
            return activa != null && activa.Bono == tipo ? ValorPara(activa) : 0f;
        }

        /// <summary>Bono promedio durante los próximos segundos (para el tiempo fuera del juego).</summary>
        public float BonoPromedio(TipoBono tipo, float segundos)
        {
            if (segundos <= 0f) return Bono(tipo);
            return Bono(tipo) * Mathf.Min(_restante, segundos) / segundos;
        }

        // ---------- Favor de Huitzilopochtli ----------

        public bool FavorAltoActivo => UsaFavor && Favor >= FavorAlto;
        public bool FavorBajoActivo => UsaFavor && Favor <= FavorBajo;

        /// <summary>Multiplicador de ataque por el favor: +10 % alto, −10 % bajo.</summary>
        public float AtaquePorFavor => FavorAltoActivo ? 1.1f : FavorBajoActivo ? 0.9f : 1f;

        /// <summary>Multiplicador del costo de entrenar: −10 % alto, +20 % bajo.</summary>
        public float CostoEntrenamientoPorFavor => FavorAltoActivo ? 0.9f : FavorBajoActivo ? 1.2f : 1f;

        /// <summary>Multiplicador de vida de murallas y torres: −10 % con favor bajo.</summary>
        public float DefensasPorFavor => FavorBajoActivo ? 0.9f : 1f;

        // ---------- Guardado ----------

        public void Exportar(SaveData datos)
        {
            datos.deidadActiva = (int)_activa;
            datos.ofrendaRestante = _restante;
            datos.favorHuitzilopochtli = Favor;
        }

        public void Importar(SaveData datos)
        {
            int id = datos.deidadActiva;
            _activa = id >= 0 && id < Deidad.Todas.Length ? (DeidadId)id : DeidadId.Huitzilopochtli;
            _restante = _activa == DeidadId.Huitzilopochtli ? 0f : Mathf.Clamp(datos.ofrendaRestante, 0f, SegundosOfrenda);
            Favor = Mathf.Clamp(datos.favorHuitzilopochtli, 0f, 100f);
        }
    }
}
