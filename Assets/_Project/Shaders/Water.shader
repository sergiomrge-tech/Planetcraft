// Água estilizada para URP: cor por profundidade, espuma na margem, ondas procedurais
// (só nos normais, o plano não se deforma), fresnel com a cor do céu e brilho do sol.
// Precisa de "Depth Texture" ligado no URP Asset para profundidade e espuma.
Shader "Planetcraft/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.22, 0.62, 0.68, 0.45)
        _DeepColor ("Deep Color", Color) = (0.03, 0.14, 0.28, 0.95)
        _DepthDistance ("Depth Distance", Float) = 7
        _FoamColor ("Foam Color", Color) = (0.95, 0.97, 1, 1)
        _FoamDistance ("Foam Distance", Float) = 0.35
        _WaveScale ("Wave Scale", Float) = 0.18
        _WaveSpeed ("Wave Speed", Float) = 0.5
        _WaveStrength ("Wave Normal Strength", Range(0, 1)) = 0.35
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _FresnelPower ("Fresnel Power", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthDistance;
                half4 _FoamColor;
                float _FoamDistance;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float _Smoothness;
                float _FresnelPower;
            CBUFFER_END

            // Definida globalmente pelo SkyReactor, acompanha a cor do céu.
            half4 _PlanetSkyColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453);
            }

            float GradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(dot(Hash2(i), f), dot(Hash2(i + float2(1, 0)), f - float2(1, 0)), u.x),
                    lerp(dot(Hash2(i + float2(0, 1)), f - float2(0, 1)), dot(Hash2(i + float2(1, 1)), f - float2(1, 1)), u.x),
                    u.y);
            }

            float WaveHeight(float2 p, float t)
            {
                return GradientNoise(p + float2(t, t * 0.7)) * 0.6
                     + GradientNoise(p * 2.3 - float2(t * 1.3, -t * 0.4)) * 0.3
                     + GradientNoise(p * 5.1 + float2(-t * 0.6, t * 1.7)) * 0.1;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float2 p = i.positionWS.xz * _WaveScale;
                const float e = 0.05;
                float h = WaveHeight(p, t);
                float hx = WaveHeight(p + float2(e, 0), t);
                float hz = WaveHeight(p + float2(0, e), t);
                float3 n = normalize(float3((h - hx) / e * _WaveStrength, 1.0, (h - hz) / e * _WaveStrength));

                // Profundidade da água atrás deste pixel
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterDepth = max(0.0, sceneDepth - i.screenPos.w);

                half4 col = lerp(_ShallowColor, _DeepColor, saturate(waterDepth / _DepthDistance));

                Light sun = GetMainLight();
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ndl = saturate(dot(n, sun.direction));
                col.rgb *= sun.color * (0.45 + 0.55 * ndl) + 0.15;

                // Lâmina rasa sobre o terreno reflete pouco, senão a margem fica esbranquiçada.
                float fresnel = pow(1.0 - saturate(dot(n, viewDir)), _FresnelPower) * saturate(waterDepth / 1.5);
                col.rgb = lerp(col.rgb, _PlanetSkyColor.rgb, fresnel * 0.7);
                col.a = lerp(col.a, 1.0, fresnel * 0.5);

                float3 halfDir = normalize(sun.direction + viewDir);
                float spec = pow(saturate(dot(n, halfDir)), exp2(_Smoothness * 10.0 + 1.0)) * _Smoothness;
                col.rgb += sun.color * spec;

                // Espuma na margem, quebrada por ruído para não virar uma linha
                float foamNoise = GradientNoise(i.positionWS.xz * 1.5 + t * 2.0) * 0.5 + 0.5;
                float foam = saturate(1.0 - waterDepth / (_FoamDistance * (0.6 + foamNoise * 0.8)));
                foam = smoothstep(0.45, 0.75, foam) * 0.8;
                col.rgb = lerp(col.rgb, _FoamColor.rgb * (sun.color * 0.6 + 0.4), foam);
                col.a = max(col.a, foam * _FoamColor.a);

                col.rgb = MixFog(col.rgb, i.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
}
