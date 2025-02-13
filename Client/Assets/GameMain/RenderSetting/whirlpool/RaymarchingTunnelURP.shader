Shader "Custom/RayMarchingShader"
{
    Properties
    {
        _FOV ("Field of View", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Name "RayMarching"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ✅ 传入相机矩阵
            float4x4 _CameraToWorld;
            float _FOV;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            // 💡 计算屏幕 UV 并转换到 [-1,1]
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.uv = IN.uv;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            // 🎨 Hue 转 RGB
            float3 HueToRGB(float hue)
            {
                return cos(((hue) + 0.5) * 6.2832 + radians(float3(60, 0, -60))) * 0.5 + 0.5;
                float3 k = float3(0.0, -0.3333, 0.3333);
                k = frac(k + hue) * 6.0;
                return saturate(abs(k - 3.0) - 1.0);
            }

            // 🎯 计算场景的 SDF (Signed Distance Function)
            float map(float3 p)
            {
                float t = _Time.y;
                float l = 6;
                float w = 40.0;
                float s = 0.4;
                float f = 1e20;
                float i = 0.0;
                float y, z;

                //p.yz = -p.zy;
                p.xz = float2(atan2(p.x, p.z), length(p.xz));  // 转换到极坐标
                //p.x -= t/2;  // 旋转偏移

                float3 q;
                for (; i++ < l;)
                {
                    q = p;
                    z = round(max(q.z - i, 0.0) / l) * l + i;
                    q.x *= z;
                    q.x -= sqrt(z * t*t * 20.0);
                    q.x -= round(q.x / TWO_PI) * TWO_PI;
                    q.z -= z;
                    q.y -= sqrt(z / w) * w;
                    y = cos(z * t / 20.0) * 0.5 + 0.5;
                    q.y -= y * 2.0;
                    q = abs(q);
                    f = min(f, max(q.x, max(q.y, q.z)) - s*y);
                }
                return f;
            }

            // 🏞 主 Ray Marching 逻辑
            float4 frag (Varyings IN) : SV_Target
            {
                float2 uv = (IN.uv - 0.5) * 2.0;
                //uv.x *= _ScreenParams.x / _ScreenParams.y;  // 纠正宽高比

                // 📷 计算光线方向
                float4 matchNearWorldPos4 = mul(unity_MatrixInvVP, float4(uv , 0, 1));
                float3 matchNearWorldPos = matchNearWorldPos4.xyz/matchNearWorldPos4.w;
                float3 rayDir = normalize(matchNearWorldPos - _WorldSpaceCameraPos);
                //return float4(rayDir,1);
                //float3 rayDir = normalize(mul(_CameraToWorld, float4(uv , 1.0, 0)).xyz);
                float3 rayPos = _WorldSpaceCameraPos;  // 相机位置
                
                // 🔄 旋转光线方向 (绕Y轴旋转)
                //float angle = _Time.y * 0.5;
                //float2x2 rot = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
                //rayDir.xz = mul(rot, rayDir.xz);

                // 🔄 极坐标转换
                //rayPos.xy = float2(atan2(rayPos.x, rayPos.y), length(rayPos.xy));

                float3 color = float3(0, 0, 0);
                float i = 0.0, d = 0.0, s, r;
                float l = 50.0;

                // 🔍 Ray Marching 遍历
                for (; i++ < l;)
                {
                    float3 p = rayPos + rayDir * d;
                    s = map(p);

                    // 🎨 计算颜色渐变
                    r = (cos(round(length(p.xz)) * _Time.y / 20.0) * 0.7 - 1.8) / 2.0;
                    color += min(s, exp(-s / 0.07)) * HueToRGB(r + 0.5) * (r + 2.4);

                    if (s < 1e-3 || d > 1e3) break;
                    d += s * 0.7;
                }

                return float4(exp(log(color) / 2.2), 1);
            }
            ENDHLSL
        }
    }
}
