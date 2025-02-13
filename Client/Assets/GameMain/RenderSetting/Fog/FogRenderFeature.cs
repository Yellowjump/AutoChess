using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FogRenderFeature : ScriptableRendererFeature
{
    class FogPass : ScriptableRenderPass
    {
        ProfilingSampler m_ProfilingSampler = new ProfilingSampler("FogBlit");
        Material m_Material;
        RTHandle m_CameraColorTarget;
        RTHandle m_TempColorTarget;
        RTHandle m_DepthTarget;
        /*Color m_FogColor;

        //float m_FogDensity;
        float m_FogStart;
        float m_FogEnd;
        float m_NoiseCellSize;
        float m_NoiseRoughness;
        float m_NoisePersistance;
        Vector3 m_NoiseSpeed;
        float m_NoiseScale;*/


        public void SetTarget(RTHandle colorHandle,RTHandle depthRt,RTHandle tempRt, Material fogMat)
        {
            m_CameraColorTarget = colorHandle;
            m_TempColorTarget = tempRt;
            m_Material = fogMat;
            m_DepthTarget = depthRt;
            /*m_FogStart = fogStart;
            m_FogEnd = fogEnd;
            m_FogColor = fogColor;
            m_NoiseCellSize = noiseCellSize;
            m_NoiseRoughness = noiseRoughness;
            m_NoisePersistance = noisePersistance;
            m_NoiseSpeed = noiseSpeed;
            m_NoiseScale = noiseScale;*/
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(m_CameraColorTarget);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (m_Material == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                /*m_Material.SetColor("_FogColor", m_FogColor);
                m_Material.SetFloat("_FogStart", m_FogStart);
                m_Material.SetFloat("_FogEnd", m_FogEnd);
                m_Material.SetFloat("_NoiseCellSize", m_NoiseCellSize);
                m_Material.SetFloat("_NoiseRoughness", m_NoiseRoughness);
                m_Material.SetFloat("_NoisePersistance", m_NoisePersistance);
                m_Material.SetVector("_NoiseSpeed", m_NoiseSpeed);
                m_Material.SetFloat("_NoiseScale", m_NoiseScale);

                m_Material.SetMatrix("_InverseView", renderingData.cameraData.camera.cameraToWorldMatrix);
                Blitter.BlitCameraTexture(cmd, m_CameraColorTarget, m_CameraColorTarget, m_Material, 0);*/
                cmd.Blit(m_CameraColorTarget,m_TempColorTarget);
                m_Material.SetTexture(ColorTex,m_TempColorTarget);
                //m_Material.SetTexture("_MainTex", m_TempColorTarget);
                //Debug.Log(m_Material.GetTexture("_MainTex")); // 确保不为 null
                m_Material.SetMatrix("_InverseVPMatrix", (renderingData.cameraData.camera.projectionMatrix*renderingData.cameraData.camera.worldToCameraMatrix).inverse);
                //m_Material.SetTexture(DepthTex,m_DepthTarget);
                cmd.Blit(null,m_CameraColorTarget,m_Material,0);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }
    }

    /*public Shader m_shader;
    public Color m_FogColor;
    public float m_FogStart;
    public float m_FogEnd;
    public float m_NoiseCellSize;
    public float m_NoiseRoughness;
    public float m_NoisePersistance;
    public Vector3 m_NoiseSpeed;
    public float m_NoiseScale;*/
    private RTHandle m_tempColorRt;
    public Material m_Material;

    FogPass m_RenderPass = null;
    public bool ActiveInSceneView = false;
    private static readonly int ColorTex = Shader.PropertyToID("_ColorTex");
    private static readonly int DepthTex = Shader.PropertyToID("_DepthTex");
    public override void Create()
    {
        m_RenderPass = new FogPass();
        m_RenderPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game||ActiveInSceneView)
            renderer.EnqueuePass(m_RenderPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref m_tempColorRt, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "temp Rt");
            m_RenderPass.ConfigureInput(ScriptableRenderPassInput.Color);
            
            m_RenderPass.SetTarget(renderer.cameraColorTargetHandle,renderer.cameraDepthTargetHandle ,m_tempColorRt,m_Material);
        }
    }

    protected override void Dispose(bool disposing)
    {
        m_tempColorRt?.Release();
    }
}