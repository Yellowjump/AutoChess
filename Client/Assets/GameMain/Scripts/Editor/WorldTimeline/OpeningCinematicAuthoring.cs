using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
using UnityEngine.Video;

public static class OpeningCinematicAuthoring
{
    private const string Art = "Assets/GameMain/Art/Opening/";
    private const string TimelinePath = Art + "TheTwelfthName.playable";
    private const string PrefabPath = Art + "OpeningCinematic.prefab";
    private const string TitlePath = "Assets/GameMain/Prefeb/UIPrefab/main_title_panel.prefab";
    private const string Ferry = "Assets/GameMain/Image/WorldTimeline/title_ink_ferry.png";
    private static readonly Color Paper = new Color32(248, 236, 210, 255);
    private static readonly Color Gold = new Color32(235, 203, 144, 255);

    [MenuItem("Tools/World Timeline/Build Opening Cinematic")]
    public static void Build()
    {
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        if (timeline != null && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("重建开场 CG", "将用脚本中的分镜与台词重建开场 Timeline 和预制体。", "重建", "取消"))
            return;
        if (timeline == null)
        {
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);
        }
        else
        {
            foreach (var track in new System.Collections.Generic.List<TrackAsset>(timeline.GetRootTracks()))
                timeline.DeleteTrack(track);
        }

        var shots = timeline.CreateTrack<OpeningShotTrack>("画面与台词");
        var ferry = AddShot(shots, "渡口 · 点名", Ferry, 0, 6.4, 1f, 1.08f,
            new Vector2(.55f, .48f), new Vector2(.59f, .48f), .67f, fadeIn: .85f);
        ferry.motion = Load<VideoClip>(Art + "TheTwelfthNamePicture.mp4");
        ferry.heading = Line("青禾渡口", "QINGHE FERRY", 1.25f, 4.45f);
        ferry.dialogue = Line("船夫：十、十一……齐了。", "Ferryman: Ten, eleven… that's everyone.", 2.25f, 4.6f);

        var ledger = AddShot(shots, "渡船旧簿", Art + "FerryLedger.png", 6.4, 6.4, 1f, 1.12f,
            new Vector2(.49f, .48f), new Vector2(.39f, .49f), .08f);
        ledger.dialogue = Line("簿上本有十二个名字。", "There had been twelve names in the ledger.", .45f, 3.1f);
        ledger.secondDialogue = Line("最后一个，只剩一道刮痕。", "Of the last, only a scrape remained.", 3.28f, 6.15f);

        var child = AddShot(shots, "被漏掉的孩子", Art + "ForgottenChild.png", 12.8, 6.4, 1f, 1.16f,
            new Vector2(.5f, .49f), new Vector2(.60f, .49f), .78f);
        child.dialogue = Line("孩子：我还在这儿。", "Child: I'm still here.", 2.35f, 4.03f);
        child.secondDialogue = Line("船离了岸。没有人回头。", "The ferry pulled away. No one looked back.", 4.2f, 6.15f);

        var formation = AddShot(shots, "归墟 · 征名", Art + "GuixuFormation.png", 19.2, 6.4, 1f, 1.10f,
            new Vector2(.5f, .52f), new Vector2(.55f, .49f), .62f);
        formation.dialogue = Line("归墟大阵破了。守印人拿活人的名字补阵。", "The Guixu formation cracked. Its keeper used living names to mend it.", .4f, 3.5f);
        formation.secondDialogue = Line("名字一入阵，亲友便会忘了那个人。", "Once a name entered the formation, even loved ones forgot.", 3.65f, 6.15f);

        var gate = AddShot(shots, "元年 · 古修石门", Art + "StoneGate.png", 25.6, 6.4, 1.12f, 1f,
            new Vector2(.5f, .58f), new Vector2(.5f, .5f), .60f, fadeOut: .55f);
        gate.heading = Line("元年 · 古修石门", "YEAR ZERO  /  ANCIENT STONE GATE", .5f, 5.95f);
        gate.dialogue = Line("第四年，渡口那孩子会走到这里。", "In the fourth year, the child from the ferry will reach this gate.", .8f, 3.7f);
        gate.secondDialogue = Line("这一回，你也在路上。", "This time, you're on the road too.", 3.88f, 6.15f);

        var music = AddAudio(timeline, "配乐 · 第十二个名字", "TheTwelfthName.wav");
        var effects = AddAudio(timeline, "音效 · 水声与刮纸", "RiverAndPaper.wav");

        var root = new GameObject("Opening Cinematic", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PlayableDirector), typeof(OpeningCinematic));
        try
        {
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            var director = root.GetComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            director.playableAsset = timeline;

            var black = Fill<RawImage>("Black", root.transform);
            black.color = new Color32(13, 14, 16, 255);
            black.raycastTarget = true;
            var frame = Fill<AspectRatioFitter>("Film Frame", root.transform);
            frame.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            frame.aspectRatio = 2.12f;
            var picture = Fill<CanvasGroup>("Picture", frame.transform);
            picture.alpha = 0;
            picture.blocksRaycasts = false;
            var image = Fill<RawImage>("Painting", picture.transform);
            var mist = Fill<RawImage>("Drifting Mist", picture.transform);
            mist.texture = Load<Texture2D>(Art + "DriftingMist.png");
            Fill<RawImage>("Shade", picture.transform).texture = Load<Texture2D>(Art + "SubtitleShade.png");

            var heading = Caption("Location", frame.transform, Gold, 30, TextAlignmentOptions.TopLeft);
            Place(heading.rectTransform, .055f, .82f, .9f, .92f);
            heading.alpha = 0;
            var subtitles = Fill<CanvasGroup>("Subtitles", frame.transform);
            subtitles.alpha = 0;
            subtitles.blocksRaycasts = false;
            var subtitle = Caption("Dialogue", subtitles.transform, Paper, 40, TextAlignmentOptions.Center);
            Place(subtitle.rectTransform, .065f, .035f, .935f, .175f);
            subtitle.enableAutoSizing = true;
            subtitle.fontSizeMin = 24;
            subtitle.fontSizeMax = 40;

            var skip = Fill<Button>("Skip", root.transform);
            var skipRect = (RectTransform)skip.transform;
            Place(skipRect, 1, 1, 1, 1);
            skipRect.offsetMin = new Vector2(-172, -63);
            skipRect.offsetMax = new Vector2(-20, -19);
            var skipSurface = skip.gameObject.AddComponent<Image>();
            skipSurface.color = new Color32(24, 27, 28, 205);
            skip.targetGraphic = skipSurface;
            var skipLabel = Caption("Label", skip.transform, Paper, 23, TextAlignmentOptions.Center);
            skipLabel.text = "跳过  /  Esc";
            skipLabel.raycastTarget = false;

            var view = root.GetComponent<OpeningCinematic>();
            var score = CreateSource("Score", root.transform);
            var atmosphere = CreateSource("Atmosphere", root.transform);
            director.SetGenericBinding(shots, view);
            director.SetGenericBinding(music, score);
            director.SetGenericBinding(effects, atmosphere);
            var viewFields = new SerializedObject(view);
            Set(viewFields, "director", director);
            Set(viewFields, "score", score);
            Set(viewFields, "atmosphere", atmosphere);
            Set(viewFields, "picture", picture);
            Set(viewFields, "image", image);
            Set(viewFields, "mist", mist);
            Set(viewFields, "heading", heading);
            Set(viewFields, "subtitles", subtitles);
            Set(viewFields, "subtitle", subtitle);
            Set(viewFields, "skip", skip);
            Set(viewFields, "skipLabel", skipLabel);
            Set(viewFields, "endingPaintings", Load<Texture2D>("Assets/GameMain/Art/Ending/EpiloguePaintings.png"));
            Set(viewFields, "endingScore", Load<AudioClip>(Art + "TheTwelfthName.wav"));
            Set(viewFields, "endingAtmosphere", Load<AudioClip>(Art + "RiverAndPaper.wav"));
            viewFields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            BindTitle(prefab.GetComponent<OpeningCinematic>());
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
    }

    private static OpeningShot AddShot(OpeningShotTrack track, string name, string path, double start, double duration,
        float startZoom, float endZoom, Vector2 startFocus, Vector2 endFocus, float mist, float fadeIn = 0, float fadeOut = 0)
    {
        var clip = track.CreateClip<OpeningShot>();
        clip.displayName = name;
        clip.start = start;
        clip.duration = duration;
        var shot = (OpeningShot)clip.asset;
        shot.name = name;
        shot.painting = Load<Texture2D>(path);
        shot.startZoom = startZoom;
        shot.endZoom = endZoom;
        shot.startFocus = startFocus;
        shot.endFocus = endFocus;
        shot.mist = mist;
        shot.fadeIn = fadeIn;
        shot.fadeOut = fadeOut;
        return shot;
    }

    private static OpeningShot.Caption Line(string chinese, string english, float start, float end) =>
        new OpeningShot.Caption { chinese = chinese, english = english, start = start, end = end };

    private static AudioTrack AddAudio(TimelineAsset timeline, string name, string file)
    {
        var track = timeline.CreateTrack<AudioTrack>(name);
        track.CreateClip(Load<AudioClip>(Art + file));
        return track;
    }

    private static AudioSource CreateSource(string name, Transform parent)
    {
        var source = new GameObject(name, typeof(AudioSource)).GetComponent<AudioSource>();
        source.transform.SetParent(parent, false);
        source.playOnAwake = false;
        return source;
    }

    private static T Fill<T>(string name, Transform parent) where T : Component
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(T));
        child.transform.SetParent(parent, false);
        Place((RectTransform)child.transform, 0, 0, 1, 1);
        var component = child.GetComponent<T>();
        if (component is Graphic graphic) graphic.raycastTarget = false;
        return component;
    }

    private static void Place(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
    {
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI Caption(string name, Transform parent, Color color, float size, TextAlignmentOptions align)
    {
        var label = Fill<TextMeshProUGUI>(name, parent);
        label.font = Load<TMP_FontAsset>("Assets/GameMain/Fonts/SIMHEI SDF.asset");
        label.color = color;
        label.fontSize = size;
        label.alignment = align;
        return label;
    }

    private static void BindTitle(OpeningCinematic prefab)
    {
        var root = PrefabUtility.LoadPrefabContents(TitlePath);
        try
        {
            var title = new SerializedObject(root.GetComponentInChildren<MainTitlePanelCtrl>(true));
            Set(title, "openingCinematicPrefab", prefab);
            title.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, TitlePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Set(SerializedObject owner, string field, Object value) =>
        owner.FindProperty(field).objectReferenceValue = value;

    private static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new System.IO.FileNotFoundException(path);
}
