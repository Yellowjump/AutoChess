Shader "WorldTimeline/LavaPool"
{
    Properties
    {
        _Age ("Age in seconds", Float) = 0
        _Pulse ("Damage pulse", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Age;
                float _Pulse;
                float4 _Centers[3];
            CBUFFER_END

            struct Attributes
            {
                float3 position : POSITION;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            float Spread()
            {
                float remaining = 1.0 - saturate(_Age / 0.68);
                return 1.0 - 0.78 * remaining * remaining * (0.72 + 0.28 * remaining);
            }

            // Coverage stays inside the union of the authored gameplay circles.
            float3 PoolDepth(float2 ground, float spread, out float2 flow)
            {
                float hard = -10.0;
                float front = -10.0;
                float body = -10.0;
                float2 drift = 0.0;
                float weightSum = 0.001;
                [unroll] for (int i = 0; i < 3; i++)
                {
                    float4 circle = _Centers[i];
                    if (circle.w < 0.5) continue;
                    float2 offset = ground - circle.xy;
                    float radialDistance = length(offset);
                    hard = max(hard, circle.z - radialDistance);
                    float depth = circle.z * spread - radialDistance;
                    front = max(front, depth);
                    float join = saturate(1.0 - abs(depth - body) / 0.16);
                    body = max(body, depth) + join * join * 0.04;
                    float weight = saturate(1.0 - radialDistance / circle.z);
                    weight = weight * weight;
                    drift += offset * weight;
                    weightSum += weight;
                }
                flow = drift / weightSum;
                return float3(hard, front, body);
            }

            float PoolEdge(float3 depth, float2 ground)
            {
                float2 crawl = ground + float2(-0.075, 0.11) * _Age;
                float lobes = sin(dot(crawl, float2(13.0, 8.0))) * cos(dot(crawl, float2(-6.0, 17.0)));
                float roll = sin(dot(crawl, float2(20.0, -15.0)) - _Age * 1.7);
                float inset = 0.030 + 0.021 * lobes + 0.010 * roll;
                return depth.y - inset * (1.0 - smoothstep(0.0, 0.037, depth.z - depth.y));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.position);
                float spread = Spread();
                float2 drift;
                float3 depth = PoolDepth(world.xz, spread, drift);
                float edge = PoolEdge(depth, world.xz);
                float mound = smoothstep(0.0, 0.29, depth.z);
                float hump = smoothstep(0.0, 0.14, edge);
                float roll = sin(dot(world.xz, float2(8.0, 7.5)) - _Age * 3.2);
                float bulge = 0.094 + (1.0 - spread) * 0.37;
                world.y += hump * (0.031 + mound * (bulge + 0.028 * roll));
                world.y += 0.028 * smoothstep(0.0, 0.028, edge) * (1.0 - smoothstep(0.028, 0.13, edge));
                output.position = TransformWorldToHClip(world);
                output.world = world;
                return output;
            }

            float SurfaceNoise(float2 uv)
            {
                float2 cell = floor(uv);
                float2 t = frac(uv);
                t *= t * (3.0 - 2.0 * t);
                float4 seeds = dot(cell, float2(127.1, 311.7)) + float4(0.0, 127.1, 311.7, 438.8);
                float4 samples = frac(sin(seeds) * 43758.5453);
                return lerp(lerp(samples.x, samples.y, t.x), lerp(samples.z, samples.w, t.x), t.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 ground = input.world.xz;
                float spread = Spread();
                float2 drift;
                float3 depth = PoolDepth(ground, spread, drift);
                float edge = PoolEdge(depth, ground);
                float edgeWidth = clamp(fwidth(edge), 0.005, 0.028);
                float3 normal = normalize(cross(ddy(input.world), ddx(input.world)));
                normal *= normal.y < 0.0 ? -1.0 : 1.0;
                clip(depth.x);
                clip(edge + edgeWidth);

                float2 flow = ground - drift * (0.48 * spread + 0.17 * sin(1.9 * _Age));
                flow -= float2(0.060, -0.091) * _Age;
                float2 swirl = float2(SurfaceNoise(flow * 3.5), SurfaceNoise(flow.yx * 3.5 + 7.4)) - 0.5;
                float2 warped = flow + swirl * 0.47;
                float folds = SurfaceNoise(warped * float2(5.0, 8.8));
                float streak = 1.0 - smoothstep(0.027, 0.18, abs(folds - 0.57));
                streak *= smoothstep(0.23, 0.65, SurfaceNoise(warped * 4.9 + 3.2));
                float grit = SurfaceNoise(warped * 25.0);
                float rim = 1.0 - smoothstep(0.014, 0.12, edge);
                float cooling = smoothstep(4.08, 5.00, _Age);
                float heat = (0.20 + 0.72 * streak) * (1.0 + _Pulse * 0.32) * (1.0 - cooling * 0.58);
                float3 light = normalize(float3(-0.63, 0.78, -0.41));
                float3 view = normalize(GetCameraPositionWS() - input.world);
                float facing = saturate(dot(normal, light));
                float gloss = pow(saturate(dot(normal, normalize(light + view))), 15.0);
                half3 color = lerp(half3(0.13, 0.028, 0.015), half3(0.63, 0.14, 0.025), folds);
                color *= 0.63 + facing * 0.50;
                color = lerp(color, half3(1.17, 0.48, 0.075), heat * (1.0 - 0.52 * rim));
                color += half3(0.29, 0.115, 0.032) * gloss * (0.47 + grit * 0.53);
                color = lerp(color, half3(0.18, 0.035, 0.018), rim * 0.48);
                return half4(color, smoothstep(-edgeWidth, edgeWidth, edge));
            }
            ENDHLSL
        }
    }
}
