using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// Cuadrícula de la aldea sobre el plano XZ. Cada casilla mide 1 unidad
    /// y la casilla (0,0) va de (0,0) a (1,1) en el mundo.
    /// </summary>
    public sealed class GridMap
    {
        public readonly int Ancho;
        public readonly int Alto;

        private readonly Building[,] _casillas;

        public GridMap(int ancho, int alto)
        {
            Ancho = ancho;
            Alto = alto;
            _casillas = new Building[ancho, alto];
            AreaMax = System.Math.Min(ancho, alto);
        }

        public Vector3 Centro => new Vector3(Ancho * 0.5f, 0f, Alto * 0.5f);

        public bool DentroDelMapa(Vector2Int casilla)
        {
            return casilla.x >= 0 && casilla.y >= 0 && casilla.x < Ancho && casilla.y < Alto;
        }

        public Vector2Int MundoACasilla(Vector3 punto)
        {
            return new Vector2Int(Mathf.FloorToInt(punto.x), Mathf.FloorToInt(punto.z));
        }

        /// <summary>Origen (esquina inferior izquierda) de un área de tamaño dado, ajustado al mapa.</summary>
        public Vector2Int AjustarOrigen(Vector2Int origen, int tamano)
        {
            return new Vector2Int(
                Mathf.Clamp(origen.x, 0, Ancho - tamano),
                Mathf.Clamp(origen.y, 0, Alto - tamano));
        }

        /// <summary>Centro en el mundo de un área cuadrada, a nivel del suelo.</summary>
        public Vector3 CentroDeArea(Vector2Int origen, int tamano)
        {
            return new Vector3(origen.x + tamano * 0.5f, 0f, origen.y + tamano * 0.5f);
        }

        /// <summary>Zona donde se puede construir (crece con el tecpan): de AreaMin a AreaMax-1 en x y en y.</summary>
        public int AreaMin { get; private set; }
        public int AreaMax { get; private set; }

        public void FijarArea(int lado)
        {
            lado = Mathf.Clamp(lado, 1, System.Math.Min(Ancho, Alto));
            AreaMin = (System.Math.Min(Ancho, Alto) - lado) / 2;
            AreaMax = AreaMin + lado;
        }

        public bool EnArea(Vector2Int origen, int tamano)
        {
            return origen.x >= AreaMin && origen.y >= AreaMin
                   && origen.x + tamano <= AreaMax && origen.y + tamano <= AreaMax;
        }

        /// <summary>Libre y dentro de la zona abierta: donde se puede construir algo nuevo.</summary>
        public bool PuedeConstruir(Vector2Int origen, int tamano) => EnArea(origen, tamano) && EstaLibre(origen, tamano);

        /// <summary>Libre o solo ocupado por ese mismo edificio (para moverlo).</summary>
        public bool EstaLibreSalvo(Vector2Int origen, int tamano, Building propio)
        {
            for (int x = origen.x; x < origen.x + tamano; x++)
            {
                for (int y = origen.y; y < origen.y + tamano; y++)
                {
                    if (!DentroDelMapa(new Vector2Int(x, y))) return false;
                    var ocupante = _casillas[x, y];
                    if (ocupante != null && ocupante != propio) return false;
                }
            }
            return true;
        }

        public bool EstaLibre(Vector2Int origen, int tamano)
        {
            for (int x = origen.x; x < origen.x + tamano; x++)
            {
                for (int y = origen.y; y < origen.y + tamano; y++)
                {
                    if (!DentroDelMapa(new Vector2Int(x, y)) || _casillas[x, y] != null) return false;
                }
            }
            return true;
        }

        public void Ocupar(Vector2Int origen, int tamano, Building edificio)
        {
            for (int x = origen.x; x < origen.x + tamano; x++)
            {
                for (int y = origen.y; y < origen.y + tamano; y++)
                {
                    _casillas[x, y] = edificio;
                }
            }
        }

        public Building En(Vector2Int casilla)
        {
            return DentroDelMapa(casilla) ? _casillas[casilla.x, casilla.y] : null;
        }
    }
}
