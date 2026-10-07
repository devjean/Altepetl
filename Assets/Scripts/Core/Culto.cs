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
        public string Lore;           // texto breve para "Más información"
        public Color Color = Color.white; // color de su insignia mientras la ofrenda está activa
        public Color Cielo = CieloNormal;  // ambiente de la aldea mientras la ofrenda está activa
        public Color Luz = Color.white;

        public static readonly Color CieloNormal = new Color(0.55f, 0.75f, 0.85f);
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
            new Deidad { Id = DeidadId.Huitzilopochtli, Color = new Color(0.25f, 0.45f, 0.85f),
                Lore = "Colibrí del sur. Dios del sol y de la guerra, protector de los mexicas. Según su tradición, los guió desde Aztlán hasta fundar Tenochtitlan, y su templo compartía la cima del Templo Mayor con el de Tláloc. Los mexicas creían que el sol necesitaba fuerza para seguir su camino, y las ofrendas de cautivos se la daban.",
                Nombre = "Huitzilopochtli", Dominio = "Sol y guerra",
                Pueblos = new[] { PuebloId.Mexicas } },
            new Deidad { Id = DeidadId.Tlaloc, Cielo = new Color(0.42f, 0.50f, 0.58f), Luz = new Color(0.75f, 0.80f, 0.90f),Color = new Color(0.30f, 0.65f, 0.85f),
                Lore = "Señor de la lluvia y del agua que hace crecer las milpas. Lo acompañaban los tlaloques, dioses de los montes donde nacen las nubes. Era venerado en toda la región: compartía el Templo Mayor con Huitzilopochtli y tenía culto en Texcoco y en Tlaxcala.",
                Nombre = "Tláloc", Dominio = "Lluvia y fertilidad",
                Bono = TipoBono.Produccion, Valor = 0.20f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Acolhuas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Cinteotl, Cielo = new Color(0.75f, 0.78f, 0.60f), Luz = new Color(1f, 0.95f, 0.75f),Color = new Color(0.95f, 0.80f, 0.30f),
                Lore = "Dios del maíz. Junto con Chicomecóatl, diosa de los mantenimientos, recibía ofrendas para pedir abundancia en la cosecha. También aparece entre las deidades veneradas en Tlaxcala.",
                Nombre = "Cintéotl", Dominio = "Maíz",
                Bono = TipoBono.Maiz, Valor = 0.30f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Tezcatlipoca, Cielo = new Color(0.35f, 0.32f, 0.45f), Luz = new Color(0.75f, 0.70f, 0.85f),Color = new Color(0.25f, 0.25f, 0.30f),
                Lore = "Espejo humeante. Dios del destino, del poder y de la guerra, que todo lo ve en su espejo de obsidiana. En Ocotelulco, Tlaxcala, aparece asociado a la guerra. En la fiesta de Tóxcatl, un joven lo representaba durante todo un año.",
                Nombre = "Tezcatlipoca", Dominio = "Poder y guerra",
                Bono = TipoBono.Ataque, Valor = 0.15f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Acolhuas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.XipeTotec, Cielo = new Color(0.55f, 0.80f, 0.70f), Luz = new Color(0.95f, 1f, 0.90f),Color = new Color(0.80f, 0.35f, 0.30f),
                Lore = "Nuestro señor el desollado. Dios de la renovación y de la primavera, como la semilla que deja su cubierta para brotar. Su gran fiesta era Tlacaxipehualiztli, al inicio de la temporada de siembra.",
                Nombre = "Xipe Tótec", Dominio = "Renovación",
                Bono = TipoBono.Entrenamiento, Valor = 0.25f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Xiuhtecuhtli, Cielo = new Color(0.85f, 0.60f, 0.40f), Luz = new Color(1f, 0.85f, 0.65f),Color = new Color(0.95f, 0.50f, 0.15f),
                Lore = "Señor del fuego y del año, el dios viejo que habita el centro del mundo y de cada hogar. Cada 52 años, en la ceremonia del Fuego Nuevo, se encendía de nuevo el fuego para que el mundo continuara.",
                Nombre = "Xiuhtecuhtli", Dominio = "Fuego",
                Bono = TipoBono.Construccion, Valor = 0.25f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Acolhuas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Ehecatl, Cielo = new Color(0.70f, 0.85f, 0.90f), Luz = new Color(0.95f, 1f, 1f),Color = new Color(0.55f, 0.80f, 0.75f),
                Lore = "Quetzalcóatl en su forma de viento. Su soplo barría el camino a la lluvia de Tláloc, y sus templos eran redondos para que el viento circulara. En Zultépec-Tecoaque, pueblo acolhua sujeto a Texcoco, se le rindió culto.",
                Nombre = "Ehécatl-Quetzalcóatl", Dominio = "Viento",
                Bono = TipoBono.Velocidad, Valor = 0.20f,
                Pueblos = new[] { PuebloId.Mexicas, PuebloId.Acolhuas, PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Mixcoatl, Cielo = new Color(0.40f, 0.42f, 0.62f), Luz = new Color(0.85f, 0.85f, 1f),Color = new Color(0.65f, 0.55f, 0.75f),
                Lore = "Serpiente de nubes. Dios de la caza y de la guerra, asociado a la Vía Láctea. En su fiesta, Quecholli, se fabricaban flechas y se organizaban grandes cacerías.",
                Nombre = "Mixcóatl", Dominio = "Caza y guerra",
                Bono = TipoBono.DanoDistancia, Valor = 0.20f,
                Pueblos = new[] { PuebloId.Mexicas } },
            new Deidad { Id = DeidadId.Camaxtli, Cielo = new Color(0.40f, 0.42f, 0.62f), Luz = new Color(0.85f, 0.85f, 1f),Color = new Color(0.75f, 0.40f, 0.55f),
                Lore = "Dios principal de Tlaxcala, de la caza y de la guerra. Es la forma tlaxcalteca de Mixcóatl y protegía a los guerreros de los señoríos tlaxcaltecas.",
                Nombre = "Camaxtli", Dominio = "Caza y guerra",
                Bono = TipoBono.DanoDistancia, Valor = 0.20f,
                Pueblos = new[] { PuebloId.Tlaxcaltecas } },
            new Deidad { Id = DeidadId.Mayahuel, Cielo = new Color(0.60f, 0.78f, 0.65f), Luz = new Color(0.95f, 1f, 0.90f),Color = new Color(0.45f, 0.70f, 0.40f),
                Lore = "Diosa del maguey. De esta planta se obtenía el aguamiel, fibras para cuerdas y telas, y púas que servían de agujas. Se le rindió culto en Zultépec-Tecoaque.",
                Nombre = "Mayahuel", Dominio = "Maguey",
                Bono = TipoBono.Almacen, Valor = 0.25f,
                Pueblos = new[] { PuebloId.Acolhuas } },
            new Deidad { Id = DeidadId.Mictlantecuhtli, Cielo = new Color(0.30f, 0.30f, 0.35f), Luz = new Color(0.65f, 0.65f, 0.75f),Color = new Color(0.85f, 0.85f, 0.80f),
                Lore = "Señor del Mictlan, el lugar de los muertos. Allí llegaban quienes morían de muerte común, tras un viaje de cuatro años por los nueve niveles del inframundo. También tuvo culto en Zultépec-Tecoaque.",
                Nombre = "Mictlantecuhtli", Dominio = "Muerte e inframundo",
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
