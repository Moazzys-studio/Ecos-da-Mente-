Shader "Custom/URP_Dissolve"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _NoiseMap ("Noise (ruído)", 2D) = "gray" {}
        _Dissolve ("Dissolve (0=oculto,1=visível)", Range(0,1)) = 1
        _EdgeWidth ("Largura da borda", Range(0.001,0.2)) = 0.06
        _EdgeColor ("Cor da borda", Color) = (1,0.8,0.2,1)
        _EdgeStrength ("Brilho da borda", Range(0,5)) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _EdgeColor;
                float   _Dissolve;
                float   _EdgeWidth;
                float   _EdgeStrength;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert (Attributes v) {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                // amostra ruído (pode tilar o UV como quiser)
                float n = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, i.uv * 2.0).r;

                // limiar dissolve: n < _Dissolve => aparece
                float dist = n - _Dissolve;

                // clip região "queimada"
                clip(-dist); // descarta quando dist>0 (n > _Dissolve)

                // borda: quando |dist| < _EdgeWidth
                float edge = saturate(1.0 - abs(dist) / max(1e-5, _EdgeWidth));
                float3 glow = _EdgeColor.rgb * (edge * _EdgeStrength);

                float3 color = baseCol.rgb + glow;
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}
