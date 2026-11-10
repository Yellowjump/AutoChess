using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Entity
{
    public sealed class TimelineMonsterAnimation : MonoBehaviour
    {
        public AnimationClip Idle;
        public AnimationClip Move;
        public AnimationClip Windup;
        public AnimationClip Strike;

        private AnimationClip playing;
        private float duration;
        private float elapsed;
        private readonly List<Mesh> combinedMeshes = new List<Mesh>();

        internal void CombineRigidParts()
        {
            var handle = GetComponent<EntityQiziObjHandle>();
            if (handle == null || combinedMeshes.Count != 0) return;
            var batches = GetComponentsInChildren<MeshRenderer>()
                .GroupBy(renderer => renderer.sharedMaterial);
            var active = new List<Renderer>();
            foreach (var batch in batches)
            {
                var parts = batch.ToArray();
                var bones = parts.Select(part => part.transform.parent).Distinct().ToArray();
                var bindPoses = bones.Select(bone => bone.worldToLocalMatrix * transform.localToWorldMatrix).ToArray();
                var rootFromWorld = transform.worldToLocalMatrix;
                var instances = new CombineInstance[parts.Length];
                var weights = new List<BoneWeight>();
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i].transform;
                    var source = part.GetComponent<MeshFilter>().sharedMesh;
                    instances[i] = new CombineInstance
                    {
                        mesh = source,
                        transform = rootFromWorld * part.localToWorldMatrix
                    };
                    // Every part moves rigidly with its existing animation pivot.
                    var weight = new BoneWeight { boneIndex0 = System.Array.IndexOf(bones, part.parent), weight0 = 1f };
                    for (int vertex = 0; vertex < source.vertexCount; vertex++) weights.Add(weight);
                }
                var mesh = new Mesh { name = batch.Key.name + " combined mesh" };
                mesh.CombineMeshes(instances);
                mesh.bindposes = bindPoses;
                mesh.boneWeights = weights.ToArray();
                combinedMeshes.Add(mesh);

                var piece = new GameObject(batch.Key.name + " combined renderer") { layer = parts[0].gameObject.layer };
                piece.transform.SetParent(transform, false);
                var renderer = piece.AddComponent<SkinnedMeshRenderer>();
                renderer.rootBone = transform;
                renderer.bones = bones;
                renderer.sharedMesh = mesh;
                renderer.sharedMaterial = batch.Key;
                // Allow for the arms swinging outside the mesh's rest pose.
                var bounds = mesh.bounds;
                bounds.Expand(2f);
                renderer.localBounds = bounds;
                renderer.shadowCastingMode = batch.Key.name == "GateGolemShade"
                    ? ShadowCastingMode.On : ShadowCastingMode.Off;
                foreach (var part in parts) part.enabled = false;
                active.Add(renderer);
            }
            handle.EntityRendererList = active.ToArray();
        }

        private void OnDestroy()
        {
            foreach (var mesh in combinedMeshes) Destroy(mesh);
        }

        public void Play(AnimationClip clip, float durationMs = 0f)
        {
            if (playing == clip && clip.isLooping) return;
            playing = clip;
            duration = durationMs > 0f ? durationMs / 1000f : clip.length;
            elapsed = 0f;
            clip.SampleAnimation(gameObject, 0f);
        }

        public void Advance(float seconds)
        {
            if (playing == null || elapsed >= duration) return;
            elapsed = playing.isLooping
                ? Mathf.Repeat(elapsed + seconds, duration)
                : Mathf.Min(elapsed + seconds, duration);
            playing.SampleAnimation(gameObject, elapsed / duration * playing.length);
        }
    }
}
