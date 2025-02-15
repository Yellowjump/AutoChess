Shader "Universal Render Pipeline/HeightFogEffect"
{
    Properties
    {
        [HDR]_FogColor("Fog Color", Color) = (0.5, 0.6, 0.7, 1)
        _FogHeight("Fog Height", Float) = 10.0
        _FogTopOffset("Fog TopOffset", Float) = 10.0
        _FogDensity("Fog Density", Float) = 0.1
        _TilingFactor("Tiling Factor", Float) = 1.0
        _CloudSpeed("Cloud Speed", Vector) = (1, 1, 1, 1)
        _CloudTex("CloudTexture", 2D) = "white" {}
        [HDR]_CloudColor("Cloud Color", Color) = (0, 0.3490196, 0.6980392, 1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    float4 _FogColor;
    float _FogHeight;
    float _FogTopOffset;
    float _FogDensity;
    sampler2D _MainTex;
    sampler2D _CloudTex;
    float4 _CloudColor;
    sampler2D _ColorTex;
    float _TilingFactor;
    float4 _CloudSpeed;
    TEXTURE2D(_CameraDepthTexture);
    SAMPLER(sampler_CameraDepthTexture);

    struct appdata
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    // 顶点着色器
    v2f vert(appdata v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
        o.uv = v.uv;
        return o;
    }

    float3 ComputeFogIntersection(float3 camPos, float3 rayDir, float fogPlaneY)
    {
        // 计算交点参数 t
        float t = (fogPlaneY - camPos.y) / rayDir.y;

        // 计算交点 P_intersect
        float3 P_intersect = camPos + t * rayDir;

        return P_intersect;
    }

    float4 BlendTwoCloud(float2 uv)
    {
        float4 c = tex2D(_CloudTex, uv * _TilingFactor + _Time.x * _CloudSpeed.xy);
        c.g = c.r * _CloudColor.g;
        c.b = c.r * _CloudColor.b;
        c.r = c.r * _CloudColor.r;
        c.a = min(c.r * 5, 1);
        float4 c2 = tex2D(_CloudTex, uv * _TilingFactor + _Time.x * _CloudSpeed.zw);
        c2.r = c2.g * _CloudColor.r;
        c2.b = c2.g * _CloudColor.b;
        c2.g = c2.g * _CloudColor.g;
        c2.a = min(c2.g * 5, 1);
        c = (c + c2) / 2;
        c = min(c, 1);
        return c;
    }

    // 片元着色器
    float4 frag(v2f i) : SV_Target
    {
        // 采样深度贴图
        float sceneRawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv);
        float3 cameraPos = _WorldSpaceCameraPos;
        float4 fogColor = _FogColor;
        float maxFogFactor = 0.9; // 完全被雾气遮挡
        float minFogFactor = 0.2;
        // 如果是天空盒区域，使用设定颜色
        if (sceneRawDepth == 0.0)
        {
            if (cameraPos.y > _FogHeight)
            {
                // 采样原始颜色
                float4 baseColor = tex2D(_ColorTex, i.uv);
                //return baseColor;
                // 线性插值混合雾气颜色
                return lerp(baseColor, fogColor, minFogFactor);
            }
            return _FogColor;
        }
        // 计算世界坐标
        float3 worldPos = ComputeWorldSpacePosition(i.uv, sceneRawDepth, unity_MatrixInvVP);


        // 判断是否在雾气区域
        float fogFactor = minFogFactor;
        // 摄像机高于雾气高度
        if (cameraPos.y > _FogHeight)
        {
            if (worldPos.y < _FogHeight)
            {
                // 计算视线方向
                float3 rayDir = normalize(worldPos - cameraPos);
                // 计算雾海交点
                float3 fogIntersect = ComputeFogIntersection(cameraPos, rayDir, _FogHeight);
                float4 cloudColor = BlendTwoCloud(fogIntersect.xz);
                fogFactor = lerp(maxFogFactor, minFogFactor,
                                                 saturate((worldPos.y - _FogHeight + _FogTopOffset) / _FogTopOffset));
                fogColor = lerp(_FogColor, cloudColor,
                    cloudColor.a * (fogFactor - minFogFactor) / (maxFogFactor - minFogFactor)); //
            }
        }
        else
        {
            // 摄像机在雾气中，计算随距离增加的雾效
            float dist = distance(cameraPos, worldPos);
            fogFactor = clamp(saturate(dist * _FogDensity + 0.2), minFogFactor, maxFogFactor);
        }

        // 采样原始颜色
        float4 baseColor = tex2D(_ColorTex, i.uv);
        //return baseColor;
        // 线性插值混合雾气颜色
        return lerp(baseColor, fogColor, fogFactor);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-499"
        }
        LOD 100
        ZTest Always Cull Off ZWrite Off
        Blend One Zero

        Pass
        {
            Name "HeightFogEffect"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}