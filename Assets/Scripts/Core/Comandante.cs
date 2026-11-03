using System;
using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    /// <summary>Comandantes que forma el cuauhcalli. Nuevos al final: el número se guarda en la partida.</summary>
    public enum ComandanteId
    {
        Tlacateccatl,        // mexicas, control
        Tlacochcalcatl,      // mexicas, ofensiva
        CapitanTepeticpac,   // tlaxcaltecas, defensa
        CapitanOcotelolco,   // tlaxcaltecas, coordinación
        Cihuacuecuenotzin,   // acolhuas, mando
        CapitanAliados,      // acolhuas, contingentes aliados
    }

    /// <summary>Lo que da el aura de un comandante a las tropas propias a su alrededor.</summary>
    public enum TipoAura
    {
        Ninguna,
        Resistencia, // reciben menos daño
        Ataque,      // pegan más
    }

    /// <summary>La habilidad de un toque de cada comandante.</summary>
    public enum Habilidad
    {
        Cohesion,    // −daño recibido en el aura (todo el campo a nivel 5)
        Embestida,   // +ataque en el aura (+velocidad nv 3, resto +10 % tras terminar nv 5)
        CerrarFilas, // −daño recibido en el aura (dura más a nivel 5)
        Llamado,     // +ataque a todas las tropas del campo (+velocidad nv 3, dos usos nv 5)
        OrdenAsalto, // las tropas del aura van por las defensas con +ataque (+velocidad nv 3, +distancia nv 5)
        Refuerzos,   // llegan 3 aliados más (se desbloquea a nivel 3)
    }

    /// <summary>
    /// Todos los números de los comandantes en un solo lugar, para ajustarlos un poquitín cuando haga falta.
    /// Los niveles 1 a 5 son del juego, no rangos históricos.
    /// </summary>
    public static class ReglasComandante
    {
        public const int NivelMaximo = 5;
        /// <summary>Vida de unos tres guerreros de élite; sube 10 % por nivel.</summary>
        public const float VidaBase = 600f;
        public const float VidaPorNivel = 0.10f;
        public const float DanoPorSegundo = 10f;   // pega poco: su fuerza es dirigir
        public const float Alcance = 0.5f;
        public const float Velocidad = 1.5f;
        /// <summary>El aura usa el mismo radio que la del yaotequihua y no se acumula con ella: se toma la mayor.</summary>
        public const float Radio = Rangos.RadioLider;
        public const float AuraBase = 0.08f;
        public const float AuraPorNivel = 0.01f;
        public const float TopeAuraResistencia = 0.12f;
        public const float TopeAuraAtaque = 0.14f;
        /// <summary>El daño recibido nunca baja de este tanto, por más auras y habilidades.</summary>
        public const float MinimoDanoRecibido = 0.4f;

        public const float DuracionHabilidad = 10f;
        public const float DuracionCerrarFilasNivel5 = 15f;
        public const float CohesionReduccion = 0.30f;          // Cohesión y Cerrar filas
        public const float CohesionEliteNivel3 = 0.05f;        // Tlacateccatl nv 3: la élite en su aura, −5 % extra
        public const float EmbestidaAtaque = 0.40f;
        public const float EmbestidaVelocidadNivel3 = 0.20f;
        public const float EmbestidaResidualNivel5 = 0.10f;
        public const float LlamadoAtaque = 0.15f;
        public const float LlamadoVelocidadNivel3 = 0.10f;
        public const float OrdenAtaque = 0.25f;
        public const float OrdenVelocidadNivel3 = 0.20f;
        public const float OrdenDistanciaNivel5 = 0.25f;
        public const int RefuerzosAliados = 3;
        public const int RefuerzosDesdeNivel = 3;
        /// <summary>Capitán de los aliados: llegan con él tantos guerreros aliados como su nivel.</summary>
        public const int AliadosPorNivel = 1;
        public const int AliadosMaximo = 5;

        /// <summary>Experiencia necesaria para estar en cada nivel (índice = nivel − 1).</summary>
        public static readonly int[] UmbralesExperiencia = { 0, 5, 12, 22, 35 };
        public const int ExperienciaPorEstrella = 1;
        public const int CapturasPorExperiencia = 3; // +1 por cada 3 cautivos que tomen las tropas en su aura
        public const int ExperienciaPorDefensa = 2;  // asaltos defendidos (PR de defensa de la aldea)

        public static readonly int[] CostoReclutar = ResourceInfo.Costo(maiz: 300, plumas: 5);
        /// <summary>Segundos para curarse del todo en el cuauhcalli, de 0 a toda la vida.</summary>
        public const float SegundosCuracion = 240f;

        /// <summary>Subir del nivel actual al siguiente: 200 maíz y 2 plumas por nivel actual.</summary>
        public static int[] CostoPromocion(int nivel) => ResourceInfo.Costo(maiz: 200 * nivel, plumas: 2 * nivel);
        public static float SegundosPromocion(int nivel) => 60f * nivel;

        /// <summary>Experiencia que falta para el siguiente nivel; 0 si ya alcanza o está en el máximo.</summary>
        public static int UmbralSiguiente(int nivel)
        {
            return nivel >= NivelMaximo ? UmbralesExperiencia[NivelMaximo - 1] : UmbralesExperiencia[nivel];
        }
    }

    /// <summary>Datos fijos de un comandante: quién es, su aura, su habilidad y sus rasgos.</summary>
    public sealed class ComandanteDefinition
    {
        public ComandanteId Id;
        public PuebloId Pueblo;
        public string Nombre;
        public string NombreBoton;   // con salto de línea para los botones angostos
        public string Papel;         // control, ofensiva, defensa...
        public string Descripcion;   // ficha: qué era y qué es documentado o del juego
        public TipoAura Aura = TipoAura.Ninguna;
        public Habilidad Habilidad;
        public string NombreHabilidad;
        public string DescripcionHabilidad;
        public string RasgoNivel3;
        public string RasgoNivel5;
        public Color Color = Color.white;

        public float TopeAura => Aura == TipoAura.Ataque ? ReglasComandante.TopeAuraAtaque : ReglasComandante.TopeAuraResistencia;
        public bool TraeAliados => Id == ComandanteId.CapitanAliados;

        /// <summary>Qué tan fuerte es el aura en ese nivel.</summary>
        public float AuraEn(int nivel)
        {
            if (Aura == TipoAura.Ninguna) return 0f;
            return Mathf.Min(TopeAura, ReglasComandante.AuraBase + ReglasComandante.AuraPorNivel * (nivel - 1));
        }

        public string TextoAura(int nivel)
        {
            int tanto = Mathf.RoundToInt(AuraEn(nivel) * 100f);
            switch (Aura)
            {
                case TipoAura.Resistencia: return $"Aura: {Terminos.Los} {Terminos.Tropas} a {ReglasComandante.Radio} casillas reciben {tanto}% menos daño.";
                case TipoAura.Ataque: return $"Aura: {Terminos.Los} {Terminos.Tropas} a {ReglasComandante.Radio} casillas atacan {tanto}% más.";
                default: return "Sin aura: su fuerza son los aliados que trae.";
            }
        }
    }

    public static class ComandanteCatalog
    {
        private const string NotaMexica =
            "Tlacateccatl y tlacochcálcatl eran los dos altos mandos militares mexicas, también con peso político "
            + "(documentado en Sahagún). Las fuentes no reparten sus funciones de forma uniforme: que uno sea de control "
            + "y el otro de ofensiva es interpretación del juego.";

        public static readonly ComandanteDefinition[] Todos =
        {
            new ComandanteDefinition
            {
                Id = ComandanteId.Tlacateccatl,
                Pueblo = PuebloId.Mexicas,
                Nombre = "Tlacateccatl",
                NombreBoton = "Tlacateccatl",
                Papel = "control",
                Descripcion = "«El que manda a la gente». " + NotaMexica,
                Aura = TipoAura.Resistencia,
                Habilidad = Habilidad.Cohesion,
                NombreHabilidad = "Cohesión",
                DescripcionHabilidad = "Durante 10 s, [los] [tropas] de su aura reciben 30% menos daño.",
                RasgoNivel3 = "La élite dentro de su aura recibe 5% menos daño todavía.",
                RasgoNivel5 = "«Cohesión» cubre todo el campo, no solo su aura.",
                Color = new Color(0.20f, 0.35f, 0.70f),
            },
            new ComandanteDefinition
            {
                Id = ComandanteId.Tlacochcalcatl,
                Pueblo = PuebloId.Mexicas,
                Nombre = "Tlacochcálcatl",
                NombreBoton = "Tlacochcálcatl",
                Papel = "ofensiva",
                Descripcion = "«El de la casa de los dardos». " + NotaMexica,
                Aura = TipoAura.Ataque,
                Habilidad = Habilidad.Embestida,
                NombreHabilidad = "Embestida",
                DescripcionHabilidad = "Durante 10 s, [los] [tropas] de su aura atacan 40% más.",
                RasgoNivel3 = "La embestida también da 20% de velocidad.",
                RasgoNivel5 = "Al terminar la embestida, [los] [tropas] que estaban en su aura conservan +10% de ataque el resto de la batalla.",
                Color = new Color(0.75f, 0.25f, 0.20f),
            },
            new ComandanteDefinition
            {
                Id = ComandanteId.CapitanTepeticpac,
                Pueblo = PuebloId.Tlaxcaltecas,
                Nombre = "Capitán de Tepeticpac",
                NombreBoton = "Capitán de\nTepeticpac",
                Papel = "defensa",
                Descripcion = "Tlaxcallan se gobernaba desde cuatro cabeceras, y Tepeticpac fue la primera (documentado). "
                              + "Un capitán por cabecera, sin nombre propio, es recreación del juego: los señores conocidos "
                              + "son de 1519, mucho después de la fundación.",
                Aura = TipoAura.Resistencia,
                Habilidad = Habilidad.CerrarFilas,
                NombreHabilidad = "Cerrar filas",
                DescripcionHabilidad = "Durante 10 s, [los] [tropas] de su aura reciben 30% menos daño.",
                RasgoNivel3 = "Defendiendo [tuAldea], su aura protege 4% más (llega con la defensa de la aldea).",
                RasgoNivel5 = "«Cerrar filas» dura 15 s.",
                Color = new Color(0.35f, 0.55f, 0.30f),
            },
            new ComandanteDefinition
            {
                Id = ComandanteId.CapitanOcotelolco,
                Pueblo = PuebloId.Tlaxcaltecas,
                Nombre = "Capitán de Ocotelolco",
                NombreBoton = "Capitán de\nOcotelolco",
                Papel = "coordinación",
                Descripcion = "Ocotelolco, segunda cabecera de Tlaxcallan (documentado). El capitán sin nombre es recreación del juego. "
                              + "Tener a los dos capitanes dará un extra pequeño a murallas y torres (llega con la defensa de la aldea).",
                Aura = TipoAura.Ataque,
                Habilidad = Habilidad.Llamado,
                NombreHabilidad = "Llamado de las cabeceras",
                DescripcionHabilidad = "Durante 10 s, todas tus [tropas] en el campo atacan 15% más, estén donde estén.",
                RasgoNivel3 = "El llamado también da 10% de velocidad.",
                RasgoNivel5 = "Se puede usar dos veces por batalla.",
                Color = new Color(0.80f, 0.55f, 0.20f),
            },
            new ComandanteDefinition
            {
                Id = ComandanteId.Cihuacuecuenotzin,
                Pueblo = PuebloId.Acolhuas,
                Nombre = "Cihuacuecuenotzin",
                NombreBoton = "Cihuacue-\ncuenotzin",
                Papel = "mando",
                Descripcion = "Tlacateccatl de Texcoco en tiempos de Ixtlilxóchitl Ome Tochtli, hacia 1418, según Alva Ixtlilxóchitl "
                              + "(pendiente de confirmar en la fuente). Su habilidad es del juego.",
                Aura = TipoAura.Ataque,
                Habilidad = Habilidad.OrdenAsalto,
                NombreHabilidad = "Orden de asalto",
                DescripcionHabilidad = "Durante 10 s, [los] [tropas] de su aura van derecho contra la defensa enemiga más cercana con 25% más de ataque.",
                RasgoNivel3 = "La orden también da 20% de velocidad.",
                RasgoNivel5 = "Durante la orden, [los] [tropas] a distancia hacen 25% más de daño.",
                Color = new Color(0.55f, 0.30f, 0.60f),
            },
            new ComandanteDefinition
            {
                Id = ComandanteId.CapitanAliados,
                Pueblo = PuebloId.Acolhuas,
                Nombre = "Capitán de los aliados",
                NombreBoton = "Capitán de\nlos aliados",
                Papel = "aliados",
                Descripcion = "Texcoco movilizaba contingentes de los altepemeh sujetos y aliados (documentado); el capitán "
                              + "sin nombre es recreación del juego. Al soltarlo llegan con él guerreros aliados, uno por nivel: "
                              + "pelean y cuentan para la destrucción, pero no toman cautivos y, si caen, no regresan.",
                Aura = TipoAura.Ninguna,
                Habilidad = Habilidad.Refuerzos,
                NombreHabilidad = "Refuerzos",
                DescripcionHabilidad = "Desde nivel 3: una vez por batalla llegan 3 guerreros aliados más a su lado.",
                RasgoNivel3 = "Desbloquea «Refuerzos».",
                RasgoNivel5 = "Los aliados llegan como guerreros experimentados.",
                Color = new Color(0.45f, 0.50f, 0.55f),
            },
        };

        public static ComandanteDefinition Get(ComandanteId id)
        {
            foreach (var def in Todos)
            {
                if (def.Id == id) return def;
            }
            return null;
        }

        public static List<ComandanteDefinition> DePueblo(PuebloId pueblo)
        {
            var lista = new List<ComandanteDefinition>();
            foreach (var def in Todos)
            {
                if (def.Pueblo == pueblo) lista.Add(def);
            }
            return lista;
        }
    }

    /// <summary>Lo que se guarda de un comandante reclutado.</summary>
    [Serializable]
    public sealed class ComandanteGuardado
    {
        public int id;
        public int nivel = 1;
        public int experiencia;
        public float vida = 1f;          // 0..1; menos de 1 es que está herido en el cuauhcalli
        public float promocionRestante;  // segundos que le faltan para subir de nivel (0: no está subiendo)
    }

    /// <summary>Un comandante reclutado: su nivel, experiencia y estado en la aldea.</summary>
    public sealed class Comandante
    {
        public ComandanteDefinition Definicion { get; }
        public int Nivel { get; private set; } = 1;
        public int Experiencia { get; private set; }
        /// <summary>Fracción de vida (0..1). Se cura sola en el cuauhcalli.</summary>
        public float Vida { get; set; } = 1f;
        public float PromocionRestante { get; private set; }

        public Comandante(ComandanteDefinition definicion)
        {
            Definicion = definicion;
        }

        public string Nombre => Definicion.Nombre;
        public bool Promoviendo => PromocionRestante > 0f;
        public bool Herido => Vida < 0.999f;
        /// <summary>Puede salir a batalla: sano y sin estar subiendo de nivel.</summary>
        public bool Disponible => !Herido && !Promoviendo;
        public float Aura => Definicion.AuraEn(Nivel);
        public bool TieneRasgo3 => Nivel >= 3;
        public bool TieneRasgo5 => Nivel >= 5;
        public int UsosHabilidad => Definicion.Habilidad == Habilidad.Refuerzos && Nivel < ReglasComandante.RefuerzosDesdeNivel ? 0
            : Definicion.Habilidad == Habilidad.Llamado && TieneRasgo5 ? 2 : 1;
        public float DuracionHabilidad => Definicion.Habilidad == Habilidad.CerrarFilas && TieneRasgo5
            ? ReglasComandante.DuracionCerrarFilasNivel5 : ReglasComandante.DuracionHabilidad;
        /// <summary>Guerreros aliados que llegan con el capitán de los aliados.</summary>
        public int Aliados => Definicion.TraeAliados ? Mathf.Min(ReglasComandante.AliadosMaximo, ReglasComandante.AliadosPorNivel * Nivel) : 0;
        public int RangoAliados => TieneRasgo5 ? Rangos.Experimentado : Rangos.Joven;
        public float VidaMaxima => ReglasComandante.VidaBase * (1f + ReglasComandante.VidaPorNivel * (Nivel - 1));

        public bool EnNivelMaximo => Nivel >= ReglasComandante.NivelMaximo;
        public int UmbralSiguiente => ReglasComandante.UmbralSiguiente(Nivel);
        /// <summary>Tiene experiencia para el siguiente nivel; falta pagar la promoción en el cuauhcalli.</summary>
        public bool PuedeSubir => !EnNivelMaximo && Experiencia >= UmbralSiguiente;

        public void GanarExperiencia(int cantidad)
        {
            if (cantidad > 0) Experiencia += cantidad;
        }

        /// <summary>Vuelve de batalla con esa fracción de vida; si cayó, queda herido pero no se pierde.</summary>
        public void Regresar(float vida)
        {
            Vida = Mathf.Clamp(vida, 0.01f, 1f);
        }

        /// <summary>Se cura solo con el tiempo. Devuelve true si en este paso quedó sano.</summary>
        public bool Curar(float segundos)
        {
            if (!Herido || segundos <= 0f) return false;
            Vida = Mathf.Min(1f, Vida + segundos / ReglasComandante.SegundosCuracion);
            return !Herido;
        }

        public void EmpezarPromocion(float segundos)
        {
            PromocionRestante = Mathf.Max(0.01f, segundos);
        }

        /// <summary>Avanza la promoción. Devuelve true si en este paso subió de nivel.</summary>
        public bool AvanzarPromocion(float segundos)
        {
            if (!Promoviendo) return false;
            PromocionRestante -= segundos;
            if (PromocionRestante > 0f) return false;
            TerminarPromocion();
            return true;
        }

        public void TerminarPromocion()
        {
            if (!Promoviendo) return;
            PromocionRestante = 0f;
            if (!EnNivelMaximo) Nivel++;
        }

        public ComandanteGuardado Exportar()
        {
            return new ComandanteGuardado
            {
                id = (int)Definicion.Id,
                nivel = Nivel,
                experiencia = Experiencia,
                vida = Vida,
                promocionRestante = PromocionRestante,
            };
        }

        public static Comandante Importar(ComandanteGuardado datos, PuebloId pueblo)
        {
            if (datos == null) return null;
            var definicion = ComandanteCatalog.Get((ComandanteId)datos.id);
            if (definicion == null || definicion.Pueblo != pueblo) return null;
            return new Comandante(definicion)
            {
                Nivel = Mathf.Clamp(datos.nivel, 1, ReglasComandante.NivelMaximo),
                Experiencia = Mathf.Max(0, datos.experiencia),
                Vida = Mathf.Clamp(datos.vida, 0.01f, 1f),
                PromocionRestante = Mathf.Max(0f, datos.promocionRestante),
            };
        }
    }
}
