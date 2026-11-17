Shader "WorldTimeline/CombatEnergy"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+30" "RenderType" = "Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha, Zero One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 position : POSITION;
                float3 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float profile = input.uv.z;
                half edge = saturate(1.0h - abs(input.uv.y * 2.0h - 1.0h));
                if (profile < -.5)
                {
                    float u = input.uv.x;
                    float v = input.uv.y;
                    if (profile < -4.5)
                    {
                        float2 p = input.uv.xy * 2.0 - 1.0;
                        float edgeJitter = sin(p.x * 15.0 + p.y * 8.0) * sin(p.y * 13.0);
                        half alpha = (1.0h - smoothstep(.18, 1.0, dot(p, p) + edgeJitter * .14))
                            * input.color.a;
                        return half4(input.color.rgb * alpha, alpha);
                    }
                    if (profile < -3.5)
                    {
                        float2 p = input.uv.xy * 2.0 - 1.0;
                        half area = 1.0h - smoothstep(.67h, 1.0h, length(p));
                        float2 ripple = sin(p.yx * 10.0 + _Time.y * 5.0);
                        float a = sin((p.x + p.y) * 18.0 + ripple.x * 1.1);
                        float b = sin((p.x - p.y) * 14.0 + ripple.y * 1.25);
                        half gleam = smoothstep(.87, .99, max(a, b));
                        half opacity = area * input.color.a * (.21h + gleam * .20h);
                        half3 water = lerp(input.color.rgb * .55h, half3(.59, .92, .91), gleam);
                        return half4(water * opacity, opacity);
                    }
                    float grain = sin(u * 61.0 + v * 15.0) * sin(u * 117.0 - v * 27.0);
                    half shape = smoothstep(.015, .115, edge + grain * .050);
                    half3 color = input.color.rgb;
                    if (profile < -1.5 && profile > -2.5)
                    {
                        float flow = v + .11 * sin(u * 29.0) + .05 * sin(u * 81.0);
                        half foam = smoothstep(.79, .93, flow + grain * .095);
                        half veins = smoothstep(.74, .98, sin(u * 62.0 + v * 15.0)) *
                            smoothstep(.11, .40, v) * (1.0 - smoothstep(.55, .85, v));
                        color = lerp(color * .52, color, saturate(flow));
                        color = lerp(color, half3(.84, 1, .99), saturate(foam * .95 + veins * .34));
                        shape *= lerp(.59, .97, saturate(foam + veins * .45));
                    }
                    else
                    {
                        color *= .96 + grain * .04;
                    }
                    half alpha = shape * input.color.a;
                    return half4(color * alpha, alpha);
                }
                half intensity = edge * edge;
                if (profile > .5 && profile < 1.5)
                {
                    half2 centered = input.uv.xy * 2.0h - 1.0h;
                    half radiusSquared = dot(centered, centered);
                    half halo = saturate(1.0h - radiusSquared);
                    intensity = exp2(-6.0h * radiusSquared) * halo * halo;
                }
                else if (profile > 1.5 && profile < 2.5)
                {
                    intensity = smoothstep(0.0h, .45h, edge);
                }
                else if (profile > 2.5)
                {
                    float segment = abs(frac(input.uv.x * (profile - 2.0)) - .5);
                    intensity *= 1.0h - smoothstep(.25h, .38h, segment);
                }
                return half4(input.color.rgb * (input.color.a * intensity), 0.0h);
            }
            ENDHLSL
        }
    }
}
