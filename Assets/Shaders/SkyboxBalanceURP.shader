Shader "Custom/MysticBalanceSkyboxURP"
{
    Properties
    {
        // PALETA 0 - INÍCIO (azul profundo, neutro, introspectivo)
        _StartTop      ("Start Top Color", Color)    = (0.06, 0.14, 0.22, 1)
        _StartBottom   ("Start Bottom Color", Color) = (0.01, 0.03, 0.06, 1)

        // PALETA 1 - TURNO 1 (trabalho: azul/cinza, disciplina, pressão sutil)
        _T1Top         ("Turn1 Top Color", Color)    = (0.08, 0.18, 0.28, 1)
        _T1Bottom      ("Turn1 Bottom Color", Color) = (0.02, 0.05, 0.10, 1)

        // PALETA 2 - TURNO 2 (vida pessoal: azul-esverdeado + calor suave)
        _T2Top         ("Turn2 Top Color", Color)    = (0.07, 0.20, 0.23, 1) // teal escuro
        _T2Bottom      ("Turn2 Bottom Color", Color) = (0.06, 0.06, 0.10, 1)

        // PALETA 3 - TURNO 3 (embate: lado trabalho x lado pessoal)
        _T3LeftTop     ("Turn3 LEFT Top (Work)", Color)    = (0.10, 0.20, 0.32, 1)
        _T3LeftBottom  ("Turn3 LEFT Bottom (Work)", Color) = (0.02, 0.05, 0.12, 1)

        _T3RightTop    ("Turn3 RIGHT Top (Personal)", Color)    = (0.10, 0.23, 0.20, 1)
        _T3RightBottom ("Turn3 RIGHT Bottom (Personal)", Color) = (0.06, 0.07, 0.12, 1)

        // CONTROLE DE FASE
        _Phase ("Phase (0=start,1=t1,2=t2,3=t3)", Range(0,3)) = 0

        // VINHETA / “NÉVOA MÍSTICA”
        _VignetteStrength ("Vignette Strength", Range(0,1)) = 0.6
        _VignettePower    ("Vignette Power", Range(0.5,4))  = 2.0
        _FogStrength      ("Vertical Fog Strength", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "Queue"          = "Background"
            "RenderType"     = "Background"
            "IgnoreProjector" = "True"
            "PreviewType"    = "Skybox"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "SkyboxPass"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dirWS       : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _StartTop;
                float4 _StartBottom;

                float4 _T1Top;
                float4 _T1Bottom;

                float4 _T2Top;
                float4 _T2Bottom;

                float4 _T3LeftTop;
                float4 _T3LeftBottom;
                float4 _T3RightTop;
                float4 _T3RightBottom;

                float  _Phase;
                float  _VignetteStrength;
                float  _VignettePower;
                float  _FogStrength;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);

                o.dirWS       = normalize(posWS);
                o.positionHCS = TransformWorldToHClip(posWS);
                return o;
            }

            // Calcula cor da PALETA 0, 1, 2, 3 para uma dada direção
            float3 EvalPaletteColor(int idx, float3 dir)
            {
                // dir.y: -1..1 -> 0..1
                float h = saturate(dir.y * 0.5 + 0.5);

                // Paleta 0 (início)
                float3 col0 = lerp(_StartBottom.rgb, _StartTop.rgb, h);

                // Paleta 1 (trabalho)
                float3 col1 = lerp(_T1Bottom.rgb, _T1Top.rgb, h);

                // Paleta 2 (vida pessoal)
                float3 col2 = lerp(_T2Bottom.rgb, _T2Top.rgb, h);

                // Paleta 3 (embate: esquerda/direita)
                float3 leftCol  = lerp(_T3LeftBottom.rgb,  _T3LeftTop.rgb,  h);
                float3 rightCol = lerp(_T3RightBottom.rgb, _T3RightTop.rgb, h);

                // dir.x: -1..1 -> 0..1 (0 = esquerda, 1 = direita)
                float side = saturate(dir.x * 0.5 + 0.5);
                float3 col3 = lerp(leftCol, rightCol, side);

                if (idx == 0) return col0;
                if (idx == 1) return col1;
                if (idx == 2) return col2;
                return col3; // idx 3+
            }

            float4 frag (Varyings i) : SV_Target
            {
                float3 dir = normalize(i.dirWS);

                // ---------------- PALETA INTERPOLADA POR FASE ----------------
                float p  = clamp(_Phase, 0.0, 3.0);
                float p0 = floor(p);
                float p1 = min(p0 + 1.0, 3.0);
                float t  = p - p0; // 0..1

                int   idx0 = (int)p0;
                int   idx1 = (int)p1;

                float3 c0 = EvalPaletteColor(idx0, dir);
                float3 c1 = EvalPaletteColor(idx1, dir);

                float3 baseCol = lerp(c0, c1, t);

                // ---------------- NÉVOA VERTICAL MÍSTICA ----------------
                // mais claro na altura do personagem, mais escuro pra cima/baixo
                float fogY   = 1.0 - abs(dir.y); // 0 na vertical, 1 no horizonte
                float fogAmt = fogY * _FogStrength;
                float3 fogCol = float3(0.02, 0.03, 0.05); // tom bem escuro/azulado
                baseCol = lerp(baseCol, fogCol, fogAmt);

                // ---------------- VINHETA RADIAL ----------------
                float2 pxy = float2(dir.x, dir.y * 0.7);
                float  len = saturate(length(pxy));  // 0 centro, 1 borda
                float  vig = pow(1.0 - len, _VignettePower);
                float  vigMask = lerp(1.0, vig, _VignetteStrength);

                float3 finalCol = baseCol * vigMask;

                return float4(finalCol, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
