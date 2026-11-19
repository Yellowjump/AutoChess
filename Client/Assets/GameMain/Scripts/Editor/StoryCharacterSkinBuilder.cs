using System;
using System.Collections.Generic;
using Entity;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class StoryCharacterSkinBuilder
{
    private static readonly HumanBodyBones[] BoneTypes =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
    };

    internal static void Bind(GameObject instance, GameObject geometry, string folder, string identity, Material material)
    {
        var animator = instance.GetComponent<Animator>();
        if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            throw new InvalidOperationException("Source rig requires a valid Humanoid Avatar.");
        animator.Rebind();
        var bones = new Transform[BoneTypes.Length];
        for (int i = 0; i < bones.Length; i++) bones[i] = animator.GetBoneTransform(BoneTypes[i]);
        foreach (var old in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            Object.DestroyImmediate(old.gameObject);
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        var weights = new List<BoneWeight>();
        var filters = geometry.GetComponentsInChildren<MeshFilter>();
        // Fit the mesh to the rig before scaling down the entire character.
        float scale = (bones[2].position.y - bones[11].position.y) / (1.77f - .20f);
        foreach (var filter in filters)
        {
            if (filter.name.StartsWith("Axe", StringComparison.Ordinal) ||
                filter.name.StartsWith("Mountain axe", StringComparison.Ordinal) ||
                filter.name.StartsWith("Curved bow", StringComparison.Ordinal) ||
                filter.name.StartsWith("Bowstring", StringComparison.Ordinal))
                continue;

            var source = filter.sharedMesh;
            Vector3 center = filter.transform.TransformPoint(source.bounds.center) * scale;
            int bone = PartBone(filter.name, center, bones);
            float side = Mathf.Sign(center.x);
            int arm = side == Mathf.Sign(bones[3].position.x) ? 3 : 6;
            int leg = arm == 3 ? 9 : 12;
            Vector3 shoulder = new Vector3(side * .27f, 1.38f, 0);
            Vector3 elbow = new Vector3(side * .43f, 1.12f, .045f);
            Vector3 wrist = new Vector3(side * .49f, .93f, side > 0 ? .15f : .075f);
            Vector3 ankle = new Vector3(side * .16f, .20f, 0);
            Vector3 knee = new Vector3(side * .147f, .53f, 0);
            Vector3 hip = new Vector3(side * .136f, .87f, 0);
            int offset = vertices.Count;
            // The robe's UV2 marks the sleeve rings beyond each sewn armhole.
            var sleeveFit = filter.name == "Short robe" ? source.uv2 : null;
            var sourceVertices = source.vertices;
            for (int v = 0; v < sourceVertices.Length; v++)
            {
                Vector3 point = filter.transform.TransformPoint(sourceVertices[v]);
                Vector3 fitted;
                BoneWeight weight;
                if (sleeveFit != null && v < sleeveFit.Length && sleeveFit[v].x > .5f)
                {
                    float sleeveSide = Mathf.Sign(point.x);
                    int upper = sleeveSide == Mathf.Sign(bones[3].position.x) ? 3 : 6;
                    var root = new Vector3(sleeveSide * .27f, 1.38f, 0);
                    var cuff = new Vector3(sleeveSide * .43f, 1.12f, .045f);
                    (fitted, weight) = FitSegment(point, root, cuff, Torso(root), bones[upper + 1].position,
                        1, upper, upper + 1, scale, startWeight: 0f);
                }
                else if (bone == arm && filter.name.StartsWith("Broad sleeve"))
                    (fitted, weight) = FitSegment(point, shoulder, elbow, Torso(shoulder), bones[arm + 1].position,
                        1, arm, arm + 1, scale, startWeight: 0f);
                else if (bone == arm + 1 && filter.name.StartsWith("Forearm") && !filter.name.StartsWith("Forearm plate"))
                    (fitted, weight) = FitSegment(point, elbow, wrist, bones[arm + 1].position, bones[arm + 2].position,
                        arm, arm + 1, arm + 2, scale, endWeight: 1f);
                else if (bone == leg)
                {
                    if (point.y >= knee.y)
                        (fitted, weight) = FitSegment(point, hip, knee, Torso(hip), bones[leg + 1].position,
                            0, leg, leg + 1, scale, startWeight: 0f);
                    else
                        (fitted, weight) = FitSegment(point, knee, ankle, bones[leg + 1].position, bones[leg + 2].position,
                            leg, leg + 1, leg + 2, scale, endWeight: 1f);
                }
                else if (bone == arm + 2)
                {
                    fitted = bones[bone].position + (point - wrist) * scale;
                    weight = Blend(bone, bone, 0);
                }
                else if (bone == leg + 2)
                {
                    fitted = bones[bone].position + (point - ankle) * scale;
                    weight = Blend(bone, bone, 0);
                }
                else if (bone == arm || bone == arm + 1)
                {
                    fitted = bone == arm ? Torso(point) : bones[bone].position + (point - elbow) * scale;
                    weight = Blend(bone, bone, 0);
                }
                else if (bone == 2)
                {
                    Vector3 headOffset = bones[2].position - Vector3.up * (1.77f * scale);
                    float headWeight = filter.name.StartsWith("Neck") || filter.name.StartsWith("Short angular beard")
                        ? Mathf.InverseLerp(1.43f, 1.56f, point.y) : 1f;
                    fitted = Vector3.Lerp(Torso(point), point * scale + headOffset, headWeight);
                    weight = Blend(1, 2, headWeight);
                }
                else
                {
                    fitted = Torso(point);
                    weight = Blend(0, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.02f, 1.36f, point.y)));
                }
                vertices.Add(instance.transform.InverseTransformPoint(fitted));
                weights.Add(weight);
            }
            uv.AddRange(source.uv);
            foreach (int index in source.triangles) triangles.Add(offset + index);
        }
        var bindposes = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            bindposes[i] = bones[i].worldToLocalMatrix * instance.transform.localToWorldMatrix;
        string path = folder + "/" + identity + "_Skin.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var mesh = existing != null ? existing : new Mesh { name = identity + "Skin" };
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.boneWeights = weights.ToArray();
        mesh.bindposes = bindposes;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (existing == null) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        var visual = new GameObject("CharacterMesh");
        visual.transform.SetParent(instance.transform, false);
        visual.layer = instance.layer;
        var skin = visual.AddComponent<SkinnedMeshRenderer>();
        skin.sharedMesh = mesh;
        skin.sharedMaterial = material;
        skin.bones = bones;
        skin.rootBone = bones[0];
        skin.localBounds = new Bounds(Vector3.up * .65f, Vector3.one * 3f);
        instance.GetComponent<EntityQiziObjHandle>().EntityRendererList = new Renderer[] { skin };
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        instance.transform.localScale = Vector3.one * .60f;
        Debug.Log($"Built {identity}: valid Humanoid Avatar={animator.avatar.name}, skin vertices={mesh.vertexCount}");

        Vector3 Torso(Vector3 point)
        {
            Vector3 hipsOffset = bones[0].position - Vector3.up * (.89f * scale);
            Vector3 shoulderCenter = (bones[3].position + bones[6].position) * .5f;
            Vector3 shoulderOffset = shoulderCenter - Vector3.up * (1.38f * scale);
            float height = Mathf.InverseLerp(.89f, 1.38f, point.y);
            return point * scale + Vector3.Lerp(hipsOffset, shoulderOffset, height);
        }
    }

    // Adjacent sections share the same center and weights at each joint.
    private static (Vector3, BoneWeight) FitSegment(Vector3 point, Vector3 start, Vector3 end,
        Vector3 targetStart, Vector3 targetEnd, int previous, int current, int next, float scale,
        float startWeight = .5f, float endWeight = .5f)
    {
        Vector3 axis = end - start;
        float t = Mathf.Clamp01(Vector3.Dot(point - start, axis) / axis.sqrMagnitude);
        Vector3 radial = point - Vector3.Lerp(start, end, t);
        Vector3 fitted = Vector3.Lerp(targetStart, targetEnd, t) + radial * scale;
        BoneWeight weight = t < .5f
            ? Blend(previous, current, Mathf.Lerp(startWeight, 1f, Mathf.Clamp01(t * 5f)))
            : Blend(current, next, Mathf.Lerp(0f, endWeight, Mathf.Clamp01((t - .8f) * 5f)));
        return (fitted, weight);
    }

    private static BoneWeight Blend(int first, int second, float amount)
    {
        if (first == second || amount <= 0f)
            return new BoneWeight { boneIndex0 = first, weight0 = 1f };
        if (amount >= 1f)
            return new BoneWeight { boneIndex0 = second, weight0 = 1f };
        return new BoneWeight
        {
            boneIndex0 = amount <= .5f ? first : second,
            boneIndex1 = amount <= .5f ? second : first,
            weight0 = Mathf.Max(1f - amount, amount),
            weight1 = Mathf.Min(1f - amount, amount)
        };
    }

    private static int PartBone(string name, Vector3 center, Transform[] bones)
    {
        bool left = Mathf.Sign(center.x) == Mathf.Sign(bones[3].position.x);
        if (name.StartsWith("Broad sleeve") || name.Contains("shoulder", StringComparison.OrdinalIgnoreCase))
            return left ? 3 : 6;
        if (name.StartsWith("Forearm") || name.StartsWith("Wrist")) return left ? 4 : 7;
        if (name.StartsWith("Mitten") || name.StartsWith("Thumb")) return left ? 5 : 8;
        if (name.StartsWith("Trouser")) return left ? 9 : 12;
        if (name.StartsWith("Cloth boot") || name.StartsWith("Boot cuff")) return left ? 11 : 14;
        if (name.StartsWith("Oar")) return left ? 5 : 8;
        if (name.Contains("robe") || name.Contains("sash") || name.Contains("Belt") ||
            name.Contains("ledger") || name.Contains("Ledger") || name.Contains("seal") || name.Contains("Seal"))
            return 0;
        if (name.Contains("Sword") || name.Contains("armor") || name.Contains("mantle") ||
            name.Contains("Quiver") || name.Contains("Arrow") || name.Contains("Star")) return 1;
        return 2;
    }
}
