// Island lawns: grass colour painted in world space (no tiling seams), with big sunny and shady patches, dry
// yellowish spots, fine blade streaks and a soft wind sheen rolling over it. Soft wrapped lighting, no gloss.
Shader "Maya/Lawn"
{
    Properties
    {
        _BaseColor ("Grass", Color) = (0.35, 0.7, 0.3, 1)
        _DryColor ("Dry Patches", Color) = (0.72, 0.74, 0.36, 1)
        _LushColor ("Lush Patches", Color) = (0.16, 0.48, 0.26, 1)
        _Scale ("Pattern Scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _DryColor, _LushColor;
            float _Scale;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Lawn"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float fog : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.world = p.positionWS;
                o.normal = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.eyeDepth = -p.positionVS.z;
                return o;
            }

            float Hash (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), u.x), u.y);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 p = i.world.xz * _Scale;
                float near = 1 - saturate(i.eyeDepth / 60); // fine detail fades with distance (no shimmer)
                // Big patches: lush / dry, then mid-size mottling.
                // (noise sums bunch up around 0.5: stretch them so patches really show)
                float big = saturate((Noise(p * 0.08) * 0.6 + Noise(p * 0.21 + 3.1) * 0.4 - 0.5) * 2.6 + 0.5);
                float mid = saturate((Noise(p * 0.55 + 7.7) * 0.7 + Noise(p * 1.3 + 1.9) * 0.3 - 0.5) * 2.2 + 0.5);
                half3 col = lerp(_LushColor.rgb, _BaseColor.rgb, smoothstep(0.2, 0.55, big));
                col = lerp(col, _DryColor.rgb, smoothstep(0.58, 0.8, big) * 0.75);
                col *= lerp(0.8, 1.15, mid);
                // Fine blade streaks: stretched noise in two crossing directions, a few bright blade tips.
                float streak = Noise(float2(p.x * 9, p.y * 2.2)) * 0.5 + Noise(float2(p.x * 2.4 + p.y * 1.1, p.y * 10)) * 0.5;
                col *= lerp(1, lerp(0.84, 1.12, streak), near);
                // Wind sheen: soft bright bands drifting across the lawn.
                float t = _Time.y;
                float wind = Noise(p * 0.08 + float2(t * 0.18, t * 0.07));
                col *= 1 + smoothstep(0.55, 0.85, wind) * 0.12;

                float3 n = normalize(i.normal);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.world));
                half ndl = saturate((dot(n, light.direction) + 0.35) / 1.35);
                half shadow = lerp(0.35, 1, light.shadowAttenuation);
                half3 lit = col * (light.color * ndl * shadow * 0.95 + SampleSH(n) * 1.05);
                lit = MixFog(lit, i.fog);
                return half4(lit, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 vert (Attributes v) : SV_POSITION
            {
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(world, TransformObjectToWorldNormal(v.normalOS), _LightDirection));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return cs;
            }
            half4 frag () : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert (float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normal : TEXCOORD0; };
            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normal = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag (Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normal), 0); }
            ENDHLSL
        }
    }
}
