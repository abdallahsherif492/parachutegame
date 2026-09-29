// Ruined-city building sides: a wall colour with a grid of windows drawn in the shader (some lit),
// the same wrapped lighting and fog as ZombiePile/Lit. No textures, so every building only needs a few numbers.
Shader "ZombiePile/Facade"
{
    Properties
    {
        _WallColor ("Wall", Color) = (0.38, 0.32, 0.36, 1)
        _WinLit ("Lit window", Color) = (1, 0.76, 0.36, 1)
        _WinDark ("Dark window", Color) = (0.07, 0.06, 0.09, 1)
        _Lit ("Lit fraction", Range(0,1)) = 0.2
        _Seed ("Seed", Float) = 1
        _Glow ("Window glow", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _WallColor, _WinLit, _WinDark;
            half _Lit, _Glow;
            float _Seed;

            float4 _SD_LightDir;
            fixed4 _SD_LightColor;
            fixed4 _SD_AmbientSky;
            fixed4 _SD_AmbientGround;
            fixed4 _SD_FogColor;
            float4 _SD_FogParams;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float3 n : TEXCOORD1;
                fixed3 light : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.n = UnityObjectToWorldNormal(v.normal);
                float w = dot(o.n, normalize(_SD_LightDir.xyz)) * 0.5 + 0.5;
                fixed3 amb = lerp(_SD_AmbientGround.rgb, _SD_AmbientSky.rgb, o.n.y * 0.5 + 0.5);
                o.light = amb * 0.9 + _SD_LightColor.rgb * (w * w) * 0.85;
                return o;
            }

            float hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                fixed3 col = _WallColor.rgb * i.light;
                fixed3 glow = 0;
                if (abs(n.y) < 0.5)
                {
                    // along the wall, and up it, in window cells 2.6 m wide and 3.2 m high
                    float u = dot(i.wpos.xz, float2(n.z, -n.x)) / 2.6;
                    float v = i.wpos.y / 3.2;
                    float2 id = floor(float2(u, v));
                    float2 f = frac(float2(u, v));
                    float win = step(0.2, f.x) * step(f.x, 0.8) * step(0.22, f.y) * step(f.y, 0.78) * step(1.0, id.y);
                    // far away the grid would shimmer: blend to the average colour
                    float fade = saturate(max(fwidth(u), fwidth(v)) * 1.6);
                    win *= 1.0 - fade;
                    float lit = step(hash21(id + _Seed), _Lit);
                    fixed3 wc = lerp(_WinDark.rgb * i.light, _WinLit.rgb, lit);
                    col = lerp(col, wc, win);
                    glow = _WinLit.rgb * lit * win * _Glow;
                }
                else if (n.y > 0.5) col *= 0.8;
                col += glow;
                float d = distance(i.wpos, _WorldSpaceCameraPos);
                float fg = saturate((d - _SD_FogParams.x) / max(_SD_FogParams.y - _SD_FogParams.x, 0.001));
                return fixed4(lerp(col, _SD_FogColor.rgb, fg), 1);
            }
            ENDCG
        }
    }
}
