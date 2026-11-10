using System;
using System.Collections.Generic;
using Entity;
using UnityEditor;
using UnityEngine;

public static class TimelineMonsterAuthoring
{
    private const string Folder = "Assets/GameMain/Art/Characters/Resources/StoryCharacters";
    private const string SteleName = "NamelessStele";
    private static readonly int[] Faces =
    {
        0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 2, 5,
        1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2
    };

    [MenuItem("Tools/World Timeline/Build Monster Prefabs %#F10")]
    public static void Build()
    {
        Vector3[] points = { Vector3.up, Vector3.down, Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
        var mesh = Asset("MonsterFacet", new Mesh());
        mesh.Clear();
        var vertices = new Vector3[Faces.Length];
        var triangles = new int[Faces.Length];
        for (int i = 0; i < Faces.Length; i++)
        {
            vertices[i] = points[Faces[i]];
            triangles[i] = i;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        BuildOne("WreckBeast", mesh, .68f, new Color32(39, 85, 77, 255), new Color32(167, 203, 169, 255), Water);
        BuildOne("GateGolem", mesh, .83f, new Color32(71, 74, 82, 255), new Color32(215, 151, 66, 255), Stone);
        BuildOne("RitualWraith", mesh, .95f, new Color32(57, 41, 66, 255), new Color32(208, 98, 124, 255), Ritual);
        AssetDatabase.SaveAssets();
        Debug.Log("Built WreckBeast, GateGolem and RitualWraith with idle, move and two-phase attack clips.");
    }

    [MenuItem("Tools/World Timeline/Build Nameless Stele Boss")]
    public static void BuildNamelessStele()
    {
        var wedge = Prism("NamelessSteleFoot", new[]
        {
            new Vector2(-.48f,.025f), new Vector2(-.37f,.24f), new Vector2(-.12f,.36f),
            new Vector2(.26f,.32f), new Vector2(.48f,.025f)
        }, .46f, .74f);
        BuildOne(SteleName, wedge, 1f, new Color32(54, 51, 55, 255),
            new Color32(82, 104, 87, 255), NamelessStele);
        AssetDatabase.SaveAssets();
        Debug.Log("Built the Nameless Stele boss prefab.");
    }

    private static void NamelessStele(Transform body, Mesh wedge, Material stone, Material bronze,
        TimelineMonsterAnimation clips)
    {
        var dark = Tint(SteleName + "Base", new Color32(43, 43, 49, 255));
        var seal = Tint(SteleName + "Seal", new Color32(158, 58, 40, 255));
        var page = Tint(SteleName + "Page", new Color32(228, 193, 126, 255));
        var feet = new Transform[6];
        for (int i = 0; i < feet.Length; i++)
        {
            float angle = i * 60f;
            var direction = Quaternion.Euler(0, angle, 0) * Vector3.right;
            feet[i] = Pivot(body, "Foot" + i, direction * .93f);
            var foot = Part(feet[i], "Slate", wedge, stone, Vector3.zero, Vector3.one);
            foot.localEulerAngles = new Vector3(0f, angle + 90f, 0f);
            Part(body, "Plinth" + i, wedge, dark, direction * .39f + Vector3.up * .09f,
                new Vector3(.82f, .78f, .92f)).localEulerAngles = new Vector3(0f, angle + 90f, 0f);
        }

        var slab = Pivot(body, "Stele", Vector3.up * .24f);
        var left = Prism("NamelessSteleLeft", new[]
        {
            new Vector2(-.65f,0), new Vector2(-.78f,.44f), new Vector2(-.69f,1.48f),
            new Vector2(-.51f,2.13f), new Vector2(.13f,2.52f), new Vector2(.10f,1.83f),
            new Vector2(-.07f,1.24f), new Vector2(.05f,.83f), new Vector2(-.09f,.33f), new Vector2(-.04f,0)
        }, .44f, .57f);
        var right = Prism("NamelessSteleRight", new[]
        {
            new Vector2(.11f,0), new Vector2(.15f,.41f), new Vector2(.25f,.84f),
            new Vector2(.13f,1.27f), new Vector2(.31f,1.88f), new Vector2(.58f,2.16f),
            new Vector2(.71f,1.72f), new Vector2(.63f,1.28f), new Vector2(.77f,.94f), new Vector2(.65f,0)
        }, .42f, .53f);
        Part(slab, "Broken Left", left, stone, Vector3.zero, Vector3.one);
        Part(slab, "Broken Right", right, dark, Vector3.zero, Vector3.one);
        var strip = Prism("NamelessSteleBand", new[]
        {
            new Vector2(-.5f,0), new Vector2(-.45f,.83f), new Vector2(-.15f,1f),
            new Vector2(.5f,.92f), new Vector2(.47f,.16f), new Vector2(.19f,.07f)
        }, .025f, .02f);
        var folio = Prism("NamelessSteleFolio", new[]
        {
            new Vector2(-.43f,0), new Vector2(-.5f,.3f), new Vector2(-.39f,.95f),
            new Vector2(.32f,1f), new Vector2(.5f,.83f), new Vector2(.41f,.23f), new Vector2(.23f,.04f)
        }, .05f, .055f);
        for (int side = -1; side <= 1; side += 2)
        {
            var front = side > 0 ? " Front" : " Back";
            Part(slab, "Register" + front, folio, page, new Vector3(.08f,.37f,side * .215f),
                new Vector3(.26f,1.51f,1f)).localEulerAngles = new Vector3(0,0,-8f);
            Part(slab, "Lower Seal Left" + front, strip, seal, new Vector3(-.36f,.28f,side * .223f),
                new Vector3(.54f,.19f,1f));
            Part(slab, "Lower Seal Right" + front, strip, seal, new Vector3(.38f,.25f,side * .216f),
                new Vector3(.39f,.20f,1f));
            for (int i = 0; i < 2; i++)
                Part(slab, "Clamp " + i + front, strip, bronze,
                    new Vector3(.10f + i * .1f,.74f + i * .66f,side * .262f),
                    new Vector3(.64f,.105f,1.7f)).localEulerAngles = new Vector3(0,0,6f + i * 2f);
        }
        clips.Idle = Clip(clips, "Idle", true, c => Rotate(c, slab, "z", -1f, 1f, -1f));
        clips.Move = Clip(clips, "Move", true, c =>
        {
            Rotate(c, slab, "x", 5f, -4f, 5f);
            for (int i = 0; i < feet.Length; i++)
                Offset(c, feet[i], "y", i % 2 == 0 ? .12f : 0f, i % 2 == 0 ? 0f : .12f, i % 2 == 0 ? .12f : 0f);
        });
        clips.Windup = Clip(clips, "Windup", false, c =>
        {
            Rotate(c, slab, "x", 0f, -9f, -17f);
            Offset(c, slab, "y", 0f, .12f, .22f);
        });
        clips.Strike = Clip(clips, "Strike", false, c =>
        {
            Rotate(c, slab, "x", 20f, 7f, 0f);
            Offset(c, slab, "y", -.10f, -.03f, 0f);
        });
        const float impactPitch = 87f;
        float restHeight = slab.localPosition.y;
        float impactHeight = restHeight + .22f;
        // The last windup frame and the first recovery frame are the impact pose.
        // The attack state samples recovery frame zero before resolving the hit.
        clips.LineWindup = Clip(clips, "LineWindup", false, c =>
        {
            Curve(c, slab, "localEulerAnglesRaw.x",
                new Keyframe(0f, 0f), new Keyframe(.72f, -28f), new Keyframe(.87f, -28f), new Keyframe(1f, impactPitch));
            Curve(c, slab, "localPosition.y",
                new Keyframe(0f, restHeight), new Keyframe(.72f, restHeight + .28f),
                new Keyframe(.87f, restHeight + .28f), new Keyframe(1f, impactHeight));
        });
        clips.LineRecovery = Clip(clips, "LineRecovery", false, c =>
        {
            Curve(c, slab, "localEulerAnglesRaw.x",
                new Keyframe(0f, impactPitch), new Keyframe(.25f, impactPitch), new Keyframe(1f, 0f));
            Curve(c, slab, "localPosition.y",
                new Keyframe(0f, impactHeight), new Keyframe(.25f, impactHeight), new Keyframe(1f, restHeight));
        });
    }

    private static Mesh Prism(string name, Vector2[] profile, float frontDepth, float backDepth)
    {
        var vertices = new List<Vector3>();
        var faces = new List<int>();
        var center = Vector2.zero;
        foreach (var point in profile) center += point;
        center /= profile.Length;
        Vector3 FacePoint(Vector2 point, float depth) => new Vector3(point.x, point.y, depth);
        void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            faces.Add(vertices.Count);
            faces.Add(vertices.Count + 1);
            faces.Add(vertices.Count + 2);
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
        }
        for (int i = 0; i < profile.Length; i++)
        {
            int next = (i + 1) % profile.Length;
            var a = FacePoint(profile[i], frontDepth * .48f);
            var b = FacePoint(profile[next], frontDepth * .48f);
            var c = FacePoint(profile[next], -backDepth * .48f);
            var d = FacePoint(profile[i], -backDepth * .48f);
            Triangle(FacePoint(center, frontDepth * .56f), b, a);
            Triangle(FacePoint(center, -backDepth * .54f), d, c);
            Triangle(a, b, c);
            Triangle(a, c, d);
        }
        var mesh = Asset(name, new Mesh());
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(faces, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static void BuildOne(string name, Mesh mesh, float scale, Color shade, Color accent,
        Action<Transform, Mesh, Material, Material, TimelineMonsterAnimation> build)
    {
        var root = new GameObject(name);
        root.layer = 11;
        try
        {
            root.transform.localScale = Vector3.one * scale;
            var clips = root.AddComponent<TimelineMonsterAnimation>();
            build(Pivot(root.transform, "Body", Vector3.zero), mesh, Tint(name + "Shade", shade),
                Tint(name + "Accent", accent), clips);
            var handle = root.AddComponent<EntityQiziObjHandle>();
            handle.EntityRendererList = root.GetComponentsInChildren<Renderer>();
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Water(Transform body, Mesh mesh, Material shade, Material accent, TimelineMonsterAnimation clips)
    {
        Part(body, "Hull", mesh, shade, new Vector3(0, .36f, -.12f), new Vector3(.52f, .38f, .83f));
        Part(body, "Tail", mesh, accent, new Vector3(0, .31f, -.82f), new Vector3(.28f, .12f, .46f));
        var head = Pivot(body, "Head", new Vector3(0, .43f, .36f));
        Part(head, "Skull", mesh, shade, Vector3.zero, new Vector3(.36f, .23f, .43f), true);
        var jaw = Pivot(head, "Jaw", new Vector3(0, -.16f, 0));
        Part(jaw, "LowerJaw", mesh, accent, new Vector3(0, 0, .19f), new Vector3(.25f, .09f, .29f));
        for (int side = -1; side <= 1; side += 2)
        {
            Part(body, "Fin" + side, mesh, accent, new Vector3(side * .44f, .30f, -.34f), new Vector3(.34f, .08f, .35f));
            Part(head, "Eye" + side, mesh, accent, new Vector3(side * .21f, .085f, .24f), new Vector3(.08f, .075f, .10f));
        }
        clips.Idle = Clip(clips, "Idle", true, c => Offset(c, body, "y", 0, .045f, 0));
        clips.Move = Clip(clips, "Move", true, c => Rotate(c, body, "y", -13, 13, -13));
        clips.Windup = Clip(clips, "Windup", false, c => { Offset(c, body, "z", 0, -.12f, -.29f); Rotate(c, head, "x", 0, -12, -27); Rotate(c, jaw, "x", 0, 15, 34); });
        clips.Strike = Clip(clips, "Strike", false, c => { Offset(c, body, "z", .43f, .15f, 0); Rotate(c, head, "x", 12, 6, 0); Rotate(c, jaw, "x", -5, 0, 0); });
    }

    private static void Stone(Transform body, Mesh mesh, Material shade, Material accent, TimelineMonsterAnimation clips)
    {
        Part(body, "Torso", mesh, shade, new Vector3(0, .68f, 0), new Vector3(.43f, .44f, .32f), true);
        Part(body, "Seal", mesh, accent, new Vector3(0, .69f, .27f), new Vector3(.17f, .23f, .08f));
        Part(body, "Helmet", mesh, shade, new Vector3(0, 1.12f, .02f), new Vector3(.26f, .23f, .23f), true);
        var arms = Pivot(body, "Arms", new Vector3(0, .89f, 0));
        for (int side = -1; side <= 1; side += 2)
        {
            Part(body, "Foot" + side, mesh, shade, new Vector3(side * .24f, .20f, .02f), new Vector3(.22f, .24f, .29f), true);
            Part(arms, "Boulder" + side, mesh, shade, new Vector3(side * .46f, -.25f, .07f), new Vector3(.30f, .38f, .27f), true);
            Part(arms, "Mark" + side, mesh, accent, new Vector3(side * .48f, -.25f, .31f), new Vector3(.11f, .13f, .04f));
        }
        clips.Idle = Clip(clips, "Idle", true, c => Rotate(c, body, "z", -2, 2, -2));
        clips.Move = Clip(clips, "Move", true, c => Rotate(c, body, "z", -9, 9, -9));
        clips.Windup = Clip(clips, "Windup", false, c => Rotate(c, arms, "x", 0, -80, -148));
        clips.Strike = Clip(clips, "Strike", false, c => { Rotate(c, arms, "x", -43, -14, 0); Rotate(c, body, "x", 28, 8, 0); });
    }

    private static void Ritual(Transform body, Mesh mesh, Material shade, Material accent, TimelineMonsterAnimation clips)
    {
        Part(body, "Heart", mesh, accent, new Vector3(0, .76f, .02f), new Vector3(.25f, .44f, .25f));
        var seal = Pivot(body, "Seal", new Vector3(0, .76f, 0));
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.PI * 2 / 5;
            var shard = Part(seal, "Tablet" + i, mesh, shade,
                new Vector3(Mathf.Sin(angle) * .46f, 0, Mathf.Cos(angle) * .46f), new Vector3(.13f, .39f, .11f), true);
            shard.localEulerAngles = new Vector3(12, i * 72, 18);
        }
        clips.Idle = Clip(clips, "Idle", true, c => { Offset(c, body, "y", 0, .09f, 0); Rotate(c, seal, "y", 0, 180, 360); });
        clips.Move = Clip(clips, "Move", true, c => { Offset(c, body, "y", 0, .14f, 0); Rotate(c, seal, "y", 0, 180, 360); });
        clips.Windup = Clip(clips, "Windup", false, c => { Rotate(c, seal, "y", 0, 90, 165); Scale(c, seal, 1, .83f, .57f); });
        clips.Strike = Clip(clips, "Strike", false, c => { Offset(c, body, "z", .34f, .13f, 0); Scale(c, seal, 1.32f, 1.13f, 1); Rotate(c, seal, "y", 195, 280, 360); });
    }

    private static Transform Pivot(Transform parent, string name, Vector3 position)
    {
        var go = new GameObject(name) { layer = parent.gameObject.layer };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        return go.transform;
    }

    private static Transform Part(Transform parent, string name, Mesh mesh, Material material,
        Vector3 position, Vector3 size, bool bevel = false)
    {
        var transform = Pivot(parent, name, position);
        transform.localScale = bevel ? size * 1.45f : size;
        if (bevel) transform.localEulerAngles = new Vector3(24, 45, 28);
        transform.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        transform.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        return transform;
    }

    private static Material Tint(string name, Color color)
    {
        var material = Asset(name, new Material(Shader.Find("Universal Render Pipeline/Lit")), ".mat");
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static AnimationClip Clip(TimelineMonsterAnimation rig, string action, bool loop, Action<AnimationClip> animate)
    {
        var clip = Asset(rig.name + action, new AnimationClip(), ".anim");
        clip.ClearCurves();
        // Key every animated pivot channel so crossfades restore the bind pose.
        foreach (var pivot in rig.GetComponentsInChildren<Transform>())
        {
            if (pivot == rig.transform || pivot.GetComponent<MeshRenderer>() != null) continue;
            for (int i = 0; i < 3; i++)
            {
                string axis = "xyz"[i].ToString();
                Curve(clip, pivot, "localPosition." + axis, pivot.localPosition[i], pivot.localPosition[i], pivot.localPosition[i]);
                Rotate(clip, pivot, axis, 0, 0, 0);
                Curve(clip, pivot, "localScale." + axis, 1, 1, 1);
            }
        }
        animate(clip);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void Offset(AnimationClip clip, Transform pivot, string axis, float a, float b, float c)
    {
        float origin = pivot.localPosition[axis[0] - 'x'];
        Curve(clip, pivot, "localPosition." + axis, origin + a, origin + b, origin + c);
    }

    private static void Rotate(AnimationClip clip, Transform pivot, string axis, float a, float b, float c)
        => Curve(clip, pivot, "localEulerAnglesRaw." + axis, a, b, c);

    private static void Scale(AnimationClip clip, Transform pivot, float a, float b, float c)
    {
        foreach (char axis in "xyz") Curve(clip, pivot, "localScale." + axis, a, b, c);
    }

    private static void Curve(AnimationClip clip, Transform pivot, string property, float a, float b, float c)
        => Curve(clip, pivot, property, new Keyframe(0, a), new Keyframe(.5f, b), new Keyframe(1, c));

    private static void Curve(AnimationClip clip, Transform pivot, string property, params Keyframe[] keys)
    {
        string path = AnimationUtility.CalculateTransformPath(pivot, pivot.root);
        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), property);
        AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
    }

    private static T Asset<T>(string name, T created, string extension = ".asset") where T : UnityEngine.Object
    {
        string path = Folder + "/" + name + extension;
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(created);
            return existing;
        }
        created.name = name;
        AssetDatabase.CreateAsset(created, path);
        return created;
    }
}
