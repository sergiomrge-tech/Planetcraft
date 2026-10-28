// Sprite suave para partículas de máquinas (fumaça, poeira, névoa, faíscas) no HDRP: transparente, sem iluminação,
// cor vinda do sistema de partículas. Fica na pasta de Resources por meio do material Fx/FxSoft.
Shader "Planetcraft/FxSoft"
{
    Properties
    {
        _MainTex("Textura suave", 2D) = "white" {}
        [Toggle] _Additive("Aditivo", Float) = 0
        // Unlit do HDRP é em nits: sem isso a cor (0-1) some depois da exposição. ~10000 = fumaça sob o sol; 300-800 = holograma em ambiente interno.
        _Intensity("Intensidade (nits)", Float) = 10000
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _Intensity;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionRWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 c = input.color * tex;
                c.rgb *= _Intensity * GetCurrentExposureMultiplier();
                return c;
            }
            ENDHLSL
        }
    }
}
