Shader "Universal Render Pipeline/ReconstructPositionWithDepth"
{
    Properties
    {
        _MainTex("Base (RGB)", 2D) = "white" {}
        [HDR]_ScanLineColor("_ScanLineColor (default = 1,1,1,1)", color) = (1,1,1,1)
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        float4x4 _InverseVPMatrix;
        half4 _ScanLineColor;
    CBUFFER_END

    sampler2D _MainTex;
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
        //float4 screenPos : TEXCOORD1;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    //vertex shader
    v2f vert(appdata v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        //o.positionCS = float4(v.positionOS.x * 2 - 1,1-v.positionOS.y * 2, 0, 1);

        o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
        // prepare depth texture's screen space UV
        //o.screenPos = ComputeScreenPos(o.positionCS);
        o.uv = v.uv;

        return o;
    }

    //fragment shader
    float4 frag(v2f i) : SV_Target
    {
        float sceneRawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv);
        // 如果是天空盒区域，使用设定颜色
        if (sceneRawDepth == 0.0)
        {
            return _ScanLineColor;
        }
        float3 worldPos = ComputeWorldSpacePosition(i.uv, sceneRawDepth, unity_MatrixInvVP);
        return float4(worldPos, 1);
    }
    ENDHLSL
    SubShader
    {

        //Tags {"RenderType" = "Opaque"  "RenderPipeline" = "UniversalPipeline"}
        Tags
        {
            "RenderPipeline" = "UniversalPipeline" "RenderType" = "Overlay" "Queue" = "Transparent-499" "DisableBatching" = "True"
        }
        LOD 100
        ZTest Always Cull Off ZWrite Off
        Blend one zero
        Pass
        {
            Name "ReconstructPositionByInvMatrix"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

    }
}