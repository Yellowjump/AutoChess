Shader "Custom/UVColorShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // 可选的主纹理
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // 使用 UV 坐标来确定颜色
                float2 uv = i.uv;
                half4 color = half4(uv.x, uv.y, 1.0, 1.0); // 根据 UV 值确定颜色
                return color;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
