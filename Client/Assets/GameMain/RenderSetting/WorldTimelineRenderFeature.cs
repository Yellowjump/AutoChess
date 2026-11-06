using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using WorldTimelineDebug;

// Only nodes, links and pieces render above the map's ordinary scenery renderers.
public sealed class WorldTimelineRenderFeature : ScriptableRendererFeature
{
    private sealed class TimelineOverlayPass : ScriptableRenderPass
    {
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var board = WorldTimelineBoard.ForRenderingCamera(renderingData.cameraData.camera);
            if (board == null) return;
            var cmd = CommandBufferPool.Get("World Timeline Overlay");
            try
            {
                // DrawMesh uses global shader matrices, which previous custom passes can leave changed.
                // Bind this camera's cached URP matrices, including the render-target Y convention.
                var cameraData = renderingData.cameraData;
                var view = cameraData.GetViewMatrix();
                var projection = cameraData.GetGPUProjectionMatrix();
                cmd.SetViewport(new Rect(0, 0, cameraData.cameraTargetDescriptor.width,
                    cameraData.cameraTargetDescriptor.height));
                cmd.SetViewProjectionMatrices(view, projection);
                RenderingUtils.SetViewAndProjectionMatrices(cmd, view, projection, true);
                board.DrawOverlay(cmd, renderingData.cameraData.camera);
                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }
    }

    private TimelineOverlayPass overlayPass;

    public override void Create()
    {
        overlayPass = new TimelineOverlayPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (WorldTimelineBoard.ForRenderingCamera(renderingData.cameraData.camera) != null)
            renderer.EnqueuePass(overlayPass);
    }
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (WorldTimelineBoard.ForRenderingCamera(renderingData.cameraData.camera) != null)
            overlayPass.ConfigureTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
    }
}
