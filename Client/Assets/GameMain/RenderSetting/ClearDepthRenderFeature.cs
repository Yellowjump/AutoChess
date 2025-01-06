using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ClearDepthRenderFeature : ScriptableRendererFeature
{
    class ClearDepthPass : ScriptableRenderPass
    {
        private CommandBuffer commandBuffer;
        // 执行深度清除操作
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            commandBuffer = CommandBufferPool.Get("Clear Depth Buffer");
            // 清除深度缓存，保留颜色（不清除颜色缓冲区）
            commandBuffer.ClearRenderTarget(true, false,Color.black);  // 参数：清除颜色，清除深度，清除模版
            context.ExecuteCommandBuffer(commandBuffer);
            context.ExecuteCommandBuffer(commandBuffer);
        }
    }

    // 声明一个 ClearDepthPass 实例
    ClearDepthPass clearDepthPass;
    public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    // 在初始化时创建和配置 Pass
    public override void Create()
    {
        clearDepthPass = new ClearDepthPass();
        clearDepthPass.renderPassEvent = RenderPassEvent;
    }

    // 在渲染过程中添加 Pass
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(clearDepthPass);
    }
}