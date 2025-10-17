Shader "Custom/LiquidShaderURP_Volume_Responsive"
{
    Properties
    {
        _Color("Color", Color) = (1, 0.8, 0.2, 1)
        _FillLevel("Fill Level", Range(0,1)) = 0.5
        _Tilt("Tilt", Vector) = (0, 0, 0, 0)
        _WaveSpeed("Wave Speed", Float) = 2
        _WaveAmplitude("Wave Amplitude", Float) = 0.03
        _EmissionStrength("Emission Strength", Float) = 2
        _Alpha("Alpha", Range(0,1)) = 0.9
        _RefractionStrength("Refraction Strength", Range(0,0.1)) = 0.03
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimPower("Rim Power", Range(0.1,8)) = 2
        _TiltMultiplier("Tilt Multiplier", Float) = 3.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 300

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _FillLevel;
                float4 _Tilt;
                float _WaveSpeed;
                float _WaveAmplitude;
                float _EmissionStrength;
                float _Alpha;
                float _RefractionStrength;
                float4 _RimColor;
                float _RimPower;
                float _TiltMultiplier;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float normalizedY = i.positionOS.y + 0.5;

                // --- Movimento da superfície ---
                float wave = sin(i.positionOS.x * 5.0 + _Time.y * _WaveSpeed) * _WaveAmplitude;

                // --- Inclinação mais forte e responsiva ---
                float tiltEffect = (i.positionOS.x * _Tilt.x + i.positionOS.z * _Tilt.y) * _TiltMultiplier;

                // --- Superfície final com compensação de equilíbrio ---
                float surface = _FillLevel + wave + tiltEffect * 0.5;

                // --- Definição de preenchimento ---
                float fillMask = smoothstep(surface - 0.01, surface + 0.01, normalizedY);
                float liquidMask = 1.0 - fillMask;

                // --- Cor base e emissão ---
                float3 color = _Color.rgb * liquidMask;
                float3 emission = color * _EmissionStrength * liquidMask;

                // --- Refração / brilho volumétrico ---
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                float fresnel = pow(1.0 - saturate(dot(viewDir, normalize(i.normalWS))), 2.0);
                float3 refracted = color * (1.0 + _RefractionStrength * fresnel);

                // --- Rim Light ---
                float rim = pow(fresnel, _RimPower);
                float3 rimGlow = _RimColor.rgb * rim * liquidMask;

                float3 finalColor = refracted + emission + rimGlow;

                return half4(finalColor, _Alpha * liquidMask);
            }
            ENDHLSL
        }
    }
}
