// Unlit alpha-blended shader (beams, shadows, shield bubble, speed streaks).
Shader "SkyDrop/Transparent"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.5)
        _FogAmount ("Fog Amount", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _FogAmount;
            fixed4 _SD_FogColor;
            float4 _SD_FogParams;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
            };

            v2f vert (float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.wpos = mul(unity_ObjectToWorld, vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = distance(i.wpos, _WorldSpaceCameraPos);
                float f = saturate((d - _SD_FogParams.x) / max(_SD_FogParams.y - _SD_FogParams.x, 0.001)) * _FogAmount;
                return fixed4(lerp(_Color.rgb, _SD_FogColor.rgb, f), _Color.a * (1 - f * 0.7));
            }
            ENDCG
        }
    }
}
