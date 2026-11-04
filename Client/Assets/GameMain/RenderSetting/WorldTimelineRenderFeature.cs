using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldTimelineDebug;

// Nodes, links and pieces render above terrain, independently of scene fog.
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
}
