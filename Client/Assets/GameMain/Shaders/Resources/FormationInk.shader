Shader "WorldTimeline/FormationInk"
{
    Properties
    {
        _Color ("Ink", Color) = (.43,.57,.48,.7)
        _Selected ("Selected", Float) = 0
        _Particle ("Particle", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Selected, _Particle;
            CBUFFER_END
            struct Attributes { float4 position:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 position:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.position=TransformObjectToHClip(input.position.xyz); o.uv=input.uv; o.color=input.color; return o;
            }
            float Band(float value, float radius, float width, float aa)
            { return 1-smoothstep(width, width+aa, abs(value-radius)); }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;
                float r=length(p);
                if (_Particle>.5)
                {
                    float a=pow(saturate(1-r), 1.6);
                    return half4(_Color.rgb*input.color.rgb, a*_Color.a*input.color.a);
                }
                float angle=atan2(p.y,p.x);
                float aa=max(fwidth(r)*1.2,.004);
                float dry=.78+.22*sin(angle*43+r*180);
                float edge=r+.009*sin(angle*9)+.006*sin(angle*23);
                float baseRing=Band(edge,.78,.012,aa)*dry + Band(edge,.88,.004,aa)*.50;
                float turn=angle+_Time.y*.24;
                float arc=pow(saturate(sin(turn*3)),6);
                float selectedRing=Band(edge,.80,.024,aa)*arc + Band(edge,.69,.009,aa)*.65;
                float glyph=Band(r,.92,.025,aa)*pow(saturate(cos(angle*8)),30);
                float pulse=.88+.12*sin(_Time.y*1.7);
                float alpha=saturate(baseRing+_Selected*(selectedRing+glyph)*pulse);
                return half4(_Color.rgb,alpha*_Color.a);
            }
            ENDHLSL
        }
    }
}
