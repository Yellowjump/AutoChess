Shader "Custom/CloudShader"
{
    Properties
    {
        _CloudTex("Cloud Texture", 2D) = "white" {}
        _MaskTex("Mask Texture", 2D) = "white" {}
        _TilingFactor("Tiling Factor", Float) = 1.0
        _CloudSpeed("Cloud Speed", Vector) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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

            sampler2D _CloudTex;
            float _TilingFactor;
            float4 _CloudSpeed;
            sampler2D _MaskTex;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 BlendTwoCloud(float2 uv)
            {
                float4 c = tex2D(_CloudTex, uv * _TilingFactor + _Time.x * _CloudSpeed.xy);
                c.g = c.r;
                c.b = c.r;
                c.a = min(c.r*5,1);
                float4 c2 = tex2D(_CloudTex, uv + _Time.x * _CloudSpeed.zw);
                c2.r = c2.g;
                c2.b = c2.g;
                c2.a = min(c2.g*5,1);
                c = (c+ c2)/1;
                c= min(c, 1);
                return c;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 color = BlendTwoCloud(i.uv);
                // 从mask贴图的R通道设置透明度
                float mask = tex2D(_MaskTex, i.uv).r;
                color.a = mask;
                return color;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}