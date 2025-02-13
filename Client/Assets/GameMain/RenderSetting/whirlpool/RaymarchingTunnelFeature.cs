using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RaymarchingTunnelFeature : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private Material _material;
        private RTHandle source;
        private RTHandle tempTexture;

        public void SetTarget(RTHandle tempRt,RTHandle colorSource, Material mat)
        {
            tempTexture = tempRt;
            _material = mat;
            source = colorSource;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("Raymarching Tunnel");
            Camera cam = renderingData.cameraData.camera;
            Matrix4x4 cameraToWorld = cam.cameraToWorldMatrix;
            _material.SetMatrix("_CameraToWorld", cameraToWorld.transpose);
            _material.SetVector("_CameraPosition", cam.transform.position);
            cmd.Blit(source, tempTexture, _material);
            cmd.Blit(tempTexture, source);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    public Material material;
    private CustomRenderPass _pass;
    private RTHandle tempRT;

    public override void Create()
    {
        _pass = new CustomRenderPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_pass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 0;
        RenderingUtils.ReAllocateIfNeeded(ref tempRT, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "temp Rt");
        _pass.ConfigureInput(ScriptableRenderPassInput.Color);
        _pass.SetTarget(tempRT, renderer.cameraColorTargetHandle, material);
    }

    protected override void Dispose(bool disposing)
    {
        tempRT?.Release();
    }
}