using System;
using System.Collections.Generic;
using System.Linq;
using Entity;
using UnityEditor;
using UnityEngine;

// Sections are in the local space of the original animation pivots.
public static partial class TimelineMonsterAuthoring
{
    private const string SurfaceLibrary = "Assets/GameMain/Art/Characters/FigureSurfaces.asset";
    private static readonly string[] SculptedFigures =
    {
        "WreckBeast", "LavascaleLizard", "BronzeSluiceBeast", "CliffKite",
        "ColdcoreGolem", "QiMo", "NamelessClerk", "LanternWatch", "GateGolem", "RitualWraith", "SaltShellCrab"
    };

    [MenuItem("Tools/World Timeline/Rebuild Sculpted Battle Figures")]
    public static void RebuildSculptedFigures()
    {
        foreach (string name in SculptedFigures)
        {
            string path = Folder + "/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SculptFigure(root);
                root.GetComponent<EntityQiziObjHandle>().EntityRendererList = root.GetComponentsInChildren<Renderer>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    private static void SculptFigure(GameObject root)
    {
        var body = root.transform.Find("Body");
        switch (root.name)
        {
            case "WreckBeast": SculptSwimmer(body); break;
            case "GateGolem": SculptGate(body); break;
            case "RitualWraith": SculptWraith(body); break;
            case "SaltShellCrab": SculptCrab(body); break;
            case "LavascaleLizard":
            case "BronzeSluiceBeast": SculptLizard(body.Find("Low trunk")); break;
            case "CliffKite": SculptKite(body.Find("Slate breast")); break;
            case "ColdcoreGolem": SculptGlacier(body); break;
            case "QiMo": SculptScribe(body); break;
            case "NamelessClerk": SculptClerk(body.Find("Official robe")); break;
            case "LanternWatch": SculptWatch(body.Find("Paper body")); break;
        }
    }

    private readonly struct CrossSection
    {
        public readonly float Station, A, B, OffsetA, OffsetB;
        public CrossSection(float station, float a, float b, float offsetA, float offsetB)
            => (Station, A, B, OffsetA, OffsetB) = (station, a, b, offsetA, offsetB);
    }
    private static CrossSection R(float station, float a, float b, float offsetA = 0, float offsetB = 0)
        => new CrossSection(station, a, b, offsetA, offsetB);

    // Viewed end-on, width and depth are X/Z for vertical profiles, X/Y for spines,
    // and Z/Y for wings. Mirroring a wing also reverses its winding.
    private enum Sweep { Up, Forward, Right, Left }
    private static Mesh Surface(string name, Sweep axis, int sides, params CrossSection[] rows)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        bool flip = axis == Sweep.Up || axis == Sweep.Right;
        void Triangle(int a, int b, int c)
        {
            indices.Add(a); indices.Add(flip ? c : b); indices.Add(flip ? b : c);
        }
        foreach (var r in rows)
            for (int i = 0; i < sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides;
                float a = r.OffsetA + Mathf.Cos(angle) * r.A;
                float b = r.OffsetB + Mathf.Sin(angle) * r.B;
                vertices.Add(axis switch
                {
                    Sweep.Up => new Vector3(a, r.Station, b),
                    Sweep.Forward => new Vector3(a, b, r.Station),
                    Sweep.Right => new Vector3(r.Station, b, a),
                    _ => new Vector3(-r.Station, b, a)
                });
            }
        for (int j = 1; j < rows.Length; j++)
            for (int i = 0; i < sides; i++)
            {
                int a = (j - 1) * sides + i, b = (j - 1) * sides + (i + 1) % sides;
                Triangle(a, b, b + sides); Triangle(a, b + sides, a + sides);
            }
        int last = vertices.Count - sides;
        for (int i = 1; i < sides - 1; i++)
        {
            Triangle(0, i + 1, i);
            Triangle(last, last + i, last + i + 1);
        }

        // Unity renames a main mesh to the file name on import. Use the face as
        // the library's main mesh whichever individual figure is rebuilt first.
        bool main = name == "Folded cheek and chin";
        var library = AssetDatabase.LoadAssetAtPath<Mesh>(SurfaceLibrary);
        if (!library && !main) library = FaceForm();
        var mesh = main ? library : AssetDatabase.LoadAllAssetsAtPath(SurfaceLibrary)
            .OfType<Mesh>().FirstOrDefault(m => m.name == name);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            if (main) AssetDatabase.CreateAsset(mesh, SurfaceLibrary);
            else AssetDatabase.AddObjectToAsset(mesh, SurfaceLibrary);
        }
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();
        // Keep the long planes of a cut stone or feather visible from the board.
        // Blend each strip's geometric normal with the shared silhouette normal.
        var rounded = mesh.normals;
        var split = new Vector3[indices.Count];
        var normals = new Vector3[indices.Count];
        int wallIndices = (rows.Length - 1) * sides * 6;
        for (int i = 0; i < indices.Count; i += 3)
        {
            var a = vertices[indices[i]];
            var b = vertices[indices[i + 1]];
            var c = vertices[indices[i + 2]];
            var plane = Vector3.Cross(b - a, c - a).normalized;
            for (int j = 0; j < 3; j++)
            {
                int source = indices[i + j];
                split[i + j] = vertices[source];
                normals[i + j] = i < wallIndices ? Vector3.Lerp(rounded[source], plane, .48f).normalized : plane;
            }
        }
        mesh.vertices = split;
        mesh.SetTriangles(Enumerable.Range(0, split.Length).ToArray(), 0);
        mesh.normals = normals;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Transform SkinPart(Transform parent, string name, Mesh mesh, Vector3 position = default,
        Vector3? scale = null, Material material = null)
    {
        var part = parent.Find(name);
        if (part == null)
        {
            if (!material) throw new InvalidOperationException($"Missing figure part {parent.name}/{name}");
            return Part(parent, name, mesh, material, position, scale ?? Vector3.one);
        }
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        part.localPosition = position;
        part.localScale = scale ?? Vector3.one;
        part.localRotation = Quaternion.identity;
        if (material) part.GetComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    private static Material Paint(Transform part) => part.GetComponent<MeshRenderer>().sharedMaterial;

    private static void SculptLizard(Transform trunk)
    {
        var skin = Surface("Salamander back", Sweep.Forward, 12,
            R(-.62f, .105f, .088f, 0, -.035f), R(-.39f, .255f, .16f), R(-.12f, .309f, .198f),
            R(.16f, .285f, .184f), R(.36f, .227f, .162f, 0, .009f), R(.55f, .15f, .122f, 0, .015f));
        SkinPart(trunk, "Body", skin);
        var head = trunk.Find("Head");
        var skull = Surface("Salamander skull", Sweep.Forward, 10, R(-.18f, .14f, .116f),
            R(-.065f, .198f, .142f), R(.11f, .211f, .126f, 0, .026f), R(.255f, .165f, .088f, 0, .024f),
            R(.398f, .107f, .053f, 0, .005f), R(.43f, .069f, .036f));
        SkinPart(head, "Snout", skull);
        var jaw = Surface("Salamander jaw", Sweep.Forward, 10, R(-.07f, .12f, .052f), R(.11f, .183f, .067f),
            R(.27f, .157f, .045f), R(.408f, .086f, .027f, 0, .018f));
        SkinPart(head.Find("Jaw"), "Lower jaw", jaw);
        bool bronze = trunk.root.name == "BronzeSluiceBeast";
        if (bronze)
        {
            head.Find("Snout").localScale = new Vector3(1.26f, 1.20f, .93f);
            head.Find("Jaw/Lower jaw").localScale = new Vector3(1.29f, 1.18f, .94f);
        }
        var iris = FaceForm();
        for (int i = 0; i < 2; i++)
        {
            int side = i == 0 ? -1 : 1;
            float width = bronze ? 1.26f : 1;
            var eye = SkinPart(head, "Eye " + i, iris, new Vector3(side * .172f * width, .088f, .14f),
                new Vector3(.078f, .061f, .095f));
            SkinPart(head, "Slit pupil " + i, iris, eye.localPosition + new Vector3(side * .027f, .005f, .017f),
                new Vector3(.026f, .049f, .022f), Paint(head.Find("Snout")));
        }
        var tail = trunk.Find("Tail");
        // Each color band meets the next on the same cross section.
        var baseTail = Surface("Salamander tail base", Sweep.Forward, 10, R(-.50f, .082f, .068f, .072f, .007f),
            R(-.31f, .118f, .095f, .034f, -.014f), R(-.13f, .155f, .114f, .009f, -.019f), R(.13f, .20f, .14f));
        SkinPart(tail, "Tail segment 0", baseTail);
        SkinPart(tail, "Tail segment 1", Surface("Salamander tail bend", Sweep.Forward, 10,
            R(-.78f, .037f, .042f, .168f, .057f), R(-.64f, .057f, .055f, .120f, .025f), R(-.50f, .082f, .068f, .072f, .007f)));
        SkinPart(tail, "Tail segment 2", Surface("Salamander tail ember", Sweep.Forward, 10,
            R(-1.0f, .006f, .009f, .271f, .150f), R(-.91f, .022f, .026f, .212f, .096f), R(-.78f, .037f, .042f, .168f, .057f)));
        var ridge = Surface("Basalt dorsal sail", Sweep.Up, 8, R(0, .115f, .175f), R(.091f, .096f, .142f, 0, -.022f),
            R(.165f, .041f, .077f, 0, -.071f), R(.193f, .008f, .012f, 0, -.109f));
        for (int i = 0; i < 3; i++)
            SkinPart(trunk, "Back ridge " + i, ridge, new Vector3(0, .138f, -.29f + i * .246f), Vector3.one * (.78f + .12f * i));
        for (int i = 0; i < 4; i++)
        {
            int side = i < 2 ? -1 : 1;
            var axis = side < 0 ? Sweep.Left : Sweep.Right;
            var limb = Surface("Salamander leg " + side, axis, 8, R(-.10f, .104f, .11f, 0, .02f),
                R(.02f, .107f, .112f), R(.141f, .079f, .085f, -.064f, -.087f),
                R(.21f, .052f, .053f, -.037f, -.161f), R(.247f, .101f, .039f, .046f, -.206f),
                R(.292f, .087f, .030f, .077f, -.219f), R(.324f, .022f, .013f, .129f, -.222f));
            SkinPart(trunk.Find("Foot " + i), "Splayed foot", limb);
            var foot = trunk.Find("Foot " + i);
            var nail = Surface("Salamander toe " + side, axis, 6, R(0, .032f, .026f),
                R(.058f, .028f, .022f, .019f), R(.117f, .008f, .008f, .066f, -.012f));
            for (int digit = 0; digit < 2; digit++)
                SkinPart(foot, "Outer toe " + digit, nail, new Vector3(side * .26f, -.217f, .068f - digit * .069f),
                    material: Paint(foot.Find("Splayed foot"))).localEulerAngles = new Vector3(0, side * digit * 29, 0);
        }
    }

    private static void SculptKite(Transform trunk)
    {
        var torso = Surface("Kite keel", Sweep.Forward, 12, R(-.56f, .044f, .060f, 0, .045f), R(-.34f, .145f, .158f),
            R(-.13f, .251f, .259f), R(.10f, .261f, .287f, 0, .013f), R(.27f, .192f, .232f, 0, .077f), R(.40f, .12f, .142f, 0, .18f));
        var dark = Paint(SkinPart(trunk, "Breast", torso));
        SkinPart(trunk, "Head", Surface("Kite hood", Sweep.Forward, 12, R(.14f, .118f, .132f, 0, .203f),
            R(.28f, .166f, .188f, 0, .263f), R(.42f, .176f, .157f, 0, .29f), R(.53f, .128f, .117f, 0, .295f), R(.59f, .09f, .066f, 0, .28f)));
        SkinPart(trunk, "Hooked Beak", Surface("Kite beak", Sweep.Forward, 8, R(.485f, .081f, .071f, 0, .286f),
            R(.593f, .082f, .069f, 0, .277f), R(.674f, .047f, .055f, 0, .247f), R(.698f, .012f, .022f, 0, .187f)));
        for (int i = 0; i < 2; i++)
        {
            int side = i == 0 ? -1 : 1;
            var wing = trunk.Find("Rock Wing " + i);
            var axis = i == 0 ? Sweep.Left : Sweep.Right;
            var surface = Surface("Kite wing " + side, axis, 10, R(-.09f, .218f, .086f), R(.12f, .324f, .089f, -.025f, .040f),
                R(.32f, .298f, .065f, -.097f, .10f), R(.49f, .241f, .038f, -.172f, .137f),
                R(.66f, .149f, .024f, -.254f, .159f), R(.83f, .012f, .006f, -.43f, .175f));
            SkinPart(wing, "Split slate 0", surface);
            var feather = Surface("Kite pinion " + side, axis, 8, R(0, .101f, .023f), R(.10f, .096f, .020f, -.018f),
                R(.23f, .055f, .014f, -.11f), R(.33f, .004f, .004f, -.236f));
            for (int f = 1; f < 5; f++)
                SkinPart(wing, "Split slate " + f, feather, new Vector3(side * (.007f + .105f * f), .029f + .022f * f, -.224f - .047f * f));
            SkinPart(trunk, "Amber slit " + i, FaceForm(), new Vector3(side * .131f, .329f, .490f),
                new Vector3(.054f, .045f, .059f));
            var talon = Surface("Kite folded talon", Sweep.Up, 8, R(-.456f, .019f, .023f, 0, .118f),
                R(-.418f, .044f, .063f, 0, .094f), R(-.377f, .048f, .089f, 0, .057f),
                R(-.308f, .036f, .044f, 0, -.013f), R(-.22f, .058f, .063f, 0, -.038f), R(-.122f, .069f, .085f));
            SkinPart(trunk, "Talon " + i, talon, new Vector3(side * .126f, 0, .047f));
            var claw = Surface("Kite curled toe", Sweep.Forward, 6, R(0, .024f, .029f),
                R(.077f, .026f, .021f, 0, .005f), R(.12f, .016f, .019f, 0, -.007f), R(.141f, .005f, .008f, 0, -.045f));
            for (int toe = -1; toe <= 1; toe++)
                SkinPart(trunk, "Talon " + i + " toe " + toe, claw, new Vector3(side * .126f + toe * .026f, -.401f, .131f),
                    material: dark).localEulerAngles = new Vector3(0, toe * 21, 0);
        }
        SkinPart(trunk, "Forked tail", Surface("Kite train", Sweep.Forward, 10, R(-.89f, .135f, .018f, 0, .082f),
            R(-.79f, .17f, .031f, 0, .076f), R(-.58f, .109f, .049f, 0, .043f), R(-.32f, .09f, .072f)), material: dark);
    }

    private static void SculptSwimmer(Transform body)
    {
        SkinPart(body, "Hull", Surface("Estuary swimmer", Sweep.Forward, 12, R(-1.06f, .084f, .076f, 0, .315f),
            R(-.83f, .193f, .137f, 0, .304f), R(-.53f, .367f, .251f, 0, .325f), R(-.17f, .489f, .322f, 0, .349f),
            R(.13f, .432f, .296f, 0, .37f), R(.35f, .292f, .207f, 0, .41f), R(.51f, .237f, .157f, 0, .421f)));
        SkinPart(body, "Tail", Surface("Swimmer tail fan", Sweep.Forward, 10, R(-1.37f, .249f, .023f, 0, .426f),
            R(-1.27f, .278f, .044f, 0, .375f), R(-1.07f, .136f, .065f, 0, .318f), R(-.87f, .124f, .097f, 0, .311f)));
        var head = body.Find("Head");
        SkinPart(head, "Skull", Surface("Swimmer broad snout", Sweep.Forward, 12, R(-.23f, .228f, .152f), R(-.04f, .323f, .197f),
            R(.18f, .297f, .153f, 0, .004f), R(.38f, .224f, .103f, 0, -.015f), R(.5f, .128f, .065f, 0, -.028f)));
        SkinPart(head.Find("Jaw"), "LowerJaw", Surface("Swimmer jaw", Sweep.Forward, 10, R(-.09f, .189f, .071f),
            R(.112f, .272f, .088f), R(.34f, .221f, .062f), R(.48f, .124f, .031f, 0, .024f)));
        foreach (int s in new[] { -1, 1 })
        {
            var axis = s < 0 ? Sweep.Left : Sweep.Right;
            SkinPart(body, "Fin" + s, Surface("Swimmer fin " + s, axis, 8, R(.22f, .224f, .061f, -.20f, .34f),
                R(.44f, .245f, .064f, -.27f, .306f), R(.60f, .184f, .044f, -.386f, .265f), R(.79f, .018f, .012f, -.565f, .23f)));
            var eye = SkinPart(head, "Eye" + s, FaceForm(), new Vector3(s * .252f, .096f, .198f), new Vector3(.106f, .085f, .113f));
            var pale = Paint(eye);
            SkinPart(head, "Black pupil " + s, FaceForm(), eye.localPosition + new Vector3(s * .032f, .013f, .025f),
                new Vector3(.052f, .050f, .045f), Paint(head.Find("Skull")));
            var stripe = Surface("Swimmer lateral stripe " + s, Sweep.Forward, 8,
                R(-.78f, .011f, .015f, s * .217f, .354f), R(-.53f, .022f, .033f, s * .346f, .404f),
                R(-.17f, .024f, .043f, s * .463f, .416f), R(.13f, .019f, .047f, s * .405f, .442f),
                R(.30f, .009f, .020f, s * .316f, .467f));
            SkinPart(body, "Pale flank " + s, stripe, material: pale);
        }
    }

    private static void SculptGlacier(Transform body)
    {
        var torso = body.Find("Torso");
        SkinPart(torso, "Boulder", Surface("Glacier breastplate", Sweep.Up, 8,
            R(-.19f, .212f, .184f), R(.016f, .29f, .226f), R(.26f, .413f, .303f),
            R(.49f, .564f, .334f), R(.61f, .509f, .283f, -.028f, -.028f), R(.72f, .253f, .184f, -.05f, -.039f)));
        SkinPart(torso, "Brow", Surface("Glacier hood", Sweep.Up, 7, R(.577f, .172f, .169f, 0, .07f),
            R(.725f, .224f, .209f, 0, .078f), R(.872f, .237f, .194f, -.022f, .045f), R(1.017f, .098f, .097f, -.074f, -.045f)));
        var fissure = Surface("Glacier eye fissure", Sweep.Right, 6, R(-.159f, .006f, .009f),
            R(-.074f, .022f, .024f, .047f), R(0, .024f, .018f, .059f, -.013f),
            R(.079f, .021f, .025f, .043f), R(.154f, .007f, .009f));
        SkinPart(torso, "Glacial eye slit", fissure, new Vector3(0, .778f, .203f), material: Paint(torso.Find("Boulder")));
        SkinPart(torso, "Glacial eye", FaceForm(), new Vector3(0, .77f, .276f),
            new Vector3(.080f, .037f, .030f), Paint(torso.Find("Core")));
        var crystal = Surface("Cleaved ice", Sweep.Up, 6, R(0, .091f, .078f), R(.103f, .107f, .083f),
            R(.276f, .067f, .048f, .016f), R(.428f, .008f, .007f, .061f, -.019f));
        for (int i = 0; i < 2; i++)
        {
            int s = i == 0 ? -1 : 1;
            var shoulder = torso.Find("Shoulder " + i);
            SkinPart(shoulder, "Arm", Surface("Glacier forearm " + s, Sweep.Up, 8,
                R(-.667f, .184f, .184f, s * .166f, .072f), R(-.48f, .191f, .192f, s * .155f, .045f),
                R(-.27f, .126f, .143f, s * .118f), R(-.096f, .163f, .18f, s * .046f), R(.086f, .154f, .172f)));
            var ice = Paint(SkinPart(shoulder, "Ice cap", Surface("Glacier pauldron " + s, Sweep.Up, 8,
                R(-.155f, .156f, .19f), R(-.038f, .254f, .231f), R(.107f, .229f, .205f, s * .043f),
                R(.214f, .121f, .141f, s * .093f), R(.257f, .032f, .052f, s * .14f))));
            SkinPart(shoulder, "Ice spur", crystal, new Vector3(-s * .071f, .069f, -.095f), material: ice)
                .localEulerAngles = new Vector3(-17, 0, -s * 22);
            SkinPart(shoulder, "Glacier fist", Surface("Glacier gauntlet", Sweep.Up, 8, R(-.18f, .146f, .179f),
                R(-.12f, .224f, .24f), R(.019f, .235f, .218f), R(.155f, .166f, .159f)), new Vector3(s * .165f, -.64f, .089f));
            var leg = body.Find("Leg " + i);
            SkinPart(leg, "Pillar", Surface("Glacier greave", Sweep.Up, 8, R(-.71f, .158f, .179f),
                R(-.55f, .173f, .183f), R(-.36f, .132f, .159f, 0, -.033f), R(-.23f, .188f, .226f, 0, .038f),
                R(-.079f, .20f, .196f), R(.119f, .168f, .16f)));
            SkinPart(leg, "Foot", Surface("Glacier tread", Sweep.Up, 8, R(-.855f, .205f, .246f, 0, .101f),
                R(-.794f, .225f, .281f, 0, .123f), R(-.681f, .182f, .228f, 0, .067f), R(-.608f, .138f, .159f)));
        }
    }

    private static Mesh Gown(string name, float hem, float waist, float shoulder, float height, float depth)
        => Surface(name, Sweep.Up, 12, R(0, hem * .92f, depth, 0, -.025f), R(height * .055f, hem, depth * 1.06f, 0, -.024f),
            R(height * .29f, hem * .82f, depth * .88f), R(height * .48f, waist, depth * .73f),
            R(height * .68f, waist * 1.07f, depth * .87f), R(height * .84f, shoulder, depth),
            R(height * .925f, shoulder * .92f, depth * .84f), R(height, shoulder * .38f, depth * .56f));

    private static Mesh Sleeve() => Surface("Turned cuff sleeve", Sweep.Up, 10,
        R(-.5f, .48f, .49f, 0, .054f), R(-.435f, .51f, .50f, 0, .045f), R(-.22f, .44f, .43f),
        R(.095f, .394f, .407f, 0, -.06f), R(.317f, .469f, .47f, 0, -.047f), R(.43f, .385f, .41f), R(.5f, .206f, .28f));

    private static Mesh FaceForm() => Surface("Folded cheek and chin", Sweep.Up, 12,
        R(-.50f, .158f, .242f, 0, .022f), R(-.40f, .32f, .348f, 0, .039f), R(-.196f, .432f, .421f),
        R(.057f, .50f, .488f), R(.245f, .491f, .472f, 0, -.014f), R(.419f, .407f, .394f), R(.5f, .215f, .25f));

    private static void SculptScribe(Transform body)
    {
        SkinPart(body, "Indigo coat", Gown("Ink scholar coat", .278f, .177f, .269f, .727f, .156f),
            new Vector3(0, .591f, 0));
        SkinPart(body, "Waist sash", Surface("Bound scholar sash", Sweep.Up, 12, R(-.039f, .193f, .135f),
            R(-.024f, .197f, .139f), R(.033f, .186f, .134f), R(.042f, .18f, .126f)), new Vector3(0, .964f, 0));
        var face = FaceForm();
        var skin = Paint(SkinPart(body, "Face", face, new Vector3(0, 1.489f, .029f), new Vector3(.29f, .31f, .263f)));
        var hair = Paint(SkinPart(body, "Hair", Surface("Scholar swept hair", Sweep.Up, 12, R(-.099f, .123f, .088f, 0, -.053f),
            R(-.035f, .149f, .127f, 0, -.024f), R(.045f, .151f, .142f, 0, .013f), R(.102f, .139f, .14f, 0, .009f),
            R(.142f, .079f, .092f, 0, -.028f)), new Vector3(0, 1.535f, .012f)));
        SkinPart(body, "Hair knot", face, new Vector3(0, 1.685f, -.068f), new Vector3(.15f, .127f, .15f));
        var stroke = Surface("Calligraphic stroke", Sweep.Right, 6, R(-.50f, .09f, .10f),
            R(-.27f, .39f, .31f), R(.16f, .46f, .39f), R(.5f, .038f, .088f));
        SkinPart(body, "Swept forelock", stroke, new Vector3(.024f, 1.569f, .143f), new Vector3(.209f, .069f, .039f), hair)
            .localEulerAngles = new Vector3(0, 8, 17);
        for (int i = 0; i < 2; i++)
        {
            int s = i == 0 ? -1 : 1;
            string prefix = i == 0 ? "Left" : "Right";
            var arm = body.Find(prefix + " sleeve");
            SkinPart(arm, "Sleeve", Sleeve(), new Vector3(0, -.136f, .018f), new Vector3(.212f, .416f, .221f));
            SkinPart(arm, "Linen cuff", Sleeve(), new Vector3(0, -.329f, .038f), new Vector3(.191f, .075f, .195f));
            SkinPart(arm, "Hand", face, new Vector3(0, -.391f, .054f), new Vector3(.104f, .137f, .113f));
            var leg = body.Find(prefix + " leg");
            SkinPart(leg, "Trousers", Surface("Scholar trousers", Sweep.Up, 10, R(-.56f, .066f, .073f),
                R(-.407f, .077f, .078f), R(-.245f, .09f, .095f), R(-.072f, .105f, .102f), R(.042f, .093f, .087f)));
            SkinPart(leg, "Boot", Surface("Scholar cloth shoe", Sweep.Up, 10, R(-.713f, .081f, .129f, 0, .048f),
                R(-.681f, .091f, .149f, 0, .063f), R(-.624f, .085f, .137f, 0, .053f),
                R(-.55f, .073f, .084f), R(-.481f, .075f, .077f)));
            SkinPart(body, "Ink brow " + i, stroke, new Vector3(s * .059f, 1.519f, .145f), new Vector3(.079f, .021f, .017f), hair)
                .localEulerAngles = new Vector3(0, s * 25, s * 8);
            SkinPart(body, "Eye line " + i, stroke, new Vector3(s * .063f, 1.493f, .147f), new Vector3(.060f, .014f, .018f), hair)
                .localEulerAngles = new Vector3(0, s * 25, s * 7);
        }
        SkinPart(body, "Nose bridge", face, new Vector3(0, 1.475f, .152f), new Vector3(.040f, .077f, .052f), skin);
        var collar = body.Find("Linen collar");
        collar.localPosition = new Vector3(.047f, 1.174f, .139f);
        collar.localEulerAngles = new Vector3(-9, 10, -31);
        var grip = body.Find("Right sleeve/Brush grip");
        foreach (string part in new[] { "Bamboo brush", "Ink bristles" })
        {
            var item = grip.Find(part);
            var shape = part == "Bamboo brush" ? Surface("Bamboo ferrule", Sweep.Up, 8, R(-.50f, .39f, .39f),
                R(-.31f, .46f, .46f), R(.31f, .40f, .40f), R(.45f, .51f, .51f), R(.5f, .48f, .48f)) :
                Surface("Pointed brush hairs", Sweep.Up, 8, R(-.5f, .48f, .48f), R(-.29f, .55f, .55f),
                    R(.015f, .41f, .41f), R(.31f, .17f, .17f), R(.5f, .018f, .018f, .041f));
            item.GetComponent<MeshFilter>().sharedMesh = shape;
        }
    }

    private static void SculptClerk(Transform trunk)
    {
        SkinPart(trunk, "Torn Robe", Gown("Registrar full robe", .503f, .294f, .382f, 1.16f, .293f));
        SkinPart(trunk, "Red sash", Surface("Registrar sash", Sweep.Up, 12, R(-.04f, .303f, .235f),
            R(-.027f, .311f, .24f), R(.028f, .308f, .236f), R(.042f, .303f, .231f)), new Vector3(0, .585f, 0));
        SkinPart(trunk, "Blank paper face", FaceForm(), new Vector3(0, 1.263f, .103f), new Vector3(.36f, .425f, .34f));
        var stitch = Surface("Register collar piping", Sweep.Up, 8, R(0, .016f, .01f),
            R(.17f, .025f, .013f), R(.35f, .041f, .012f));
        var trim = Paint(trunk.Find("Blank paper face"));
        SkinPart(trunk, "Folded collar", stitch, new Vector3(.098f, .777f, .277f), material: trim)
            .localEulerAngles = new Vector3(-10, 0, 32);
        for (int i = 0; i < 2; i++)
        {
            int s = i == 0 ? -1 : 1;
            var arm = trunk.Find("Wide sleeve " + i);
            SkinPart(arm, "Sleeve", Sleeve(), new Vector3(s * .092f, -.208f, .069f), new Vector3(.40f, .64f, .395f))
                .localEulerAngles = new Vector3(-16, 0, s * 19);
            SkinPart(arm, "Paper hand", FaceForm(), new Vector3(s * .12f, -.46f, .23f), new Vector3(.163f, .20f, .15f));
        }
    }

    private static void SculptWatch(Transform trunk)
    {
        SkinPart(trunk, "Worn straw cape", Surface("Rain mantle", Sweep.Up, 14, R(0, .51f, .284f, 0, -.04f),
            R(.105f, .569f, .34f, 0, -.046f), R(.353f, .472f, .318f), R(.635f, .342f, .269f),
            R(.776f, .321f, .248f), R(.883f, .242f, .196f), R(.935f, .135f, .126f)));
        SkinPart(trunk, "Folded face", FaceForm(), new Vector3(0, .911f, .089f), new Vector3(.357f, .409f, .348f));
        var hat = Surface("Reed watch hat", Sweep.Up, 14, R(0, .409f, .365f), R(.029f, .423f, .381f),
            R(.074f, .338f, .305f, 0, -.009f), R(.213f, .106f, .105f, 0, -.032f), R(.227f, .059f, .069f, 0, -.037f));
        SkinPart(trunk, "Broad hat", hat, new Vector3(0, 1.084f, .016f));
        for (int i = 0; i < 7; i++)
        {
            float angle = (i - 3) * .256f;
            var straw = Surface("Rain cape reed pleat", Sweep.Up, 6, R(0, .007f, .007f, 0, .302f),
                R(.135f, .021f, .014f, 0, .311f), R(.354f, .024f, .014f, 0, .320f),
                R(.597f, .019f, .012f, 0, .277f), R(.764f, .005f, .008f, 0, .247f));
            var strip = SkinPart(trunk, "Rain-cut straw " + i, straw, new Vector3(0, .025f, 0), new Vector3(1, 1, 1 / Mathf.Cos(angle)));
            strip.localEulerAngles = new Vector3(0, angle * Mathf.Rad2Deg * 1.33f, 0);
        }
        var lantern = trunk.Find("Frosted watch lantern");
        SkinPart(lantern, "Paper shade", Surface("Folded lantern", Sweep.Up, 8, R(-.197f, .116f, .10f),
            R(-.139f, .149f, .122f), R(.082f, .161f, .129f), R(.145f, .143f, .119f), R(.198f, .115f, .099f)));
        for (int s = -1; s <= 1; s += 2)
            SkinPart(lantern, "Folded sleeve " + s, Sleeve(), new Vector3(s * .24f, -.28f, -.10f), new Vector3(.182f, .35f, .21f))
                .localEulerAngles = new Vector3(35, 0, s * 27);
    }

    private static void SculptGate(Transform body)
    {
        SkinPart(body, "Torso", Surface("Gate guardian breast", Sweep.Up, 8, R(.22f, .413f, .294f),
            R(.41f, .535f, .377f), R(.68f, .658f, .447f), R(.964f, .887f, .499f, 0, -.029f),
            R(1.13f, .79f, .452f, 0, -.064f), R(1.24f, .462f, .311f, 0, -.072f)));
        SkinPart(body, "Helmet", Surface("Gate guardian helm", Sweep.Up, 8, R(1.103f, .237f, .241f, 0, .058f),
            R(1.255f, .318f, .293f, 0, .112f), R(1.397f, .375f, .349f, 0, .09f), R(1.53f, .294f, .274f),
            R(1.625f, .166f, .143f, 0, -.093f)));
        var gold = Paint(body.Find("Seal"));
        var visor = Surface("Gate visor inlay", Sweep.Right, 6, R(-.263f, .007f, .009f), R(-.131f, .025f, .027f, .047f),
            R(0, .028f, .019f, .069f, -.012f), R(.131f, .025f, .027f, .047f), R(.263f, .007f, .009f));
        SkinPart(body, "Amber visor", visor, new Vector3(0, 1.357f, .369f), material: gold);
        var arms = body.Find("Arms");
        for (int s = -1; s <= 1; s += 2)
        {
            var axis = s < 0 ? Sweep.Left : Sweep.Right;
            SkinPart(body, "Shoulder" + s, Surface("Gate pauldrons " + s, axis, 8, R(.42f, .294f, .246f, -.053f, 1.00f),
                R(.691f, .425f, .31f, -.051f, 1.028f), R(.886f, .383f, .283f, -.014f, 1.04f), R(1.015f, .249f, .193f, 0, .982f)));
            SkinPart(arms, "Boulder" + s, Surface("Gate hammer arm " + s, Sweep.Up, 8, R(-.63f, .282f, .316f, s * 1.069f, .194f),
                R(-.523f, .379f, .39f, s * 1.09f, .194f), R(-.29f, .375f, .393f, s * 1.063f, .182f),
                R(-.105f, .259f, .277f, s * .988f, .104f), R(.081f, .26f, .258f, s * .844f, .022f),
                R(.152f, .189f, .212f, s * .771f, .005f)));
            arms.Find("Mark" + s).localPosition = new Vector3(s * 1.069f, -.31f, .557f);
            SkinPart(body, "Foot" + s, Surface("Gate stone tread", Sweep.Up, 8, R(.009f, .312f, .45f, 0, .104f),
                R(.087f, .379f, .549f, 0, .119f), R(.226f, .351f, .492f, 0, .074f), R(.379f, .241f, .309f),
                R(.553f, .267f, .276f)), new Vector3(s * .49f, 0, 0));
        }
    }

    private static void SculptWraith(Transform body)
    {
        var seal = body.Find("Seal");
        var veil = Paint(seal.Find("Tablet0"));
        var rose = Paint(body.Find("Heart"));
        SkinPart(body, "Heart", Surface("Wraith ember", Sweep.Up, 9, R(.067f, .015f, .012f, .088f, -.05f),
            R(.247f, .079f, .07f, .064f), R(.439f, .138f, .119f), R(.641f, .176f, .155f, -.021f),
            R(.855f, .209f, .158f), R(1.025f, .12f, .097f, 0, .027f)));
        SkinPart(body, "Cowl", Surface("Wraith cowl", Sweep.Up, 10, R(.494f, .098f, .108f, 0, -.051f),
            R(.683f, .154f, .148f, 0, -.073f), R(.881f, .253f, .190f, 0, -.038f), R(1.023f, .245f, .196f),
            R(1.166f, .202f, .175f, 0, .017f), R(1.285f, .134f, .139f, 0, -.027f), R(1.365f, .022f, .027f, .023f, -.113f)), material: veil);
        SkinPart(body, "Spirit mask", FaceForm(), new Vector3(0, 1.107f, .155f), new Vector3(.217f, .256f, .116f), rose);
        for (int s = -1; s <= 1; s += 2)
            SkinPart(body, "Hollow eye " + s, FaceForm(), new Vector3(s * .045f, 1.128f, .203f),
                new Vector3(.059f, .024f, .018f), veil).localEulerAngles = new Vector3(0, s * 19, s * 17);
        var tablet = Surface("Orbiting seal leaf", Sweep.Up, 6, R(-.413f, .013f, .015f, -.048f),
            R(-.259f, .066f, .031f, -.023f), R(.057f, .095f, .039f), R(.266f, .071f, .036f, .024f),
            R(.357f, .035f, .025f, .063f), R(.406f, .009f, .008f, .079f));
        for (int i = 0; i < 5; i++)
        {
            var leaf = seal.Find("Tablet" + i);
            leaf.GetComponent<MeshFilter>().sharedMesh = tablet;
            leaf.localScale = Vector3.one;
        }
    }

    private static void SculptCrab(Transform body)
    {
        var shell = body.Find("Barnacle shell");
        SkinPart(shell, "Upper Shell", Surface("Salt crab shield", Sweep.Up, 14, R(-.166f, .543f, .431f),
            R(-.074f, .653f, .524f), R(.035f, .683f, .56f), R(.167f, .599f, .499f, 0, -.021f),
            R(.272f, .422f, .37f, 0, -.047f), R(.343f, .171f, .178f, 0, -.061f)));
        SkinPart(shell, "Shell Rim", Surface("Crab shell lip", Sweep.Up, 14, R(-.203f, .49f, .417f),
            R(-.155f, .61f, .51f), R(-.085f, .649f, .539f), R(-.041f, .628f, .509f)));
        var barnacle = Surface("Salt barnacle", Sweep.Up, 7, R(-.5f, .59f, .53f), R(-.24f, .65f, .63f),
            R(.21f, .45f, .398f, .032f), R(.41f, .32f, .30f, .052f), R(.445f, .16f, .144f, .052f),
            R(.22f, .095f, .085f, .052f));
        for (int i = 0; i < 10; i++)
        {
            var growth = shell.Find("White Barnacle " + i);
            growth.GetComponent<MeshFilter>().sharedMesh = barnacle;
            var p = growth.localPosition;
            float r = Mathf.Sqrt(p.x * p.x / (.683f * .683f) + p.z * p.z / (.56f * .56f));
            p.y = .366f - .27f * r * r;
            growth.localPosition = p;
        }
        for (int i = 0; i < 2; i++)
        {
            int s = i == 0 ? -1 : 1;
            var claw = body.Find("Claw " + i);
            SkinPart(claw, "Pincer", Surface("Salt claw palm " + s, Sweep.Forward, 10, R(-.085f, .097f, .119f),
                R(.10f, .147f, .161f, s * .105f), R(.252f, .177f, .194f, s * .209f), R(.389f, .134f, .144f, s * .232f),
                R(.469f, .092f, .101f, s * .209f)));
            SkinPart(claw, "Fixed jaw", Surface("Salt claw upper hook " + s, Sweep.Forward, 8, R(.285f, .090f, .111f, s * .251f, .055f),
                R(.448f, .091f, .097f, s * .287f, .104f), R(.574f, .061f, .068f, s * .262f, .13f), R(.67f, .012f, .015f, s * .214f, .071f)));
            SkinPart(claw, "Hook jaw", Surface("Salt claw lower hook " + s, Sweep.Forward, 8, R(.298f, .075f, .082f, s * .187f, -.042f),
                R(.461f, .071f, .070f, s * .159f, -.091f), R(.575f, .038f, .051f, s * .164f, -.069f), R(.647f, .009f, .014f, s * .184f, -.012f)));
            var axis = i == 0 ? Sweep.Left : Sweep.Right;
            var thigh = Surface("Salt crab leg " + s, axis, 8, R(-.074f, .068f, .078f), R(.11f, .078f, .088f, 0, -.018f),
                R(.271f, .065f, .059f, -.017f, -.069f), R(.349f, .047f, .05f, -.027f, -.103f), R(.428f, .034f, .037f, -.024f, -.238f));
            var toe = Surface("Salt crab tapered toe " + s, axis, 7, R(.381f, .04f, .053f, -.022f, -.153f),
                R(.428f, .04f, .055f, -.023f, -.239f), R(.458f, .027f, .046f, -.028f, -.311f), R(.491f, .009f, .01f, -.044f, -.419f));
            for (int leg = 0; leg < 3; leg++)
            {
                var joint = body.Find("Jointed leg " + (i * 3 + leg));
                SkinPart(joint, "Elbow", thigh);
                SkinPart(joint, "Ground tip", toe);
            }
        }
    }
}
