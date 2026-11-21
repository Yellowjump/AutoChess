Shader "WorldTimeline/StormVapour"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Vertex { float4 position:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Pixel { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            Pixel Vert(Vertex v) {
                Pixel o;
                o.position=TransformObjectToHClip(v.position.xyz);
                o.uv=v.uv;
                o.color=v.color;
                return o;
            }
            half4 Frag(Pixel i):SV_Target {
                float2 p=i.uv*2-1;
                float ripple=.045*sin(p.y*12+sin(p.x*9))+.04*sin(p.x*13+p.y*7);
                float radius=length(p)+ripple;
                float alpha=1-smoothstep(.57,1,radius);
                float light=saturate(.38+p.y*.45-p.x*.15+ripple);
                return half4(lerp(float3(.048,.052,.09),float3(.40,.43,.54),light)*i.color.rgb,alpha*i.color.a);
            }
            ENDHLSL
        }
    }
}
