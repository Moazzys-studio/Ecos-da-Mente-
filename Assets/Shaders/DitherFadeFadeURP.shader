Shader "Custom/URP_DitherFade_Opaque"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Fade ("Fade (0=oculto,1=visível)", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHadows
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Fade;
                float4 _BaseMap_ST;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            // 4x4 Bayer matrix (valores 0..1/16)
            static const float bayer4x4[16] = {
                0/16.0,  8/16.0,  2/16.0, 10/16.0,
                12/16.0, 4/16.0, 14/16.0, 6/16.0,
                3/16.0, 11/16.0, 1/16.0,  9/16.0,
                15/16.0, 7/16.0, 13/16.0, 5/16.0
            };

            Varyings vert (Attributes v) {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            float4 frag (Varyings i) : SV_Target {
                float2 uv = i.uv;
                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

                // índice dither por pixel
                int2 pix = int2(floor(_ScreenParams.xy * (i.positionCS.xy / i.positionCS.w * 0.5 + 0.5)));
                int2 m = int2(pix.x & 3, pix.y & 3);
                float threshold = bayer4x4[m.y * 4 + m.x];

                // compara threshold com _Fade
                clip(_Fade - threshold); // quando _Fade cai, mais pixels são descartados
                return albedo;
            }
            ENDHLSL
        }
    }
}
