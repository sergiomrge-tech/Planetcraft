// Camada de nuvens procedural num plano alto. _Coverage (0-1) é controlado pelo WeatherController.
Shader "Planetcraft/CloudLayer"
{
    Properties
    {
        _Coverage ("Coverage", Range(0, 1)) = 0
        _Softness ("Softness", Range(0.01, 0.5)) = 0.16
        _Scale ("Scale", Float) = 0.004
        _Speed ("Wind Speed", Float) = 0.01
        _LitColor ("Lit Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color", Color) = (0.6, 0.64, 0.7, 1)
        _FadeDistance ("Fade Distance", Float) = 1300
        _Opacity ("Opacity", Range(0, 1)) = 0.95
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Clouds"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Coverage;
                float _Softness;
                float _Scale;
                float _Speed;
                half4 _LitColor;
                half4 _ShadeColor;
                float _FadeDistance;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                return o;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    v += a * ValueNoise(p);
                    p = p * 2.03 + float2(17.1, 9.3);
                    a *= 0.5;
                }
                return v;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 wind = float2(1.0, 0.35) * _Time.y * _Speed;
                float2 p = i.positionWS.xz * _Scale + wind;
                float n = Fbm(p);

                float threshold = lerp(0.85, 0.25, _Coverage);
                float density = smoothstep(threshold, threshold + _Softness, n);

                float detail = Fbm(p * 3.1 - wind * 2.0);
                float lit = saturate(0.45 + (detail - 0.5) * 1.3 + (1.0 - density) * 0.25);
                Light sun = GetMainLight();
                half3 col = saturate(lerp(_ShadeColor.rgb, _LitColor.rgb, lit) * sun.color);

                float dist = length(i.positionWS.xz - _WorldSpaceCameraPos.xz);
                float fade = 1.0 - smoothstep(_FadeDistance * 0.5, _FadeDistance, dist);
                float alpha = density * fade * _Opacity * step(0.001, _Coverage);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
