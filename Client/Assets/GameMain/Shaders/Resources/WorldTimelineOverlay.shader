Shader "WorldTimeline/Overlay"
{
    Properties
    {
        _MarkerColor ("Marker Color", Color) = (1,1,1,1)
        _LandmarkInk ("Landmark Ink", 2D) = "white" {}
        [NoScaleOffset] _TimelineMapBadgeAtlas ("Map Badge Atlas", 2D) = "white" {}
        _TimelineMapBadgeUV ("Map Badge UV", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            UNITY_INSTANCING_BUFFER_START(Markers)
                UNITY_DEFINE_INSTANCED_PROP(float4, _MarkerColor)
            UNITY_INSTANCING_BUFFER_END(Markers)

            struct Attributes
            {
                float3 position : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.position = TransformObjectToHClip(input.position);
                output.color = UNITY_ACCESS_INSTANCED_PROP(Markers, _MarkerColor);
                return output;
            }
            float4 Frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _TimelineWeatherDusk;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
            };
            Varyings Vert(float3 position : POSITION, float4 color : COLOR)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position);
                output.color = color;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float4 color = input.color;
                color.rgb = lerp(color.rgb, float3(.75, .82, .85), .55 * _TimelineWeatherDusk);
                color.a += .13 * _TimelineWeatherDusk * (1 - color.a);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_LandmarkInk);
            SAMPLER(sampler_LandmarkInk);
            float _TimelineWeatherDusk;
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            Varyings Vert(float3 position : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position);
                output.uv = uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 ink = SAMPLE_TEXTURE2D(_LandmarkInk, sampler_LandmarkInk, input.uv);
                ink.rgb *= lerp(.70, 1.05, smoothstep(.08, .80, input.uv.y));
                ink.rgb *= lerp(half3(1, .975, .93), half3(.38, .43, .54), _TimelineWeatherDusk);
                return ink;
            }
            ENDHLSL
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TimelineMapBadgeAtlas);
            SAMPLER(sampler_TimelineMapBadgeAtlas);
            UNITY_INSTANCING_BUFFER_START(MapBadges)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TimelineMapBadgeUV)
            UNITY_INSTANCING_BUFFER_END(MapBadges)

            struct Attributes
            {
                float3 position : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float4 rect = UNITY_ACCESS_INSTANCED_PROP(MapBadges, _TimelineMapBadgeUV);
                output.position = TransformObjectToHClip(input.position);
                output.uv = input.uv * rect.xy + rect.zw;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_TimelineMapBadgeAtlas, sampler_TimelineMapBadgeAtlas, input.uv);
            }
            ENDHLSL
        }
        Pass
        {
            Name "LandmarkSelectionSweep"
            Stencil { Ref 1 ReadMask 1 WriteMask 1 Comp Always Pass Replace }
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _SelectionSweep;
            struct Varyings
            {
                float4 position : SV_POSITION;
                float height : TEXCOORD0;
            };
            Varyings Vert(float3 position : POSITION)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position);
                output.height = (position.y - _SelectionSweep.x) * _SelectionSweep.y;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float band = 1 - smoothstep(0, .17, abs(input.height - _SelectionSweep.z));
                return half4(1, .84, .47, band * .58);
            }
            ENDHLSL
        }
        Pass
        {
            Name "LandmarkSelectionOutline"
            Stencil { Ref 1 ReadMask 1 WriteMask 0 Comp NotEqual }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _OutlineOffset;
            float4 Vert(float3 position : POSITION) : SV_POSITION
            {
                float4 clipPos = TransformObjectToHClip(position);
                clipPos.xy += 2 * _OutlineOffset.xy * clipPos.w / _ScaledScreenParams.xy;
                return clipPos;
            }
            half4 Frag() : SV_Target { return half4(.94, .66, .23, 1); }
            ENDHLSL
        }
    }
}
