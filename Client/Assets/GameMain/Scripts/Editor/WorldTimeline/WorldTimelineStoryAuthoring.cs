using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class WorldTimelineStoryAuthoring
{
    private const string Atlas = "Assets/GameMain/Image/WorldTimeline/Story/story_backgrounds.png";
    public const string PrefabPath = "Assets/GameMain/Prefeb/UIPrefab/world_timeline_story.prefab";
    private static TMP_FontAsset font;
    private static Sprite paper, slate;
    private static readonly Color Ivory = new Color(.95f, .90f, .77f);
    [MenuItem("Tools/World Timeline/Build Story Dialogue Prefab %&F3")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play。");
        AssetDatabase.ImportAsset(Atlas, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Atlas);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        string[] keys = { "Gate", "Well", "Traveler", "Cave", "Bridge", "BridgeRestored" };
        var slices = new SpriteMetaData[6];
        for (int i = 0; i < 6; i++) slices[i] = new SpriteMetaData { name = keys[i], pivot = new Vector2(.5f, .5f),
            rect = new Rect(i % 3 * width / 3f + 2, (1 - i / 3) * height / 2f + 2, width / 3f - 4, height / 2f - 4) };
#pragma warning disable 0618
        importer.spritesheet = slices;
#pragma warning restore 0618
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToArray();
        var theme = AssetDatabase.LoadAllAssetsAtPath("Assets/GameMain/Image/WorldTimeline/cultivation_ui_atlas.png").OfType<Sprite>().ToArray();
        paper = theme.Single(x => x.name == "Paper"); slate = theme.Single(x => x.name == "Slate");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GameMain/Fonts/SIMHEI SDF.asset");
        var root = new GameObject("world_timeline_story", typeof(RectTransform), typeof(CanvasGroup)); root.layer = 5;
        var rt = (RectTransform)root.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        var ctrl = root.AddComponent<WorldTimelineStoryCtrl>(); var bind = new SerializedObject(ctrl);
        Bind(bind, "battleReportPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(WorldTimelineBattleReport.PrefabPath)
            .GetComponent<WorldTimelineBattleReport>());
        Bind(bind, "merchantPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(WorldTimelineMerchantCtrl.PrefabPath)
            .GetComponent<WorldTimelineMerchantCtrl>());
        try
        {
            var content = Rect(root.transform, "Layout_1920x1080", 0, 0, 1920, 1080);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f); content.anchoredPosition = Vector2.zero;
            var fit = new SerializedObject(root.AddComponent<WorldTimelinePreparationLayout>());
            fit.FindProperty("content").objectReferenceValue = content; fit.ApplyModifiedPropertiesWithoutUndo();
            var presentation = Rect(content, "剧情呈现", 0, 0, 1920, 1080);
            Bind(bind, "presentation", presentation.gameObject);
            var backdrop = Image(presentation, "宣纸幕", paper, 0, 0, 1920, 1080); backdrop.raycastTarget = true;
            var artFrame = Image(presentation, "画卷边框", paper, 42, 116, 1000, 916);
            var art = Image(artFrame.transform, "Background", sprites.Single(x => x.name == "Gate"), 28, 28, 944, 860);
            art.type = UnityEngine.UI.Image.Type.Simple; art.preserveAspect = true; Bind(bind, "art", art);
            Bind(bind, "caption", Label(presentation, "年份与修士", "元年 · 本世修士 · 世界暂驻", 70, 43, 1760, 42, 25, new Color(.22f,.29f,.25f)));
            var dialogue = Image(presentation, "对话框", slate, 1064, 116, 812, 916).transform;
            Bind(bind, "title", Label(dialogue, "Title", "古修石门", 48, 42, 710, 66, 43, Ivory));
            Bind(bind, "resultLabel", Label(dialogue, "阶段", "此刻抉择", 50, 125, 700, 36, 22, new Color(.76f,.64f,.43f)));
            Bind(bind, "body", Label(dialogue, "正文", "山壁嵌着一扇无锁石门。", 50, 185, 710, 296, 28, Ivory));
            var buttons = bind.FindProperty("options"); var labels = bind.FindProperty("optionLabels");
            buttons.arraySize = labels.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var button = Button(dialogue, "选项_" + i, 50, 500 + i * 86, 710, 76, "选项", out var label);
                buttons.GetArrayElementAtIndex(i).objectReferenceValue = button; labels.GetArrayElementAtIndex(i).objectReferenceValue = label;
            }
            Bind(bind, "proceed", Button(dialogue, "继续", 470, 799, 290, 68, "继续前行", out _));
            var backgrounds = bind.FindProperty("backgrounds"); backgrounds.arraySize = keys.Length;
            for (int i = 0; i < keys.Length; i++)
            {
                var entry = backgrounds.GetArrayElementAtIndex(i); entry.FindPropertyRelative("key").stringValue = keys[i];
                entry.FindPropertyRelative("sprite").objectReferenceValue = sprites.Single(x => x.name == keys[i]);
            }
            presentation.gameObject.SetActive(false);
            bind.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); AssetDatabase.SaveAssets();
            Debug.Log("剧情对话预制体已保存：" + PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
    }
    private static Image Image(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
    {
        var image = Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false;
        image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 2.4f; return image;
    }
    private static TMP_Text Label(Transform parent, string name, string text, float x, float y, float w, float h, int size, Color color)
    {
        var label = Rect(parent,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>(); label.font = font;
        label.text = text; label.fontSize = size; label.color = color; label.raycastTarget = false;
        label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Ellipsis; return label;
    }
    private static Button Button(Transform parent,string name,float x,float y,float w,float h,string text,out TMP_Text label)
    {
        var image = Image(parent,name,null,x,y,w,h); image.color = new Color(.20f,.34f,.30f); image.raycastTarget = true;
        var border = image.gameObject.AddComponent<Outline>(); border.effectDistance = new Vector2(1,-1); border.effectColor = new Color(.72f,.58f,.36f);
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        label = Label(image.transform,"Text",text,18,8,w-36,h-16,23,Ivory); label.alignment = TextAlignmentOptions.Center;
        return button;
    }
    private static void Bind(SerializedObject obj,string name,Object value) => obj.FindProperty(name).objectReferenceValue = value;
}
