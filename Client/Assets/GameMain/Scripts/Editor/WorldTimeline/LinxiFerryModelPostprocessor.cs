using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal sealed class LinxiFerryModelPostprocessor : AssetPostprocessor
{
    private const string FerryPath = WorldTimelineInkMapAuthoring.Folder + "/Resources/WorldTimeline/LinxiFerry.fbx";
    private const float PlaneTolerance = .00001f;

    private void OnPostprocessModel(GameObject model)
    {
        if (assetPath != FerryPath) return;

        var processed = new HashSet<Mesh>();
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = filter.sharedMesh;
            if (mesh != null && processed.Add(mesh)) RemoveDepthConflicts(mesh);
        }
    }

    private static void RemoveDepthConflicts(Mesh mesh)
    {
        var vertices = mesh.vertices;
        bool movedFoundation = false;
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i];
            // The exported pavilion foundation ends on the same X plane as a bridge segment.
            // Move both corner rings of that foundation edge, leaving the bridge untouched.
            if (Mathf.Abs(p.x + 1.29f) > PlaneTolerance || p.z > -.539f ||
                (Mathf.Abs(p.y - .013f) > PlaneTolerance && Mathf.Abs(p.y - .263f) > PlaneTolerance)) continue;
            vertices[i].x -= .018f;
            movedFoundation = true;
        }
        if (movedFoundation) mesh.vertices = vertices;

        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
        {
            int[] triangles = mesh.GetTriangles(submesh);
            var kept = new List<int>(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                var p = vertices[a];
                var q = vertices[b];
                var r = vertices[c];
                // The riverbank rocks are open underneath on the map; shared bottom caps can fight.
                bool groundCap = Mathf.Abs(p.y) <= PlaneTolerance && Mathf.Abs(q.y) <= PlaneTolerance &&
                    Mathf.Abs(r.y) <= PlaneTolerance && Vector3.Cross(q - p, r - p).y < 0f;
                if (groundCap) continue;
                kept.Add(a);
                kept.Add(b);
                kept.Add(c);
            }
            if (kept.Count != triangles.Length) mesh.SetTriangles(kept, submesh, calculateBounds: false);
        }
        if (movedFoundation) mesh.RecalculateBounds();
    }
}
