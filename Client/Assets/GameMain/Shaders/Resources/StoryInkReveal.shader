Shader "UI/Story Ink Reveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _InkProgress ("Ink Progress", Range(0, 1)) = 1
        _ArtUv ("Sprite UV Bounds", Vector) = (0, 0, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"
               "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 paper : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float4 _ArtUv;
            float _InkProgress;

            Varyings Vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.texcoord.xy;
                output.paper = (output.uv - _ArtUv.xy) / max(_ArtUv.zw - _ArtUv.xy, 0.0001);
                return output;
            }

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 samplePosition)
            {
                float2 cell = floor(samplePosition);
                float2 weight = frac(samplePosition);
                weight = weight * weight * (3.0 - 2.0 * weight);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), weight.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), weight.x), weight.y);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, input.uv) + _TextureSampleAdd) * input.color;
                if (_InkProgress < 1.0)
                {
                    float2 paper = saturate(input.paper);
                    float direction = dot(float2(paper.x, 1.0 - paper.y), float2(0.64, 0.36));
                    float edge = 0.20 * Noise(paper * float2(5, 7))
                        + 0.07 * Noise(paper * float2(17, 24));
                    float distance = lerp(-0.08, 1.33, _InkProgress) - direction - edge;
                    color.a *= smoothstep(-0.05, 0.05, distance);
                    color.rgb *= lerp(0.66, 1.0, smoothstep(-0.02, 0.10, distance));
                }
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
