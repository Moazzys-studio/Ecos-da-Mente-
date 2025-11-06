Shader "Custom/ColorBlindnessCorrection"
{
    Properties
    {
        _Mode ("Mode", Int) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            float _Mode;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float3 protanopia(float3 c)
            {
                return float3(
                    0.567*c.r + 0.433*c.g,
                    0.558*c.r + 0.442*c.g,
                    0.0*c.r   + 0.242*c.g + 0.758*c.b
                );
            }

            float3 deuteranopia(float3 c)
            {
                return float3(
                    0.625*c.r + 0.375*c.g,
                    0.7*c.r   + 0.3*c.g,
                    0.0*c.r   + 0.3*c.g + 0.7*c.b
                );
            }

            float3 tritanopia(float3 c)
            {
                return float3(
                    0.95*c.r  + 0.05*c.g,
                    0.0*c.r   + 0.433*c.g + 0.567*c.b,
                    0.0*c.r   + 0.475*c.g + 0.525*c.b
                );
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv).rgb;

                if (_Mode == 1) col = protanopia(col);
                else if (_Mode == 2) col = deuteranopia(col);
                else if (_Mode == 3) col = tritanopia(col);

                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}