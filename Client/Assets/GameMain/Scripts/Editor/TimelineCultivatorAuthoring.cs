using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class TimelineCultivatorAuthoring
{
    private const string Characters = "Assets/GameMain/Art/Characters";
    private const string Prefabs = Characters + "/Resources/StoryCharacters";
    private const string Skins = Characters + "/Cultivators";
    private const string Rig = "Assets/GameMain/Prefeb/Charactor/qizi0/KyleRobot.prefab";
    private const int Hair = 4, Robe = 6, Sleeve = 7, Trim = 8, Light = 9, Belt = 10, Accent = 11;

    private sealed class Design
    {
        public readonly string Name, Base, Weapon;
        public readonly uint[] Colors;
        public readonly Action<Transform> Dress;

        public Design(string name, string basis, string weapon, Action<Transform> dress, params uint[] colors)
        {
            Name = name;
            Base = basis;
            Weapon = weapon;
            Dress = dress;
            Colors = colors;
        }
    }

    private static readonly Design[] Designs =
    {
        new Design("Qinghuang", "LuYing", "Jade sword", Qinghuang, 0x211c20, 0x354560, 0x465c79, 0xb5a06f, 0xa2ccb4, 0x223334, 0x438a77),
        new Design("Chili", "LuYing", "Cleaver", Chili, 0x30231d, 0x63382c, 0x795346, 0x99744e, 0xe2b78b, 0x49342a, 0xae4c39),
        new Design("Baixi", "Zhuxing", "Coldstream sword", Baixi, 0x252536, 0xb6c9d5, 0xd6e4e4, 0x6e9bb6, 0xf1e8d9, 0x527894, 0x88b7d2),
        new Design("Hegui", "Zhuxing", "Needle case", Hegui, 0x292429, 0x444d63, 0x5b6878, 0xb4a083, 0xf1ead9, 0x653431, 0xa04536),
        new Design("Xuansha", "Chongshan", "Pestle", Xuansha, 0x918379, 0x544534, 0x6d5844, 0xb09a6d, 0xd3c0a0, 0x463326, 0x876044),
        new Design("Wenchao", "Zhuxing", "Tidecaller sword", Wenchao, 0x202a33, 0x478a8e, 0x69a9a4, 0xb8aa74, 0xe7e9d4, 0x26546b, 0x8ad0c4),
        new Design("Zhaoye", "LuYing", "Spear", Zhaoye, 0x21212c, 0x2a374c, 0x3b4d69, 0xa68854, 0xcdd4d8, 0x242635, 0xa94837),
        new Design("Gusong", "Chongshan", "Pine sword", Gusong, 0xd6d3c4, 0x536754, 0x7a8465, 0xb0a377, 0xe6e3cd, 0x393d2e, 0x6d9b69),
        new Design("ChosenSwordsman", "LuYing", "Gilded sword", Chosen, 0x252126, 0xd2c5a8, 0xe4dbbc, 0xb38b4a, 0xffe2a1, 0x614338, 0xa96644)
    };

    [MenuItem("Tools/World Timeline/Build Cultivator Battle Models")]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before rebuilding cultivator models.");
        if (!AssetDatabase.IsValidFolder(Skins)) AssetDatabase.CreateFolder(Characters, "Cultivators");
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Rig);
        foreach (var design in Designs) Build(design, rig);
        AssetDatabase.SaveAssets();
        Debug.Log("CULTIVATOR_MODELS_BUILT " + Designs.Length);
    }

    private static void Build(Design design, GameObject rig)
    {
        string skinPath = $"{Skins}/{design.Name}_Skin.asset";
        var subassets = AssetDatabase.LoadAllAssetsAtPath(skinPath);
        var palette = subassets.OfType<Texture2D>().FirstOrDefault();
        bool newPalette = palette == null;
        if (newPalette) palette = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = design.Name + " Palette" };
        WritePalette(palette, design.Colors);

        var material = subassets.OfType<Material>().FirstOrDefault();
        bool newMaterial = material == null;
        if (newMaterial)
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = design.Name + " Cloth" };
        material.SetTexture("_BaseMap", palette);
        material.SetFloat("_Smoothness", .12f);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(rig);
        var template = AssetDatabase.LoadAssetAtPath<GameObject>($"{Characters}/{design.Base}/{design.Base}_Parts.fbx");
        var geometry = (GameObject)PrefabUtility.InstantiatePrefab(template);
        var newMeshes = new List<Mesh>();
        try
        {
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = design.Name;
            foreach (var part in geometry.GetComponentsInChildren<MeshFilter>())
                if (RemovePart(part.name, design.Name)) Object.DestroyImmediate(part.gameObject);
            design.Dress(geometry.transform);
            Weapon(geometry.transform, design.Weapon);
            foreach (var part in geometry.GetComponentsInChildren<MeshFilter>())
                if (!EditorUtility.IsPersistent(part.sharedMesh)) newMeshes.Add(part.sharedMesh);

            StoryCharacterSkinBuilder.Bind(model, geometry, Skins, design.Name, material);
            if (newPalette) AssetDatabase.AddObjectToAsset(palette, skinPath);
            if (newMaterial) AssetDatabase.AddObjectToAsset(material, skinPath);
            EditorUtility.SetDirty(palette);
            EditorUtility.SetDirty(material);
            PrefabUtility.SaveAsPrefabAsset(model, $"{Prefabs}/{design.Name}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(geometry);
            Object.DestroyImmediate(model);
            foreach (var mesh in newMeshes) Object.DestroyImmediate(mesh);
        }
    }

    private static void WritePalette(Texture2D palette, uint[] colors)
    {
        uint[] defaults = { 0xb88564, 0xdaa97f, 0xf1c89c, 0x55352b, 0x292731, 0x37353a, 0, 0,
            0, 0, 0, 0, 0x42454b, 0x646c72, 0xa3afb5, 0x7b5b3d };
        int[] slots = { Hair, Robe, Sleeve, Trim, Light, Belt, Accent };
        for (int i = 0; i < slots.Length; i++) defaults[slots[i]] = colors[i];
        palette.filterMode = FilterMode.Point;
        palette.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < defaults.Length; i++)
        {
            uint color = defaults[i];
            palette.SetPixel(i % 4, i / 4, new Color32((byte)(color >> 16), (byte)(color >> 8), (byte)color, 255));
        }
        palette.Apply();
    }

    private static bool RemovePart(string part, string name)
    {
        if (HasPrefix(part, "Sword", "Axe", "Mountain axe", "Bowstring", "Curved bow", "Quiver", "Arrow",
                "Stolen pass seal", "Seal mark", "Star clasp")) return true;
        if (name == "Chili") return false;
        if (name is "Baixi" or "Hegui" or "Wenchao" && HasPrefix(part, "Heavy expressive eyebrow")) return true;
        return HasPrefix(part, "Iron shoulder", "Forearm plate", "Cross body armor", "Heavy shoulder guard",
            "Brow scar", "Short angular beard", "Short hood mantle", "Tied hair", "Ponytail", "Hair tie");
    }

    private static bool HasPrefix(string name, params string[] prefixes) =>
        prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    private static void Qinghuang(Transform root)
    {
        Jewel(root, "Jade crown", new Vector3(0, 2.31f, .04f), new Vector3(.14f, .075f, .12f), Trim);
        Jewel(root, "Jade crown stone", new Vector3(0, 2.31f, .14f), new Vector3(.05f, .06f, .03f), Accent);
        Ribbons(root, Accent, 2.28f);
        Collar(root, Trim);
    }

    private static void Chili(Transform root)
    {
        Jewel(root, "Short angular beard", new Vector3(0, 1.63f, .12f), new Vector3(.145f, .10f, .095f), Hair);
        Segment(root, "Red mantle", new Vector3(-.39f, 1.41f, .02f), new Vector3(.33f, 1.39f, .015f), .10f, .13f, Accent);
        Segment(root, "Long mantle", new Vector3(-.27f, 1.46f, -.17f), new Vector3(-.37f, .80f, -.29f), .15f, .22f, Accent);
    }

    private static void Baixi(Transform root)
    {
        LongHair(root);
        Brows(root);
        Collar(root, Light);
        Flowers(root, Light, .18f);
        Segment(root, "Pale robe panel", new Vector3(0, .53f, 0), new Vector3(0, .91f, 0),
            .34f, .24f, Robe, 8, .64f);
        Segment(root, "Belt ribbon", new Vector3(.22f, 1.01f, .18f), new Vector3(.29f, .57f, .10f),
            .051f, .022f, Light, 4);
    }

    private static void Hegui(Transform root)
    {
        Brows(root);
        Collar(root, Trim);
        Ribbons(root, Accent, 2.16f);
        Segment(root, "Crane crest", new Vector3(.14f, 2.06f, .04f), new Vector3(.25f, 2.36f, -.11f),
            .052f, .009f, Light, 4);
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
                Segment(root, "Feather mantle", new Vector3(side * (.10f + i * .068f), 1.49f, .115f - i * .029f),
                    new Vector3(side * (.22f + i * .083f), 1.30f - i * .026f, .17f - i * .055f), .076f, .008f,
                    i % 3 == 1 ? Trim : Light, 4);
    }

    private static void Xuansha(Transform root)
    {
        Beard(root, 1.40f);
        Jewel(root, "Grey tied bun", new Vector3(0, 2.19f, -.08f), new Vector3(.11f, .12f, .12f), Hair);
        for (int i = -3; i <= 3; i++)
            Jewel(root, "Prayer bead mantle", new Vector3(i * .074f, 1.19f + i * i * .022f, .194f),
                Vector3.one * .049f, i % 2 == 0 ? Trim : 15);
    }

    private static void Wenchao(Transform root)
    {
        LongHair(root);
        Brows(root);
        Collar(root, Light);
        Flowers(root, Accent, -.16f);
        Segment(root, "Wave mantle", new Vector3(.15f, 1.43f, -.14f), new Vector3(.40f, .91f, -.31f),
            .13f, .18f, Sleeve, 5, .6f);
        Segment(root, "Wave mantle border", new Vector3(.28f, 1.32f, -.08f), new Vector3(.47f, .90f, -.23f),
            .039f, .055f, Accent, 4);
        Jewel(root, "Shell sash", new Vector3(.18f, 1.02f, .233f), new Vector3(.079f, .10f, .038f), Light);
    }

    private static void Zhaoye(Transform root)
    {
        LongHair(root);
        Ribbons(root, Accent, 2.09f);
        Collar(root, Trim);
        Jewel(root, "Moon mantle", new Vector3(-.25f, 1.42f, .055f), new Vector3(.16f, .11f, .17f), 12);
        Jewel(root, "Moon clasp mantle", new Vector3(.19f, 1.32f, .207f), new Vector3(.047f, .064f, .024f), Light);
    }

    private static void Gusong(Transform root)
    {
        Beard(root, 1.28f);
        Jewel(root, "Silver bun", new Vector3(0, 2.20f, -.06f), new Vector3(.12f, .12f, .13f), Hair);
        Segment(root, "Pine hairpin", new Vector3(-.14f, 2.22f, -.06f), new Vector3(.20f, 2.29f, -.05f),
            .023f, .008f, 15, 4);
        Jewel(root, "Pine hairpin tip", new Vector3(.19f, 2.28f, -.05f), new Vector3(.062f, .031f, .052f), Accent);
        Collar(root, Trim);
    }

    private static void Chosen(Transform root)
    {
        Collar(root, Trim);
        Jewel(root, "Gold crown", new Vector3(0, 2.28f, .01f), new Vector3(.13f, .15f, .12f), Trim);
        Jewel(root, "Crown stone", new Vector3(0, 2.29f, .116f), new Vector3(.048f, .067f, .03f), Light);
        Ribbons(root, Light, 2.20f);
        Jewel(root, "Gold clasp mantle", new Vector3(.14f, 1.32f, .198f), new Vector3(.074f, .074f, .03f), Accent);
    }

    private static void LongHair(Transform root)
    {
        Jewel(root, "High bun", new Vector3(0, 2.22f, -.11f), new Vector3(.13f, .14f, .13f), Hair);
        Segment(root, "Long hair", new Vector3(0, 2.04f, -.18f), new Vector3(.09f, 1.38f, -.28f), .16f, .074f, Hair, 5);
    }

    private static void Brows(Transform root)
    {
        for (int side = -1; side <= 1; side += 2)
            Segment(root, "Fine brow", new Vector3(side * .051f, 1.99f, .224f),
                new Vector3(side * .174f, 2.009f, .205f), .016f, .009f, Hair, 4);
    }

    private static void Beard(Transform root, float tip)
    {
        Segment(root, "Short angular beard", new Vector3(0, 1.69f, .144f), new Vector3(.034f, tip, .235f),
            .118f, .021f, Hair, 5);
        for (int side = -1; side <= 1; side += 2)
            Segment(root, "Grey whisker", new Vector3(side * .035f, 1.76f, .184f),
                new Vector3(side * .153f, 1.66f, .175f), .032f, .012f, Hair, 4);
    }

    private static void Collar(Transform root, int color)
    {
        Segment(root, "Collar mantle", new Vector3(-.15f, 1.46f, .132f), new Vector3(.14f, 1.14f, .193f),
            .041f, .026f, color, 4);
        Segment(root, "Collar mantle", new Vector3(.14f, 1.46f, .139f), new Vector3(-.07f, 1.24f, .204f),
            .037f, .025f, color, 4);
    }

    private static void Flowers(Transform root, int color, float side)
    {
        for (int i = 0; i < 3; i++)
            Jewel(root, "Flower hairpin", new Vector3(side + i * .034f, 2.11f + i * .065f, .106f),
                new Vector3(.050f, .043f, .032f), i == 1 ? Trim : color);
    }

    private static void Ribbons(Transform root, int color, float height)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            var knot = new Vector3(side * .07f, height, -.14f);
            var bend = new Vector3(side * .25f, height - .13f, -.28f);
            Segment(root, "Ribbon", knot, bend, .043f, .038f, color, 4);
            Segment(root, "Ribbon", bend, new Vector3(side * .36f, height - .32f, -.31f), .038f, .013f, color, 4);
        }
    }

    private static void Weapon(Transform root, string style)
    {
        var pommel = new Vector3(.31f, 1.81f, -.265f);
        var grip = new Vector3(.23f, 1.56f, -.265f);
        var tip = new Vector3(-.26f, .64f, -.265f);
        string label = "Sword " + style;
        switch (style)
        {
            case "Needle case":
                for (int i = 0; i < 3; i++)
                {
                    var offset = Vector3.right * (i * .084f - .11f);
                    Segment(root, label, grip + offset, tip + offset, .040f, .036f, 12, 5);
                    Segment(root, label, grip + offset, grip + offset + Vector3.up * (.17f + i * .046f),
                        .014f, .009f, Trim, 4);
                }
                return;
            case "Spear":
            case "Pestle":
                var head = new Vector3(.34f, 1.94f, -.28f);
                Segment(root, label, new Vector3(-.39f, .36f, -.28f), head, .038f, .035f, 15, 6);
                if (style == "Spear")
                {
                    Segment(root, label, head, head + new Vector3(.14f, .37f, 0), .095f, .004f, Light, 4);
                    Segment(root, label, head, head + new Vector3(.047f, -.20f, 0), .073f, .023f, Accent, 5);
                }
                else
                    Jewel(root, label, head + Vector3.up * .08f, new Vector3(.12f, .17f, .12f), Trim);
                return;
        }
        int scabbard = style == "Jade sword" ? Accent : style == "Gilded sword" ? Trim : 12;
        Segment(root, label, grip, tip, style == "Cleaver" ? .125f : .065f, .041f, scabbard, 4);
        Segment(root, label, grip, pommel, .032f, .032f, 15, 6);
        Segment(root, label, grip + new Vector3(-.12f, .057f, 0), grip + new Vector3(.12f, -.057f, 0),
            .038f, .038f, Trim, 5);
        Jewel(root, label, pommel, Vector3.one * .048f, Light);
    }

    private static void Jewel(Transform root, string name, Vector3 center, Vector3 size, int color)
    {
        float depth = size.z / size.x;
        Segment(root, name, center - Vector3.up * size.y, center, size.x * .45f, size.x, color, 6, depth);
        Segment(root, name, center, center + Vector3.up * size.y, size.x, size.x * .42f, color, 6, depth);
    }

    private static void Segment(Transform root, string name, Vector3 start, Vector3 end, float baseRadius,
        float tipRadius, int color, int sides = 6, float depth = 1f)
    {
        Vector3 axis = (end - start).normalized;
        Vector3 across = Vector3.Cross(axis, Mathf.Abs(axis.z) < .9f ? Vector3.forward : Vector3.right).normalized;
        Vector3 forward = Vector3.Cross(axis, across) * depth;
        var vertices = new List<Vector3>(sides * 12);
        for (int side = 0; side < sides; side++)
        {
            Vector3 a = Rim(side);
            Vector3 b = Rim((side + 1) % sides);
            Vector3 lowA = start + a * baseRadius, lowB = start + b * baseRadius;
            Vector3 highA = end + a * tipRadius, highB = end + b * tipRadius;
            vertices.AddRange(new[] { lowA, lowB, highB, lowA, highB, highA, start, lowB, lowA, end, highA, highB });
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(Enumerable.Range(0, vertices.Count).ToArray(), 0);
        var uv = new Vector2((color % 4 + .5f) / 4, (color / 4 + .5f) / 4);
        mesh.uv = Enumerable.Repeat(uv, vertices.Count).ToArray();
        mesh.RecalculateNormals();
        var piece = new GameObject(name);
        piece.transform.SetParent(root, false);
        piece.AddComponent<MeshFilter>().sharedMesh = mesh;

        Vector3 Rim(int index)
        {
            float angle = (index + .5f) * 2f * Mathf.PI / sides;
            return across * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
        }
    }
}
