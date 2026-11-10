Shader "WorldTimeline/BattleStone"
{
    Properties { _ArenaSurfaces ("Arena Surfaces", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _ArenaSurfaces;
            CBUFFER_END
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 localPosition : TEXCOORD0;
                half4 color : COLOR;
            };
            Varyings Vert(float3 position : POSITION, half4 color : COLOR)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position);
                output.localPosition = position;
                output.color = color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.localPosition.xz;
                float grain = frac(sin(dot(floor(p * 75), float2(12.9898, 78.233))) * 43758.5453);
                float mottling = sin(p.x * 13 + sin(p.y * 9)) * sin(p.y * 15);
                if (_ArenaSurfaces > 0.5 && input.color.a > 0.4)
                {
                    float wave = sin(p.y * 12 + sin(p.x * 3.2 + p.y * 1.4) * 1.2 - _Time.y * 2.5);
                    float current = sin(p.x * 2.4 + p.y * 3.1 - _Time.y * 1.4);
                    float shore = (1 - smoothstep(.05, .75, abs(p.y - 4.9)))
                        * (1 - smoothstep(4.0, 5.4, abs(p.x + .25)));
                    float foam = smoothstep(.77, .99, wave) * smoothstep(-.25, .7, current);
                    return half4(input.color.rgb * (.96 + .035 * wave + .065 * current)
                        + half3(.17, .23, .21) * foam * (.035 + .25 * shore), 1);
                }
                if (_ArenaSurfaces > 0.5 && input.color.a > 0.1 && input.localPosition.y > -.04)
                {
                    // Broad staggered stone joints. Their shape and scale are independent of the hex board.
                    float2 slabs = p / float2(1.52, .98) + float2(sin(p.y * 4), sin(p.x * 4.7)) * .035;
                    slabs.x += fmod(abs(floor(slabs.y)), 2) * .5;
                    float2 within = frac(slabs);
                    float2 toEdge = min(within, 1 - within);
                    float joint = smoothstep(.012, .034, min(toEdge.x, toEdge.y));
                    float variation = frac(sin(dot(floor(slabs), float2(39.31, 17.91))) * 19412.19);
                    return half4(input.color.rgb * (.87 + joint * (.09 + .09 * variation) + grain * .065 + mottling * .025), 1);
                }
                return half4(input.color.rgb * (0.95 + grain * 0.08 + mottling * 0.035), 1);
            }
            ENDHLSL
        }
    }
}
