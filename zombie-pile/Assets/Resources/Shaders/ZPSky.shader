// Gradient skybox with a soft sun. Colors are set per world from SkyEnv.
Shader "ZombiePile/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.25, 0.5, 0.9, 1)
        _Horizon ("Horizon", Color) = (0.6, 0.8, 1, 1)
        _Bottom ("Bottom", Color) = (0.5, 0.6, 0.7, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top, _Horizon, _Bottom;
            float4 _SD_LightDir;
            fixed4 _SD_LightColor;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert (float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.dir = vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                fixed3 c = d.y > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(d.y), 0.55))
                                   : lerp(_Horizon.rgb, _Bottom.rgb, pow(saturate(-d.y), 0.4));
                float s = saturate(dot(d, normalize(_SD_LightDir.xyz)));
                c += _SD_LightColor.rgb * (pow(s, 400.0) * 1.5 + pow(s, 12.0) * 0.25);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
