Shader "WorldTimeline/CollectionItemInk"
{
    Properties { [PerRendererData] _MainTex ("Item illustration", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Output { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            Output vert(Input v)
            {
                Output o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(Output i) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, i.uv);
                // The inventory atlas is painted on cream paper. Lift its ink onto the book page.
                half green = sample.g;
                #ifndef UNITY_COLORSPACE_GAMMA
                    green = LinearToGammaSpace(half3(green,green,green)).g;
                #endif
                half ink = 1.0h - smoothstep(.79h,.90h,green);
                return fixed4(sample.rgb * i.color.rgb, sample.a * ink * i.color.a);
            }
            ENDCG
        }
    }
}
