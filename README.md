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
- La aldea se guarda sola (cada 30 s, al construir y al salir). Al volver, las obras avanzan y los edificios producen por el tiempo que estuviste fuera.
- Para empezar de cero, detén el juego y usa el menú **Altepetl → Borrar partida guardada**.

## Estructura

| Carpeta | Contenido |
| --- | --- |
| `Assets/Scripts/Core` | Datos puros: recursos, pueblos, catálogo de edificios, banco de recursos, cuadrícula, guardado |
| `Assets/Scripts/Gameplay` | `GameManager` (escena, entrada, colocación) y `Building` (construcción y producción) |
| `Assets/Scripts/UI` | HUD provisional con IMGUI |
| `Assets/Scripts/Editor` | Menú "Altepetl" del editor (borrar partida, abrir carpeta de guardado) |
