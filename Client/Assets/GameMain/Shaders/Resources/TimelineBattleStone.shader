Shader "WorldTimeline/BattleStone"
{
    Properties
    {
        _ArenaSurfaces ("Arena Surfaces", Float) = 0
        _TidegateInk ("Tidegate Ink", Float) = 0
    }
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
                float _TidegateInk;
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
            float Hash(float2 p)
            {
                p = frac(p * float2(.3183, .3679));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y * 37.5);
            }

            float Wash(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            half3 TidegateSurface(float3 local, half4 pigment)
            {
                float2 p = local.xz;
                float x = abs(p.x + .25);
                float broad = Wash(p * .63);
                float stain = Wash(p * 2.1 + 11.3);
                float grain = Hash(floor(p * 64));
                half3 paper = half3(.52, .49, .425);
                float distanceWash = smoothstep(5.1, 21.0, p.y + broad * 1.8);

                if (pigment.a > .4)
                {
                    float bank = (1 - smoothstep(.10, 2.4, abs(x - 5.25)))
                        * (1 - smoothstep(4.8, 8.0, p.y));
                    float wave = sin(p.y * 10.0 + broad * 3.8 - _Time.y * .65);
                    float foam = smoothstep(.80, .99, wave) * smoothstep(.41, .77, stain);
                    half3 water = pigment.rgb * (.72 + .34 * broad)
                        + half3(.09, .10, .086) * foam * (.10 + bank * .88);
                    float waterHorizon = smoothstep(20, 29, max(abs(p.x), abs(p.y)));
                    return lerp(water, paper, max(distanceWash, waterHorizon));
                }

                if (pigment.a > .1 && local.y > -.04)
                {
                    float2 slabs = (p + float2(.25, 0)) / float2(1.57, 1.24);
                    slabs += float2(Wash(p * 2.9), Wash(p.yx * 2.9)) * .048;
                    slabs.x += fmod(abs(floor(slabs.y)), 2) * .5;
                    float2 toEdge = min(frac(slabs), 1 - frac(slabs));
                    float edge = smoothstep(2.85, 4.68, x + (broad - .5) * 1.05);
                    edge = max(edge, smoothstep(2.8, 4.8, p.y + (stain - .5) * 1.2) * .78);
                    float joint = 1 - smoothstep(.013, .037, min(toEdge.x, toEdge.y));
                    float damp = edge * smoothstep(.26, .67, stain * .68 + broad * .32);
                    float slabTone = Hash(floor(slabs)) - .5;
                    return pigment.rgb * (1.02 + slabTone * .075 + (grain - .5) * .038
                        - joint * (.105 + edge * .20) - damp * .34);
                }

                float stratification = Wash(float2(p.x + p.y * .6, local.y * 3.8) + 19);
                half3 surface = pigment.rgb * (.84 + broad * .20 + stratification * .19);
                float baseDamp = (1 - smoothstep(-.35, .62, local.y + stain * .45));
                surface *= 1 - baseDamp * .22;
                return lerp(surface, paper, distanceWash * .87);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_TidegateInk > .5)
                    return half4(TidegateSurface(input.localPosition, input.color), 1);
                float2 p = input.localPosition.xz;
                float grain = frac(sin(dot(floor(p * 75), float2(12.9898, 78.233))) * 43758.5453);
                float mottling = sin(p.x * 13 + sin(p.y * 9)) * sin(p.y * 15);
                if (_ArenaSurfaces > 0.5 && input.color.a > .92)
                {
                    float flow = sin(p.y * 2.2 + sin(p.x * 1.3 - _Time.y * .8) * 1.4 + _Time.y * 1.5);
                    float crust = smoothstep(.22, .81, sin(p.x * 2.2 + flow * .8) * sin(p.y * 2.7 + flow));
                    return half4(lerp(input.color.rgb * (.66 + flow * .11), half3(1, .63, .17), crust * .76), 1);
                }
                if (_ArenaSurfaces > 0.5 && input.color.a > .69)
                {
                    float shimmer = sin(p.x * 3.2 + p.y * 2.7) * sin(p.y * 2.4 + sin(p.x * 2.0));
                    return half4(input.color.rgb * (.68 + grain * .12 + smoothstep(.58, .98, shimmer) * .45), 1);
                }
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
