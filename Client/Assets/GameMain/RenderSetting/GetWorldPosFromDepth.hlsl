void GetWorldPosFromDepth_float(float2 uv, float sceneRawDepth, float4x4 ivp, out float3 worldPos)
{
    //float sceneRawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
    float4 positionCS = float4(uv * 2.0 - 1.0, sceneRawDepth, 1.0);
    float4 hpositionWS = mul(ivp, positionCS);
    worldPos = hpositionWS.xyz / hpositionWS.w;
}

void GetWorldPosFromDepth_half(float2 uv, float sceneRawDepth, float4x4 ivp, out half3 worldPos)
{
    float4 positionCS = float4(uv * 2.0 - 1.0, sceneRawDepth, 1.0);
    float4 hpositionWS = mul(ivp, positionCS);
    worldPos = hpositionWS.xyz / hpositionWS.w;
}
