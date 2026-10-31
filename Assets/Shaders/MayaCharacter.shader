// Maya herself, lit like a soft studio portrait instead of the harsh outdoor PBR look: wrapped half-lambert light,
// soft shadows, a fill light from the camera, a warm rim and no gloss. "metallicFactor" > 0 turns her chrome (Metal cap).
// Property names match glTFast's materials, so the existing property blocks keep working.
Shader "Maya/Character"
{
    Properties
    {
        [MainTexture] baseColorTexture ("Base Color", 2D) = "white" {}
        [MainColor] baseColorFactor ("Base Color Factor", Color) = (1, 1, 1, 1)
        [Normal] normalTexture ("Normal Map", 2D) = "bump" {}
        normalTexture_scale ("Normal Scale", Float) = 1
        metallicFactor ("Metal", Range(0, 1)) = 0
        _FaceTex ("Face Expressions", 2D) = "white" {}
        _FaceRect ("Face Rect (uv min, max)", Vector) = (0, 0, 0, 0)
        _FaceRect2 ("Eye Corner Rect", Vector) = (0, 0, 0, 0)
        _FaceLayout ("Atlas: split, rect2 height, tiles", Vector) = (1, 1, 1, 0)
        _FaceIndex ("Expression", Float) = 0
        _JacketTint ("Jacket Colour (a = on)", Color) = (0, 0, 0, 0)
        _Wrap ("Light Wrap", Range(0, 1)) = 0.55
        _ShadowSoftness ("Shadow Softness", Range(0, 1)) = 0.5
        _Fill ("Camera Fill", Range(0, 1)) = 0.32
        _Rim ("Rim", Range(0, 1)) = 0.35
        _Saturation ("Saturation", Range(0, 2)) = 0.92
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 baseColorTexture_ST;
            half4 baseColorFactor;
            float4 _FaceRect, _FaceRect2, _FaceLayout;
            float _FaceIndex;
            half4 _JacketTint;
            half normalTexture_scale, metallicFactor, _Wrap, _ShadowSoftness, _Fill, _Rim, _Saturation;
        CBUFFER_END
        TEXTURE2D(baseColorTexture); SAMPLER(sampler_baseColorTexture);
        TEXTURE2D(normalTexture); SAMPLER(sampler_normalTexture);
        TEXTURE2D(_FaceTex); SAMPLER(sampler_FaceTex);
        ENDHLSL

        Pass
        {
            Name "Character"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float fog : TEXCOORD3;
                float4 tangent : TEXCOORD4;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.world = p.positionWS;
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.normal = nrm.normalWS;
                o.tangent = float4(nrm.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, baseColorTexture);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Negative mip bias keeps her eyes and face crisp at game distance.
                half3 albedo = SAMPLE_TEXTURE2D_BIAS(baseColorTexture, sampler_baseColorTexture, i.uv, -0.8).rgb;
                if (_FaceIndex > 0.5)
                {
                    // Expressions: the face parts of the texture come from a tile of the expression atlas instead.
                    // Each tile = the face rect on the left, the small eye-corner rect top-right.
                    float2 l1 = (i.uv - _FaceRect.xy) / (_FaceRect.zw - _FaceRect.xy);
                    float2 l2 = (i.uv - _FaceRect2.xy) / (_FaceRect2.zw - _FaceRect2.xy);
                    bool in1 = all(l1 >= 0) && all(l1 <= 1);
                    bool in2 = all(l2 >= 0) && all(l2 <= 1);
                    float2 t = in1 ? float2(l1.x * _FaceLayout.x, l1.y)
                                   : float2(_FaceLayout.x + l2.x * (1 - _FaceLayout.x), 1 - _FaceLayout.y + l2.y * _FaceLayout.y);
                    t.y = (t.y + (_FaceLayout.z - 1 - _FaceIndex)) / _FaceLayout.z;
                    half3 face = SAMPLE_TEXTURE2D_GRAD(_FaceTex, sampler_FaceTex, t, ddx(i.uv) * 2, ddy(i.uv) * 2).rgb;
                    albedo = (in1 || in2) ? face : albedo;
                }
                if (_JacketTint.a > 0.01)
                {
                    // Recolour the pink clothes (jacket, sneakers, shirt hearts) but never the face.
                    float2 f1 = (i.uv - _FaceRect.xy) / (_FaceRect.zw - _FaceRect.xy);
                    bool onFace = all(f1 >= 0) && all(f1 <= 1);
                    half pink = saturate((albedo.r - albedo.g) * 5 - 0.5) * saturate((albedo.b - albedo.g + 0.04) * 10);
                    half lum = dot(albedo, half3(0.299, 0.587, 0.114));
                    albedo = lerp(albedo, _JacketTint.rgb * lum * 1.35, onFace ? 0 : pink * _JacketTint.a);
                }
                albedo *= baseColorFactor.rgb;
                half grey = dot(albedo, half3(0.299, 0.587, 0.114));
                albedo = lerp(grey.xxx, albedo, _Saturation);

                // The model's normal map carries the smooth surface of the sculpt (the mesh itself is faceted).
                half3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(normalTexture, sampler_normalTexture, i.uv), normalTexture_scale);
                float3 bitangent = i.tangent.w * cross(i.normal, i.tangent.xyz);
                float3 n = normalize(TransformTangentToWorld(tn, half3x3(i.tangent.xyz, bitangent, i.normal)));
                float3 v = normalize(GetCameraPositionWS() - i.world);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.world));
                half ndl = dot(n, light.direction);
                half wrap = saturate((ndl + _Wrap) / (1 + _Wrap));
                half shadow = lerp(1, light.shadowAttenuation, 1 - _ShadowSoftness);
                half3 direct = light.color * wrap * wrap * shadow * 0.85;
                half3 ambient = SampleSH(n) * 1.1 + 0.06;
                half ndv = saturate(dot(n, v));
                half3 fill = _Fill * ndv * half3(1, 0.97, 0.94);
                half rim = pow(1 - ndv, 4) * _Rim;
                half3 col = albedo * (direct + ambient + fill) + rim * (albedo * 0.6 + light.color * 0.15);

                if (metallicFactor > 0.01)
                {
                    // Metal cap: polished chrome reflecting the sky, with a sharp sun highlight.
                    float3 r = reflect(-v, n);
                    half3 env = GlossyEnvironmentReflection(r, 0.12, 1);
                    half spec = pow(saturate(dot(r, light.direction)), 60) * 3;
                    half3 chrome = env * baseColorFactor.rgb * 1.3 + light.color * spec + albedo * 0.08;
                    col = lerp(col, chrome, metallicFactor);
                }
                col = MixFog(col, i.fog);
                return half4(col, 1);
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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            float4 vert (Attributes v) : SV_POSITION
            {
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 dir = normalize(_LightPosition - world);
            #else
                float3 dir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(world, normal, dir));
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
