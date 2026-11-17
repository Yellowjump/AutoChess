Shader "WorldTimeline/AshridgeMist"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-20" "RenderType" = "Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 drift : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(float3 position : POSITION, float2 uv : TEXCOORD0, half4 color : COLOR)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position);
                output.uv = uv;
                output.drift = float2(position.x * .31, uv.y * 2.3 + position.z * .24);
                output.color = color;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(.3183, .3679));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y * 37.5);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1.0), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.drift - float2(_Time.y * .14, 0);
                float body = Noise(p) * .66 + Noise(p * 2.18 + .61) * .34;
                float horizontal = smoothstep(.0, .22, min(input.uv.x, 1.0 - input.uv.x));
                float vertical = smoothstep(.03, .25, input.uv.y) * (1.0 - smoothstep(.28, .99, input.uv.y));
                float density = smoothstep(.26, .77, body) * horizontal * vertical;
                return half4(input.color.rgb, input.color.a * density);
            }
            ENDHLSL
        }
    }
}
