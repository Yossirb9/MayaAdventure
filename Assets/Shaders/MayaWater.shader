// Stylised water for URP: depth-tinted colour, animated normals, fresnel sky reflection, sun glints and shoreline foam.
// Needs the camera depth texture (enabled in the URP assets).
Shader "Maya/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.25, 0.88, 0.82, 0.55)
        _Deep ("Deep", Color) = (0.02, 0.25, 0.5, 0.94)
        _SkyColor ("Sky Reflection", Color) = (0.7, 0.88, 1, 1)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _DepthRange ("Depth Range", Float) = 6
        _FoamWidth ("Foam Width", Float) = 0.9
        _WaveScale ("Wave Scale", Float) = 0.35
        _WaveStrength ("Wave Strength", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _SkyColor, _FoamColor;
                float _DepthRange, _FoamWidth, _WaveScale, _WaveStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 world : TEXCOORD0;
                float4 screen : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.world = p.positionWS;
                o.screen = ComputeScreenPos(p.positionCS);
                o.eyeDepth = -p.positionVS.z;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Slope of a sum of travelling sine waves.
            float2 WaveSlope (float2 p, float t)
            {
                float2 g = 0;
                g += float2(0.8, 0.6) * cos(dot(p, float2(0.8, 0.6)) * 1.0 + t * 1.3);
                g += float2(-0.5, 0.86) * cos(dot(p, float2(-0.5, 0.86)) * 1.9 + t * 1.8) * 0.6;
                g += float2(0.3, -0.95) * cos(dot(p, float2(0.3, -0.95)) * 3.1 + t * 2.4) * 0.35;
                g += float2(-0.92, -0.2) * cos(dot(p, float2(-0.92, -0.2)) * 5.3 + t * 3.1) * 0.2;
                g += float2(0.6, -0.8) * cos(dot(p, float2(0.6, -0.8)) * 8.7 + t * 3.9) * 0.12;
                return g;
            }

            half4 frag (Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float t = _Time.y;
                float2 g = WaveSlope(i.world.xz * _WaveScale, t) * _WaveStrength;
                float3 n = normalize(float3(-g.x, 1, -g.y));
                float3 v = normalize(GetCameraPositionWS() - i.world);
                bool below = !front;
                if (below) n = -n;

                float2 uv = i.screen.xy / i.screen.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float diff = max(0, sceneZ - i.eyeDepth);
                float depth01 = saturate(diff / _DepthRange);

                half4 col = lerp(_Shallow, _Deep, depth01);
                float fres = pow(1 - saturate(dot(n, v)), 4);
                if (!below)
                {
                    col.rgb = lerp(col.rgb, _SkyColor.rgb, fres * 0.6);
                    col.a = lerp(col.a, 1, fres * 0.6);
                    Light sun = GetMainLight();
                    float spec = pow(saturate(dot(reflect(-sun.direction, n), v)), 220) * 3.0;
                    col.rgb += spec * sun.color;
                    col.a = saturate(col.a + spec);

                    // Shoreline foam with a little wobble.
                    float wob = sin(i.world.x * 1.7 + t * 1.5) * sin(i.world.z * 1.3 - t * 1.1) * 0.5 + 0.5;
                    float foam = saturate(1 - diff / (_FoamWidth * (0.6 + 0.8 * wob)));
                    foam = smoothstep(0.25, 0.85, foam);
                    col.rgb = lerp(col.rgb, _FoamColor.rgb, foam);
                    col.a = max(col.a, foam);
                }
                else
                {
                    // Seen from below: bright, rippling surface.
                    col.rgb = lerp(_Shallow.rgb, _SkyColor.rgb, 0.5 + 0.3 * g.x);
                    col.a = 0.85;
                }
                col.rgb = MixFog(col.rgb, i.fog);
                return col;
            }
            ENDHLSL
        }
    }
}
