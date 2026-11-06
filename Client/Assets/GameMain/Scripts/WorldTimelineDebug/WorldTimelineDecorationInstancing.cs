using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldTimelineDebug
{
    // Keep editable source objects and LOD activation, but submit repeated meshes explicitly.
    public sealed class WorldTimelineDecorationInstancing : MonoBehaviour
    {
        private sealed class Batch
        {
            public Mesh Mesh;
            public Material Material;
            public int Submesh, Layer;
            public ShadowCastingMode Shadows;
            public bool ReceiveShadows;
            public readonly List<MeshRenderer> Sources = new List<MeshRenderer>();
            public Matrix4x4[] Matrices;
        }

        private readonly List<Batch> batches = new List<Batch>();
        private readonly List<MeshRenderer> managed = new List<MeshRenderer>();
        public int SubmittedBatches { get; private set; }
        public int SubmittedInstances { get; private set; }
        public int ManagedRendererCount => managed.Count;

        private void OnEnable()
        {
            if (!Application.isPlaying || !SystemInfo.supportsInstancing) return;
            foreach (var source in GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = source.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                var materials = source.sharedMaterials;
                // Keep unsupported renderers on Unity's ordinary rendering path.
                if (source.forceRenderingOff || mesh == null || materials.Length != mesh.subMeshCount
                    || source.HasPropertyBlock() || source.lightmapIndex >= 0) continue;
                bool supported = true;
                foreach (var material in materials)
                    if (material == null || !material.enableInstancing) supported = false;
                if (!supported) continue;
                managed.Add(source);
                source.forceRenderingOff = true;
                for (int submesh = 0; submesh < materials.Length; submesh++)
                {
                    Batch batch = batches.Find(b => b.Mesh == mesh && b.Material == materials[submesh]
                        && b.Submesh == submesh && b.Layer == source.gameObject.layer
                        && b.Shadows == source.shadowCastingMode && b.ReceiveShadows == source.receiveShadows);
                    if (batch == null)
                    {
                        batch = new Batch { Mesh = mesh, Material = materials[submesh], Submesh = submesh,
                            Layer = source.gameObject.layer, Shadows = source.shadowCastingMode,
                            ReceiveShadows = source.receiveShadows };
                        batches.Add(batch);
                    }
                    batch.Sources.Add(source);
                }
            }
            foreach (var batch in batches) batch.Matrices = new Matrix4x4[Mathf.Min(1023, batch.Sources.Count)];
            RenderPipelineManager.beginCameraRendering += Render;
        }

        private void Render(ScriptableRenderContext context, Camera camera)
        {
            if (WorldTimelineBoard.ForRenderingCamera(camera) == null) return;
            SubmittedBatches = SubmittedInstances = 0;
            foreach (var batch in batches)
            {
                int count = 0;
                foreach (var source in batch.Sources)
                {
                    if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) continue;
                    batch.Matrices[count++] = source.localToWorldMatrix;
                    if (count == batch.Matrices.Length) { Draw(batch, camera, count); count = 0; }
                }
                if (count > 0) Draw(batch, camera, count);
            }
        }

        private void Draw(Batch batch, Camera camera, int count)
        {
            Graphics.DrawMeshInstanced(batch.Mesh, batch.Submesh, batch.Material, batch.Matrices, count,
                null, batch.Shadows, batch.ReceiveShadows, batch.Layer, camera, LightProbeUsage.BlendProbes);
            SubmittedBatches++;
            SubmittedInstances += count;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            foreach (var source in managed) if (source != null) source.forceRenderingOff = false;
            managed.Clear();
            batches.Clear();
            SubmittedBatches = SubmittedInstances = 0;
        }
    }
}
