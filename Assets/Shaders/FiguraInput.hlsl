#ifndef ALTEPETL_FIGURA_INPUT_INCLUDED
#define ALTEPETL_FIGURA_INPUT_INCLUDED

// Reutilizar Lit evita mantener otra implementación de iluminación, texturas y sombras.
#define InitializeStandardLitSurfaceData InicializarLitFiguraBase
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData

UNITY_INSTANCING_BUFFER_START(ColoresFigura)
    UNITY_DEFINE_INSTANCED_PROP(float4, _ColorFigura)
UNITY_INSTANCING_BUFFER_END(ColoresFigura)

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData superficie)
{
    InicializarLitFiguraBase(uv, superficie);
    half4 color = UNITY_ACCESS_INSTANCED_PROP(ColoresFigura, _ColorFigura);
    superficie.albedo *= color.rgb;
    superficie.alpha *= color.a;
}

#endif
