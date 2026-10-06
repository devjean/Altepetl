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
