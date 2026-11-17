Shader "WorldTimeline/Ashridge"
{
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

            struct Attributes
            {
                float3 position : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 local : TEXCOORD0;
                float3 world : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position);
                output.world = TransformObjectToWorld(input.position);
                output.local = input.position;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float Hash(float2 cell)
            {
                float3 p = frac(cell.xyx * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1.0), f.x), f.y);
            }

            float2 Swirl(float2 p, float2 center, float radius, float strength)
            {
                float2 delta = p - center;
                float weight = saturate(1.0 - length(delta) / radius);
                float angle = strength * weight * weight;
                float sine, cosine;
                sincos(angle, sine, cosine);
                return center + float2(cosine * delta.x - sine * delta.y, sine * delta.x + cosine * delta.y);
            }

            float2 Flow(float2 p)
            {
                p = Swirl(p, float2(-3.95, -1.65), 3.2, 4.9);
                p = Swirl(p, float2(4.05, 2.15), 3.9, -4.2);
                p = Swirl(p, float2(-3.25, 7.70), 5.5, -4.0);
                float2 drift = float2(Noise(p * float2(.24, .13) + 12.5),
                    Noise(p * float2(.21, .17) - 7.2)) - .5;
                float2 bend = float2(1.30 * sin(p.y * .32 + .6) + .53 * sin(p.y * .82),
                    .45 * sin(p.x * .56));
                return p + bend + drift * float2(2.9, 2.2);
            }

            half3 Ground(Varyings input)
            {
                float2 p = input.local.xz;
                float2 q = Flow(p);
                float flank = smoothstep(1.8, 6.25, abs(p.x + .26 * sin(p.y * .61)));
                float weathering = Noise(q * float2(.28, .12));
                float swirls = sin(q.x * 1.64 + sin(q.y * .46 + q.x * .54) * 1.65
                    + Noise(q * float2(.56, .24)) * 3.9);
                float fine = sin(q.x * 4.2 + sin(q.y * .64 + q.x * .48) * 2.65 + weathering * 5.5);
                float feather = smoothstep(.28, .70, Noise(q * float2(.49, .26)));
                float thickStroke = smoothstep(.38, .64, swirls) * feather;
                float contour = 1.0 - smoothstep(.036, .15, abs(swirls - .18));
                float dryStroke = smoothstep(.76, .96, fine) * smoothstep(.34, .65, weathering);
                float banks = smoothstep(.43, .64, weathering + flank * .27) * flank;
                float ink = saturate(banks * .85 + thickStroke * lerp(.34, .87, flank)
                    + contour * feather * lerp(.08, .36, flank) + dryStroke * lerp(.08, .53, flank));
                float farValley = smoothstep(4.5, 15.0, p.y);
                float grain = .92 + .11 * Noise(q * 2.9) + .055 * Noise(p * 11.0);
                return lerp(input.color.rgb * grain, half3(.034, .036, .039),
                    ink * lerp(.66, .97, max(flank, farValley * .70)));
            }

            half3 Rock(Varyings input)
            {
                float3 p = input.local;
                float fracture = Noise(float2(p.x * 3.5 + p.z * 1.75 - p.y * .92, p.y * .47));
                float layers = Noise(float2((p.x + p.z * .35) * 1.45, p.y * 3.5 - p.z * .54));
                float ash = smoothstep(.46, .81, layers) * smoothstep(.30, .86, fracture);
                float dust = (1.0 - smoothstep(.14, 1.38, p.y)) * Noise(p.xz * .83);
                float seam = smoothstep(.53, .80, fracture);
                return input.color.rgb * (.62 + .48 * layers + seam * .36)
                    + half3(.067, .070, .076) * (ash * .49 + dust * .25);
            }

            half3 Clouds(Varyings input)
            {
                float2 p = input.local.xy * float2(.040, .095);
                p.x -= _Time.y * .017;
                float cloud = Noise(p + Noise(p * .52) * 1.5) * .60
                    + Noise(p * 2.1 + 2.1) * .28 + Noise(p * 4.0) * .12;
                float ceiling = smoothstep(.07, .72, input.uv.y);
                half3 light = lerp(half3(.48, .49, .51), half3(.33, .35, .39), ceiling);
                return lerp(light, half3(.16, .175, .205), smoothstep(.24, .78, cloud) * lerp(.54, .88, ceiling));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (input.color.a > .65 && input.color.a < .9)
                    return half4(Clouds(input), 1);

                float viewDistance = distance(input.world, GetCameraPositionWS());
                float depthMist = smoothstep(13.0, 90.0, viewDistance);
                float valleyMist = smoothstep(12.0, 43.0, viewDistance) * exp(-max(0.0, input.local.y) * .51);
                float haze = saturate(depthMist * .90 + valleyMist * .35);
                half3 hazeColor = lerp(half3(.31, .329, .353), half3(.385, .405, .434), depthMist);
                half3 color;
                if (input.color.a > .9)
                {
                    float edge = abs(input.uv.x * 2.0 - 1.0);
                    float seam = 1.0 - smoothstep(.29, .91, edge);
                    float core = 1.0 - smoothstep(.10, .42, edge);
                    float flicker = .83 + .17 * sin(_Time.y * 1.8 + input.local.z * 1.12 + input.local.y * 4.2);
                    color = lerp(half3(.046, .029, .024), input.color.rgb * flicker, seam * .86);
                    color += half3(.28, .18, .047) * core * flicker;
                }
                else
                {
                    color = input.color.a > .15 ? Rock(input) : Ground(input);
                }
                return half4(lerp(color, hazeColor, haze), 1);
            }
            ENDHLSL
        }
    }
}
