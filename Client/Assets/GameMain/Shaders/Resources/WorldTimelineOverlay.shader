Shader "WorldTimeline/Overlay"
{
    Properties { _MarkerColor ("Marker Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off
        Pass
        {
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
    }
}
