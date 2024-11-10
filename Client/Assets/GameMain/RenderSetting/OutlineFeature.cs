using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

public class OutlineFeature : ScriptableRendererFeature
{
    class NormalRenderPass : ScriptableRenderPass
    {
        private RTHandle normalRt;
        private Material normal_mat;
        private RenderTexture realTexture;
        private ShaderTagId shadertag = new ShaderTagId("DepthOnly");
        ProfilingSampler m_ProfilerSampler = new("m_ProfilerSampler DrawNormalTex");
        private FilteringSettings filter;
        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        public void SetUp(RTHandle normalRt,Material normalMat)
        {
            this.normalRt = normalRt;
            normal_mat = normalMat;
            filter = new FilteringSettings(RenderQueueRange.opaque);
        }
        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
            CommandBuffer cmd = CommandBufferPool.Get("Draw NormalTex");
            var drawSetting = CreateDrawingSettings(shadertag, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
            drawSetting.overrideMaterial = normal_mat;
            drawSetting.overrideMaterialPassIndex = 0;
            
            RendererListParams param = new RendererListParams(renderingData.cullResults, drawSetting, filter);
            cmd.ClearRenderTarget(true,true,Color.black);
            //context.DrawRenderers(renderingData.cullResults, ref drawSetting, ref filter);
            //cmd.Blit(renderingData.cameraData.targetTexture, normalRt);
            
            cmd.DrawRendererList(context.CreateRendererList(ref param));
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            //cmd.ClearRenderTarget(true,true,Color.yellow);
        }
    }

    class NormalDepthOutLineDrawPass : ScriptableRenderPass
    {
        private Material m_OutlineMat;
        private RTHandle m_OutlineRt;
        private RTHandle m_NormalRT;
        private RTHandle m_DepthRT;
        private RTHandle m_ExpansionOutlineRt;
        public void SetUp(RTHandle normalRt,RTHandle depthRt,RTHandle outlineRt,RTHandle expansionOutlineRt,Material outlineMat)
        {
            m_NormalRT = normalRt;
            m_OutlineMat = outlineMat;
            m_DepthRT = depthRt;
            m_OutlineRt = outlineRt;
            m_ExpansionOutlineRt = expansionOutlineRt;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Draw Outline");
            cmd.ClearRenderTarget(true,true,Color.black);
            cmd.SetGlobalTexture("_NormalTex", m_NormalRT);
            cmd.SetGlobalTexture("_DepthTex", m_DepthRT);
            cmd.Blit(m_NormalRT,m_OutlineRt,m_OutlineMat,0);
            cmd.SetGlobalTexture("_OutlineTex", m_OutlineRt);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
    class ColorOutLineDrawPass : ScriptableRenderPass
    {
        private Material m_ColorOutlineMat;
        private RTHandle m_OutlineRt;
        private RTHandle m_ColorOutlineRt;
        private RTHandle m_cameraTarget;
        public void SetUp(RTHandle cameraTarget,RTHandle outlineRt,RTHandle colorOutlineRt,Material outlineMat)
        {
            m_cameraTarget = cameraTarget;
            m_ColorOutlineMat = outlineMat;
            m_OutlineRt = outlineRt;
            m_ColorOutlineRt = colorOutlineRt;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Draw Color Outline");
            cmd.SetGlobalTexture("_ColorTex",m_cameraTarget);
            cmd.Blit(null,m_ColorOutlineRt,m_ColorOutlineMat,0);
            cmd.Blit(m_ColorOutlineRt, m_OutlineRt);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            ConfigureTarget(m_cameraTarget);
        }
    }
    class AddDrawOutlinePass : ScriptableRenderPass
    {
        private Material m_addOutlineScreenMat;
        private RTHandle m_tempRt;
        private RTHandle m_cameraTarget;
        public void SetUp(RTHandle cameraTarget, RTHandle tempRt,Material addOutlineMat)
        {
            m_addOutlineScreenMat = addOutlineMat;
            m_tempRt = tempRt;
            m_cameraTarget = cameraTarget;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Add Draw Outline");
            cmd.Blit(null,m_tempRt,m_addOutlineScreenMat,0);
            cmd.Blit(m_tempRt,m_cameraTarget);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
    NormalRenderPass m_NormalPass;
    private RenderObjectsPass m_RenderObjectPass;
    private NormalDepthOutLineDrawPass m_DrawOutlinePass;
    private ColorOutLineDrawPass m_DrawColorOutlinePass;
    private AddDrawOutlinePass m_AddDrawOutline;
    private RTHandle m_NormalHandle;
    private RTHandle m_DepthHandle;
    private RTHandle m_OutlineHandle;
    private RTHandle m_ColorOutlineHandle;
    private RTHandle m_ExpansionOutlineHandle;
    public Material normal_mat;
    public Material outline_mat;
    public Material colorOutline_mat;
    public Material addOutline_mat;
    public RenderTexture t;
    /// <inheritdoc/>
    public override void Create()
    {
        m_NormalPass = new NormalRenderPass();

        // Configures where the render pass should be injected.
        m_NormalPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        var cameraSetting = new RenderObjects.CustomCameraSettings();
        var defaultLayer = LayerMask.NameToLayer("Default");
        var shaderTags = new string[]
        {
            "UniversalForward",
            "SRPDefaultUnlit",
            "DepthOnly"
        };
        m_RenderObjectPass = new RenderObjectsPass("normal RenderObject",RenderPassEvent.AfterRenderingOpaques,shaderTags,RenderQueueType.Opaque,defaultLayer,cameraSetting);

        m_DrawOutlinePass = new NormalDepthOutLineDrawPass();
        m_DrawOutlinePass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        m_AddDrawOutline = new AddDrawOutlinePass();
        m_AddDrawOutline.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        m_DrawColorOutlinePass = new ColorOutLineDrawPass();
        m_DrawColorOutlinePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        //if (!ShouldRender(in renderingData)) return;
        renderer.EnqueuePass(m_NormalPass);
        renderer.EnqueuePass(m_DrawOutlinePass);
        renderer.EnqueuePass(m_DrawColorOutlinePass);
        renderer.EnqueuePass(m_AddDrawOutline);
    }
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        //if (!ShouldRender(in renderingData)) return;
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 32;
        RenderingUtils.ReAllocateIfNeeded(ref m_DepthHandle, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "normal test Depth");
        descriptor.depthBufferBits = 0;
        RenderingUtils.ReAllocateIfNeeded(ref m_OutlineHandle, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "OutLine Rt");
        RenderingUtils.ReAllocateIfNeeded(ref m_ColorOutlineHandle, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Color OutLine Rt");
        RenderingUtils.ReAllocateIfNeeded(ref m_ExpansionOutlineHandle, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "ExpansionOutLine Rt");
        descriptor.depthBufferBits = 0;
        descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SNorm;
        //descriptor.graphicsFormat = GraphicsFormat.R8G8B8_UNorm;
        var textureName = "_NormalTex";
        RenderingUtils.ReAllocateIfNeeded(ref m_NormalHandle, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: textureName);
        //m_RenderObjectPass.ConfigureTarget(m_OldFrameHandle);
        m_NormalPass.SetUp(m_NormalHandle,normal_mat);
        //m_NormalPass.ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
        m_NormalPass.ConfigureTarget(m_NormalHandle,m_DepthHandle);
        //m_NormalPass.ConfigureTarget(m_OldFrameHandle);
        /*m_RenderObjectPass.overrideMaterial = normal_mat;
        m_RenderObjectPass.overrideMaterialPassIndex = 0;*/


        m_DrawOutlinePass.SetUp(m_NormalHandle,m_DepthHandle,m_OutlineHandle,m_ExpansionOutlineHandle,outline_mat);
        m_DrawOutlinePass.ConfigureColorStoreAction(RenderBufferStoreAction.DontCare);
        
        m_DrawColorOutlinePass.SetUp(renderer.cameraColorTargetHandle,m_OutlineHandle,m_ColorOutlineHandle,colorOutline_mat);
        //m_DrawOutlinePass.ConfigureTarget(m_OutlineHandle);
        m_AddDrawOutline.SetUp(renderer.cameraColorTargetHandle,m_ColorOutlineHandle,addOutline_mat);
        //m_OnlyDrawOutline.ConfigureTarget(m_OutlineHandle);
    }

    protected override void Dispose(bool disposing)
    {
        m_DepthHandle?.Release();
        m_OutlineHandle?.Release();
        m_ExpansionOutlineHandle?.Release();
        m_NormalHandle?.Release();
        m_ColorOutlineHandle?.Release();
    }

    bool ShouldRender(in RenderingData data) {
        if (data.cameraData.cameraType != CameraType.Game) {
            return false;
        }
        if (m_NormalPass == null) {
            Debug.LogError($"RenderPass = null!");
            return false;
        }
        return true;
    }
}


