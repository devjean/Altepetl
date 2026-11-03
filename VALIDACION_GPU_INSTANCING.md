# Validación de GPU instancing

Prueba manual realizada en Unity 6000.3.27f2, Windows/DX12, el 3 de noviembre de 2026. No sustituye una prueba de rendimiento en celular.

## Aldea

- Partida tlaxcalteca en Play, con pobladores, tropas y paisaje visibles.
- Frame Debugger: una llamada `Draw Mesh (instanced) Macehualli`, malla Capsule, con 31 instancias y shader `Altepetl/FiguraInstanciada`.
- También se observaron cubos instanciados.

## Combate

- Campaña tlaxcalteca, capítulo Poyauhtlan, ejército de 18 tropas de tres tipos y varios rangos.
- Despliegue de guerreros, arqueros y honderos; combate contra defensores y edificios hasta victoria con 100 % de destrucción.
- Se observaron tropas de distintos colores, oscurecimiento de edificios dañados y ruinas planas oscuras.
- Frame Debugger al finalizar, con el campo de batalla aún visible: una llamada instanciada Capsule con 12 instancias y otra Cube con 4 instancias, ambas con el shader nuevo.
- El código de tropas, defensores y edificios enemigos ya usa `Figuras.Crear` y `Figuras.Colorear`; el shader compartido se aplica también en combate sin otra ruta de materiales.
- No se encontraron errores de shader, errores de compilación C#, excepciones ni aviso de material Figura faltante en el registro de esta sesión.
- Partida respaldada y restaurada después de salir de Play; hash SHA-256 idéntico al respaldo.

## Pendiente

- Capturar específicamente el destello blanco de daño y su restauración, incluidos los casos de fin de batalla.
- Batallas sobre agua: canoas, desembarco y plataformas; élites y asaltos a la aldea.
- Build Android/iOS y comprobación de variantes del shader en dispositivo.
- Comparación controlada con main de tiempos de CPU/GPU y memoria; las instancias observadas no equivalen a una medición de mejora de FPS.
