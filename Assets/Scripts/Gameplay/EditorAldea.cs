using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    /// <summary>Borrador de posiciones: no cambia la cuadrícula ni el guardado de la aldea hasta confirmar.</summary>
    public sealed class EditorAldea
    {
        public sealed class Pieza
        {
            public Building Edificio;
            public Vector2Int Origen;
            public bool Colocada = true;
            public readonly List<Renderer> Dibujos = new List<Renderer>();
        }

        public sealed class Grupo
        {
            public BuildingDefinition Definicion;
            public int Nivel;
            public bool Puerta;
            public bool EnObra;
            public readonly List<Pieza> Piezas = new List<Pieza>();
            public int Disponibles
            {
                get { int n = 0; foreach (var pieza in Piezas) if (!pieza.Colocada) n++; return n; }
            }
        }

        public readonly List<Grupo> Grupos = new List<Grupo>();
        public readonly List<Pieza> Piezas = new List<Pieza>();
        public GridMap Mapa { get; }
        public Pieza Elegida { get; private set; }
        public bool Arrastrando { get; private set; }
        public bool PosicionValida { get; private set; }
        public readonly List<Pieza> MurosElegidos = new List<Pieza>();
        public Grupo GrupoColocacion { get; private set; }
        public Grupo GrupoTrazo => GrupoColocacion?.Definicion.Id == BuildingId.Muralla ? GrupoColocacion : null;
        public bool Trazando { get; private set; }
        private readonly List<Pieza> _arrastre = new List<Pieza>();
        private readonly List<Vector2Int> _desfases = new List<Vector2Int>();
        private Vector2Int _inicioTrazo;
        private int _ejeTrazo;
        private Vector2Int _ultimaCasillaTrazo;
        private Vector2Int _destino;
        private readonly GameManager _manager;
        private readonly bool[,] _ocupadas = new bool[GameManager.TamanoMapa, GameManager.TamanoMapa];

        public EditorAldea(GameManager manager)
        {
            _manager = manager;
            Mapa = new GridMap(GameManager.TamanoMapa, GameManager.TamanoMapa);
            ActualizarArea();
            foreach (var edificio in manager.Edificios)
            {
                var pieza = new Pieza { Edificio = edificio, Origen = edificio.Origen };
                Piezas.Add(pieza);
                Mapa.Ocupar(pieza.Origen, edificio.Definicion.Tamano, edificio);
                Agrupar(pieza);
            }
        }

        private void Agrupar(Pieza pieza)
        {
            var edificio = pieza.Edificio;
            Grupo grupo = null;
            foreach (var candidato in Grupos)
                if (candidato.Definicion == edificio.Definicion && candidato.Nivel == edificio.Nivel
                    && candidato.Puerta == edificio.EsPuerta && candidato.EnObra == edificio.EnConstruccion)
                { grupo = candidato; break; }
            if (grupo == null)
            {
                grupo = new Grupo { Definicion = edificio.Definicion, Nivel = edificio.Nivel,
                    Puerta = edificio.EsPuerta, EnObra = edificio.EnConstruccion };
                Grupos.Add(grupo);
            }
            grupo.Piezas.Add(pieza);
        }

        public void ActualizarGrupos()
        {
            bool cambio = false;
            foreach (var grupo in Grupos)
                foreach (var pieza in grupo.Piezas)
                    if (grupo.Nivel != pieza.Edificio.Nivel || grupo.EnObra != pieza.Edificio.EnConstruccion) cambio = true;
            if (!cambio) return;
            CancelarArrastre();
            var siguiente = GrupoColocacion != null ? Disponible(GrupoColocacion) : null;
            GrupoColocacion = null;
            Grupos.Clear();
            foreach (var pieza in Piezas) Agrupar(pieza);
            if (siguiente != null)
                foreach (var grupo in Grupos)
                    if (grupo.Piezas.Contains(siguiente)) { ElegirGrupo(grupo); break; }
        }

        public int SinColocar
        {
            get { int n = 0; foreach (var pieza in Piezas) if (!pieza.Colocada) n++; return n; }
        }

        public void ActualizarArea() => Mapa.FijarArea(GameManager.LadoConstruible[
            Mathf.Clamp(_manager.NivelTecpan, 1, GameManager.LadoConstruible.Length) - 1]);

        public Pieza Disponible(Grupo grupo)
        {
            foreach (var pieza in grupo.Piezas) if (!pieza.Colocada) return pieza;
            return null;
        }

        public Pieza Tocar(Ray rayo, Vector3 suelo)
        {
            Pieza elegida = null;
            float cerca = float.PositiveInfinity;
            foreach (var pieza in Piezas)
                if (pieza.Colocada && pieza.Edificio.TryTocarFigura(rayo, out float distancia) && distancia < cerca)
                { cerca = distancia; elegida = pieza; }
            if (elegida != null) return elegida;
            var edificio = Mapa.En(Mapa.MundoACasilla(suelo));
            foreach (var pieza in Piezas) if (pieza.Edificio == edificio) return pieza;
            return null;
        }

        public void Elegir(Pieza pieza)
        {
            CancelarArrastre();
            GrupoColocacion = null;
            Elegida = pieza;
            MurosElegidos.Clear();
            if (pieza != null && pieza.Colocada && EsMuro(pieza)) MurosElegidos.Add(pieza);
        }

        private static bool EsMuro(Pieza pieza) => pieza.Edificio.Definicion.Id == BuildingId.Muralla;

        /// <summary>Mantiene el grupo disponible en la mano hasta agotarlo o pulsar Listo.</summary>
        public void ElegirGrupo(Grupo grupo)
        {
            CancelarArrastre();
            GrupoColocacion = grupo != null && grupo.Disponibles > 0 ? grupo : null;
            Elegida = GrupoColocacion != null ? Disponible(GrupoColocacion) : null;
            MurosElegidos.Clear();
        }

        private void PrepararSiguiente()
        {
            if (GrupoColocacion == null) return;
            var siguiente = Disponible(GrupoColocacion);
            if (siguiente != null) Elegida = siguiente;
            else GrupoColocacion = null;
        }

        public void ElegirLinea()
        {
            if (Elegida == null || !Elegida.Colocada || !EsMuro(Elegida)) return;
            CancelarArrastre();
            GrupoColocacion = null;
            MurosElegidos.Clear();
            // En una esquina se elige el eje con más tramos contiguos; los empates van en X.
            int x = LargoLinea(Vector2Int.right), y = LargoLinea(Vector2Int.up);
            var paso = x >= y ? Vector2Int.right : Vector2Int.up;
            MurosElegidos.Add(Elegida);
            for (int signo = -1; signo <= 1; signo += 2)
                for (int n = 1; n < GameManager.TamanoMapa; n++)
                {
                    var pieza = MuroEn(Elegida.Origen + paso * (n * signo));
                    if (pieza == null) break;
                    MurosElegidos.Add(pieza);
                }
        }

        private Pieza MuroEn(Vector2Int origen)
        {
            var edificio = Mapa.En(origen);
            foreach (var pieza in Piezas)
                if (pieza.Colocada && pieza.Edificio == edificio && EsMuro(pieza)) return pieza;
            return null;
        }

        private int LargoLinea(Vector2Int paso)
        {
            int total = 1;
            for (int signo = -1; signo <= 1; signo += 2)
                for (int n = 1; n < GameManager.TamanoMapa; n++)
                {
                    if (MuroEn(Elegida.Origen + paso * (n * signo)) == null) break;
                    total++;
                }
            return total;
        }

        public void EmpezarTrazo(Grupo grupo, Vector3 suelo)
        {
            CancelarArrastre();
            if (grupo == null || grupo.Definicion.Id != BuildingId.Muralla) return;
            if (grupo.Disponibles == 0) return;
            GrupoColocacion = grupo;
            MurosElegidos.Clear();
            _inicioTrazo = Mapa.MundoACasilla(suelo);
            _ejeTrazo = 0;
            _ultimaCasillaTrazo = new Vector2Int(int.MinValue, int.MinValue);
            Trazando = true;
            ActualizarTrazo(suelo);
        }

        public void ActualizarTrazo(Vector3 suelo)
        {
            if (!Trazando || GrupoTrazo == null) return;
            var casilla = Mapa.MundoACasilla(suelo);
            if (casilla == _ultimaCasillaTrazo) return;
            _ultimaCasillaTrazo = casilla;
            // Cada casilla se confirma al pasar: regresar el dedo no recoge tramos ya colocados.
            ActualizarArea();
            var delta = casilla - _inicioTrazo;
            if (_ejeTrazo == 0 && delta != Vector2Int.zero)
                _ejeTrazo = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? 1 : 2;
            var paso = _ejeTrazo == 2 ? Vector2Int.up : Vector2Int.right;
            int distancia = _ejeTrazo == 2 ? delta.y : delta.x;
            int signo = distancia < 0 ? -1 : 1;
            int hasta = Mathf.Min(Mathf.Abs(distancia), GameManager.TamanoMapa);
            for (int n = 0; n <= hasta; n++)
            {
                var origen = _inicioTrazo + paso * (n * signo);
                if (!Mapa.PuedeConstruir(origen, 1)) continue;
                var pieza = Disponible(GrupoTrazo);
                if (pieza == null) break;
                pieza.Origen = origen;
                pieza.Colocada = true;
                Mapa.Ocupar(origen, 1, pieza.Edificio);
                Elegida = pieza;
            }
            RefrescarVista();
            PrepararSiguiente();
        }

        public void TerminarTrazo()
        {
            if (!Trazando) return;
            Trazando = false;
        }

        public void TerminarHerramientaMuros()
        {
            CancelarArrastre();
            GrupoColocacion = null;
            MurosElegidos.Clear();
            Elegida = null;
        }

        public void EmpezarArrastre(Pieza pieza)
        {
            CancelarArrastre();
            var grupo = pieza != null && !pieza.Colocada ? GrupoColocacion : null;
            if (!MurosElegidos.Contains(pieza)) Elegir(pieza);
            Elegida = pieza;
            GrupoColocacion = grupo;
            _arrastre.Clear();
            _desfases.Clear();
            if (pieza != null)
            {
                if (pieza.Colocada && MurosElegidos.Contains(pieza)) _arrastre.AddRange(MurosElegidos);
                else _arrastre.Add(pieza);
                foreach (var parte in _arrastre) _desfases.Add(parte.Origen - pieza.Origen);
            }
            Arrastrando = pieza != null;
            PosicionValida = false;
        }

        public void Previsualizar(Vector3 suelo)
        {
            if (!Arrastrando) return;
            ActualizarArea();
            int tamano = Elegida.Edificio.Definicion.Tamano;
            // El dedo sostiene el centro del edificio, también en los de más de una casilla.
            _destino = new Vector2Int(Mathf.RoundToInt(suelo.x - tamano * 0.5f),
                Mathf.RoundToInt(suelo.z - tamano * 0.5f));
            PosicionValida = GrupoCabe(_destino);
            for (int i = 0; i < _arrastre.Count; i++)
            {
                var parte = _arrastre[i];
                parte.Edificio.transform.position = Mapa.CentroDeArea(_destino + _desfases[i], parte.Edificio.Definicion.Tamano);
                Visibilidad(parte, true);
            }
        }

        private bool GrupoCabe(Vector2Int destino)
        {
            for (int i = 0; i < _arrastre.Count; i++)
            {
                var origen = destino + _desfases[i];
                int tamano = _arrastre[i].Edificio.Definicion.Tamano;
                if (!Mapa.EnArea(origen, tamano)) return false;
                for (int x = origen.x; x < origen.x + tamano; x++)
                    for (int y = origen.y; y < origen.y + tamano; y++)
                    {
                        var ocupante = Mapa.En(new Vector2Int(x, y));
                        if (ocupante == null) continue;
                        bool propio = false;
                        foreach (var parte in _arrastre) propio |= parte.Edificio == ocupante;
                        if (!propio) return false;
                    }
            }
            return true;
        }

        public bool GirarMuros()
        {
            if (Arrastrando || Trazando || MurosElegidos.Count == 0) return false;
            var ancla = MurosElegidos.Contains(Elegida) ? Elegida : MurosElegidos[0];
            EmpezarArrastre(ancla);
            for (int i = 0; i < _desfases.Count; i++)
                _desfases[i] = new Vector2Int(-_desfases[i].y, _desfases[i].x);
            _destino = ancla.Origen;
            PosicionValida = GrupoCabe(_destino);
            return Soltar(true);
        }

        public bool Soltar(bool sobreMapa)
        {
            if (!Arrastrando) return false;
            bool valido = sobreMapa && PosicionValida;
            if (valido)
            {
                foreach (var parte in _arrastre)
                    if (parte.Colocada) Mapa.Ocupar(parte.Origen, parte.Edificio.Definicion.Tamano, null);
                for (int i = 0; i < _arrastre.Count; i++)
                {
                    var parte = _arrastre[i];
                    parte.Origen = _destino + _desfases[i];
                    parte.Colocada = true;
                    Mapa.Ocupar(parte.Origen, parte.Edificio.Definicion.Tamano, parte.Edificio);
                }
            }
            Arrastrando = false;
            RefrescarVista();
            if (valido) PrepararSiguiente();
            return valido;
        }

        public void CancelarArrastre()
        {
            if (Trazando) TerminarTrazo();
            if (!Arrastrando) return;
            Arrastrando = false;
            RefrescarVista();
        }

        public void Recoger(Pieza pieza)
        {
            CancelarArrastre();
            if (pieza == null || !pieza.Colocada) return;
            Mapa.Ocupar(pieza.Origen, pieza.Edificio.Definicion.Tamano, null);
            pieza.Colocada = false;
            Elegida = null;
            MurosElegidos.Remove(pieza);
            RefrescarVista();
        }

        public void RecogerMurosElegidos()
        {
            CancelarArrastre();
            foreach (var pieza in MurosElegidos)
            {
                Mapa.Ocupar(pieza.Origen, 1, null);
                pieza.Colocada = false;
            }
            MurosElegidos.Clear();
            Elegida = null;
            RefrescarVista();
        }

        public void RecogerTodo()
        {
            TerminarHerramientaMuros();
            foreach (var pieza in Piezas)
            {
                if (pieza.Colocada) Mapa.Ocupar(pieza.Origen, pieza.Edificio.Definicion.Tamano, null);
                pieza.Colocada = false;
            }
            Elegida = null;
            RefrescarVista();
        }

        public bool PuedeGuardar()
        {
            if (Arrastrando || Trazando || SinColocar != 0) return false;
            ActualizarArea();
            System.Array.Clear(_ocupadas, 0, _ocupadas.Length);
            foreach (var pieza in Piezas)
            {
                int tamano = pieza.Edificio.Definicion.Tamano;
                if (!Mapa.EnArea(pieza.Origen, tamano)) return false;
                for (int x = pieza.Origen.x; x < pieza.Origen.x + tamano; x++)
                    for (int y = pieza.Origen.y; y < pieza.Origen.y + tamano; y++)
                    {
                        if (_ocupadas[x, y]) return false;
                        _ocupadas[x, y] = true;
                    }
            }
            return true;
        }

        private void RefrescarVista()
        {
            foreach (var pieza in Piezas)
                if (pieza.Colocada) pieza.Edificio.MostrarPosicionEditor(pieza.Origen, Mapa);
            foreach (var pieza in Piezas) Visibilidad(pieza, pieza.Colocada);
        }

        private static void Visibilidad(Pieza pieza, bool visible)
        {
            // Se oculta solo el dibujo: producción y obras siguen sobre las mismas instancias.
            pieza.Edificio.GetComponentsInChildren(true, pieza.Dibujos);
            foreach (var renderer in pieza.Dibujos)
                renderer.forceRenderingOff = !visible;
        }

        public void RestaurarVista()
        {
            Arrastrando = false;
            foreach (var pieza in Piezas)
            {
                pieza.Edificio.MostrarPosicionEditor(pieza.Edificio.Origen, _manager.Mapa);
                Visibilidad(pieza, true);
            }
        }
    }
}
