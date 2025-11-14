Shader "Custom/MysticBalanceSkyboxURP"
{
    Properties
    {
        _StartTop("Start Top", Color) = (0.2,0.1,0.35,1)
        _StartBottom("Start Bottom", Color) = (0.02,0.08,0.25,1)

        _T1Top("Turn1 Top", Color) = (0.12,0.35,0.8,1)
        _T1Bottom("Turn1 Bottom", Color) = (0.02,0.12,0.35,1)

        _T2Top("Turn2 Top", Color) = (0.18,0.75,0.55,1)
        _T2Bottom("Turn2 Bottom", Color) = (0.95,0.55,0.35,1)

        _T3LeftTop("Turn3 Left Top", Color) = (0.07,0.55,0.9,1)
        _T3LeftBottom("Turn3 Left Bottom", Color) = (0.02,0.16,0.40,1)
        _T3RightTop("Turn3 Right Top", Color) = (0.95,0.35,0.75,1)
        _T3RightBottom("Turn3 Right Bottom", Color) = (0.9,0.5,0.25,1)

        _Phase("Phase", Range(0,3)) = 0

        _FogStrength("Fog Strength", Range(0,1)) = 0.2
        _VignetteStrength("Vignette Strength", Range(0,1)) = 0.45
        _VignettePower("Vignette Power", Range(0.5,4)) = 2.0

        _MagicIntensity("Magic Intensity", Range(0,2)) = 1.0
        _MagicScale("Magic Scale", Range(0.5,5)) = 2.0
        _MagicSpeed1("Magic Speed1", Range(0,5)) = 1.2
        _MagicSpeed2("Magic Speed2", Range(0,5)) = 0.7

        _GlitterIntensity("Glitter Intensity", Range(0,5)) = 1.0
        _GlitterThreshold("Glitter Threshold", Range(0.9,0.999)) = 0.97
    }

    SubShader
    {
        Tags { 
            "RenderPipeline"="UniversalRenderPipeline"
            "Queue"="Background"
            "RenderType"="Background"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "SkyboxPass"
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 pos : SV_POSITION; float3 dirWS : TEXCOORD0; };

            // --- UNIFORMES (SEM _Time DUPLICADO) ---
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _StartTop)
                UNITY_DEFINE_INSTANCED_PROP(float4, _StartBottom)
                UNITY_DEFINE_INSTANCED_PROP(float4, _T1Top)
                UNITY_DEFINE_INSTANCED_PROP(float4, _T1Bottom)
                UNITY_DEFINE_INSTANCED_PROP(float4, _T2Top)
                UNITY_DEFINE_INSTANCED_PROP(float4, _T2Bottom)
                UNITY_DEFINE_INSTANCED_PROP(float4, _T3LeftTop)
                UNITY_DEFINE_INSTANCED_PROP(float4, _T3LeftBottom)
                UNITY_DEFINE_INSTANCED_PROP(float4, _T3RightTop)
                UNITY_DEFINE_INSTANCED_PROP(float4, _T3RightBottom)
                UNITY_DEFINE_INSTANCED_PROP(float, _Phase)
                UNITY_DEFINE_INSTANCED_PROP(float, _FogStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _VignetteStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _VignettePower)
                UNITY_DEFINE_INSTANCED_PROP(float, _MagicIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _MagicScale)
                UNITY_DEFINE_INSTANCED_PROP(float, _MagicSpeed1)
                UNITY_DEFINE_INSTANCED_PROP(float, _MagicSpeed2)
                UNITY_DEFINE_INSTANCED_PROP(float, _GlitterIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _GlitterThreshold)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                OUT.pos = TransformObjectToHClip(IN.positionOS);

                // DIREÇÃO CORRETA DA SKYBOX: usando matriz da câmera
                float3 viewDir = normalize(mul((float3x3)UNITY_MATRIX_I_V, IN.positionOS.xyz));

                OUT.dirWS = viewDir;
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dirWS);

                // Gradiente vertical
                float h = saturate(dir.y * 0.5 + 0.5);

                float3 c0 = lerp(_StartBottom, _StartTop, h).rgb;
                float3 c1 = lerp(_T1Bottom, _T1Top, h).rgb;
                float3 c2 = lerp(_T2Bottom, _T2Top, h).rgb;

                float side = saturate(dir.x * 0.5 + 0.5);
                float3 c3L = lerp(_T3LeftBottom, _T3LeftTop, h).rgb;
                float3 c3R = lerp(_T3RightBottom, _T3RightTop, h).rgb;
                float3 c3 = lerp(c3L, c3R, side);

                float p = saturate(_Phase);
                float a = floor(p);
                float b = min(a + 1, 3);
                float t = p - a;

                float3 palA = (a==0)?c0 : (a==1)?c1 : (a==2)?c2 : c3;
                float3 palB = (b==0)?c0 : (b==1)?c1 : (b==2)?c2 : c3;

                float3 baseCol = lerp(palA, palB, t);

                return float4(baseCol,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
