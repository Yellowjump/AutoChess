Shader "Custom/HexagonOutlineWithGradient"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0, 0, 1) // 描边颜色
        _OutlineWidth ("Outline Width UV", Range(0.0, 0.8660255)) = 0.1 // 描边宽度
        _OutlineOffset ("Outline Offset UV", Range(0.0, 0.8660255)) = 0.1 // 描边宽度
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha // 设置透明度混合模式
        ZWrite Off // 关闭深度写入

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

            float _OutlineWidth;
            float4 _OutlineColor;
            float _OutlineOffset;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0; // 将 UV 坐标从 [0,1] 映射到 [-1,1]
                return o;
            }

            float hexDistance(float2 uv)
            {
                uv = abs(uv);
                return max(uv.x/2+uv.y*1.7320508/2, uv.x); // dot(uv, float2(cos(30°), sin(30°)))
            }

            half4 frag (v2f i) : SV_Target
            {
                float distance = hexDistance(i.uv);
                

                // 计算从中心到边缘的透明度渐变
                float value = distance + _OutlineOffset - 0.8660255;
                float inRange = step(-_OutlineWidth, value) - step(0.0, value);
                float centerAlpha = lerp(0.0, 1.0, inRange);
                half4 color = half4(_OutlineColor.rgb, _OutlineColor.a * centerAlpha);

                return color;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
