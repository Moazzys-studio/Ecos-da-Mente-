Shader "Hidden/Daltonismo/URPBlit"
{
    Properties
    {
        _Intensity ("Intensidade (0-1)", Range(0,1)) = 1
        _Mode      ("Modo (0..5)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "IgnoreProjector"="True" }
        ZWrite Off ZTest Always Cull Off

        // ===== PASS 0: EFEITO =====
        Pass
        {
            Name "DaltonismoPass"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex   Vert
            #pragma fragment Frag

            // NÃO redeclarar _BlitTexture/sampler_LinearClamp: já vêm do Blit.hlsl
            float _Intensity;
            float _Mode;

            float3 ApplyProtanopia(float3 c)
            {
                const float3x3 m = float3x3(
                    0.567, 0.433, 0.000,
                    0.558, 0.442, 0.000,
                    0.000, 0.242, 0.758
                );
                return mul(m, c);
            }

            float3 ApplyDeuteranopia(float3 c)
            {
                const float3x3 m = float3x3(
                    0.625, 0.375, 0.000,
                    0.700, 0.300, 0.000,
                    0.000, 0.300, 0.700
                );
                return mul(m, c);
            }

            float3 ApplyTritanopia(float3 c)
            {
                const float3x3 m = float3x3(
                    0.950, 0.050, 0.000,
                    0.000, 0.433, 0.567,
                    0.000, 0.475, 0.525
                );
                return mul(m, c);
            }

            float3 ApplyAchromatopsia(float3 c)
            {
                float g = dot(c, float3(0.299, 0.587, 0.114));
                return float3(g, g, g);
            }

            float3 ApplyAchromatomalia(float3 c)
            {
                float g = dot(c, float3(0.333, 0.333, 0.333));
                return lerp(c, float3(g,g,g), 0.5);
            }

            float3 Process(float3 src)
            {
                if (_Mode < 0.5) return src;

                float3 sim;
                if (_Mode < 1.5)      sim = ApplyProtanopia(src);
                else if (_Mode < 2.5) sim = ApplyDeuteranopia(src);
                else if (_Mode < 3.5) sim = ApplyTritanopia(src);
                else if (_Mode < 4.5) sim = ApplyAchromatopsia(src);
                else                  sim = ApplyAchromatomalia(src);

                return lerp(src, saturate(sim), saturate(_Intensity));
            }

            half4 Frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord).rgb;
                half3 outRgb = Process(src);
                return half4(outRgb, 1);
            }
            ENDHLSL
        }

        // ===== PASS 1: CÓPIA (sem alterar cor) =====
        Pass
        {
            Name "CopyPass"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex   Vert
            #pragma fragment FragCopy

            half4 FragCopy (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
