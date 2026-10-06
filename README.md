# Altepetl

Juego móvil de estrategia y gestión de recursos con temática prehispánica. Eliges un pueblo nahua (mexicas, tlaxcaltecas o acolhuas), levantas tu altepetl y gestionas maíz, madera y obsidiana. Motor: Unity 6 LTS (URP), para Android e iOS.

## Primera vez: abrir el proyecto

1. Instala [Git LFS](https://git-lfs.com) y ejecuta una vez `git lfs install`. El arte y el audio se guardan con LFS (ver `.gitattributes`).
2. Clona este repositorio.
3. En Unity Hub crea un proyecto nuevo con la plantilla **Universal 3D** en una carpeta cualquiera (por ejemplo, el escritorio) y cierra Unity.
4. Copia las carpetas `Assets`, `Packages` y `ProjectSettings` de ese proyecto a la carpeta del repositorio. Si te pregunta, combina la carpeta `Assets`.
5. En Unity Hub usa **Add → Add project from disk** y elige la carpeta del repositorio.
6. Sube a git las carpetas nuevas (`Packages`, `ProjectSettings` y los archivos `.meta`). Las carpetas `Library`, `Temp` y `Logs` ya están ignoradas.

## Probar el prototipo

Abre cualquier escena (por ejemplo `SampleScene`) y pulsa **Play**. El juego se arma solo, no hace falta añadir nada a la escena.

- Elige un pueblo.
- Toca un edificio del menú inferior y luego una casilla libre del mapa. Clic derecho o Esc cancela.
- Toca un edificio para ver su información. Si está en construcción, puedes terminarlo con plumas de quetzal.
- Los edificios se mejoran desde su panel (hasta nivel 5): producen, almacenan y aguantan más. Ningún edificio puede superar el nivel del tecpan.
- La aldea se guarda sola (cada 30 s, al construir y al salir). Al volver, las obras avanzan y los edificios producen por el tiempo que estuviste fuera.
- Construye un **telpochcalli** para entrenar tropas (guerreros con macuahuitl, arqueros y honderos). Cada nivel da 10 de espacio y las tropas se siguen entrenando aunque cierres el juego.
- Con el botón **Atacar** eliges una aldea de la campaña. Toca el campo para desplegar la tropa elegida; pelean solas. Ganas con al menos 50% destruido o al tirar el tecpan (la ciudad se rinde y te llevas todo el botín); el botín depende de lo destruido y la primera victoria de cada nivel da plumas de quetzal. Solo pelean las tropas que llevabas al empezar; las desplegadas no regresan y las que no despliegues vuelven a la aldea.
- Para empezar de cero, detén el juego y usa el menú **Altepetl → Borrar partida guardada**.

## Estructura

| Carpeta | Contenido |
| --- | --- |
| `Assets/Scripts/Core` | Datos puros: recursos, pueblos, catálogo de edificios, banco de recursos, cuadrícula, guardado, tropas, ejército y campaña |
| `Assets/Scripts/Gameplay` | `GameManager` (escena, entrada, colocación) y `Building` (construcción y producción) |
| `Assets/Scripts/Battle` | Batalla contra la IA: `BattleManager`, tropas y edificios enemigos |
| `Assets/Scripts/UI` | HUD provisional con IMGUI |
| `Assets/Scripts/Editor` | Menú "Altepetl" del editor (borrar partida, abrir carpeta de guardado) |
