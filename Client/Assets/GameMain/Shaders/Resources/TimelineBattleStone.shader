Shader "WorldTimeline/BattleStone"
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
                return half4(input.color.rgb * (0.95 + grain * 0.08 + mottling * 0.035), 1);
            }
            ENDHLSL
        }
    }
}
