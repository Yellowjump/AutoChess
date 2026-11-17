Shader "WorldTimeline/GateGolemInkStone"
{
    Properties
    {
        _BaseColor ("Slate", Color) = (.278, .29, .322, 1)
        _BaseMap ("Base Map", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = .5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "Ink Stone"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 restPosition : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 stonePosition : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.stonePosition = lerp(input.positionOS.xyz, input.restPosition.xyz, input.restPosition.w);
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * float3(.3183, .3679, .3292));
                p += dot(p, p.zxy + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            float Wash(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3 - 2 * f);
                float low = lerp(lerp(Hash(cell), Hash(cell + float3(1,0,0)), f.x),
                    lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), f.x), f.y);
                cell.z += 1;
                float high = lerp(lerp(Hash(cell), Hash(cell + float3(1,0,0)), f.x),
                    lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), f.x), f.y);
                return lerp(low, high, f.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Dry short strokes break the longer sediment bands into patches.
                float3 p = input.stonePosition * float3(.65, 1, .78);
                float wet = Wash(p * float3(3.2, 3.7, 3.4) + 9);
                float dry = Wash(p * float3(8.5, 5.1, 8.5) + 27);
                float bedding = p.y * 14 + p.x * 4.0 - p.z * 3.4 + wet * 2.6;
                float band = frac(bedding);
                float brushMask = smoothstep(.32, .64, dry);
                float crease = (1 - smoothstep(.07, .25, abs(band - .28))) * brushMask;
                float lip = (1 - smoothstep(.04, .17, abs(band - .51))) * brushMask;
                float grit = Wash(p * float3(24, 36, 26) + 6);

                // A broad upper light, a softer halftone and a cool ink foot.
                half3 normal = normalize(input.normalWS);
                half3 key = normalize(half3(-.48, .81, -.42) + GetMainLight().direction * .27);
                half bedLight = saturate(dot(normal, key) * .50 + .50 + (wet - .5) * .38);
                half halftone = smoothstep(.17, .59, bedLight);
                half lightWash = smoothstep(.53, .94, bedLight);
                half foot = 1 - smoothstep(.15, .58, p.y + wet * .25);
                half3 pigment = _BaseColor.rgb * (.57 + halftone * .66 + lightWash * 1.04);
                pigment *= 1 + (wet - .5) * .46 + (grit - .5) * .16 - crease * .43 + lip * .26;
                pigment = lerp(pigment, pigment * half3(.62, .78, .82), foot * .5);
                pigment += half3(.044, .038, .022) * lightWash * (.27 + .73 * wet);
                return half4(max(pigment, 0), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
