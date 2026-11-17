using System;
using System.Collections.Generic;
using Entity;
using UnityEditor;
using UnityEngine;

public static partial class TimelineMonsterAuthoring
{
    [MenuItem("Tools/World Timeline/Build Regional Guardians")]
    public static void BuildRegionalGuardians()
    {
        var rock = Prism("RegionalGuardianChippedStone", new[]
        {
            new Vector2(-.49f,-.38f), new Vector2(-.37f,-.48f), new Vector2(.35f,-.47f),
            new Vector2(.49f,-.33f), new Vector2(.46f,.34f), new Vector2(.31f,.49f),
            new Vector2(-.34f,.46f), new Vector2(-.50f,.28f)
        }, .91f, 1.03f);
        BuildOne("BlackfangBadger", rock, 1f, new Color32(58, 56, 59, 255),
            new Color32(200, 182, 143, 255), Blackfang);
        BuildOne("AshKilnMacaque", rock, 1.06f, new Color32(53, 49, 51, 255),
            new Color32(174, 108, 58, 255), AshKiln);
        BuildOne("ContractGolem", rock, 1.05f, new Color32(89, 83, 73, 255),
            new Color32(146, 52, 43, 255), Contract);
        AssetDatabase.SaveAssets();
    }

    private static void Blackfang(Transform body, Mesh rock, Material hide, Material ivory, TimelineMonsterAnimation clips)
    {
        var mantle = Tint("BlackfangBadgerMantle", new Color32(119, 110, 95, 255));
        var mask = Tint("BlackfangBadgerMask", new Color32(45, 38, 42, 255));
        var eyes = Glow("BlackfangBadgerEye", new Color32(224, 141, 64, 255), .5f);
        var fur = Oval("RegionalGuardianOval");
        var back = Pivot(body, "Shoulders", new Vector3(0,.63f,-.17f));
        Part(back,"Hunched Body",fur,hide,Vector3.zero,new Vector3(.52f,.46f,.83f));
        Part(back,"Pale Saddle",fur,mantle,new Vector3(0,.24f,-.06f),new Vector3(.45f,.25f,.76f));
        Part(back,"Rump",fur,hide,new Vector3(0,-.08f,-.56f),new Vector3(.43f,.36f,.44f));
        Part(back,"Brush Tail",fur,mask,new Vector3(0,-.04f,-.97f),new Vector3(.21f,.16f,.35f));
        for (int i = 0; i < 5; i++)
            Part(back,"Rock Scab " + i,rock, i % 2 == 0 ? mantle : hide,
                new Vector3(Mathf.Sin(i * 2.2f) * .27f,.39f,-.52f + i * .205f),
                new Vector3(.21f,.12f + .026f * (i % 3),.25f)).localEulerAngles = new Vector3(18f,i * 37f,21f);

        var head = Pivot(body,"Head",new Vector3(0,.59f,.53f));
        Part(head,"Skull",fur,mantle,new Vector3(0,.02f,.055f),new Vector3(.37f,.29f,.36f));
        Part(head,"Snout",fur,hide,new Vector3(0,-.105f,.31f),new Vector3(.25f,.16f,.34f));
        Part(head,"Nose",fur,mask,new Vector3(0,-.068f,.57f),new Vector3(.145f,.103f,.115f));
        var jaw = Pivot(head,"Jaw",new Vector3(0,-.12f,.16f));
        Part(jaw,"Lower Jaw",fur,mask,new Vector3(0,-.09f,.22f),new Vector3(.23f,.09f,.30f));
        var legs = new Transform[4];
        for (int s = 0; s < 2; s++)
        {
            float sign = s == 0 ? -1f : 1f;
            Part(head,"Mask " + s,fur,mask,new Vector3(sign * .224f,.052f,.244f),
                new Vector3(.127f,.138f,.207f)).localEulerAngles = new Vector3(10,sign * -20,0);
            Part(head,"Ear " + s,fur,hide,new Vector3(sign * .28f,.22f,-.06f),new Vector3(.13f,.15f,.10f));
            Part(head,"Eye " + s,fur,eyes,new Vector3(sign * .251f,.083f,.337f),new Vector3(.047f,.037f,.035f));
            Part(jaw,"Broken Fang " + s,rock,ivory,new Vector3(sign * .167f,.017f,.37f),
                new Vector3(.082f,s == 0 ? .20f : .13f,.085f)).localEulerAngles = new Vector3(-18,0,sign * -10);
            for (int i = 0; i < 2; i++)
            {
                int index = s * 2 + i;
                legs[index] = Pivot(body,"Leg " + index,new Vector3(sign * .36f,.47f,i == 0 ? .43f : -.61f));
                Part(legs[index],"Haunch",fur,hide,new Vector3(0,-.14f,-.028f),new Vector3(.19f,.25f,.18f));
                Part(legs[index],"Paw",fur,mask,new Vector3(0,-.35f,.075f),new Vector3(.19f,.115f,.23f));
                for (int toe = -1; toe <= 1; toe++)
                    Part(legs[index],"Claw " + toe,rock,ivory,new Vector3(toe * .082f,-.345f,.247f),new Vector3(.044f,.056f,.10f));
            }
        }
        clips.Idle = Clip(clips,"Idle",true,c => { Offset(c,back,"y",0,.034f,0); Rotate(c,head,"x",3,-3,3); });
        clips.Move = Clip(clips,"Move",true,c =>
        {
            for (int i = 0; i < legs.Length; i++)
            {
                float pitch = i is 0 or 3 ? 25f : -25f;
                Rotate(c,legs[i],"x",pitch,-pitch,pitch);
            }
            Offset(c,back,"y",0,.06f,0);
        });
        clips.Windup = Clip(clips,"Windup",false,c =>
        {
            Offset(c,body,"y",0,-.14f,-.22f); Offset(c,body,"z",0,-.14f,-.30f);
            Rotate(c,head,"x",0,-9,-20); Rotate(c,jaw,"x",0,15,28);
            foreach (var leg in legs) Rotate(c,leg,"x",0,-18,-28);
        });
        clips.Strike = Clip(clips,"Strike",false,c =>
        {
            Offset(c,body,"z",.50f,.26f,0); Offset(c,body,"y",.13f,.05f,0);
            Rotate(c,head,"x",12,5,0); Rotate(c,jaw,"x",24,3,0);
            foreach (var leg in legs) Rotate(c,leg,"x",25,8,0);
        });
    }

    private static void AshKiln(Transform body, Mesh rock, Material ash, Material copper, TimelineMonsterAnimation clips)
    {
        var dark = Tint("AshKilnMacaqueCharcoal", new Color32(34,29,32,255));
        var hair = Tint("AshKilnMacaqueHair", new Color32(113,96,82,255));
        var ember = Glow("AshKilnMacaqueEmber", new Color32(221,93,37,255),1.1f);
        var fur = Oval("RegionalGuardianOval");
        var torso = Pivot(body,"Bent Spine",new Vector3(0,.81f,-.07f));
        Part(torso,"Lean Chest",fur,ash,new Vector3(0,.22f,.03f),new Vector3(.34f,.52f,.32f))
            .localEulerAngles = new Vector3(25,0,0);
        Part(torso,"Humped Shoulder",fur,hair,new Vector3(0,.48f,-.10f),new Vector3(.42f,.28f,.32f));
        var head = Pivot(torso,"Mask",new Vector3(0,.43f,.43f));
        Part(head,"Macaque Face",fur,dark,Vector3.zero,new Vector3(.24f,.25f,.24f));
        Part(head,"Grey Brow",fur,hair,new Vector3(0,.14f,.09f),new Vector3(.21f,.10f,.18f));
        Part(head,"Jowls",fur,ash,new Vector3(0,-.09f,.12f),new Vector3(.18f,.14f,.21f));

        var furnace = Prism("AshKilnMacaqueFirebox", new[]
        {
            new Vector2(-.36f, -.50f), new Vector2(-.49f, -.32f),
            new Vector2(-.46f, .30f), new Vector2(-.30f, .49f),
            new Vector2(.31f, .49f), new Vector2(.46f, .30f),
            new Vector2(.49f, -.32f), new Vector2(.36f, -.50f)
        }, .92f, 1.04f);
        var kiln = Pivot(torso,"Kiln",new Vector3(0,.57f,-.38f));
        Part(kiln,"Firebox",furnace,dark,new Vector3(0,.30f,0),new Vector3(.81f,.77f,.75f));
        Part(kiln,"Fire in Mouth",fur,ember,new Vector3(0,.23f,-.406f),new Vector3(.25f,.23f,.046f));
        Part(kiln,"Coal Silhouette",furnace,dark,new Vector3(0,.12f,-.455f),new Vector3(.39f,.14f,.08f));
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 7;
            Part(kiln,"Arch Brick " + i,furnace,i % 3 == 0 ? ash : dark,
                new Vector3(Mathf.Cos(angle) * .32f,.21f + Mathf.Sin(angle) * .34f,-.43f),
                new Vector3(.15f,.19f,.16f)).localEulerAngles = new Vector3(0,0,angle * Mathf.Rad2Deg - 90f);
        }
        Part(kiln,"Flue",furnace,ash,new Vector3(-.15f,.71f,-.03f),new Vector3(.32f,.24f,.33f)).localEulerAngles = new Vector3(0,7,-12);
        var plates = Pivot(torso,"Nameplates",new Vector3(0,.23f,.32f));
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * .13f, y = -.03f - .13f * (i % 2);
            Part(plates,"Copper Nameplate " + i,furnace,copper,new Vector3(x,y,.01f),new Vector3(.11f,.26f,.038f))
                .localEulerAngles = new Vector3(0,0,(i - 2) * 9);
            Part(plates,"Worn Stroke " + i,furnace,dark,new Vector3(x,y-.019f,.032f),new Vector3(.057f,.014f,.012f));
        }
        var arms = new Transform[2];
        var legs = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float sign = i == 0 ? -1f : 1f;
            Part(head,"Ear " + i,fur,hair,new Vector3(sign * .24f,.03f,0),new Vector3(.105f,.14f,.08f));
            Part(head,"Eye " + i,fur,ember,new Vector3(sign * .081f,.05f,.23f),new Vector3(.036f,.025f,.021f));
            arms[i] = Pivot(torso,"Long Arm " + i,new Vector3(sign * .33f,.34f,.07f));
            Part(arms[i],"Forelimb",fur,ash,new Vector3(sign * .09f,-.33f,.04f),new Vector3(.15f,.44f,.16f))
                .localEulerAngles = new Vector3(-12,0,sign * 12);
            Part(arms[i],"Knuckles",fur,hair,new Vector3(sign * .12f,-.68f,.14f),new Vector3(.15f,.16f,.17f));
            legs[i] = Pivot(body,"Bent Leg " + i,new Vector3(sign * .22f,.72f,-.20f));
            Part(legs[i],"Haunch",fur,ash,new Vector3(0,-.23f,.055f),new Vector3(.18f,.32f,.19f));
            Part(legs[i],"Heel",fur,dark,new Vector3(0,-.58f,.08f),new Vector3(.17f,.13f,.24f));
        }
        clips.Idle = Clip(clips,"Idle",true,c => { Rotate(c,torso,"x",7,2,7); Rotate(c,plates,"z",-4,4,-4); });
        clips.Move = Clip(clips,"Move",true,c =>
        {
            Rotate(c,legs[0],"x",-22,22,-22); Rotate(c,legs[1],"x",22,-22,22);
            Rotate(c,arms[0],"x",17,-17,17); Rotate(c,arms[1],"x",-17,17,-17);
            Offset(c,torso,"y",0,.05f,0);
        });
        clips.Windup = Clip(clips,"Windup",false,c =>
        {
            Rotate(c,torso,"x",0,-17,-27); Rotate(c,kiln,"x",0,-8,-15); Rotate(c,plates,"z",0,12,-14);
            foreach (var arm in arms) Rotate(c,arm,"x",0,-42,-77);
        });
        clips.Strike = Clip(clips,"Strike",false,c =>
        {
            Rotate(c,torso,"x",29,14,0); Rotate(c,kiln,"x",15,8,0);
            foreach (var arm in arms) Rotate(c,arm,"x",25,8,0);
        });
        clips.LineWindup = Clip(clips, "LineWindup", false, c =>
        {
            Rotate(c, torso, "x", 0, 18, 27);
            Rotate(c, torso, "y", 0, -24, -35);
            Rotate(c, kiln, "x", 0, -26, -42);
            Rotate(c, arms[0], "x", 0, 22, 36);
            Rotate(c, arms[1], "x", 0, -38, -88);
            Rotate(c, arms[1], "z", 0, 27, 42);
            Rotate(c, head, "y", 0, 14, 21);
        });
        clips.LineRecovery = Clip(clips, "LineRecovery", false, c =>
        {
            Rotate(c, torso, "x", -15, 8, 0);
            Rotate(c, torso, "y", 36, -9, 0);
            Rotate(c, kiln, "x", 32, -11, 0);
            Rotate(c, arms[0], "x", 33, 12, 0);
            Rotate(c, arms[1], "x", -132, -46, 0);
            Rotate(c, arms[1], "z", 18, 12, 0);
        });
    }

    private static void Contract(Transform body, Mesh stone, Material slate, Material cinnabar, TimelineMonsterAnimation clips)
    {
        var edge = Tint("ContractGolemEdge",new Color32(128,117,96,255));
        var ink = Tint("ContractGolemEngraving",new Color32(42,41,37,255));
        var dark = Tint("ContractGolemJoint",new Color32(58,55,49,255));
        var red = Glow("ContractGolemRedInk",new Color32(176,66,42,255),.4f);
        var torso = Pivot(body,"Contract Slabs",new Vector3(0,.80f,0));
        Part(torso,"Waist Slab",stone,dark,new Vector3(-.10f,.01f,-.06f),new Vector3(.71f,.40f,.49f)).localEulerAngles = new Vector3(0,-13,-9);
        Part(torso,"Main Tablet",stone,slate,new Vector3(.015f,.39f,0),new Vector3(.86f,.65f,.54f));
        Part(torso,"Shoulder Tablet",stone,edge,new Vector3(-.09f,.72f,-.05f),new Vector3(.79f,.21f,.60f)).localEulerAngles = new Vector3(8,6,-10);
        Part(torso,"Blank Head",stone,slate,new Vector3(-.11f,.91f,.035f),new Vector3(.45f,.41f,.40f)).localEulerAngles = new Vector3(-8,0,14);
        Part(torso,"Blind Red Slit",stone,cinnabar,new Vector3(-.10f,.95f,.249f),new Vector3(.23f,.042f,.02f));
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 4; column++)
            {
                float x = -.24f + column * .142f, y = .56f - row * .093f;
                Part(torso,"Incised Mark " + row + column,stone,ink,new Vector3(x,y,.285f),new Vector3(.074f,.025f,.014f))
                    .localEulerAngles = new Vector3(0,0,(row + column) % 2 == 0 ? -22 : 30);
                Part(torso,"Incised Stem " + row + column,stone,ink,new Vector3(x-.016f,y-.020f,.29f),new Vector3(.015f,.06f,.011f));
            }
        Part(torso,"Unwritten Bottom Line",stone,edge,new Vector3(0,.238f,.283f),new Vector3(.57f,.060f,.026f));
        var stampArm = Pivot(torso,"Stamp Arm",new Vector3(.50f,.62f,.015f));
        Part(stampArm,"Stamp Arm Slate",stone,slate,new Vector3(.05f,-.25f,.03f),new Vector3(.34f,.57f,.36f)).localEulerAngles = new Vector3(-8,0,13);
        Part(stampArm,"Cinnabar Seal",stone,cinnabar,new Vector3(.13f,-.52f,.17f),new Vector3(.44f,.29f,.42f));
        Part(stampArm,"Seal Face",stone,red,new Vector3(.13f,-.656f,.17f),new Vector3(.40f,.052f,.38f));
        var other = Pivot(torso,"Broken Arm",new Vector3(-.49f,.54f,-.03f));
        Part(other,"Fracture",stone,dark,new Vector3(-.04f,-.23f,.035f),new Vector3(.28f,.53f,.33f)).localEulerAngles = new Vector3(16,4,-19);
        var legs = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float sign = i == 0 ? -1f : 1f;
            legs[i] = Pivot(body,"Pillar Foot " + i,new Vector3(sign * .27f,.70f,-.04f));
            Part(legs[i],"Cracked Pillar",stone,slate,new Vector3(0,-.26f,0),new Vector3(.31f,.56f,.35f)).localEulerAngles = new Vector3(-6,0,sign * 11);
            Part(legs[i],"Worn Base",stone,dark,new Vector3(0,-.60f,.095f),new Vector3(.39f,.19f,.49f));
        }
        clips.Idle = Clip(clips,"Idle",true,c => Rotate(c,torso,"z",-2,2,-2));
        clips.Move = Clip(clips,"Move",true,c =>
        {
            Rotate(c,legs[0],"x",-18,18,-18); Rotate(c,legs[1],"x",18,-18,18);
            Rotate(c,torso,"z",-5,5,-5);
        });
        clips.Windup = Clip(clips,"Windup",false,c =>
        {
            Rotate(c,stampArm,"x",0,-86,-150); Rotate(c,torso,"y",0,-12,-20); Rotate(c,torso,"x",0,-8,-12);
        });
        clips.Strike = Clip(clips,"Strike",false,c =>
        {
            Rotate(c,stampArm,"x",-56,-27,0); Rotate(c,torso,"x",26,13,0); Offset(c,torso,"z",.20f,.10f,0);
        });
    }

    private static Material Glow(string name, Color color, float intensity)
    {
        var material = Tint(name,color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor",color * intensity);
        return material;
    }

    private static Mesh Oval(string name)
    {
        const int rings = 10, sides = 16;
        var points = new List<Vector3>();
        var faces = new List<int>();
        for (int y = 0; y <= rings; y++)
        {
            float latitude = Mathf.PI * y / rings;
            for (int side = 0; side <= sides; side++)
            {
                float angle = Mathf.PI * 2f * side / sides;
                points.Add(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(angle),Mathf.Cos(latitude),
                    Mathf.Sin(latitude) * Mathf.Sin(angle)));
                if (y == 0 || side == 0) continue;
                int end = points.Count - 1, top = end - sides - 1;
                faces.Add(top - 1); faces.Add(top); faces.Add(end - 1);
                faces.Add(top); faces.Add(end); faces.Add(end - 1);
            }
        }
        var mesh = Asset(name,new Mesh());
        mesh.Clear();
        mesh.SetVertices(points);
        mesh.SetNormals(points);
        mesh.SetTriangles(faces,0);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }
}
