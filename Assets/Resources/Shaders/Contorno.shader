// Contorno amarillo que sigue la silueta del edificio seleccionado.
// Se dibuja con Graphics.DrawMesh sobre cada pieza del edificio (GameManager.DibujarContorno):
// infla la malla un poco y pinta solo sus caras traseras, así solo asoma alrededor de la figura.
// Vive en Resources para que entre en el build del celular aunque no esté en ninguna escena.
Shader "Altepetl/Contorno"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.85, 0.2, 1)
        _Grosor ("Grosor (unidades del mundo)", Float) = 0.08
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Name "Contorno"
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Grosor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes entrada)
            {
                Varyings salida;
                // Las primitivas están centradas en su origen: alejar cada vértice del centro infla la
                // figura entera (un cubo sigue siendo cubo). El grosor se mide en el mundo, así que se
                // divide entre la escala de cada eje para que no dependa del tamaño de la pieza.
                float3 escala = float3(length(unity_ObjectToWorld._m00_m10_m20),
                                       length(unity_ObjectToWorld._m01_m11_m21),
                                       length(unity_ObjectToWorld._m02_m12_m22));
                float3 direccion = normalize(entrada.positionOS.xyz + float3(1e-5, 1e-5, 1e-5));
                float3 posicion = entrada.positionOS.xyz + direccion * _Grosor / max(escala, 1e-4);
                salida.positionHCS = TransformObjectToHClip(posicion);
                return salida;
            }

            half4 frag(Varyings entrada) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
