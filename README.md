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
- Pulsa **Construir** (abajo a la derecha), elige una pestaña (suministros, defensas o militar) y un edificio, y luego toca una casilla libre del mapa. Clic derecho o Esc cancela. **Información** explica qué hace cada edificio.
- **Mapa:** arrastra para moverte y usa la rueda del ratón o pellizca con dos dedos para acercar o alejar. Un toque sin arrastrar selecciona, coloca o despliega.
- **Terreno:** la aldea mide 24x24, pero al principio solo se construye en el centro (12x12). Cada nivel del tecpan abre más terreno: 12, 14, 16, 20 y 24 casillas de lado. Lo cerrado se ve más oscuro.
- Toca un edificio para ver su información. Si está en construcción, puedes terminarlo con plumas de quetzal, y si te falta material para mejorarlo, **Con plumas** compra lo que falta (1 pluma por cada 25 de maíz o madera, o por cada 8 de obsidiana) y empieza la mejora.
- Los edificios se mejoran desde su panel (hasta nivel 5): producen, almacenan y aguantan más. Ningún edificio puede superar el nivel del tecpan, y el nivel del tecpan también limita cuántos edificios de cada tipo puedes tener (se ve en el menú de construcción).
- La aldea se guarda sola (cada 30 s, al construir y al salir). Al volver, las obras avanzan y los edificios producen por el tiempo que estuviste fuera.
- Construye un **telpochcalli** para entrenar tropas (guerreros con macuahuitl, arqueros y honderos); cada nivel entrena 20% más rápido y las tropas se siguen entrenando aunque cierres el juego. El espacio para tropas lo da el **calpulli** (15 por nivel): no había cuarteles, los guerreros eran hombres de los barrios que se movilizaban para cada campaña. Tus tropas se ven alrededor del calpulli: las recién entrenadas salen caminando del telpochcalli y, al volver de una batalla, entran por la orilla de la aldea.
- Con el botón **Atacar** eliges una aldea de la campaña. Toca el campo para desplegar la tropa elegida; pelean solas. Como las estrellas de Clash, cada batalla da hasta tres macuahuitl: una por destruir el 50%, otra por derribar el tecpan y otra por destruirlo todo (las murallas no cuentan en el porcentaje). Con un macuahuitl ya es victoria. El botín depende de lo destruido, la primera victoria de cada nivel da plumas de quetzal y la campaña guarda tu mejor resultado. Las aldeas enemigas tienen guerreros defensores que esperan junto a su tecpan: cuando tus tropas se acercan salen a pelear, tus tropas les responden y al vencerlos también puedes tomar cautivos. Antes de cada batalla eliges qué tropas llevas, sanas y heridas, de cada tipo y rango (de entrada van todas las sanas). Solo pelean las que elegiste. Al terminar, las que sobreviven y las que no desplegaste vuelven a la aldea.
- **Aldea mexica sobre el lago:** con los mexicas la aldea es agua, como Tenochtitlan. Cada edificio se levanta sobre una plataforma, las chinampas llevan ahuejotes en las esquinas y las tropas se mueven en acalli (canoas). Alrededor, la ciudad del lago crece con el tecpan: más chinampas, islotes con casas y, desde el nivel 3, calzadas hacia tierra firme; en el nivel 5 aparece un templo escalonado. Los volcanes se ven al fondo.
- **Alrededores de acolhuas y tlaxcaltecas:** Texcoco está en la orilla oriental del lago, con chinampas, casas, un embarcadero con acalli (nivel 3), las terrazas del Tetzcotzinco (nivel 4) y un templo (nivel 5). Tlaxcallan está entre cerros, con la Matlalcuéyetl al fondo, casas con techo de paja y, desde el nivel 2, una cabecera nueva en su cerro con terrazas por nivel (Tepeticpac, Ocotelulco, Tizatlán, Quiahuiztlán); en el nivel 5, un templo con techo cónico de paja.
- **Campaña mexica, "La peregrinación":** si juegas con los mexicas, la campaña sigue su historia en 5 capítulos: Salida de Aztlán, La peregrinación, Chapultepec, Al servicio de Culhuacan y Tributarios de Azcapotzalco. Cada capítulo empieza con su historia. En Chapultepec la historia sigue aunque pierdas, y en los dos últimos peleas para otro señor y te quedas con la mitad del botín. Al terminar se lee un epílogo. Acolhuas y tlaxcaltecas usan por ahora la campaña general.
- **Rangos y mamaltin:** la tropa que derriba un edificio puede hacer una captura y traer un malli (cautivo): 20% de probabilidad, 26% para los mexicas. Las que sobreviven suben a guerrero experimentado, y las que capturaron a tlamani (veterano para acolhuas y tlaxcaltecas). Cada rango da +15% de vida y ataque, y en batalla salen primero las de mayor rango.
- **Heridos y temazcalli:** las tropas regresan con la vida que les quedó. Las heridas se curan en el **temazcalli** (pestaña Militar), donde el ticitl atiende a 5 heridos a la vez por nivel. Sanar a una tropa muy herida tarda 1.5 veces lo que entrenarla, y 25% más por cada rango. Mientras sanan siguen ocupando espacio del ejército, y si las llevas a batalla pelean con la vida que tengan.
- **Teocalli y ofrendas:** en la pestaña Templo se construye el teocalli. Desde su panel se ofrendan 3 mamaltin para activar el bono de un dios durante 2 horas (cada pueblo tiene sus propios dioses y el botón ? cuenta su historia; mientras la ofrenda está activa aparece su insignia y cambian el cielo y la luz; los mexicas reciben la mitad del bono en los dioses menores). Los mexicas además cuidan el favor de Huitzilopochtli, que baja con el tiempo: alto da +10% de ataque y entrenamiento más barato, bajo da -10% de ataque, entrenamiento 20% más caro y defensas más débiles.
- Para empezar de cero, detén el juego y usa el menú **Altepetl → Borrar partida guardada**.

## Estructura

| Carpeta | Contenido |
| --- | --- |
| `Assets/Scripts/Core` | Datos puros: recursos, pueblos, catálogo de edificios, banco de recursos, cuadrícula, guardado, tropas, ejército y campaña |
| `Assets/Scripts/Gameplay` | `GameManager` (escena, entrada, colocación) y `Building` (construcción y producción) |
| `Assets/Scripts/Battle` | Batalla contra la IA: `BattleManager`, tropas y edificios enemigos |
| `Assets/Scripts/UI` | HUD provisional con IMGUI |
| `Assets/Scripts/Editor` | Menú "Altepetl" del editor (borrar partida, abrir carpeta de guardado) |
