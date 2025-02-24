using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class ClearDepthRenderFeature : ScriptableRendererFeature
{
    class ClearDepthPass : ScriptableRenderPass
    {
        private CommandBuffer commandBuffer;
        private RTHandle sourceDepth;
        private RTHandle tempDepth;
        private RTHandle cameraColorRt;
        public void SetUp(RTHandle sourceDepthRt, RTHandle tempDepthRt,RTHandle cameraRt)
        {
            sourceDepth = sourceDepthRt;
            tempDepth = tempDepthRt;
            cameraColorRt = cameraRt;
        }
        // 执行深度清除操作
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            commandBuffer = CommandBufferPool.Get("Save And Clear Depth Buffer");
            //commandBuffer.Blit(sourceDepth,tempDepth);
            commandBuffer.SetRenderTarget(cameraColorRt,tempDepth);
            // 清除深度缓存，保留颜色（不清除颜色缓冲区）
            commandBuffer.ClearRenderTarget(true, false,Color.black);  // 参数：清除颜色，清除深度，清除模版
            //commandBuffer.SetRenderTarget(cameraColorRt,sourceDepth);
            context.ExecuteCommandBuffer(commandBuffer);
            CommandBufferPool.Release(commandBuffer);
        }
    }
    class ResumeDepthPass : ScriptableRenderPass
    {
        private CommandBuffer commandBuffer;
        private RTHandle sourceDepth;
        private RTHandle tempDepth;
        private RTHandle tempDepthTarget;
        private RTHandle cameraColorRt;
        private Material ResumeDepthMat;
        public void SetUp(RTHandle sourceDepthRt, RTHandle tempDepthRt,RTHandle tempDepthTargetRT,RTHandle cameraRt,Material resumeMat)
        {
            sourceDepth = sourceDepthRt;
            tempDepth = tempDepthRt;
            tempDepthTarget = tempDepthTargetRT;
            ResumeDepthMat = resumeMat;
            cameraColorRt = cameraRt;
        }
        // 执行深度恢复操作
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            commandBuffer = CommandBufferPool.Get("Resume Depth Buffer");
            /*ResumeDepthMat.SetTexture(Shader.PropertyToID("_tempDepth"),tempDepth);
            ResumeDepthMat.SetTexture(Shader.PropertyToID("_nowDepth"),tempDepthTarget);
            commandBuffer.Blit(sourceDepth,tempDepthTarget);
            commandBuffer.Blit(null,sourceDepth,ResumeDepthMat,0);*/
            //commandBuffer.Blit(tempDepthTarget,sourceDepth);
            commandBuffer.SetRenderTarget(cameraColorRt,sourceDepth);
            context.ExecuteCommandBuffer(commandBuffer);
            CommandBufferPool.Release(commandBuffer);
        }
    }
    // 声明一个 ClearDepthPass 实例
    ClearDepthPass clearDepthPass; 
    ResumeDepthPass resumeDepthPass;
    public Material ResumeDepthMat;
    public RenderPassEvent ClearRenderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    public RenderPassEvent ResumeRenderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    private RTHandle temp_DepthRt;
    private RTHandle temp_DepthRt2;

    public bool b_resume = false;
    // 在初始化时创建和配置 Pass
    public override void Create()
    {
        clearDepthPass = new ClearDepthPass();
        clearDepthPass.renderPassEvent = ClearRenderPassEvent;
        resumeDepthPass = new ResumeDepthPass();
        resumeDepthPass.renderPassEvent = ResumeRenderPassEvent;
    }

    // 在渲染过程中添加 Pass
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(clearDepthPass);
        if (ResumeDepthMat != null&&b_resume)
        {
            renderer.EnqueuePass(resumeDepthPass);
        }
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 32;
        RenderingUtils.ReAllocateIfNeeded(ref temp_DepthRt, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "temp Depth beforeClear");
        RenderingUtils.ReAllocateIfNeeded(ref temp_DepthRt2, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "temp Depth target");
        clearDepthPass.SetUp(renderer.cameraDepthTargetHandle,temp_DepthRt,renderer.cameraColorTargetHandle);
        resumeDepthPass.SetUp(renderer.cameraDepthTargetHandle,temp_DepthRt,temp_DepthRt2,renderer.cameraColorTargetHandle,ResumeDepthMat);
    }

    private void OnDestroy()
    {
        temp_DepthRt?.Release();
        temp_DepthRt2?.Release();
    }
}