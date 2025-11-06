Shader "Hidden/Daltonismo/URPBlit"
{
    Properties
    {
        _Intensity ("Intensidade (0-1)", Range(0,1)) = 1
        _CustomRow0 ("Custom Row 0 (r,g,b)", Vector) = (1,0,0,0)
        _CustomRow1 ("Custom Row 1 (r,g,b)", Vector) = (0,1,0,0)
        _CustomRow2 ("Custom Row 2 (r,g,b)", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "IgnoreProjector"="True" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "DaltonismoPass"
            HLSLPROGRAM
            // Padrão URP 14: usar VS de fullscreen do Blit.hlsl e amostrar _CameraOpaqueTexture
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            // escolha do modo por keyword
            #pragma multi_compile_local __ MODE_PROTAN MODE_DEUTER MODE_TRITAN MODE_CUSTOM

            // Textura de entrada provida pelo ConfigureInput(Color)
            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            float _Intensity;
            float4 _CustomRow0;
            float4 _CustomRow1;
            float4 _CustomRow2;

            half3x3 GetMatrix()
            {
                #if defined(MODE_PROTAN)
                    return half3x3(0.56667h,0.43333h,0.00000h,
                                   0.55833h,0.44167h,0.00000h,
                                   0.00000h,0.24167h,0.75833h);
                #elif defined(MODE_DEUTER)
                    return half3x3(0.62500h,0.37500h,0.00000h,
                                   0.70000h,0.30000h,0.00000h,
                                   0.00000h,0.30000h,0.70000h);
                #elif defined(MODE_TRITAN)
                    return half3x3(0.95000h,0.05000h,0.00000h,
                                   0.00000h,0.43300h,0.56700h,
                                   0.00000h,0.47500h,0.52500h);
                #elif defined(MODE_CUSTOM)
                    return half3x3(_CustomRow0.x,_CustomRow0.y,_CustomRow0.z,
                                   _CustomRow1.x,_CustomRow1.y,_CustomRow1.z,
                                   _CustomRow2.x,_CustomRow2.y,_CustomRow2.z);
                #else
                    return half3x3(1,0,0, 0,1,0, 0,0,1);
                #endif
            }

            half4 Frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // UV vem do Vert de Blit.hlsl: input.texcoord
                half3 src = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.texcoord).rgb;
                half3 sim = mul(GetMatrix(), src);
                half  t = saturate(_Intensity);
                half3 outRgb = lerp(src, saturate(sim), t);
                return half4(outRgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
