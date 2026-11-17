Shader "WorldTimeline/InkScenery"
{
    Properties
    {
        _BaseMap("Ink artwork", 2D) = "white" {}
        _BaseColor("Paper / tint", Color) = (1,1,1,1)
        _Paper("Paper grain", Float) = 0
        _Relief("Solid landmark", Float) = 0
        _InkFinish("Ink landmark finish", Float) = 0
        _DepthWrite("Write depth", Float) = 0
        _SoftShadow("Landmark contact shadow", Float) = 0
        _RiverWash("River wash", Float) = 0
        _TerrainWash("Terrain wash", Float) = 0
        _EmberParticle("Ember particle", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "InkScenery"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_DepthWrite]
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            // Map origin XZ, inverse node spacing, and map-active flag.
            float4 _TimelineWeatherField;
            float _TimelineWeatherDusk;
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Paper;
                float _Relief;
                float _InkFinish;
                float _SoftShadow;
                float _RiverWash;
                float _TerrainWash;
                float _EmberParticle;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float2 uv:TEXCOORD0;
                float2 world:TEXCOORD1;
                float2 shapeUV:TEXCOORD2;
                half4 relief:TEXCOORD3;
                float3 local:TEXCOORD4;
                float3 normal:TEXCOORD5;
                float worldY:TEXCOORD6;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.world = world.xz;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.shapeUV = input.uv;
                o.local = input.positionOS;
                o.relief = input.color;
                o.normal = TransformObjectToWorldNormal(input.normalOS);
                o.worldY = world.y;
                if (_Relief > .5 && _InkFinish < .5)
                {
                    float lighting = dot(o.normal, normalize(float3(-.65, .85, .52)));
                    o.relief.rgb *= lerp(.48, 1.12, saturate(lighting));
                    #ifndef UNITY_COLORSPACE_GAMMA
                        o.relief.rgb = SRGBToLinear(o.relief.rgb);
                    #endif
                }
                return o;
            }
            float WeatherHash(float2 cell)
            {
                float3 p = frac(cell.xyx * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float WeatherNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                float bottom = lerp(WeatherHash(cell), WeatherHash(cell + float2(1, 0)), f.x);
                float top = lerp(WeatherHash(cell + float2(0, 1)), WeatherHash(cell + 1), f.x);
                return lerp(bottom, top, f.y);
            }

            float ProjectedBrush(float3 brushPosition, float3 weights, float2 frequency)
            {
                return dot(weights, float3(WeatherNoise(brushPosition.zy * frequency),
                    WeatherNoise(brushPosition.xz * frequency), WeatherNoise(brushPosition.xy * frequency)));
            }

            float3 PaintLandmark(Varyings input)
            {
                float3 normal = normalize(input.normal);
                float3 weights = abs(normal);
                weights /= max(dot(weights, float3(1, 1, 1)), .001);
                float3 position = float3(input.world.x, input.worldY, input.world.y);
                float3 brushPosition = position * _TimelineWeatherField.z;
                float wash = ProjectedBrush(brushPosition, weights, float2(6, 9));
                // A fine mark fades into the wash once its wavelength approaches a screen pixel.
                float footprint = max(length(ddx(brushPosition)), length(ddy(brushPosition)));
                float brushDetail = 1 - smoothstep(.38, 1.10, footprint * 36);
                float paperDetail = 1 - smoothstep(.26, .90, footprint * 104);
                float brush = lerp(.5, ProjectedBrush(brushPosition, weights, float2(36, 13)), brushDetail);
                float paper = ProjectedBrush(brushPosition, weights, float2(104, 79));

                // Match the density range of the surrounding ink illustrations on the same paper.
                const float paperTone = .95;
                const float inkTone = .285;
                float value = dot(input.relief.rgb, float3(.299, .587, .114));
                float density = lerp(.83, .23, smoothstep(.27, .77, value));
                // Broken dry brush follows grazing faces, with a softer base wash on the main faces.
                float3 view = GetWorldSpaceNormalizeViewDir(position);
                float edge = 1 - smoothstep(.10, .47, abs(dot(normal, view)));
                float inkStroke = edge * smoothstep(.31, .67, brush);
                density += .19 * (.75 - abs(normal.y)) + .23 * (wash - .46) + .20 * inkStroke;
                density -= smoothstep(.65, .86, paper) * smoothstep(.49, .74, brush) * .15 * paperDetail;
                density = saturate(density + (paper - .5) * .11 * paperDetail);
                float3 color = lerp(paperTone, inkTone, density).xxx;
                // Vertex alpha blends painted highlights and dark accents into the ink shading.
                color = lerp(color, input.relief.rgb * lerp(.83, 1.10, wash), 1 - input.relief.a);
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = SRGBToLinear(color);
                #endif
                return color;
            }

            half4 PaintRiver(float2 uv)
            {
                // Keep the painted strokes and let the map paper show through the pale wash.
                float4 stroke = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(saturate(uv.x + .13), uv.y));
                float3 pigment = float3(.37, .355, .33);
                #ifndef UNITY_COLORSPACE_GAMMA
                    stroke.rgb = LinearToSRGB(stroke.rgb);
                    pigment = SRGBToLinear(pigment);
                #endif
                float ink = saturate((.98 - dot(stroke.rgb, float3(.299, .587, .114))) * 3.9);
                float ends = 1 - smoothstep(.74, 1, abs(uv.y * 2 - 1));
                return half4(pigment, ink * stroke.a * ends);
            }

            half4 PaintTerrain(Varyings input)
            {
                float3 pigment = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    pigment = LinearToSRGB(pigment);
                #endif
                float density = 1 - min(pigment.r, min(pigment.g, pigment.b));
                float2 edges = 1 - smoothstep(.77, 1, abs(input.shapeUV * 2 - 1));
                float coverage = saturate(density * 2.2);
                pigment = saturate(1 - (1 - pigment) / max(coverage, .001));
                #ifndef UNITY_COLORSPACE_GAMMA
                    pigment = SRGBToLinear(pigment);
                #endif
                return half4(pigment, coverage * smoothstep(.022, .085, density) * edges.x * edges.y);
            }

            half4 Frag(Varyings input):SV_Target
            {
                if (_EmberParticle > .5)
                {
                    float2 offset = input.shapeUV * 2 - 1;
                    half opacity = 1 - smoothstep(.22, 1, dot(offset, offset));
                    return half4(input.relief.rgb * lerp(1, .68, _TimelineWeatherDusk),
                        opacity * input.relief.a);
                }
                if (_SoftShadow > .5)
                {
                    float2 offset = input.shapeUV * 2 - 1;
                    float falloff = 1 - smoothstep(.04, 1, dot(offset, offset));
                    half shade = Luminance(_BaseColor.rgb) * lerp(1, .63, _TimelineWeatherDusk);
                    return half4(shade.xxx, _BaseColor.a * falloff * falloff);
                }
                half4 color;
                if (_RiverWash > .5) color = PaintRiver(input.shapeUV);
                else if (_TerrainWash > .5) color = PaintTerrain(input);
                else color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_Relief > .5)
                {
                    if (_InkFinish > .5)
                        color = half4(PaintLandmark(input), 1);
                    else
                        color = half4(input.relief.rgb * lerp(.86, 1.08,
                            WeatherNoise((input.local.xz + input.local.y * float2(.51, .89)) * 18)), 1);
                }
                float grain = frac(sin(dot(floor(input.world * 12), float2(12.9898,78.233))) * 43758.5453);
                color.rgb *= 1 - _Paper * grain * .018;
                if (_TimelineWeatherField.w > 0)
                {
                    float dusk = _TimelineWeatherDusk;
                    float3 daylight = float3(.98, .98, .98);
                    float3 night = float3(.50, .50, .50);
                    #ifndef UNITY_COLORSPACE_GAMMA
                        daylight = SRGBToLinear(daylight);
                        night = SRGBToLinear(night);
                    #endif
                    color.rgb *= lerp(daylight, night, dusk);
                    // A slow shadow over the paper. Artwork and the node overlay stay crisp.
                    if (_Paper > .5)
                    {
                        float2 map = (input.world - _TimelineWeatherField.xy) * _TimelineWeatherField.z;
                        float cloud = WeatherNoise(map * float2(.13, .24) + float2(_Time.y * .018, 0));
                        color.rgb *= 1 - dusk * smoothstep(.32, .78, cloud) * .20;
                    }
                }
                if (_Relief > .5 && _InkFinish > .5)
                    return color;
                return half4(Luminance(color.rgb).xxx, color.a);
            }
            ENDHLSL
        }
    }
}
