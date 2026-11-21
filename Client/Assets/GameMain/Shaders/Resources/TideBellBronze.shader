Shader "WorldTimeline/TideBellBronze"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct V {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
            struct F {float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 eye:TEXCOORD1;float2 uv:TEXCOORD2;};
            F Vert(V v){F o;o.p=TransformObjectToHClip(v.p.xyz);o.n=TransformObjectToWorldNormal(v.n);o.eye=GetWorldSpaceViewDir(TransformObjectToWorld(v.p.xyz));o.uv=v.uv;return o;}
            float band(float x,float w){return 1-smoothstep(w,w+.012,abs(x));}
            half4 Frag(F i):SV_Target{
                float h=-i.uv.y,a=i.uv.x*6.283185;
                float gold=max(band(h-.805,.024),band(h-.25,.013));
                gold=max(gold,band(h-.65-.035*sin(a*6),.012));
                gold=max(gold,step(.5,i.uv.y));
                float3 n=normalize(i.n),eye=normalize(i.eye);
                float rim=pow(1-saturate(abs(dot(n,eye))),2);
                float lit=saturate(dot(n,normalize(float3(-.4,.8,-.5)))*.5+.5);
                float3 bronze=lerp(float3(.11,.15,.105),float3(.77,.45,.16),lit);
                float3 color=lerp(bronze,float3(1,.73,.32),gold)+rim*float3(.42,.32,.13);
                return half4(color,lerp(.55+.23*rim,.92,gold));
            }
            ENDHLSL
        }
    }
}
