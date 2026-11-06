using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Editor-only baking. Runtime code binds existing controls; it never constructs UI objects.
public static class WorldTimelinePreparationAuthoring
{
    public const string PrefabPath = "Assets/GameMain/Prefeb/UIPrefab/world_timeline_preparation.prefab";
    private const string PortraitAtlas = "Assets/GameMain/Image/WorldTimeline/Selection/cultivator_portraits.png";
    private static TMP_FontAsset font;
    private static Sprite paper, slate;
    private static readonly Color Ink = new Color(.17f, .20f, .18f);
    private static readonly Color Gold = new Color(.73f, .59f, .36f);
    private static readonly Color Ivory = new Color(.95f, .89f, .73f);
    private static readonly Color Jade = new Color(.22f, .43f, .36f);
    private static readonly string[] Names = { "青篁", "赤砾", "白溪", "鹤归", "玄砂", "重山", "闻潮", "照夜", "孤松", "逐星" };
    private static readonly string[] Weapons = { "青竹剑", "斩妖刀", "寒溪剑", "飞羽针", "镇砂杵", "开山斧", "听潮剑", "照夜枪", "松纹剑", "逐星弓" };

    [MenuItem("Tools/World Timeline/Build Character Selection Prefab %&F6")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play，再烘焙选人预制体。");
        ImportPortraits();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GameMain/Fonts/SIMHEI SDF.asset");
        var atlas = AssetDatabase.LoadAllAssetsAtPath("Assets/GameMain/Image/WorldTimeline/cultivation_ui_atlas.png").OfType<Sprite>().ToArray();
        paper = atlas.Single(s => s.name == "Paper"); slate = atlas.Single(s => s.name == "Slate");
        var portraits = AssetDatabase.LoadAllAssetsAtPath(PortraitAtlas).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        var root = new GameObject("world_timeline_preparation", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = 5;
        var rootRect = (RectTransform)root.transform; rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;
        var ctrl = root.AddComponent<WorldTimelinePreparationCtrl>();
        var binding = new SerializedObject(ctrl);
        try
        {
            var background = Image(root.transform, "宣纸底幕", paper, new Rect(0, 0, 1920, 1080), Color.white);
            background.raycastTarget = true;
            Stretch(background.rectTransform);
            var content = Rect(root.transform, "Layout_1920x1080", new Rect(0, 0, 1920, 1080));
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.anchoredPosition = Vector2.zero;
            var fit = new SerializedObject(root.AddComponent<WorldTimelinePreparationLayout>());
            fit.FindProperty("content").objectReferenceValue = content; fit.ApplyModifiedPropertiesWithoutUndo();
            Label(content, "Eyebrow", "修 仙 轮 回", new Rect(660, 28, 600, 35), 25, Gold, TextAlignmentOptions.Center);
            Label(content, "Title", "择一人 · 入此世", new Rect(450, 66, 1020, 70), 54, Ink, TextAlignmentOptions.Center);
            Label(content, "Subtitle", "十人轮转，前世同行", new Rect(660, 138, 600, 32), 24, Ink, TextAlignmentOptions.Center);
            Bind(binding, "returnMenu", Button(content, "返回主菜单", new Rect(1620, 52, 242, 57), "返回主菜单", 25));
            var left = Image(content, "本世修士_面板", paper, new Rect(40, 193, 1178, 735), Color.white).transform;
            Image(left, "标题底", slate, new Rect(12, 10, 1154, 68), Color.white);
            Label(left, "SectionTitle", "本 世 修 士", new Rect(44, 20, 400, 45), 32, Ivory);
            Bind(binding, "cycle", Label(left, "Cycle", "第 1 轮 · 尚可选择 10 / 10", new Rect(630, 25, 500, 36), 23, Ivory, TextAlignmentOptions.Right));
            var slots = binding.FindProperty("characters"); slots.arraySize = 10;
            for (int i = 0; i < 10; i++)
            {
                float x = 26 + i % 5 * 226, y = 92 + i / 5 * 311;
                var card = Image(left, "修士_" + Names[i], slate, new Rect(x, y, 216, 298), Color.white);
                var appearance = card.gameObject.AddComponent<CanvasGroup>();
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card;
                ConfigureButton(button);
                var outline = card.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.31f, .69f, .55f); outline.effectDistance = new Vector2(4, -4);
                outline.enabled = i == 0;
                var portrait = Image(card.transform, "Portrait", portraits[i], new Rect(13, 13, 190, 211), Color.white, false);
                var name = Label(card.transform, "Name", Names[i], new Rect(18, 225, 180, 34), 27, Ivory, TextAlignmentOptions.Center);
                var weapon = Label(card.transform, "Weapon", Weapons[i], new Rect(18, 262, 180, 24), 19, Ivory, TextAlignmentOptions.Center);
                var selected = Image(card.transform, "本世标记", slate, new Rect(16, 16, 46, 66), new Color(.55f, .92f, .76f));
                Label(selected.transform, "Text", "本\n世", new Rect(4, 6, 38, 52), 20, Ivory, TextAlignmentOptions.Center);
                selected.gameObject.SetActive(i == 0);
                var used = Image(card.transform, "已历此世", slate, new Rect(25, 100, 166, 52), Color.white);
                Label(used.transform, "Text", "已历此世", new Rect(5, 5, 156, 42), 22, Ivory, TextAlignmentOptions.Center); used.gameObject.SetActive(false);
                var slot = slots.GetArrayElementAtIndex(i);
                Bind(slot, "button", button); Bind(slot, "portrait", portrait); Bind(slot, "name", name); Bind(slot, "weapon", weapon);
                Bind(slot, "appearance", appearance); Bind(slot, "selected", selected.gameObject); Bind(slot, "used", used.gameObject); Bind(slot, "outline", outline);
            }
            var profile = Image(content, "修士详情", paper, new Rect(1236, 193, 644, 302), Color.white);
            Bind(binding, "profile", profile.gameObject);
            Bind(binding, "selectedName", Label(profile.transform, "Name", "青篁", new Rect(38, 24, 365, 64), 48, Ink));
            Bind(binding, "birthplace", Label(profile.transform, "Birthplace", "出生于 · 青篁谷\n外围坐标  (0, -10)", new Rect(38, 100, 375, 67), 23, Ink));
            Bind(binding, "selectedPortrait", Image(profile.transform, "Portrait", portraits[0], new Rect(448, 28, 160, 178), Color.white, false));
            Bind(binding, "weapon", Label(profile.transform, "Weapon", "初始道具 · 青竹剑", new Rect(38, 188, 540, 32), 24, Ink));
            Bind(binding, "bonus", Label(profile.transform, "AttackBonus", "无额外攻击加成", new Rect(38, 226, 540, 26), 20, Ink));
            Label(profile.transform, "BirthRule", "固定出生点  ·  元年启程", new Rect(38, 263, 550, 24), 19, Ink);
            var history = Image(content, "前世同行_面板", slate, new Rect(1236, 510, 644, 418), Color.white).transform;
            Bind(binding, "historyCount", Label(history, "Title", "前世同行  0 / 10", new Rect(32, 22, 390, 40), 30, Ivory));
            Bind(binding, "clearHistories", Button(history, "清空路线", new Rect(472, 26, 137, 38), "清空", 19));
            Label(history, "Hint", "选择本局重现的命途 · 同一修士仅一条", new Rect(34, 69, 565, 28), 19, Ivory);
            var empty = Rect(history, "尚无前世记录", new Rect(32, 110, 580, 242));
            Bind(binding, "emptyState", empty.gameObject);
            Bind(binding, "emptyText", Label(empty, "Text", "尚无前世记录\n\n完成一世后，可在此选择前世路线。", new Rect(30, 42, 520, 150), 25, Ivory, TextAlignmentOptions.Center));
            var entries = binding.FindProperty("histories"); entries.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var row = Image(history, "路线_" + i, null, new Rect(25, 106 + 60 * i, 594, 58), new Color(.13f, .18f, .20f));
                Image(row.transform, "分隔线", null, new Rect(10, 57, 574, 1), new Color(.58f, .48f, .32f, .65f));
                var button = row.gameObject.AddComponent<Button>(); button.targetGraphic = row; ConfigureButton(button);
                var marker = Image(row.transform, "Marker", null, new Rect(13, 19, 7, 20), Gold);
                var name = Label(row.transform, "Name", "前世命途", new Rect(33, 4, 440, 27), 21, Ivory);
                var detail = Label(row.transform, "Detail", "元年启程", new Rect(33, 33, 440, 20), 15, Ivory);
                var state = Label(row.transform, "State", "选择", new Rect(497, 12, 80, 31), 20, Ivory, TextAlignmentOptions.Center);
                var slot = entries.GetArrayElementAtIndex(i); Bind(slot, "button", button); Bind(slot, "marker", marker);
                Bind(slot, "name", name); Bind(slot, "detail", detail); Bind(slot, "state", state);
                row.gameObject.SetActive(false);
            }
            Bind(binding, "previousPage", Button(history, "上一页", new Rect(300, 360, 90, 36), "上一页", 18));
            Bind(binding, "pageLabel", Label(history, "Page", "1 / 1", new Rect(396, 364, 100, 28), 19, Ivory, TextAlignmentOptions.Center));
            Bind(binding, "nextPage", Button(history, "下一页", new Rect(507, 360, 105, 36), "下一页", 18));
            Label(content, "轮转规则", "十位修士皆历一世后，开启下一轮。", new Rect(72, 955, 1080, 32), 24, Ink);
            Label(content, "同行规则", "同一修士仅可携带一条前世路线，且不能与本世身份重复。", new Rect(72, 999, 1140, 30), 20, Ink);
            Bind(binding, "issue", Label(content, "Notice", "", new Rect(1238, 934, 640, 31), 19, new Color(.62f, .22f, .16f)));
            Bind(binding, "begin", Button(content, "开启轮回", new Rect(1360, 974, 510, 76), "开 启 轮 回", 36, Jade));
            binding.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("选人预制体已保存：" + PrefabPath + "（10 个修士卡位、4 个分页路线位，原生 UGUI）");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void ImportPortraits()
    {
        AssetDatabase.ImportAsset(PortraitAtlas, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(PortraitAtlas);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
        float cellWidth = sourceWidth / 5f, cellHeight = sourceHeight / 2f;
        var sprites = new SpriteMetaData[10];
        for (int i = 0; i < 10; i++)
        {
            sprites[i] = new SpriteMetaData { name = "Portrait" + i, pivot = new Vector2(.5f, .5f), alignment = 0,
                rect = new Rect(i % 5 * cellWidth + 2, (1 - i / 5) * cellHeight + 2, cellWidth - 4, cellHeight - 4) };
        }
#pragma warning disable 0618
        importer.spritesheet = sprites;
#pragma warning restore 0618
        importer.SaveAndReimport();
    }
    private static RectTransform Rect(Transform parent, string name, Rect box)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.layer = 5;
        var rt = (RectTransform)obj.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(box.x, -box.y); rt.sizeDelta = box.size;
        return rt;
    }
    private static void Stretch(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    private static Image Image(Transform parent, string name, Sprite sprite, Rect box, Color color, bool sliced = true)
    {
        var image = Rect(parent, name, box).gameObject.AddComponent<Image>(); image.sprite = sprite;
        image.color = color; image.raycastTarget = false;
        image.preserveAspect = !sliced;
        image.type = sliced && sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        // Small controls retain the same cloud ornaments without oversized 96px corners.
        image.pixelsPerUnitMultiplier = sliced ? 2.4f : 1;
        return image;
    }
    private static TMP_Text Label(Transform parent, string name, string value, Rect box, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var text = Rect(parent, name, box).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.color = color; text.text = value;
        text.alignment = align; text.raycastTarget = false; text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    private static Button Button(Transform parent, string name, Rect box, string caption, int fontSize, Color? tint = null)
    {
        var image = Image(parent, name, null, box, tint ?? new Color(.13f, .18f, .20f));
        var border = image.gameObject.AddComponent<Outline>(); border.effectColor = Gold; border.effectDistance = new Vector2(1.5f, -1.5f);
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; ConfigureButton(button);
        Label(image.transform, "Label", caption, new Rect(8, 3, box.width - 16, box.height - 6), fontSize, Ivory, TextAlignmentOptions.Center);
        return button;
    }
    private static void ConfigureButton(Button button)
    {
        button.targetGraphic.raycastTarget = true;
        var colors = button.colors; colors.highlightedColor = new Color(1, .94f, .78f); colors.pressedColor = new Color(.68f, .80f, .72f);
        colors.disabledColor = new Color(.55f, .55f, .55f); button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
    }
    private static void Bind(SerializedObject obj, string name, Object value) => obj.FindProperty(name).objectReferenceValue = value;
    private static void Bind(SerializedProperty obj, string name, Object value) => obj.FindPropertyRelative(name).objectReferenceValue = value;
}
