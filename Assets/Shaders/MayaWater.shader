// Stylised sea for URP: layered noise waves (no regular stripes), a sky gradient reflected by fresnel, sun glitter,
// clear shallows that show the bottom with moving caustics, and textured shoreline foam.
// Needs the camera depth texture (enabled in the URP assets).
Shader "Maya/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.25, 0.88, 0.82, 0.55)
        _Deep ("Deep", Color) = (0.02, 0.25, 0.5, 0.94)
        _SkyColor ("Sky Reflection (horizon)", Color) = (0.7, 0.88, 1, 1)
        _SkyTop ("Sky Reflection (overhead)", Color) = (0.35, 0.6, 0.95, 1)
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
                half4 _Shallow, _Deep, _SkyColor, _SkyTop, _FoamColor;
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

            // Wave height: a slow swell plus two layers of choppy noise drifting in different directions.
            float Height (float2 p, float t)
            {
                float h = sin(dot(p, float2(0.8, 0.6)) * 0.6 + t * 1.1) * 0.5 + sin(dot(p, float2(-0.4, 0.9)) * 0.9 + t * 1.4) * 0.3;
                h += Noise(p * 1.1 + float2(t * 0.35, t * 0.2)) * 0.9;
                h += Noise(p * 2.7 - float2(t * 0.25, -t * 0.45)) * 0.45;
                h += Noise(p * 6.3 + float2(-t * 0.6, t * 0.3)) * 0.18;
                return h;
            }

            // Bright web of light that the waves focus onto the sea floor.
            float Caustics (float2 p, float t)
            {
                float a = Noise(p * 1.6 + float2(t * 0.4, t * 0.25));
                float b = Noise(p * 1.6 * 1.37 - float2(t * 0.3, -t * 0.35) + 7.3);
                return pow(saturate(1 - abs(a - b) * 3.2), 6);
            }

            half4 frag (Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float t = _Time.y;
                float2 wp = i.world.xz * _WaveScale;
                // Far away the small waves shrink to a calm, glossy surface (no shimmering noise).
                float detail = lerp(1, 0.25, saturate(i.eyeDepth / 90));
                const float e = 0.06;
                float h0 = Height(wp, t);
                float hx = Height(wp + float2(e, 0), t);
                float hz = Height(wp + float2(0, e), t);
                float2 g = float2(hx - h0, hz - h0) / e * _WaveStrength * 0.55 * detail;
                float3 n = normalize(float3(-g.x, 1, -g.y));
                float3 v = normalize(GetCameraPositionWS() - i.world);
                bool below = !front;
                if (below) n = -n;

                float2 uv = i.screen.xy / i.screen.w;
                float rawDepth = SampleSceneDepth(uv);
                float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
                float diff = max(0, sceneZ - i.eyeDepth);
                float depth01 = saturate(diff / _DepthRange);

                half4 col;
                if (!below)
                {
                    // Body colour: clear and bright in the shallows, deep blue further out.
                    col = lerp(_Shallow, _Deep, smoothstep(0, 1, depth01));
                    col.a = lerp(_Shallow.a * 0.7, _Deep.a, smoothstep(0, 0.8, depth01));
                    // Caustics dancing on the bottom where it is shallow enough to see.
                    float3 bottom = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                    float caus = Caustics(bottom.xz * 0.45 + g * 0.3, t) * (1 - smoothstep(0.05, 0.7, depth01)) * detail;
                    col.rgb += caus * 0.55 * _Shallow.rgb;

                    Light sun = GetMainLight();
                    // Light passing through the wave crests.
                    float crest = saturate(h0 - 1.1) * (1 - depth01 * 0.5);
                    col.rgb += crest * _Shallow.rgb * 0.35;

                    // Fresnel reflection of a sky gradient (horizon colour low, overhead colour high).
                    float3 r = reflect(-v, n);
                    half3 sky = lerp(_SkyColor.rgb, _SkyTop.rgb, saturate(r.y * 1.6));
                    float fres = 0.04 + 0.96 * pow(1 - saturate(dot(n, v)), 5);
                    col.rgb = lerp(col.rgb, sky, fres * 0.85);
                    col.a = lerp(col.a, 1, fres);

                    // Sun: a soft sheen plus sparkling glitter on the tiny waves.
                    float sd = saturate(dot(r, sun.direction));
                    float glitter = step(0.82, Noise(i.world.xz * 3.1 + t * 0.9)) * pow(sd, 40) * detail;
                    float spec = pow(sd, 260) * 2.6 + pow(sd, 18) * 0.12 + glitter * 2.2;
                    col.rgb += spec * sun.color;
                    col.a = saturate(col.a + spec);

                    // Shoreline foam: broken, lacy and gently pulsing.
                    float lace = Noise(i.world.xz * 2.4 + float2(t * 0.3, -t * 0.2)) * 0.6 + Noise(i.world.xz * 5.1 - t * 0.4) * 0.4;
                    float band = saturate(1 - diff / (_FoamWidth * (0.75 + 0.35 * sin(t * 1.3 + lace * 4))));
                    float foam = smoothstep(0.35, 0.75, band * (0.55 + lace * 0.7));
                    foam = max(foam, smoothstep(0.92, 1, band));
                    col.rgb = lerp(col.rgb, _FoamColor.rgb, foam * 0.9);
                    col.a = max(col.a, foam * 0.95);
                }
                else
                {
                    // Seen from below: a bright, rippling ceiling with the sun shining through.
                    float shimmer = Caustics(i.world.xz * 0.6, t);
                    col.rgb = lerp(_Shallow.rgb, _SkyColor.rgb, 0.45 + 0.25 * g.x) + shimmer * 0.25;
                    col.a = 0.85;
                }
                col.rgb = MixFog(col.rgb, i.fog);
                return col;
            }
            ENDHLSL
        }
    }
}
