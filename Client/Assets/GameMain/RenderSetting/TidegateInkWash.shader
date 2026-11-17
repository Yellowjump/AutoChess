Shader "Hidden/WorldTimeline/TidegateInkWash"
{
    Properties { _Strength ("Ink Wash Strength", Range(0, 1)) = .85 }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Tidegate Ink Wash"
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
            CBUFFER_END

            half Tone(half3 pigment)
            {
                return sqrt(saturate(Luminance(pigment)));
            }

            half3 SamplePigment(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            float Hash(float2 cell)
            {
                cell = frac(cell * float2(.3183, .3679));
                cell += dot(cell, cell + 19.19);
                return frac(cell.x * cell.y * 37.5);
            }

            float PaperNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 texel = _BlitTexture_TexelSize.xy;
                float2 page = uv * _BlitTexture_TexelSize.zw * (1080.0 / _ScreenParams.y);
                half originalTone = Tone(source.rgb);

                half left = Tone(SamplePigment(uv - float2(texel.x, 0)));
                half right = Tone(SamplePigment(uv + float2(texel.x, 0)));
                half down = Tone(SamplePigment(uv - float2(0, texel.y)));
                half up = Tone(SamplePigment(uv + float2(0, texel.y)));
                float2 gradient = float2(right - left, up - down);
                float edge = length(gradient);
                float2 tangent = float2(-gradient.y, gradient.x) / max(edge, .018);
                float2 brush = tangent * texel * (1.3 + PaperNoise(page * .09) * 1.7);
                half sideA = Tone(SamplePigment(uv + brush));
                half sideB = Tone(SamplePigment(uv - brush));
                half weightA = 1 - smoothstep(.019, .085, abs(sideA - originalTone));
                half weightB = 1 - smoothstep(.019, .085, abs(sideB - originalTone));
                half tone = (originalTone * 2 + sideA * weightA + sideB * weightB) / (2 + weightA + weightB);

                // Separate the charcoal, warm wash and paper without flattening the silhouettes.
                half curved = smoothstep(.055, .92, tone);
                half3 charcoal = half3(.020, .036, .037);
                half3 midWash = half3(.30, .296, .254);
                half3 paper = half3(.88, .804, .658);
                half3 pigment = lerp(charcoal, midWash, smoothstep(0, .61, curved));
                pigment = lerp(pigment, paper, smoothstep(.40, 1, curved));

                // Keep the identity of blue cloth, brown wood and amber details.
                half sourceLight = Luminance(source.rgb);
                half3 chroma = source.rgb - sourceLight;
                half colorfulness = max(source.r, max(source.g, source.b)) - min(source.r, min(source.g, source.b));
                pigment += chroma * lerp(.36, .74, smoothstep(.09, .37, colorfulness));

                // Ink pools on the dark side of established edges and breaks over the paper tooth.
                half fiber = PaperNoise(page * float2(.39, .86));
                half tooth = PaperNoise(page * float2(.81, .24) + 23);
                half wetness = PaperNoise(page * .038 + 17);
                half darkSide = smoothstep(.006, .060, max(max(left, right), max(down, up)) - originalTone);
                half bleed = smoothstep(.045, .21, edge) * darkSide * lerp(.47, 1, tooth);
                pigment = lerp(pigment, charcoal, bleed * .31);
                half textured = smoothstep(.16, .48, tone) * (1 - smoothstep(.68, .94, tone));
                pigment *= 1 + (wetness - .48) * (.07 + textured * .11)
                    + (fiber - .5) * (.105 + textured * .13);
                return half4(lerp(source.rgb, max(pigment, 0), _Strength), source.a);
            }
            ENDHLSL
        }
    }
}
