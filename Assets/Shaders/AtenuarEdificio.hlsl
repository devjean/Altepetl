#ifndef ALTEPETL_ATENUAR_EDIFICIO_INCLUDED
#define ALTEPETL_ATENUAR_EDIFICIO_INCLUDED

#if defined(_ATENUAR_EDIFICIO)
// Todos los materiales y las caras descartan los mismos píxeles de pantalla.
// Así los huecos muestran el fondo, sin acumular las capas interiores del edificio.
#define UNIVERSAL_PIPELINE_LODCROSSFADE_INCLUDED
#if !defined(LOD_FADE_CROSSFADE)
#define LOD_FADE_CROSSFADE
#endif

float _OpacidadVista;

void LODFadeCrossFade(float4 posicion)
{
    const float umbrales[16] =
    {
        0, 8, 2, 10,
        12, 4, 14, 6,
        3, 11, 1, 9,
        15, 7, 13, 5
    };
    uint2 pixel = (uint2)posicion.xy;
    uint indice = (pixel.x & 3u) + (pixel.y & 3u) * 4u;
    clip(_OpacidadVista - (umbrales[indice] + 0.5) / 16.0);
}
#endif
#endif
