using Entity;
using UnityEditor;
using UnityEngine;

public static partial class TimelineMonsterAuthoring
{
    [MenuItem("Tools/World Timeline/Build Cave Guardians")]
    public static void BuildCaveGuardians()
    {
        var slab = Prism("CaveGuardianStone", new[] { new Vector2(-.47f, -.31f), new Vector2(-.32f, -.48f),
            new Vector2(.30f, -.47f), new Vector2(.49f, -.16f), new Vector2(.42f, .39f),
            new Vector2(-.29f, .47f) }, .79f, 1f);
        BuildOne("FrostfangMarten", Oval("RegionalGuardianOval"), 1.06f,
            new Color32(151, 189, 200, 255), new Color32(32, 69, 87, 255), SnowMarten);
        BuildOne("ColdcoreGolem", slab, 1.12f, new Color32(62, 105, 127, 255),
            new Color32(147, 222, 229, 255), Coldcore);
        BuildOne("LavascaleLizard", slab, 1.03f, new Color32(54, 39, 37, 255),
            new Color32(116, 62, 45, 255), Lavascale);
        AssetDatabase.SaveAssets();
    }

    private static void Coldcore(Transform body, Mesh stone, Material slate, Material ice, TimelineMonsterAnimation rig)
    {
        var glow = Glow("ColdcoreGolemCore", new Color32(93, 198, 230, 255), .65f);
        var torso = Pivot(body, "Torso", new Vector3(0, .95f, 0));
        Part(torso, "Boulder", stone, slate, new Vector3(0, .32f, 0), new Vector3(.90f, .84f, .64f));
        Part(torso, "Core", stone, glow, new Vector3(0, .36f, .32f), new Vector3(.33f, .45f, .10f))
            .localEulerAngles = new Vector3(0, 0, 28);
        Part(torso, "Brow", stone, ice, new Vector3(0, .82f, .03f), new Vector3(.43f, .34f, .42f));
        var arms = new Transform[2];
        var legs = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i * 2 - 1;
            arms[i] = Pivot(torso, "Shoulder " + i, new Vector3(side * .61f, .53f, 0));
            Part(arms[i], "Ice cap", stone, ice, new Vector3(side * .04f, .03f, 0), new Vector3(.42f, .29f, .47f))
                .localEulerAngles = new Vector3(14, 0, side * 26);
            Part(arms[i], "Arm", stone, slate, new Vector3(side * .13f, -.33f, .03f), new Vector3(.35f, .61f, .34f));
            Part(arms[i], "Glacier fist", stone, ice, new Vector3(side * .18f, -.66f, .12f), new Vector3(.45f, .36f, .48f));
            legs[i] = Pivot(body, "Leg " + i, new Vector3(side * .26f, .87f, 0));
            Part(legs[i], "Pillar", stone, slate, new Vector3(0, -.36f, 0), new Vector3(.38f, .70f, .40f));
            Part(legs[i], "Foot", stone, ice, new Vector3(0, -.75f, .14f), new Vector3(.42f, .20f, .53f));
        }
        rig.Idle = Clip(rig, "Idle", true, c => Rotate(c, torso, "z", -2, 2, -2));
        rig.Move = Clip(rig, "Move", true, c =>
        {
            Rotate(c, legs[0], "x", -19, 19, -19);
            Rotate(c, legs[1], "x", 19, -19, 19);
        });
        rig.Windup = Clip(rig, "Windup", false, c =>
        {
            foreach (var arm in arms) Rotate(c, arm, "x", 0, -85, -153);
            Rotate(c, torso, "x", 0, -6, -15);
        });
        rig.Strike = Clip(rig, "Strike", false, c =>
        {
            foreach (var arm in arms) Rotate(c, arm, "x", -67, -38, 0);
            Rotate(c, torso, "x", 31, 16, 0);
            Offset(c, torso, "y", -.21f, -.08f, 0);
        });
    }

    private static void Lavascale(Transform body, Mesh stone, Material basalt, Material scoria, TimelineMonsterAnimation rig)
    {
        var ember = Glow("LavascaleLizardEmber", new Color32(246, 115, 49, 255), .95f);
        var trunk = Pivot(body, "Low trunk", new Vector3(0, .38f, -.05f));
        Part(trunk, "Body", stone, basalt, Vector3.zero, new Vector3(.59f, .36f, .99f));
        var tail = Pivot(trunk, "Tail", new Vector3(0, -.02f, -.44f));
        for (int i = 0; i < 3; i++)
        {
            Part(tail, "Tail segment " + i, stone, i == 2 ? ember : scoria,
                new Vector3(i * .07f, -.03f + i * .027f, -.20f - i * .28f),
                new Vector3(.25f - i * .055f, .16f - i * .029f, .39f));
            Part(trunk, "Back ridge " + i, stone, i == 1 ? ember : scoria,
                new Vector3(0, .21f, -.28f + i * .27f), new Vector3(.17f, .24f, .25f));
        }
        var head = Pivot(trunk, "Head", new Vector3(0, -.025f, .53f));
        Part(head, "Snout", stone, basalt, new Vector3(0, .04f, .19f), new Vector3(.35f, .20f, .46f));
        var jaw = Pivot(head, "Jaw", new Vector3(0, -.08f, .015f));
        Part(jaw, "Lower jaw", stone, scoria, new Vector3(0, -.01f, .19f), new Vector3(.32f, .10f, .42f));
        var legs = new Transform[4];
        for (int i = 0; i < 2; i++)
        {
            float side = i * 2 - 1;
            Part(head, "Eye " + i, stone, ember, new Vector3(side * .17f, .10f, .16f), new Vector3(.057f, .06f, .065f));
            for (int leg = 0; leg < 2; leg++)
            {
                var foot = Pivot(trunk, "Foot " + (i * 2 + leg), new Vector3(side * .26f, -.06f, .28f - leg * .63f));
                legs[i * 2 + leg] = foot;
                Part(foot, "Splayed foot", stone, basalt, new Vector3(side * .17f, -.21f, .10f), new Vector3(.32f, .18f, .28f));
            }
        }
        rig.Idle = Clip(rig, "Idle", true, c => Rotate(c, tail, "y", -9, 9, -9));
        rig.Move = Clip(rig, "Move", true, c =>
        {
            for (int i = 0; i < legs.Length; i++)
            {
                float angle = i % 3 == 0 ? -28f : 28f;
                Rotate(c, legs[i], "y", angle, -angle, angle);
            }
            Rotate(c, tail, "y", -22, 22, -22);
        });
        rig.Windup = Clip(rig, "Windup", false, c =>
        {
            Rotate(c, jaw, "x", 0, 10, 24);
            Rotate(c, head, "x", 0, -12, -22);
        });
        rig.Strike = Clip(rig, "Strike", false, c =>
        {
            Rotate(c, jaw, "x", 32, 16, 0);
            Offset(c, head, "z", .21f, .13f, 0);
        });
    }
}
