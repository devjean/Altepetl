# Guía para asistentes de código (Codex, Claude y otros)

Este archivo lo leen las herramientas de IA que trabajan en el repo. Resume cómo está hecho Altepetl y qué esperamos de cualquier cambio. Si algo aquí contradice al código, manda el código y conviene corregir esta guía.

## El proyecto

- Juego móvil de estrategia (aldea estilo Clash of Clans / Age of Empires) ambientado en los pueblos nahuas prehispánicos: mexicas, tlaxcaltecas y acolhuas.
- Unity 6.3 LTS (6000.3.x), Universal Render Pipeline, **Input System nuevo** (no `UnityEngine.Input`).
- Todo el código vive en `Assets/Scripts`: `Core` (datos puros, guardado, campaña), `Gameplay` (aldea, edificios, gente, asaltos), `Battle` (batallas contra la IA), `UI` (HUD con IMGUI), `Editor` (menú del editor).
- No hay escenas con prefabs: `GameManager` se crea solo con `RuntimeInitializeOnLoadMethod` y construye todo por código con primitivas. Los modelos y el arte finos los hace una persona aparte; el código usa placeholders simples.

## Estilo de código

- C# dentro del `namespace Altepetl`, con **identificadores en español** (`Edificio`, `Banco`, `TryMejorar`, `_murosElegidos`), igual que el código existente. Las palabras en náhuatl se escriben como ya están en el repo (tecpan, teocalli, macehualtin, acalli…).
- Comentarios y textos del juego en español. Los comentarios explican el porqué, no repiten el qué; los `<summary>` cortos en los miembros públicos.
- Sigue las convenciones del archivo que tocas: campos privados con `_`, propiedades `PascalCase`, `Try…` para acciones que pueden fallar sin lanzar, `Puede…` para consultas.
- Nada de dependencias nuevas (paquetes, assets de la store, LINQ pesado en `Update`). El juego apunta a celulares: cuida las asignaciones por frame y evita `FindObjectOfType` en bucles.
- No reordenes ni borres valores de los `enum` que se guardan (`BuildingId`, `TroopId`, `ResourceType`, `PuebloId`): solo añade al final. El guardado usa `JsonUtility` (`Core/SaveData.cs`); si añades un campo nuevo, dale un valor por defecto que sirva para partidas viejas.
- Textos en pantalla: el HUD usa un ancho de referencia de 960×540. Comprueba que las etiquetas y botones **quepan sin cortarse**, también con las palabras largas en náhuatl y con el modo de términos en español. Es la queja más repetida del proyecto.
- Reutiliza lo que ya existe antes de duplicarlo: `Caminos` para rutas, `Banco` para pagos (`PuedePagar`/`TryGastar`) y `PlumasParaCompletar` para lo que falta, `PuedeMejorar` para el estado de una mejora, `MostrarMensaje` para avisos, `Guardar()` después de cambios de estado.
- Equilibrio: los ajustes de números son pequeños y se discuten antes; no cambies costos, tiempos ni daños de paso.

## Recordatorios de arte

- Cuando se traten texturas, materiales o arte definitivo, consultar `PENDIENTES.md` y recordar a Dante el pendiente de personalizar la textura del material transparente de edificios en `Resources`.

## Historia y nombres

- La ambientación se toma en serio. No inventes palabras, instituciones, rangos ni personajes nahuas; usa los que ya están en el repo o deja el término en español y márcalo en el PR para que se decida. Los textos de la campaña distinguen lo documentado en las fuentes de lo que es recreación del juego.

## Flujo de trabajo

- **Una rama y un PR por funcionalidad**, desde `main`. El título y la descripción en español; la descripción dice qué veía el jugador antes y qué ve después, y cómo probarlo en el editor.
- Si creas un script nuevo, **commitea también su `.meta`** (lo genera Unity al abrir el proyecto). Un `.cs` sin `.meta` rompe el proyecto a los demás.
- Antes de abrir el PR: el proyecto compila sin errores en el editor y la funcionalidad se probó en Play. Di en el PR qué probaste y qué no.
- No toques `ProjectSettings` ni `Packages/manifest.json` salvo que el cambio lo necesite y se diga en el PR.
- Actualiza el `README.md` cuando cambie algo que el jugador ve (un botón, una regla, un edificio), en la misma lista de viñetas que ya existe.
