# Pendientes del proyecto

## Arte y texturas: material transparente de edificios

- [ ] Al abordar texturas, materiales o la integración del arte definitivo, recordar a Dante que quiere añadir personalmente una textura acorde con el estilo artístico del juego al material `Assets/Resources/Materials/EdificioTransparente.mat` (URP/Lit, superficie Transparent).
- La base técnica ya está implementada en la rama de transparencia: se carga con `Resources.Load<Material>("Materials/EdificioTransparente")` y se copia para cada edificio. No hace falta volver a crear esa base; el pendiente es incorporar la textura y ajustar su aspecto.
- Al integrar el arte, revisar cómo comparten las texturas y sus escalas los materiales opacos y transparentes, para que el edificio no cambie de textura al hacer zoom. Si cada edificio tiene una textura propia, adaptar la copia a ese esquema.
- Conservar el recurso y sus `.meta` para incluir el material y sus dependencias en el build. Validar el resultado en Play y en celular: textura, transparencia, sombras y restauración al alejar la cámara; las batallas deben seguir opacas.
