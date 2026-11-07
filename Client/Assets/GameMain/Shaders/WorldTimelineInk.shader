Shader "WorldTimeline/InkScenery"
{
    Properties
    {
        _BaseMap("Ink artwork", 2D) = "white" {}
        _BaseColor("Paper / tint", Color) = (1,1,1,1)
        _Paper("Paper grain", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "InkScenery"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
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
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.world = world.xz;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
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

            half4 Frag(Varyings input):SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float grain = frac(sin(dot(floor(input.world * 12), float2(12.9898,78.233))) * 43758.5453);
                color.rgb *= 1 - _Paper * grain * .018;
                if (_TimelineWeatherField.w > 0)
                {
                    float dusk = _TimelineWeatherDusk;
                    float3 sunlight = float3(1, .977, .925);
                    float3 night = float3(.46, .50, .59);
                    #ifndef UNITY_COLORSPACE_GAMMA
                        sunlight = SRGBToLinear(sunlight);
                        night = SRGBToLinear(night);
                    #endif
                    color.rgb *= lerp(sunlight, night, dusk);
                    // A slow shadow over the paper. Artwork and the node overlay stay crisp.
                    if (_Paper > .5)
                    {
                        float2 map = (input.world - _TimelineWeatherField.xy) * _TimelineWeatherField.z;
                        float cloud = WeatherNoise(map * float2(.13, .24) + float2(_Time.y * .018, 0));
                        color.rgb *= 1 - dusk * smoothstep(.32, .78, cloud) * .20;
                    }
                }
                return color;
            }
            ENDHLSL
        }
    }
}
