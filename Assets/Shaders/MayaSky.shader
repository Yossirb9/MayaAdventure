// Stylised gradient sky with a soft sun and drifting procedural clouds.
Shader "Maya/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.22, 0.5, 0.95, 1)
        _Horizon ("Horizon", Color) = (0.78, 0.9, 1, 1)
        _Bottom ("Bottom", Color) = (0.5, 0.7, 0.9, 1)
        _SunColor ("Sun", Color) = (1, 0.93, 0.78, 1)
        _SunSize ("Sun Size", Range(0.001, 0.1)) = 0.035
        _CloudColor ("Cloud Light", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud Shade", Color) = (0.72, 0.78, 0.9, 1)
        _CloudAmount ("Cloud Amount", Range(0, 1)) = 0.45
        _Stars ("Stars", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top, _Horizon, _Bottom, _SunColor, _CloudColor, _CloudShade;
            float _SunSize, _CloudAmount, _Stars;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm (float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += Noise(p) * a; p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 col = y > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(y), 0.55))
                                   : lerp(_Horizon.rgb, _Bottom.rgb, pow(saturate(-y), 0.35));

                float3 s = normalize(_WorldSpaceLightPos0.xyz);
                float sd = dot(d, s);
                float disk = smoothstep(1 - _SunSize * 0.02, 1 - _SunSize * 0.012, sd);
                float glow = pow(saturate(sd), 48) * 0.6 + pow(saturate(sd), 6) * 0.18;
                col += _SunColor.rgb * (disk * 6 + glow);

                if (_Stars > 0 && y > 0)
                {
                    float2 sp = d.xz / (y + 0.3) * 160;
                    float st = step(0.997, Hash(floor(sp))) * saturate(y * 3);
                    col += st * _Stars * 1.5;
                }

                if (y > 0)
                {
                    float2 uv = d.xz / (y + 0.12) * 0.55 + _Time.y * float2(0.004, 0.002);
                    float n = Fbm(uv * 2.2);
                    float c = smoothstep(1 - _CloudAmount, 1 - _CloudAmount + 0.28, n) * saturate(y * 5);
                    float lit = saturate(Fbm(uv * 2.2 + s.xz * 0.08) - n + 0.6);
                    float3 cloud = lerp(_CloudShade.rgb, _CloudColor.rgb, lit);
                    col = lerp(col, cloud, c * 0.9);
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
