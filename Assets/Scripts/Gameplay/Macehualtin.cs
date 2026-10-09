using System.Collections.Generic;
using UnityEngine;

namespace Altepetl
{
    /// <summary>
    /// La gente del pueblo (macehualtin) que se ve en la aldea. Cuando no hay obra pasean entre
    /// los edificios y a ratos entran a uno; cuando empieza una construcción o mejora, diez de
    /// ellos caminan hasta la obra y trabajan alrededor hasta que termina. Las murallas son
    /// rápidas: no ocupan a nadie, pero si hay gente libre un par de personas va a ayudar.
    /// En la aldea mexica, sobre el lago, cada quien va en su acalli.
    /// </summary>
    public sealed class Macehualtin : MonoBehaviour
    {
        private const float Velocidad = 1.1f;          // casillas por segundo, un poco más lento que las tropas
        private const float Tamano = 0.15f;            // más chicos que los guerreros
        private const float CadaCuantoReparte = 0.5f;  // segundos entre repartos de trabajo
        private const int AyudanEnMuralla = 2;

        private enum Estado { Paseo, Adentro, Obra }

        private sealed class Persona
        {
            public Transform Cuerpo;
            public Transform Acalli;
            public Estado Estado;
            public Building Obra;
            public Building Casa;       // edificio donde entró
            public bool CasaDeAfuera;   // entró a una casita de los alrededores (en Puerta)
            public Vector3 Puerta;
            public Vector3 Destino;
            public float Hasta;         // hasta cuándo espera, está adentro o sigue en el mismo lugar de la obra
            public float Fase;
            public float Rumbo;
        }

        public GameManager Manager;

        private readonly List<Persona> _gente = new List<Persona>();
        private readonly List<Building> _obras = new List<Building>();
        private readonly Dictionary<Building, int> _asignados = new Dictionary<Building, int>();
        private float _proximoReparto;
        private bool _colocados;   // al abrir el juego aparecen ya repartidos, sin caminar
        private Pueblo _pueblo;    // con partida nueva (otro pueblo) se vuelve a crear a la gente

        private void Update()
        {
            if (Manager == null || Manager.Pueblo == null || Manager.ModoActual == GameManager.Modo.Batalla) return;

            if (_pueblo != Manager.Pueblo)
            {
                _pueblo = Manager.Pueblo;
                AjustarCantidad(0);
                _colocados = false;
            }
            AjustarCantidad(Manager.Poblacion);
            if (Time.time >= _proximoReparto)
            {
                _proximoReparto = Time.time + CadaCuantoReparte;
                Repartir();
            }
            foreach (var persona in _gente) Avanzar(persona);
        }

        // ---------- Quién trabaja dónde ----------

        private void Repartir()
        {
            _obras.Clear();
            foreach (var edificio in Manager.Edificios)
            {
                if (edificio != null && edificio.EnConstruccion) _obras.Add(edificio);
            }

            // Primero se sueltan los que ya no tienen obra (terminó, se movió o se demolió).
            _asignados.Clear();
            foreach (var persona in _gente)
            {
                if (persona.Estado != Estado.Obra) continue;
                if (persona.Obra == null || !persona.Obra.EnConstruccion || !_obras.Contains(persona.Obra))
                {
                    Pasear(persona);
                    continue;
                }
                _asignados.TryGetValue(persona.Obra, out int cuantos);
                _asignados[persona.Obra] = cuantos + 1;
            }

            // Luego cada obra recibe a su gente: 10 en edificios y mejoras, un par en murallas si sobra gente.
            foreach (var obra in _obras)
            {
                bool muralla = obra.Definicion.Id == BuildingId.Muralla;
                int necesarios = muralla ? AyudanEnMuralla : GameManager.MacehualtinPorObra;
                _asignados.TryGetValue(obra, out int tiene);
                for (int i = tiene; i < necesarios; i++)
                {
                    var persona = Libre(obra.transform.position);
                    if (persona == null) break;
                    if (muralla && Libres() <= GameManager.MacehualtinPorObra) break; // no quitarle gente a la próxima obra
                    MandarAObra(persona, obra);
                }
            }
            _colocados = true;
        }

        private int Libres()
        {
            int libres = 0;
            foreach (var persona in _gente)
            {
                if (persona.Estado != Estado.Obra) libres++;
            }
            return libres;
        }

        /// <summary>La persona libre más cercana a la obra (los que están adentro salen).</summary>
        private Persona Libre(Vector3 cerca)
        {
            Persona mejor = null;
            float mejorDistancia = float.MaxValue;
            foreach (var persona in _gente)
            {
                if (persona.Estado == Estado.Obra) continue;
                float distancia = (Plano(persona) - cerca).sqrMagnitude;
                if (distancia < mejorDistancia)
                {
                    mejor = persona;
                    mejorDistancia = distancia;
                }
            }
            return mejor;
        }

        private void MandarAObra(Persona persona, Building obra)
        {
            Salir(persona);
            persona.Estado = Estado.Obra;
            persona.Obra = obra;
            persona.Destino = PuntoAlrededor(obra);
            persona.Hasta = Time.time + Random.Range(3f, 7f);
            if (!_colocados) Teletransportar(persona);
        }

        private void Pasear(Persona persona)
        {
            persona.Estado = Estado.Paseo;
            persona.Obra = null;
            persona.Casa = null;
            persona.CasaDeAfuera = false;
            persona.Destino = PuntoDePaseo();
            persona.Hasta = 0f;
        }

        // ---------- Movimiento ----------

        private void Avanzar(Persona persona)
        {
            if (persona.Estado == Estado.Adentro)
            {
                // Sale cuando se le acaba el rato o si su casa ya no está.
                if ((persona.Casa != null || persona.CasaDeAfuera) && Time.time < persona.Hasta) return;
                Salir(persona);
                Pasear(persona);
            }

            Vector3 plano = Plano(persona);
            Vector3 hacia = persona.Destino - plano;
            bool caminando = hacia.sqrMagnitude > 0.0004f;
            if (caminando)
            {
                float paso = Velocidad * Time.deltaTime;
                plano = hacia.magnitude <= paso ? persona.Destino : plano + hacia.normalized * paso;
                persona.Rumbo = Mathf.Atan2(hacia.x, hacia.z) * Mathf.Rad2Deg;
            }
            else
            {
                Llego(persona);
            }

            // Trabajando dan golpecitos rápidos; paseando se balancean al caminar.
            bool trabajando = persona.Estado == Estado.Obra && !caminando;
            float ritmo = trabajando ? 14f : caminando ? 8f : 2f;
            float salto = trabajando ? 0.05f : caminando ? 0.05f : 0.015f;
            float altura = Mathf.Abs(Mathf.Sin(Time.time * ritmo + persona.Fase)) * salto;

            if (persona.Acalli != null)
            {
                float vaiven = Mathf.Sin(Time.time * 1.8f + persona.Fase) * 0.015f;
                persona.Acalli.localPosition = new Vector3(plano.x, 0.03f + vaiven, plano.z);
                persona.Acalli.localRotation = Quaternion.Euler(0f, persona.Rumbo, 0f);
                persona.Cuerpo.localPosition = new Vector3(plano.x, 0.07f + vaiven + Tamano + (trabajando ? altura : 0f), plano.z);
                return;
            }
            persona.Cuerpo.localPosition = new Vector3(plano.x, Tamano + altura, plano.z);
        }

        private void Llego(Persona persona)
        {
            switch (persona.Estado)
            {
                case Estado.Obra:
                    // Cada tanto se cambia de lado de la obra.
                    if (Time.time >= persona.Hasta && persona.Obra != null)
                    {
                        persona.Destino = PuntoAlrededor(persona.Obra);
                        persona.Hasta = Time.time + Random.Range(3f, 7f);
                    }
                    break;
                case Estado.Paseo:
                    if (persona.Hasta < 0f)
                    {
                        // Iba a entrar a un edificio (o a una casita de afuera): entra si sigue ahí.
                        if (persona.Casa != null || persona.CasaDeAfuera) Entrar(persona);
                        else Pasear(persona);
                    }
                    else if (persona.Hasta == 0f)
                    {
                        persona.Hasta = Time.time + Random.Range(1f, 4f);   // se queda un rato
                    }
                    else if (Time.time >= persona.Hasta)
                    {
                        // A veces van a meterse a un edificio o a su casa en los alrededores; si no, siguen paseando.
                        var afuera = Manager.Paisaje != null ? Manager.Paisaje.CasasAfuera : null;
                        float suerte = Random.value;
                        if (suerte < 0.15f && afuera != null && afuera.Count > 0)
                        {
                            persona.CasaDeAfuera = true;
                            persona.Puerta = afuera[Random.Range(0, afuera.Count)];
                            persona.Destino = persona.Puerta;
                            persona.Hasta = -1f;
                        }
                        else if (suerte < 0.3f && EdificioAlAzar() is Building casa)
                        {
                            persona.Casa = casa;
                            persona.Destino = PuntoAlrededor(casa);
                            persona.Hasta = -1f;
                        }
                        else
                        {
                            Pasear(persona);
                        }
                    }
                    break;
            }
        }

        private void Entrar(Persona persona)
        {
            persona.Estado = Estado.Adentro;
            // En su casa de afuera se quedan más rato que en los edificios.
            persona.Hasta = Time.time + (persona.CasaDeAfuera ? Random.Range(10f, 30f) : Random.Range(4f, 12f));
            persona.Cuerpo.gameObject.SetActive(false);
            if (persona.Acalli != null) persona.Acalli.gameObject.SetActive(false);
        }

        private void Salir(Persona persona)
        {
            if (persona.Estado == Estado.Adentro && (persona.Casa != null || persona.CasaDeAfuera))
            {
                var punto = persona.CasaDeAfuera ? persona.Puerta : PuntoAlrededor(persona.Casa);
                persona.Cuerpo.localPosition = new Vector3(punto.x, Tamano, punto.z);
                if (persona.Acalli != null) persona.Acalli.localPosition = new Vector3(punto.x, 0.03f, punto.z);
            }
            persona.Casa = null;
            persona.CasaDeAfuera = false;
            persona.Cuerpo.gameObject.SetActive(true);
            if (persona.Acalli != null) persona.Acalli.gameObject.SetActive(true);
        }

        // ---------- Lugares ----------

        private Building EdificioAlAzar()
        {
            var edificios = Manager.Edificios;
            for (int intento = 0; intento < 6 && edificios.Count > 0; intento++)
            {
                var edificio = edificios[Random.Range(0, edificios.Count)];
                if (edificio != null && !edificio.EnConstruccion && edificio.Definicion.Id != BuildingId.Muralla) return edificio;
            }
            return null;
        }

        /// <summary>Un punto pegado al borde de un edificio, en cualquiera de sus lados.</summary>
        private static Vector3 PuntoAlrededor(Building edificio)
        {
            float mitad = edificio.Definicion.Tamano * 0.5f + 0.25f;
            float t = Random.Range(-mitad, mitad);
            Vector3 desplazamiento;
            switch (Random.Range(0, 4))
            {
                case 0: desplazamiento = new Vector3(t, 0f, -mitad); break;
                case 1: desplazamiento = new Vector3(t, 0f, mitad); break;
                case 2: desplazamiento = new Vector3(-mitad, 0f, t); break;
                default: desplazamiento = new Vector3(mitad, 0f, t); break;
            }
            var centro = edificio.transform.position;
            return new Vector3(centro.x, 0f, centro.z) + desplazamiento;
        }

        /// <summary>Una casilla libre al azar dentro del terreno de la aldea.</summary>
        private Vector3 PuntoDePaseo()
        {
            var mapa = Manager.Mapa;
            for (int intento = 0; intento < 10; intento++)
            {
                var casilla = new Vector2Int(Random.Range(mapa.AreaMin, mapa.AreaMax), Random.Range(mapa.AreaMin, mapa.AreaMax));
                if (mapa.En(casilla) != null) continue;
                return new Vector3(casilla.x + Random.Range(0.2f, 0.8f), 0f, casilla.y + Random.Range(0.2f, 0.8f));
            }
            return new Vector3(mapa.Centro.x, 0f, mapa.Centro.z - 2f);
        }

        private static Vector3 Plano(Persona persona)
        {
            var posicion = persona.Cuerpo.localPosition;
            return new Vector3(posicion.x, 0f, posicion.z);
        }

        private static void Teletransportar(Persona persona)
        {
            persona.Cuerpo.localPosition = new Vector3(persona.Destino.x, Tamano, persona.Destino.z);
        }

        // ---------- Crear y quitar gente ----------

        private void AjustarCantidad(int poblacion)
        {
            while (_gente.Count < poblacion)
            {
                // Los nuevos (al subir el tecpan) salen del tecpan; al abrir el juego ya están repartidos.
                var persona = Crear();
                Pasear(persona);
                Vector3 inicio = _colocados ? CentroDelTecpan() : persona.Destino;
                persona.Cuerpo.localPosition = new Vector3(inicio.x, Tamano, inicio.z);
                if (persona.Acalli != null) persona.Acalli.localPosition = new Vector3(inicio.x, 0.03f, inicio.z);
                _gente.Add(persona);
            }
            while (_gente.Count > poblacion)
            {
                var persona = _gente[_gente.Count - 1];
                _gente.RemoveAt(_gente.Count - 1);
                Destroy(persona.Cuerpo.gameObject);
                if (persona.Acalli != null) Destroy(persona.Acalli.gameObject);
            }
        }

        private Vector3 CentroDelTecpan()
        {
            foreach (var edificio in Manager.Edificios)
            {
                if (edificio != null && edificio.Definicion.Id == BuildingId.Tecpan) return PuntoAlrededor(edificio);
            }
            return PuntoDePaseo();
        }

        private Persona Crear()
        {
            var persona = new Persona { Fase = Random.value * Mathf.PI * 2f };
            var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cuerpo.name = "Macehualli";
            Destroy(cuerpo.GetComponent<Collider>()); // que no estorbe al tocar los edificios
            cuerpo.transform.SetParent(transform, false);
            cuerpo.transform.localScale = new Vector3(Tamano, Tamano, Tamano);
            // Ropa de manta (algodón o ixtle), con un poco de variación para que no sean todos iguales.
            float tono = Random.Range(-0.06f, 0.04f);
            cuerpo.GetComponent<Renderer>().material.color = new Color(0.90f + tono, 0.86f + tono, 0.74f + tono);
            persona.Cuerpo = cuerpo.transform;

            if (Manager.Pueblo.EnLago)
            {
                var acalli = GameObject.CreatePrimitive(PrimitiveType.Cube);
                acalli.name = "Acalli";
                Destroy(acalli.GetComponent<Collider>());
                acalli.transform.SetParent(transform, false);
                acalli.transform.localScale = new Vector3(0.11f, 0.05f, 0.32f);
                acalli.GetComponent<Renderer>().material.color = new Color(0.45f, 0.30f, 0.18f);
                persona.Acalli = acalli.transform;
            }
            return persona;
        }
    }
}
