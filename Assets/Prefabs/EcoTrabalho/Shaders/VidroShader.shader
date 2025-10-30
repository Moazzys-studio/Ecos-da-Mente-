Shader "Universal Render Pipeline/GlassHSL"
{
    Properties
    {
        // Base + HSL
        _Tint ("Tint", Color) = (0.82, 0.95, 1.0, 1.0)
        _Alpha ("Alpha", Range(0,1)) = 0.08
        _Hue ("Hue (deg)", Range(-180,180)) = 0
        _Saturation ("Saturation", Range(0,2)) = 1
        _Lightness ("Lightness", Range(-1,1)) = 0

        // Fresnel / Borda
        _FresnelPower ("Fresnel Power", Range(0.5,8)) = 4
        _FresnelStrength ("Fresnel Strength", Range(0,1)) = 0.2
        _EdgeColor ("Edge Color (Tint nas bordas)", Color) = (0.75, 0.9, 1, 1)
        _EdgeStrength ("Edge Color Strength", Range(0,2)) = 0.5
        _SmoothAlphaByFresnel ("Alpha Affects Fresnel?", Float) = 1

        // Refração / Normal
        _Distortion ("Distortion (screen uv)", Range(0,0.1)) = 0.03
        _Chromatic ("Chromatic Aberration", Range(0,1)) = 0.1
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0,2)) = 0.2

        // Sujeira / Óleo (R = máscara de aspereza)
        _RoughnessMask ("Roughness Mask (R)", 2D) = "white" {}
        _RoughnessScale ("Roughness Tiling", Float) = 1
        _RoughnessStrength ("Roughness Strength", Range(0,1)) = 0.6

        // Gradiente por altura (Y do mundo)
        _HeightTintColor ("Height Tint Color", Color) = (0.75, 0.9, 1, 1)
        _HeightTintStrength ("Height Tint Strength", Range(0,1)) = 0.2
        _HeightRange ("Height Start(X) / End(Y)", Vector) = (0, 2, 0, 0)

        // “Brilho” fake controlado
        _Smoothness ("Highlight Amount", Range(0,1)) = 0.98
    }

    SubShader
    {
        Tags{
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Forward"
            Tags{ "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _Alpha;
                float  _Hue, _Saturation, _Lightness;

                float  _FresnelPower, _FresnelStrength;
                float4 _EdgeColor;
                float  _EdgeStrength;
                float  _SmoothAlphaByFresnel;

                float  _Distortion;
                float  _Chromatic;
                float  _NormalScale;

                float  _RoughnessScale, _RoughnessStrength;

                float4 _HeightTintColor;
                float  _HeightTintStrength;
                float4 _HeightRange; // x=start, y=end

                float  _Smoothness;
            CBUFFER_END

            TEXTURE2D(_NormalMap);         SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMask);     SAMPLER(sampler_RoughnessMask);
            TEXTURE2D_X(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                float3 viewDirWS  : TEXCOORD4;
            };

            // --- RGB <-> HSL helpers ---
            float3 rgb2hsl(float3 c)
            {
                float maxc = max(c.r, max(c.g, c.b));
                float minc = min(c.r, min(c.g, c.b));
                float h = 0.0, s = 0.0;
                float l = (maxc + minc) * 0.5;

                if (maxc != minc)
                {
                    float d = maxc - minc;
                    s = (l > 0.5) ? d / (2.0 - maxc - minc) : d / (maxc + minc);
                    if (maxc == c.r)       h = (c.g - c.b) / d + (c.g < c.b ? 6.0 : 0.0);
                    else if (maxc == c.g)  h = (c.b - c.r) / d + 2.0;
                    else                   h = (c.r - c.g) / d + 4.0;
                    h /= 6.0;
                }
                return float3(h, s, l);
            }

            float hue2rgb(float p, float q, float t)
            {
                if (t < 0.0) t += 1.0;
                if (t > 1.0) t -= 1.0;
                if (t < 1.0/6.0) return p + (q - p) * 6.0 * t;
                if (t < 1.0/2.0) return q;
                if (t < 2.0/3.0) return p + (q - p) * (2.0/3.0 - t) * 6.0;
                return p;
            }

            float3 hsl2rgb(float3 hsl)
            {
                float h = hsl.x, s = hsl.y, l = hsl.z;
                if (s == 0.0) return float3(l,l,l);
                float q = l < 0.5 ? l * (1.0 + s) : (l + s - l * s);
                float p = 2.0 * l - q;
                return float3(
                    hue2rgb(p,q,h + 1.0/3.0),
                    hue2rgb(p,q,h),
                    hue2rgb(p,q,h - 1.0/3.0)
                );
            }

            float3 ApplyHSL(float3 rgb, float hueDeg, float satMul, float lightAdd)
            {
                float3 hsl = rgb2hsl(rgb);
                float hue01 = frac(hsl.x + (hueDeg / 360.0));
                float sat = saturate(hsl.y * satMul);
                float light = saturate(hsl.z + lightAdd);
                return hsl2rgb(float3(hue01, sat, light));
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = NormalizeNormalPerVertex(normInputs.normalWS);
                OUT.tangentWS  = float4(normInputs.tangentWS, IN.tangentOS.w);
                OUT.uv         = IN.uv;
                OUT.viewDirWS  = GetWorldSpaceViewDir(OUT.positionWS);
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                // --- TBN / normal ---
                float3 bitangentWS = cross(IN.normalWS, IN.tangentWS.xyz) * IN.tangentWS.w;
                float3x3 tbn = float3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS);

                float4 nSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv);
                float3 nTS = UnpackNormal(nSample);
                nTS.xy *= _NormalScale;
                float3 normalWS = normalize(mul(nTS, tbn));

                // Fresnel
                float3 V = normalize(IN.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, V)), _FresnelPower);
                fresnel = saturate(fresnel) * _FresnelStrength;

                // Screen UV + distorção
                float2 uvScreen = GetNormalizedScreenSpaceUV(IN.positionCS);
                uvScreen += normalWS.xy * _Distortion;

                // Aberração cromática: offsets bem pequenos por canal
                float ca = _Chromatic * 0.002; // 0.001–0.003 é legal pra mobile
                float2 offR = uvScreen + float2(-ca, -ca) * normalWS.xy;
                float2 offG = uvScreen;
                float2 offB = uvScreen + float2( ca,  ca) * normalWS.xy;

                float3 sceneR = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, offR).rgb;
                float3 sceneG = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, offG).rgb;
                float3 sceneB = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, offB).rgb;

                float3 sceneCol = float3(sceneR.r, sceneG.g, sceneB.b);

                // Tint + HSL
                float3 tintAdj = ApplyHSL(_Tint.rgb, _Hue, _Saturation, _Lightness);
                float3 baseCol = sceneCol * tintAdj;

                // Borda colorida
                float3 edgeCol = _EdgeColor.rgb * (fresnel * _EdgeStrength);
                baseCol += edgeCol;

                // Gradiente por altura
                float h01 = saturate((IN.positionWS.y - _HeightRange.x) / max(0.0001, (_HeightRange.y - _HeightRange.x)));
                baseCol = lerp(baseCol, baseCol * _HeightTintColor.rgb, h01 * _HeightTintStrength);

                // Roughness mask (sujeira): reduz highlight em áreas “sujas”
                float2 uvR = IN.uv * _RoughnessScale;
                float mask = SAMPLE_TEXTURE2D(_RoughnessMask, sampler_RoughnessMask, uvR).r; // 0 limpo / 1 sujo
                float roughFactor = saturate(1.0 - mask * _RoughnessStrength);

                // “Highlight” simples guiado por fresnel
                float specBoost = _Smoothness * 0.25 * roughFactor;
                baseCol += specBoost * fresnel;

                // Alpha
                float alpha = _Alpha + (_SmoothAlphaByFresnel > 0.5 ? fresnel * 0.5 : 0.0);
                alpha = saturate(alpha);

                return float4(baseCol, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
