using Entity;
using UnityEditor;
using UnityEngine;

public static partial class TimelineMonsterAuthoring
{
    [MenuItem("Tools/World Timeline/Build Qi Mo")]
    public static void BuildQiMo()
    {
        var block = Prism("QiMoBlock", new[]
        {
            new Vector2(-.5f, -.5f), new Vector2(-.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(.5f, -.5f)
        }, 1f, 1f);
        BuildOne("QiMo", block, 1f, new Color32(43, 62, 83, 255),
            new Color32(202, 182, 149, 255), BuildQiMoParts);
        AssetDatabase.SaveAssets();
    }

    private static void BuildQiMoParts(Transform body, Mesh block, Material indigo, Material linen,
        TimelineMonsterAnimation clips)
    {
        var ink = Tint("QiMoInk", new Color32(36, 33, 32, 255));
        var skin = Tint("QiMoSkin", new Color32(195, 145, 108, 255));
        var bamboo = Tint("QiMoBamboo", new Color32(151, 101, 52, 255));
        var face = Prism("QiMoFace", new[]
        {
            new Vector2(-.075f, -.14f), new Vector2(-.145f, -.045f), new Vector2(-.145f, .09f),
            new Vector2(-.08f, .15f), new Vector2(.09f, .15f), new Vector2(.145f, .08f),
            new Vector2(.145f, -.045f), new Vector2(.075f, -.14f)
        }, .23f, .23f);
        var coat = Prism("QiMoCoat", new[]
        {
            new Vector2(-.245f, -.28f), new Vector2(-.17f, .03f), new Vector2(-.255f, .25f),
            new Vector2(-.135f, .32f), new Vector2(.135f, .32f), new Vector2(.255f, .25f),
            new Vector2(.17f, .03f), new Vector2(.245f, -.28f)
        }, .29f, .26f);

        Part(body, "Indigo coat", coat, indigo, new Vector3(0, .98f, 0), Vector3.one);
        Part(body, "Waist sash", block, ink, new Vector3(0, .98f, .012f), new Vector3(.356f, .082f, .312f));
        Part(body, "Linen collar", block, linen, new Vector3(.052f, 1.179f, .155f),
            new Vector3(.078f, .288f, .06f)).localEulerAngles = new Vector3(0, 0, -26);
        Part(body, "Folded contract", block, linen, new Vector3(-.118f, 1.102f, .167f),
            new Vector3(.090f, .16f, .04f)).localEulerAngles = new Vector3(0, 0, 12);
        Part(body, "Neck", block, skin, new Vector3(0, 1.327f, .015f), new Vector3(.13f, .11f, .13f));
        Part(body, "Face", face, skin, new Vector3(0, 1.489f, .029f), Vector3.one);
        Part(body, "Hair", face, ink, new Vector3(0, 1.536f, -.034f), new Vector3(1.06f, .95f, .88f));
        Part(body, "Hair knot", face, ink, new Vector3(0, 1.68f, -.070f), new Vector3(.61f, .58f, .58f));
        Part(body, "Wooden hairpin", block, bamboo, new Vector3(0, 1.70f, -.052f),
            new Vector3(.32f, .026f, .031f)).localEulerAngles = new Vector3(0, 0, 13);

        Transform right = Limb(body, "Right sleeve", block, indigo, linen, skin, .255f);
        Transform left = Limb(body, "Left sleeve", block, indigo, linen, skin, -.255f);
        var grip = Pivot(right, "Brush grip", new Vector3(0, -.397f, .065f));
        BrushPart(grip, "Bamboo brush", block, bamboo, new Vector3(0, .17f, -.06f), new Vector3(0, -.37f, .14f), .039f);
        BrushPart(grip, "Ink bristles", block, ink, new Vector3(0, -.35f, .13f), new Vector3(0, -.485f, .203f), .056f);
        Transform rightLeg = Leg(body, "Right leg", block, indigo, ink, .111f);
        Transform leftLeg = Leg(body, "Left leg", block, indigo, ink, -.111f);

        clips.Idle = Clip(clips, "Idle", true, c =>
        {
            Offset(c, body, "y", 0, .012f, 0);
            Rotate(c, right, "x", -12, -16, -12);
        });
        clips.Move = Clip(clips, "Move", true, c =>
        {
            Offset(c, body, "y", .04f, .04f, .04f);
            Rotate(c, rightLeg, "x", -23, 23, -23);
            Rotate(c, leftLeg, "x", 23, -23, 23);
            Rotate(c, right, "x", -5, -25, -5);
            Rotate(c, left, "x", 17, -16, 17);
        });
        clips.Windup = Clip(clips, "Windup", false, c =>
        {
            Curve(c, right, "localEulerAnglesRaw.x", new Keyframe(0, -12), new Keyframe(.71f, 28), new Keyframe(1, -76));
            Curve(c, right, "localEulerAnglesRaw.y", new Keyframe(0, 0), new Keyframe(.71f, 16), new Keyframe(1, -14));
            Rotate(c, body, "y", 0, -12, 8);
        });
        clips.Strike = Clip(clips, "Strike", false, c =>
        {
            Rotate(c, right, "x", -76, -32, -12);
            Rotate(c, right, "y", -14, -22, 0);
            Rotate(c, body, "y", 8, 3, 0);
        });
    }

    private static Transform Limb(Transform body, string name, Mesh block, Material indigo,
        Material linen, Material skin, float x)
    {
        var sleeve = Pivot(body, name, new Vector3(x, 1.239f, .002f));
        Part(sleeve, "Sleeve", block, indigo, new Vector3(0, -.162f, .026f), new Vector3(.158f, .359f, .173f));
        Part(sleeve, "Linen cuff", block, linen, new Vector3(0, -.327f, .047f), new Vector3(.128f, .095f, .126f));
        Part(sleeve, "Hand", block, skin, new Vector3(0, -.396f, .054f), new Vector3(.089f, .101f, .09f));
        return sleeve;
    }

    private static Transform Leg(Transform body, string name, Mesh block, Material indigo, Material ink, float x)
    {
        var leg = Pivot(body, name, new Vector3(x, .729f, 0));
        Part(leg, "Trousers", block, indigo, new Vector3(0, -.30f, 0), new Vector3(.174f, .53f, .162f));
        Part(leg, "Boot", block, ink, new Vector3(0, -.62f, .065f), new Vector3(.172f, .207f, .246f));
        return leg;
    }

    private static void BrushPart(Transform grip, string name, Mesh block, Material material,
        Vector3 from, Vector3 to, float width)
    {
        var shaft = to - from;
        Part(grip, name, block, material, (from + to) * .5f, new Vector3(width, shaft.magnitude, width))
            .localRotation = Quaternion.FromToRotation(Vector3.up, shaft);
    }
}
