Shader "WorldTimeline/OctofoilBronze"
{
    Properties { _Charge ("Gathered light", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct V { float4 p:POSITION; float3 n:NORMAL; };
            struct F { float4 p:SV_POSITION; float3 local:TEXCOORD0; float3 normal:TEXCOORD1; float3 view:TEXCOORD2; };
            float _Charge;
            F Vert(V v) {
                F o; o.p=TransformObjectToHClip(v.p.xyz); o.local=v.p.xyz;
                o.normal=TransformObjectToWorldNormal(v.n); o.view=GetWorldSpaceViewDir(TransformObjectToWorld(v.p.xyz)); return o;
            }
            float stroke(float d,float w) { return 1-smoothstep(w,w+.012,abs(d)); }
            half4 Frag(F i):SV_Target {
                float2 p=i.local.xy; float r=length(p); float a=atan2(p.y,p.x+.00001);
                float petal=.5+.5*cos(8*a);
                float rim=r/(.78+.065*cos(8*a));
                float border=max(stroke(rim-.93,.022),stroke(rim-.82,.012));
                float leaf=stroke(r-(.32+.21*petal),.016);
                float vein=stroke(sin(4*a)*r,.013)*smoothstep(.20,.28,r)*(1-smoothstep(.47,.54,r));
                float boss=max(stroke(r-.17,.019),stroke(r-.215,.009));
                float relief=max(border,max(boss,max(leaf,vein)));
                float back=step(i.local.z,0);
                float3 n=normalize(i.normal), v=normalize(i.view);
                float light=saturate(.47+.53*dot(n,normalize(float3(-.4,.8,-.3))));
                float spec=pow(saturate(dot(reflect(-normalize(float3(-.4,.8,-.3)),n),v)),12);
                float3 brass=lerp(float3(.13,.064,.025),float3(.42,.23,.078),light);
                float3 gold=float3(.92,.62,.22);
                float3 polished=lerp(float3(.44,.29,.12),float3(1,.88,.56),.5+.5*sin(a+r*2));
                float3 base=lerp(polished,brass,back);
                base=lerp(base,gold,relief)+spec*.45;
                float wave=exp(-14*abs(r-.75*(1-frac(_Time.y*1.5))));
                base+=float3(.96,.55,.12)*_Charge*(relief*(.38+wave)+.4*exp(-11*r));
                return half4(base,1);
            }
            ENDHLSL
        }
    }
}
