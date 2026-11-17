using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class TidegateInkRenderFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    [SerializeField, Range(0f, 1f)] private float strength = .85f;

    internal static Camera BattleCamera { get; set; }

    private static TidegateInkRenderFeature installed;
    private Material material;
    private InkPass pass;

    internal static bool Styles(in CameraData cameraData) =>
        installed != null && installed.isActive && installed.material != null && installed.strength > 0f &&
        cameraData.camera == BattleCamera && cameraData.cameraType == CameraType.Game &&
        cameraData.renderType == CameraRenderType.Base;

    public override void Create()
    {
        Release();
        if (shader == null || !shader.isSupported) return;

        material = CoreUtils.CreateEngineMaterial(shader);
        // Run after the main renderer restores its depth attachment, ahead of effects and world UI.
        pass = new InkPass(material) { renderPassEvent = RenderPassEvent.AfterRenderingSkybox - 1 };
        installed = this;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (installed == this && Styles(in renderingData.cameraData)) renderer.EnqueuePass(pass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (installed == this && Styles(in renderingData.cameraData))
            pass.SetSource(renderer.cameraColorTargetHandle, strength);
    }

    protected override void Dispose(bool disposing) => Release();

    private void Release()
    {
        if (installed == this) installed = null;
        pass?.Release();
        pass = null;
        CoreUtils.Destroy(material);
        material = null;
    }

    private sealed class InkPass : ScriptableRenderPass
    {
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private readonly Material material;
        private readonly ProfilingSampler sampler = new ProfilingSampler("Tidegate Ink Wash");
        private RTHandle source;
        private RTHandle wash;

        internal InkPass(Material material) => this.material = material;

        internal void SetSource(RTHandle color, float strength)
        {
            source = color;
            material.SetFloat(StrengthId, strength);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref wash, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp,
                name: "_TidegateInkWash");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, sampler))
            {
                Blitter.BlitCameraTexture(cmd, source, wash, material, 0);
                Blitter.BlitCameraTexture(cmd, wash, source);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        internal void Release() => wash?.Release();
    }
}
