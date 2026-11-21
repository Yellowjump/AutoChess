Shader "WorldTimeline/MirrorSunRay"
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
            struct V {float4 p:POSITION;float2 uv:TEXCOORD0;};
            struct F {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
            F Vert(V v){ F o;o.p=TransformObjectToHClip(v.p.xyz);o.uv=v.uv;return o;}
            half4 Frag(F i):SV_Target {
                float x=i.uv.x, y=i.uv.y*2-1, t=_Time.y;
                float wave=sin(x*50-t*18);
                float core=1-smoothstep(.12,.32,abs(y+.028*wave));
                float flow=pow(saturate(.5+.5*cos(19*y+3*sin(x*22-t*12)-x*38+t*19)),5);
                float halo=exp(-3*y*y);
                float3 amber=float3(1,.38,.035)+flow*float3(.16,.51,.19);
                float3 rgb=lerp(amber,float3(1.35,1.22,.88),core);
                float ends=1-smoothstep(.96,1,x);
                return half4(rgb,ends*saturate(halo*(.62+.5*flow)+core));
            }
            ENDHLSL
        }
    }
}
