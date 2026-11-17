using Entity;
using UnityEditor;
using UnityEngine;

public static partial class TimelineMonsterAuthoring
{
    [MenuItem("Tools/World Timeline/Build Seven Outer Guardians")]
    public static void BuildOuterGuardians()
    {
        var oval = Oval("RegionalGuardianOval");
        var slab = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/RegionalGuardianChippedStone.asset");
        if (slab == null) throw new System.InvalidOperationException("Build the regional guardian meshes first.");
        BuildOne("SaltspineEel", oval, 1.04f, new Color32(49,73,68,255), new Color32(200,212,191,255), Saltspine);
        BuildOne("RopeApe", oval, 1.08f, new Color32(77,67,54,255), new Color32(172,144,102,255), RopeApe);
        BuildOne("SnowMarten", oval, .68f, new Color32(211,217,207,255), new Color32(51,55,58,255),
            SnowMarten, new Vector3(1f, 1.06f, .56f));
        BuildOne("CliffKite", slab, .78f, new Color32(86,86,86,255), new Color32(154,146,132,255), CliffKite);
        BuildOne("NamelessClerk", slab, 1.08f, new Color32(59,64,66,255), new Color32(197,177,137,255), NamelessClerk);
        BuildLanternAndTidebone();
    }

    [MenuItem("Tools/World Timeline/Rebuild Lantern Watch and Tidebone Crab")]
    public static void BuildLanternAndTidebone()
    {
        var slab = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/RegionalGuardianChippedStone.asset");
        if (slab == null) throw new System.InvalidOperationException("Build the regional guardian meshes first.");
        BuildOne("LanternWatch", slab, 1.05f, new Color32(99,87,64,255), new Color32(220,203,162,255), LanternWatch);
        BuildOne("TideboneCrab", slab, 1.06f, new Color32(67,66,62,255), new Color32(157,149,132,255), Tidebone);
        AssetDatabase.SaveAssets();
    }

    private static void Saltspine(Transform body, Mesh mesh, Material skin, Material salt, TimelineMonsterAnimation rig)
    {
        var fin = Tint("SaltspineEelFin", new Color32(99,141,127,255));
        var eye = Glow("SaltspineEelEye", new Color32(170,229,202,255), .6f);
        var head = Pivot(body, "Head", new Vector3(0,.68f,.53f));
        Part(head,"Wedge Skull",mesh,skin,Vector3.zero,new Vector3(.29f,.25f,.43f));
        Part(head,"Pale Jaw",mesh,salt,new Vector3(0,-.13f,.23f),new Vector3(.19f,.091f,.27f));
        for (int s = -1; s <= 1; s += 2)
            Part(head,"Eye "+s,mesh,eye,new Vector3(s*.23f,.06f,.25f),new Vector3(.065f,.05f,.065f));
        var spine = new Transform[5];
        for (int i = 0; i < spine.Length; i++)
        {
            float taper = 1f-i*.15f;
            spine[i]=Pivot(body,"Salt spine "+i,new Vector3(Mathf.Sin(i*.9f)*.20f,.62f-i*.082f,.34f-i*.33f));
            Part(spine[i],"Scales",mesh,skin,Vector3.zero,new Vector3(.28f*taper,.24f*taper,.36f));
            Part(spine[i],"Salt Crest",mesh,salt,new Vector3(0,.21f*taper,0),new Vector3(.093f,.115f*taper,.22f));
            Part(spine[i],"Fin",mesh,fin,new Vector3(.09f,.07f,-.03f),new Vector3(.26f*taper,.052f,.25f));
        }
        rig.Idle=Clip(rig,"Idle",true,c=>Offset(c,body,"y",0,.07f,0));
        rig.Move=Clip(rig,"Move",true,c=>
        {
            for(int i=0;i<spine.Length;i++) Offset(c,spine[i],"x",i*.033f,-i*.033f,i*.033f);
            Rotate(c,head,"y",-9,9,-9);
        });
        rig.Windup=Clip(rig,"Windup",false,c=> { Offset(c,body,"y",0,.19f,.30f); Rotate(c,head,"x",0,-25,-39); });
        rig.Strike=Clip(rig,"Strike",false,c=> { Offset(c,body,"z",.19f,.09f,0); Rotate(c,head,"x",20,9,0); });
    }

    private static void RopeApe(Transform body, Mesh mesh, Material fur, Material rope, TimelineMonsterAnimation rig)
    {
        var face=Tint("RopeApeFace",new Color32(59,45,38,255));
        var stone=Tint("RopeApeWeight",new Color32(96,99,91,255));
        var chest=Pivot(body,"Hunched torso",new Vector3(0,.91f,-.13f));
        Part(chest,"Shoulders",mesh,fur,new Vector3(0,.20f,0),new Vector3(.47f,.39f,.36f));
        Part(chest,"Torso",mesh,fur,new Vector3(0,-.07f,-.04f),new Vector3(.33f,.39f,.31f));
        Part(chest,"Muzzle",mesh,face,new Vector3(0,.22f,.39f),new Vector3(.24f,.23f,.21f));
        Part(chest,"Brow",mesh,rope,new Vector3(0,.34f,.51f),new Vector3(.19f,.076f,.085f));
        var arms=new Transform[2];
        var legs=new Transform[2];
        for(int i=0;i<2;i++)
        {
            float s=i==0?-1:1;
            arms[i]=Pivot(chest,"Long arm "+i,new Vector3(s*.40f,.14f,.04f));
            Part(arms[i],"Forelimb",mesh,fur,new Vector3(s*.09f,-.33f,.06f),new Vector3(.16f,.44f,.16f));
            Part(arms[i],"Knuckles",mesh,face,new Vector3(s*.13f,-.70f,.12f),new Vector3(.20f,.19f,.21f));
            legs[i]=Pivot(body,"Bent leg "+i,new Vector3(s*.24f,.67f,-.12f));
            Part(legs[i],"Leg",mesh,fur,new Vector3(0,-.22f,0),new Vector3(.18f,.31f,.20f));
            Part(legs[i],"Foot",mesh,face,new Vector3(0,-.54f,.10f),new Vector3(.19f,.12f,.24f));
        }
        var coil=Pivot(arms[1],"Broken rope",new Vector3(.15f,-.66f,.16f));
        for(int i=0;i<9;i++)
        {
            float a=i*Mathf.PI/4;
            Part(coil,"Twisted hemp "+i,mesh,rope,new Vector3(Mathf.Sin(a)*.16f,-.04f+Mathf.Cos(a)*.15f,i*.032f),
                new Vector3(.057f,.062f,.083f));
        }
        var weight=Part(coil,"Stone weight",mesh,stone,new Vector3(.12f,-.29f,.37f),new Vector3(.20f,.23f,.19f));
        weight.localEulerAngles=new Vector3(20,20,-23);
        rig.Idle=Clip(rig,"Idle",true,c=>Rotate(c,chest,"x",6,0,6));
        rig.Move=Clip(rig,"Move",true,c=>Gait(c,legs,arms,25f));
        rig.Windup=Clip(rig,"Windup",false,c=>
        { Rotate(c,arms[1],"x",0,-68,-146); Rotate(c,chest,"y",0,-20,-37); Rotate(c,coil,"z",0,85,165); });
        rig.Strike=Clip(rig,"Strike",false,c=>
        { Rotate(c,arms[1],"x",-57,-21,0); Rotate(c,chest,"y",31,14,0); Rotate(c,coil,"z",166,233,360); });
    }

    private static void LanternWatch(Transform body, Mesh slab, Material straw, Material paper, TimelineMonsterAnimation rig)
    {
        var flame=Glow("LanternWatchWick",new Color32(248,158,68,255),1.2f);
        var dark=Tint("LanternWatchIron",new Color32(53,59,56,255));
        var cape=Prism("LanternWatchRainCape",new[]{ new Vector2(-.57f,0),new Vector2(-.46f,.55f),
            new Vector2(-.22f,.93f),new Vector2(.22f,.94f),new Vector2(.48f,.53f),new Vector2(.59f,.08f),
            new Vector2(.34f,.15f),new Vector2(.02f,.06f),new Vector2(-.31f,.13f)},.47f,.66f);
        var trunk=Pivot(body,"Paper body",new Vector3(0,.64f,0));
        Part(trunk,"Worn straw cape",cape,straw,Vector3.zero,Vector3.one);
        Part(trunk,"Folded face",slab,paper,new Vector3(0,.91f,.11f),new Vector3(.35f,.42f,.34f));
        Part(trunk,"Broad hat",slab,dark,new Vector3(0,1.13f,.04f),new Vector3(.75f,.10f,.65f));
        for(int i=0;i<7;i++)
            Part(trunk,"Rain-cut straw "+i,slab,i%3==0?paper:straw,new Vector3((i-3)*.13f,.38f,.27f),
                new Vector3(.062f,.64f,.028f)).localEulerAngles=new Vector3(10,0,(i-3)*9);
        var lantern=Pivot(trunk,"Frosted watch lantern",new Vector3(.11f,.41f,.49f));
        Part(lantern,"Paper shade",slab,paper,Vector3.zero,new Vector3(.29f,.40f,.24f));
        Part(lantern,"Flame",slab,flame,new Vector3(0,0,.134f),new Vector3(.155f,.235f,.024f));
        for(int i=-1;i<=1;i+=2)
            Part(lantern,"Iron rim "+i,slab,dark,new Vector3(0,i*.20f,0),new Vector3(.33f,.063f,.29f));
        for (int side = -1; side <= 1; side += 2)
        {
            Part(lantern, "Paper hand " + side, slab, paper, new Vector3(side * .18f, -.18f, .04f),
                new Vector3(.15f, .15f, .13f));
            Part(lantern, "Folded sleeve " + side, slab, straw, new Vector3(side * .24f, -.29f, -.10f),
                new Vector3(.15f, .32f, .17f)).localEulerAngles = new Vector3(35f, 0f, side * 27f);
        }
        Part(trunk,"Patrol tag",slab,straw,new Vector3(-.24f,.20f,.37f),new Vector3(.14f,.24f,.039f));
        var feet=new Transform[2];
        for(int i=0;i<2;i++)
        {
            feet[i]=Pivot(body,"Paper foot "+i,new Vector3(i==0?-.18f:.18f,.55f,0));
            Part(feet[i],"Stilts",slab,dark,new Vector3(0,-.24f,0),new Vector3(.14f,.51f,.17f));
            Part(feet[i],"Sandal",slab,paper,new Vector3(0,-.49f,.08f),new Vector3(.18f,.12f,.28f));
        }
        rig.Idle=Clip(rig,"Idle",true,c=>Rotate(c,lantern,"z",-7,7,-7));
        rig.Move=Clip(rig,"Move",true,c=> { Rotate(c,feet[0],"x",-21,21,-21); Rotate(c,feet[1],"x",21,-21,21); });
        rig.Windup = Clip(rig, "Windup", false, c => { Offset(c, lantern, "z", 0, -.07f, -.13f); Rotate(c, lantern, "x", 0, -12, -22); });
        rig.Strike = Clip(rig, "Strike", false, c => { Offset(c, lantern, "z", .25f, .09f, 0); Rotate(c, lantern, "x", 23, 10, 0); });
        rig.LineWindup = Clip(rig, "LineWindup", false, c =>
        {
            Offset(c, trunk, "y", 0, -.28f, -.15f);
            Rotate(c, trunk, "x", 0, 27, -17);
            Offset(c, lantern, "y", 0, .22f, 1.36f);
            Offset(c, lantern, "z", 0, .28f, .48f);
            Rotate(c, lantern, "x", 0, -30, 18);
        });
        rig.LineRecovery = Clip(rig, "LineRecovery", false, c =>
        {
            Offset(c, lantern, "y", 1.42f, .88f, 0f);
            Offset(c, lantern, "z", .48f, .44f, 0f);
            Rotate(c, trunk, "x", -21f, 12f, 0f);
        });
    }

    private static void SnowMarten(Transform body, Mesh fur, Material snow, Material ink, TimelineMonsterAnimation rig)
    {
        var frost = Tint("SnowMartenFrost", new Color32(156, 178, 180, 255));
        var cream = Tint("SnowMartenWinterCream", new Color32(235, 231, 212, 255));
        var eye = Glow("SnowMartenEye", new Color32(171, 217, 202, 255), .45f);
        var spine = Pivot(body, "Flexible spine", new Vector3(0, .50f, -.13f));
        Part(spine, "Long rib cage", fur, snow, Vector3.zero, new Vector3(.235f, .226f, .58f));
        Part(spine, "Winter belly", fur, cream, new Vector3(0, -.102f, .09f), new Vector3(.198f, .153f, .43f));
        Part(spine, "Pale saddle", fur, frost, new Vector3(0, .125f, -.17f), new Vector3(.186f, .133f, .395f));
        var shoulders = Pivot(spine, "Shoulders", new Vector3(0, .02f, .36f));
        Part(shoulders, "Shoulder ruff", fur, snow, new Vector3(0, .038f, .016f), new Vector3(.248f, .239f, .31f))
            .localEulerAngles = new Vector3(-17, 0, 0);
        var hips = Pivot(spine, "Arched hips", new Vector3(0, .012f, -.42f));
        Part(hips, "Hindquarters", fur, snow, new Vector3(0, .06f, 0), new Vector3(.252f, .25f, .295f))
            .localEulerAngles = new Vector3(19, 0, 0);

        var neck = Pivot(shoulders, "Neck", new Vector3(0, .072f, .20f));
        Part(neck, "Throat", fur, cream, new Vector3(0, -.044f, .072f), new Vector3(.157f, .163f, .244f));
        var head = Pivot(neck, "Head", new Vector3(0, .065f, .18f));
        Part(head, "Skull", fur, snow, new Vector3(0, .027f, .008f), new Vector3(.188f, .155f, .215f));
        Part(head, "Tapered muzzle", fur, cream, new Vector3(0, -.038f, .169f), new Vector3(.114f, .087f, .155f));
        Part(head, "Nose", fur, ink, new Vector3(0, -.035f, .292f), new Vector3(.063f, .043f, .046f));
        var jaw = Pivot(head, "Jaw", new Vector3(0, -.080f, .017f));
        Part(jaw, "Lower jaw", fur, snow, new Vector3(0, -.018f, .135f), new Vector3(.093f, .046f, .137f));
        Part(jaw, "Mouth", fur, ink, new Vector3(0, .007f, .15f), new Vector3(.071f, .017f, .101f));

        var tail = Pivot(hips, "Tail base", new Vector3(0, .067f, -.211f));
        Part(tail, "Tail brush", fur, snow, new Vector3(0, .056f, -.22f), new Vector3(.141f, .13f, .284f))
            .localEulerAngles = new Vector3(14, 0, 0);
        var tailMid = Pivot(tail, "Tail middle", new Vector3(0, .108f, -.423f));
        Part(tailMid, "Tail taper", fur, frost, new Vector3(0, .053f, -.153f), new Vector3(.110f, .102f, .23f))
            .localEulerAngles = new Vector3(18, 0, 0);
        var tailTip = Pivot(tailMid, "Tail tip", new Vector3(0, .10f, -.31f));
        Part(tailTip, "Black tip", fur, ink, new Vector3(0, .024f, -.098f), new Vector3(.081f, .077f, .178f))
            .localEulerAngles = new Vector3(14, 0, 0);

        var legs = new Transform[4];
        var shins = new Transform[4];
        var paws = new Transform[4];
        var ears = new Transform[2];
        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1f : 1f;
            ears[s] = Pivot(head, "Ear " + s, new Vector3(side * .137f, .119f, -.067f));
            Part(ears[s], "Rounded ear", fur, snow, new Vector3(0, .045f, 0), new Vector3(.079f, .092f, .043f))
                .localEulerAngles = new Vector3(3, side * 15, -side * 29);
            Part(ears[s], "Dark inner ear", fur, frost, new Vector3(side * .013f, .053f, .031f), new Vector3(.049f, .057f, .020f))
                .localEulerAngles = new Vector3(3, side * 15, -side * 29);
            Part(head, "Cheek tuft " + s, fur, cream, new Vector3(side * .137f, -.046f, .006f), new Vector3(.085f, .073f, .166f))
                .localEulerAngles = new Vector3(12, side * 23, 0);
            Part(head, "Eye rim " + s, fur, ink, new Vector3(side * .143f, .038f, .128f), new Vector3(.046f, .037f, .047f));
            Part(head, "Eye " + s, fur, eye, new Vector3(side * .169f, .041f, .149f), new Vector3(.021f, .018f, .023f));

            for (int i = 0; i < 2; i++)
            {
                bool front = i == 0;
                int index = s * 2 + i;
                legs[index] = Pivot(front ? shoulders : hips, "Upper leg " + index,
                    new Vector3(side * .164f, -.098f, front ? .04f : -.035f));
                Part(legs[index], "Upper fur", fur, snow, new Vector3(0, -.089f, front ? -.021f : .045f),
                    new Vector3(front ? .098f : .134f, .167f, front ? .105f : .143f));
                shins[index] = Pivot(legs[index], "Hock", new Vector3(0, -.184f, front ? -.028f : .053f));
                Part(shins[index], "Lower fur", fur, snow, new Vector3(0, -.048f, -.022f), new Vector3(.067f, .103f, .083f));
                paws[index] = Pivot(shins[index], "Paw", new Vector3(0, -.127f, -.028f));
                Part(paws[index], "Toes", fur, cream, new Vector3(0, -.022f, .060f), new Vector3(.086f, .050f, .129f));
                for (int toe = -1; toe <= 1; toe++)
                    Part(paws[index], "Claw " + toe, fur, ink, new Vector3(toe * .037f, -.025f, .162f - Mathf.Abs(toe) * .018f),
                        new Vector3(.016f, .018f, .037f));
            }
        }

        rig.Idle = Clip(rig, "Idle", true, c =>
        {
            Rotate(c, head, "x", 1, -7, 1);
            Rotate(c, ears[0], "z", 0, 15, 0);
            Rotate(c, tail, "y", -11, 10, -11);
            Rotate(c, tailMid, "y", -20, 19, -20);
            Rotate(c, tailTip, "x", 4, -9, 4);
        });
        rig.Move = Clip(rig, "Move", true, c =>
        {
            for (int i = 0; i < legs.Length; i++)
            {
                bool lead = i is 0 or 3;
                Rotate(c, legs[i], "x", lead ? 34 : -30, lead ? -30 : 34, lead ? 34 : -30);
                Rotate(c, shins[i], "x", lead ? -44 : 9, lead ? 9 : -44, lead ? -44 : 9);
                Rotate(c, paws[i], "x", lead ? 12 : 0, lead ? 0 : 12, lead ? 12 : 0);
            }
            Rotate(c, shoulders, "z", -7, 7, -7);
            Rotate(c, hips, "z", 8, -8, 8);
            Rotate(c, tail, "y", 16, -16, 16);
            Rotate(c, tailMid, "x", 9, -11, 9);
        });
        rig.Windup = Clip(rig, "Windup", false, c => Crouch(c, 1f));
        rig.Strike = Clip(rig, "Strike", false, c => Snap(c, 1f));
        rig.LineWindup = Clip(rig, "LineWindup", false, c => Crouch(c, 1.55f));
        rig.LineRecovery = Clip(rig, "LineRecovery", false, c => Snap(c, 1.55f));

        void Crouch(AnimationClip c, float force)
        {
            Offset(c, spine, "y", 0, -.064f * force, -.102f * force);
            Rotate(c, shoulders, "x", 0, 13 * force, 23 * force);
            Rotate(c, hips, "x", 0, -9 * force, -15 * force);
            Rotate(c, neck, "x", 0, -12 * force, -27 * force);
            Rotate(c, jaw, "x", 0, 9 * force, 31 * force);
            Rotate(c, tail, "x", 0, 13 * force, 27 * force);
            Rotate(c, tailMid, "x", 0, -8 * force, -19 * force);
            for (int i = 0; i < legs.Length; i++)
            {
                float bend = i % 2 == 0 ? -15 * force : -27 * force;
                Rotate(c, legs[i], "x", 0, bend * .64f, bend);
                Rotate(c, shins[i], "x", 0, 12 * force, 25 * force);
            }
        }

        void Snap(AnimationClip c, float force)
        {
            Offset(c, spine, "y", -.055f * force, .019f * force, 0);
            Rotate(c, shoulders, "x", 16 * force, -11 * force, 0);
            Rotate(c, neck, "x", -18 * force, 11 * force, 0);
            Offset(c, head, "z", .145f * force, .018f, 0);
            Curve(c, jaw, "localEulerAnglesRaw.x", new Keyframe(0, 27 * force), new Keyframe(.16f, -5),
                new Keyframe(.43f, 3), new Keyframe(1, 0));
            Rotate(c, tail, "y", 28 * force, -12 * force, 0);
            Rotate(c, tailMid, "y", -20 * force, 17 * force, 0);
            for (int i = 0; i < legs.Length; i++)
            {
                Rotate(c, legs[i], "x", (i % 2 == 0 ? 13 : -12) * force, 0, 0);
                Rotate(c, shins[i], "x", (i % 2 == 0 ? -17 : 20) * force, 0, 0);
            }
        }
    }

    private static void CliffKite(Transform body, Mesh slab, Material stone, Material edge, TimelineMonsterAnimation rig)
    {
        var eyes=Glow("CliffKiteEye",new Color32(236,165,88,255),.7f);
        var trunk=Pivot(body,"Slate breast",new Vector3(0,.97f,-.06f));
        Part(trunk,"Breast",slab,stone,Vector3.zero,new Vector3(.55f,.61f,.65f)).localEulerAngles=new Vector3(-13,9,0);
        Part(trunk,"Head",slab,edge,new Vector3(0,.32f,.36f),new Vector3(.37f,.35f,.44f));
        Part(trunk,"Hooked Beak",slab,stone,new Vector3(0,.27f,.66f),new Vector3(.15f,.16f,.28f)).localEulerAngles=new Vector3(25,0,0);
        var wings=new Transform[2];
        for(int i=0;i<2;i++)
        {
            float sign=i==0?-1:1;
            Part(trunk,"Amber slit "+i,slab,eyes,new Vector3(sign*.17f,.36f,.52f),new Vector3(.041f,.038f,.079f));
            wings[i]=Pivot(trunk,"Rock Wing "+i,new Vector3(sign*.18f,.12f,0));
            for(int f=0;f<5;f++)
            {
                var feather=Part(wings[i],"Split slate "+f,slab,f%2==0?stone:edge,new Vector3(sign*(.16f+f*.095f),.06f+f*.043f,-.046f-f*.072f),
                    new Vector3(.31f,.09f,.48f-f*.064f));
                feather.localEulerAngles=new Vector3(-13,sign*(11+f*9),sign*7);
            }
            Part(trunk,"Talon "+i,slab,stone,new Vector3(sign*.17f,-.41f,.09f),new Vector3(.16f,.19f,.32f));
        }
        rig.Idle=Clip(rig,"Idle",true,c=> { Rotate(c,wings[0],"z",6,13,6); Rotate(c,wings[1],"z",-6,-13,-6); });
        rig.Move=Clip(rig,"Move",true,c=>
        { Rotate(c,wings[0],"z",22,-30,22); Rotate(c,wings[1],"z",-22,30,-22); Offset(c,trunk,"y",0,.16f,0); });
        rig.Windup=Clip(rig,"Windup",false,c=>
        { Offset(c,trunk,"y",0,.40f,.78f); Rotate(c,wings[0],"z",0,37,76); Rotate(c,wings[1],"z",0,-37,-76); });
        rig.Strike=Clip(rig,"Strike",false,c=>
        { Offset(c,trunk,"y",-.29f,-.11f,0); Rotate(c,trunk,"x",44,22,0); Rotate(c,wings[0],"z",63,27,0); Rotate(c,wings[1],"z",-63,-27,0); });
    }

    private static void Tidebone(Transform body, Mesh mesh, Material shell, Material chalk, TimelineMonsterAnimation rig)
    {
        var ink = Tint("TideboneCrabClaw", new Color32(37, 35, 32, 255));
        var joints = Tint("TideboneCrabMask", new Color32(91, 86, 77, 255));
        var eyes = Tint("TideboneCrabEye", new Color32(183, 174, 150, 255));
        var shellMesh = TideboneShell();
        var oval = Oval("RegionalGuardianOval");
        var pincers = new Mesh[2];
        for (int i = 0; i < pincers.Length; i++)
        {
            float side = i == 0 ? -1 : 1;
            Vector2[] outline =
            {
                new(-.16f * side, -.18f), new(.17f * side, -.15f), new(.27f * side, .04f),
                new(.24f * side, .25f), new(.10f * side, .43f), new(.04f * side, .28f),
                new(.08f * side, .09f), new(-.17f * side, .02f)
            };
            if (i == 1) System.Array.Reverse(outline);
            pincers[i] = Prism("TideboneInkPincer" + i, outline, .18f, .15f);
        }

        var back = Pivot(body, "Barnacle shell", new Vector3(0, .48f, -.11f));
        Part(back, "Broad ink carapace", shellMesh, shell, Vector3.zero, Vector3.one);
        Part(back, "Undershell", oval, ink, new Vector3(0, -.055f, .03f), new Vector3(.53f, .119f, .40f));
        for (int i = -1; i <= 1; i++)
        {
            var streak = Part(back, "Pale shell brush mark " + i, mesh, chalk,
                new Vector3(i * .25f, .207f - Mathf.Abs(i) * .021f, -.13f - i * .056f),
                new Vector3(.18f, .012f, .35f - Mathf.Abs(i) * .07f));
            streak.localEulerAngles = new Vector3(9f, -18f, -i * 11f);
        }

        var claws = new Transform[2];
        var legs = new Transform[8];
        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1 : 1;
            var stalkBase = new Vector3(side * .23f, .015f, .39f);
            var stalkTip = new Vector3(side * .28f, .13f, .51f);
            Segment(back, "Eye stalk " + s, stalkBase, stalkTip, .038f, ink);
            Part(back, "Eye bead " + s, oval, eyes, stalkTip, new Vector3(.048f, .039f, .051f));
            claws[s] = Pivot(body, "Claw " + s, new Vector3(side * .48f, .46f, .24f));
            var wrist = new Vector3(side * .15f, -.045f, .30f);
            Segment(claws[s], "Bent wrist", Vector3.zero, wrist, .087f, joints);
            var pincer = Part(claws[s], "Sickle claw", pincers[s], ink, wrist + Vector3.forward * .11f,
                Vector3.one * (s == 0 ? 1.08f : .94f));
            pincer.localEulerAngles = new Vector3(90f, side * -8f, 0f);
            var hook = Part(claws[s], "Inner hook", mesh, shell,
                wrist + new Vector3(side * -.12f, -.02f, .285f), new Vector3(.093f, .12f, .36f));
            hook.localEulerAngles = new Vector3(4f, side * 31f, 0f);

            for (int i = 0; i < 4; i++)
            {
                int j = s * 4 + i;
                legs[j] = Pivot(body, "Jointed leg " + j, new Vector3(side * .41f, .38f, .26f - i * .205f));
                var knee = new Vector3(side * (.43f - i * .037f), .035f, .13f - i * .11f);
                var ankle = knee + new Vector3(side * (.255f - i * .035f), -.22f, -.095f - i * .038f);
                var tip = ankle + new Vector3(side * .08f, -.131f, -.10f);
                Segment(legs[j], "Brush-stroke thigh", Vector3.zero, knee, .071f - i * .006f, joints);
                Segment(legs[j], "Ink shin", knee, ankle, .057f - i * .004f, ink);
                Segment(legs[j], "Tapered foot", ankle, tip, .036f - i * .003f, ink);
            }
        }

        void Segment(Transform parent, string name, Vector3 from, Vector3 to, float width, Material tint)
        {
            var direction = to - from;
            var segment = Part(parent, name, oval, tint, (from + to) * .5f,
                new Vector3(width, width * .76f, direction.magnitude * .57f));
            segment.localRotation = Quaternion.LookRotation(direction);
        }

        rig.Idle = Clip(rig, "Idle", true, c => Offset(c, back, "y", 0, .045f, 0));
        rig.Move = Clip(rig, "Move", true, c =>
        {
            for (int i = 0; i < legs.Length; i++)
            {
                float swing = i % 2 == 0 ? 27f : -27f;
                Rotate(c, legs[i], "y", swing, -swing, swing);
            }
        });
        rig.Windup = Clip(rig, "Windup", false, c => { Rotate(c, claws[0], "x", 0, -35, -66); Rotate(c, back, "y", 0, 11, 24); });
        rig.Strike = Clip(rig, "Strike", false, c => { Rotate(c, claws[0], "x", 33, 14, 0); Offset(c, body, "z", .23f, .07f, 0); });
        rig.LineWindup = Clip(rig, "LineWindup", false, c =>
        {
            Offset(c, back, "y", 0, .22f, -.22f);
            Rotate(c, back, "x", 0, -24, 32);
            for (int i = 0; i < claws.Length; i++)
            {
                float side = i == 0 ? -1 : 1;
                Rotate(c, claws[i], "y", side * 24, side * 65, side * -58);
                Rotate(c, claws[i], "x", 0, -55, -28);
                Offset(c, claws[i], "x", 0, side * .18f, side * -.22f);
            }
        });
        rig.LineRecovery = Clip(rig, "LineRecovery", false, c =>
        {
            Hold(c, back, "localPosition.y", back.localPosition.y - .22f, back.localPosition.y);
            Hold(c, back, "localEulerAnglesRaw.x", 32, 0);
            for (int i = 0; i < claws.Length; i++)
            {
                float side = i == 0 ? -1 : 1;
                Hold(c, claws[i], "localPosition.x", claws[i].localPosition.x - side * .22f, claws[i].localPosition.x);
                Hold(c, claws[i], "localEulerAnglesRaw.y", side * -58, 0);
                Hold(c, claws[i], "localEulerAnglesRaw.x", -28, 0);
            }
        });

        void Hold(AnimationClip clip, Transform pivot, string channel, float closed, float rest)
            => Curve(clip, pivot, channel, new Keyframe(0, closed), new Keyframe(.86f, closed), new Keyframe(1, rest));
    }

    private static Mesh TideboneShell()
    {
        Vector2[] outline =
        {
            new(0, .46f), new(.28f, .48f), new(.50f, .40f), new(.62f, .24f),
            new(.67f, .06f), new(.58f, -.20f), new(.40f, -.40f), new(0, -.47f),
            new(-.39f, -.42f), new(-.57f, -.24f), new(-.68f, .04f),
            new(-.59f, .24f), new(-.48f, .43f), new(-.23f, .48f)
        };
        int sides = outline.Length;
        var vertices = new Vector3[sides * 3 + 1];
        var triangles = new System.Collections.Generic.List<int>(sides * 15);
        vertices[vertices.Length - 1] = new Vector3(0f, .205f, -.055f);
        for (int i = 0; i < sides; i++)
        {
            var point = outline[i];
            vertices[i] = new Vector3(point.x * .87f, -.115f, point.y * .91f);
            vertices[i + sides] = new Vector3(point.x, -.01f, point.y);
            vertices[i + sides * 2] = new Vector3(point.x * .72f, .145f, point.y * .67f);
            int next = (i + 1) % sides;
            for (int band = 0; band < 2; band++)
            {
                int low = band * sides, high = low + sides;
                triangles.Add(low + i); triangles.Add(low + next); triangles.Add(high + i);
                triangles.Add(low + next); triangles.Add(high + next); triangles.Add(high + i);
            }
            triangles.Add(sides * 2 + i);
            triangles.Add(sides * 2 + next);
            triangles.Add(vertices.Length - 1);
            if (i > 0 && i < sides - 1)
            {
                triangles.Add(0); triangles.Add(i + 1); triangles.Add(i);
            }
        }
        var mesh = Asset("TideboneInkCarapace", new Mesh());
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static void NamelessClerk(Transform body, Mesh slab, Material cloth, Material paper, TimelineMonsterAnimation rig)
    {
        var ink=Tint("NamelessClerkInk",new Color32(40,39,42,255));
        var seal=Tint("NamelessClerkSeal",new Color32(155,68,54,255));
        var robe=Prism("NamelessClerkRobe",new[]{new Vector2(-.49f,0),new Vector2(-.37f,.71f),new Vector2(-.29f,1.13f),
            new Vector2(.28f,1.13f),new Vector2(.39f,.53f),new Vector2(.50f,.06f),new Vector2(.10f,.12f),
            new Vector2(-.05f,.03f)},.45f,.53f);
        var trunk=Pivot(body,"Official robe",new Vector3(0,.19f,0));
        Part(trunk,"Torn Robe",robe,cloth,Vector3.zero,Vector3.one);
        // Prism expects a clockwise profile when viewed from the front.
        var headMesh = Prism("NamelessClerkHead", new Vector2[]
        {
            new(-.32f, -.46f), new(-.48f, -.30f), new(-.48f, .29f), new(-.29f, .48f),
            new(.29f, .48f), new(.48f, .29f), new(.48f, -.30f), new(.32f, -.46f)
        }, 1f, 1f);
        Part(trunk, "Blank paper face", headMesh, paper, new Vector3(0, 1.27f, .11f), new Vector3(.35f, .42f, .31f));
        Part(trunk, "Official hat", headMesh, ink, new Vector3(0, 1.53f, .03f), new Vector3(.53f, .19f, .41f));
        Part(trunk, "Hat flaps", headMesh, ink, new Vector3(0, 1.47f, -.13f), new Vector3(.89f, .065f, .22f));
        Part(trunk,"Red sash",slab,seal,new Vector3(0,.62f,.27f),new Vector3(.61f,.069f,.053f));
        var arms=new Transform[2];
        for(int i=0;i<2;i++)
        {
            float s=i==0?-1:1;
            arms[i]=Pivot(trunk,"Wide sleeve "+i,new Vector3(s*.32f,.98f,0));
            Part(arms[i],"Sleeve",slab,cloth,new Vector3(s*.10f,-.21f,.09f),new Vector3(.32f,.57f,.34f)).localEulerAngles=new Vector3(-16,0,s*19);
            Part(arms[i],"Paper hand",slab,paper,new Vector3(s*.12f,-.46f,.23f),new Vector3(.14f,.17f,.13f));
        }
        var book=Pivot(arms[1],"Register",new Vector3(.12f,-.39f,.28f));
        Part(book, "Back cover", slab, seal, new Vector3(0, 0, -.027f), new Vector3(.43f, .48f, .065f));
        Part(book, "Dry pages", slab, paper, new Vector3(0, .014f, .024f), new Vector3(.39f, .44f, .045f));
        for (int i = 0; i < 4; i++)
            Part(book, "Crossed name " + i, slab, ink, new Vector3(0, .13f - i * .082f, .048f),
                new Vector3(.28f - i % 2 * .04f, .017f, .013f)).localEulerAngles = new Vector3(0, 0, 10 - i * 5);

        var cover = Pivot(book, "Register hinge", new Vector3(-.21f, 0f, .049f));
        Part(cover, "Crimson cover", slab, seal, new Vector3(.21f, 0f, .014f), new Vector3(.43f, .49f, .045f));
        Part(cover, "Paper title slip", slab, paper, new Vector3(.24f, .06f, .04f), new Vector3(.10f, .24f, .014f));
        Part(book, "Binding spine", slab, ink, new Vector3(-.21f, 0f, 0f), new Vector3(.062f, .49f, .10f));
        rig.Idle = Clip(rig, "Idle", true, c => Offset(c, trunk, "y", 0, .046f, 0));
        rig.Move = Clip(rig, "Move", true, c => { Offset(c, trunk, "y", 0, .09f, 0); Rotate(c, trunk, "z", -6, 6, -6); });
        rig.Windup = Clip(rig, "Windup", false, c =>
        {
            Rotate(c, arms[1], "x", 0, -47, -78);
            Rotate(c, arms[0], "x", 0, -37, -83);
            Rotate(c, book, "x", 0, 20, 63);
            Rotate(c, cover, "y", 0, -35, -147);
            Offset(c, trunk, "y", 0, -.08f, -.04f);
        });
        rig.Strike = Clip(rig, "Strike", false, c =>
        {
            Rotate(c, arms[1], "x", -91, -64, 0);
            Rotate(c, arms[0], "x", -127, -52, 0);
            Rotate(c, book, "x", 58, 45, 0);
            Rotate(c, cover, "y", -147, -123, 0);
        });
        AuthorClerkBasicAttack(rig);
    }

    public static void AuthorClerkBasicAttack(TimelineMonsterAnimation rig)
    {
        var robe = rig.transform.Find("Body/Official robe");
        var sleeve = robe.Find("Wide sleeve 0");
        var registerArm = robe.Find("Wide sleeve 1");
        rig.LineWindup = Clip(rig, "SleeveWindup", false, clip =>
        {
            Rotate(clip, robe, "y", 0, -13, -32);
            Rotate(clip, sleeve, "x", 0, 15, 25);
            Rotate(clip, sleeve, "z", 0, -25, -65);
            Rotate(clip, registerArm, "x", 0, -22, -22);
        });
        rig.LineRecovery = Clip(rig, "SleeveStrike", false, clip =>
        {
            Rotate(clip, robe, "y", -32, 34, 0);
            Rotate(clip, sleeve, "x", 25, -98, 0);
            Rotate(clip, sleeve, "z", -65, 26, 0);
            Rotate(clip, registerArm, "x", -22, -22, 0);
        });
    }

    private static void Gait(AnimationClip clip, Transform[] legs, Transform[] arms, float angle)
    {
        Rotate(clip,legs[0],"x",angle,-angle,angle); Rotate(clip,legs[1],"x",-angle,angle,-angle);
        Rotate(clip,arms[0],"x",-angle,angle,-angle); Rotate(clip,arms[1],"x",angle,-angle,angle);
    }
}
