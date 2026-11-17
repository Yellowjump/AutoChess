Shader "UI/FormationPaper"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _UvArea ("Atlas UV area", Vector) = (0,0,1,1)
        _Fade ("Feather left, bottom, right, top", Vector) = (0,0,0,0)
        _Brush ("Dry brush", Float) = 0
    }
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

            sampler2D _MainTex;
            float4 _UvArea, _Fade;
            float _Brush;
            struct Input { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Output vert(Input v)
            {
                Output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float noise(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            fixed4 frag(Output i) : SV_Target
            {
                float2 p = saturate((i.uv - _UvArea.xy) / _UvArea.zw);
                float4 edge = float4(p, 1 - p);
                float4 fade = smoothstep(0, max(_Fade, .001), edge);
                fixed4 texel = tex2D(_MainTex, i.uv);
                fixed4 ink = texel * i.color;
                ink.a *= fade.x * fade.y * fade.z * fade.w;
                if (_Brush > .5)
                {
                    float strands = noise(float2(0, floor(p.y * 31)));
                    float grain = noise(floor(p * float2(128, 41)));
                    float taper = pow(abs(p.y * 2 - 1), 3) * .12;
                    float ends = min(p.x, 1 - p.x) - taper - strands * .035;
                    float lip = min(p.y, 1 - p.y) - .015 - grain * .06;
                    ink.rgb = i.color.rgb * lerp(.95, 1.03, grain);
                    if (_Brush < 1.5)
                        ink.a *= smoothstep(-.015, .05, ends) * smoothstep(0, .1, lip);
                }
                return ink;
            }
            ENDCG
        }
    }
}
