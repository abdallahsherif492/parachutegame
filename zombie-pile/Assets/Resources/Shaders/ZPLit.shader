// Low-poly lit shader for the built-in render pipeline.
// Uses its own global lighting/fog values (set from Shapes.cs (SkyEnv)) so it never
// depends on scene lighting or fog keyword variants being kept in WebGL builds.
Shader "ZombiePile/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Emission ("Emission", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Emission;

            float4 _SD_LightDir;
            fixed4 _SD_LightColor;
            fixed4 _SD_AmbientSky;
            fixed4 _SD_AmbientGround;
            fixed4 _SD_FogColor;
            float4 _SD_FogParams; // x = start, y = end

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 col : COLOR0;
                float3 wpos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 V = normalize(_WorldSpaceCameraPos - o.wpos);
                // soft "wrapped" diffuse: no harsh black sides, reads well on small screens
                float w = dot(n, normalize(_SD_LightDir.xyz)) * 0.5 + 0.5;
                float diff = w * w;
                fixed3 amb = lerp(_SD_AmbientGround.rgb, _SD_AmbientSky.rgb, n.y * 0.5 + 0.5);
                // rim light makes shapes pop from the background
                float rim = pow(1.0 - saturate(dot(n, V)), 3.0) * 0.45;
                fixed3 c = _Color.rgb * (amb * 0.9 + _SD_LightColor.rgb * diff * 0.85) + _SD_AmbientSky.rgb * rim;
                c = lerp(c, _Color.rgb * 1.2, _Emission);
                o.col = fixed4(c, 1);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = distance(i.wpos, _WorldSpaceCameraPos);
                float f = saturate((d - _SD_FogParams.x) / max(_SD_FogParams.y - _SD_FogParams.x, 0.001));
                f *= (1.0 - _Emission * 0.6);
                return fixed4(lerp(i.col.rgb, _SD_FogColor.rgb, f), 1);
            }
            ENDCG
        }
    }
}
