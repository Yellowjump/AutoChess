using System;
using System.Collections.Generic;
using System.Linq;
using DataTable;
using GameFramework.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

internal sealed class TitleCollectionPanel : MonoBehaviour
{
    // Portrait indices follow the rows of the guardian atlas.
    private static readonly (string Key, string Chinese, string English, int Skill)[] Guardians =
    {
        ("GateGolem","守隘石傀","Gate Golem",9192), ("WreckBeast","沉舟水妖","Wreck Beast",9191),
        ("BlackfangBadger","黑牙岩獾","Blackfang Badger",9194), ("AshKilnMacaque","驮炉灰魈","Kilnback Macaque",9195),
        ("ContractGolem","契纹石傀","Contract Golem",9196), ("TideboneCrab","潮骨蟹","Tidebone Crab",9202),
        ("SaltspineEel","盐脊鳗","Saltspine Eel",9197), ("RopeApe","拖索猿","Rope Ape",9198),
        ("LanternWatch","负灯纸卒","Lantern Watch",9199), ("SnowMarten","踏雪白貂","Snow Marten",9200),
        ("CliffKite","峡梁石鸢","Cliff Kite",9201), ("NamelessClerk","失名纸吏","Nameless Clerk",9203),
        ("FrostfangMarten","霜牙貂","Frostfang Marten",9204), ("ColdcoreGolem","寒髓石傀","Coldcore Golem",9205),
        ("LavascaleLizard","熔壳蜥","Lavascale Lizard",9206), ("SaltShellCrab","盐壳蟹","Salt-shell Crab",9208),
        ("BronzeSluiceBeast","铜闸兽","Bronze Sluice Beast",9209), ("NamelessStele","无名碑","Nameless Stele",9193),
        ("RitualWraith","归墟阵灵","Ritual Wraith",9193), ("LuYing","前哨守卫","Vanguard Guard",9190),
        ("Guardian","山野守怪","Wild Guardian",9190)
    };
    private sealed class Entry
    {
        public string Key, Name, Detail, Description;
        public int Portrait = -1, Gear, MapIcon = -1;
    }

    private readonly List<Entry> monsters = new(), items = new();
    private HashSet<string> unlocked;
    private Action close;
    private RectTransform sheet;
    private ExpeditionPaperUi ui;
    private Texture2D atlas;
    private TMP_FontAsset font;
    private Sprite[] mapIcons;
    private ExpeditionFormationStyle art;
    private Material itemInk;
    private int selected;
    private int page;
    private static readonly Color Ink = new Color32(44,53,49,255);
    private static readonly Color FadedInk = new Color32(89,92,79,255);
    private static readonly Color Cinnabar = new Color32(149,57,42,255);
    private bool itemTab;
    private const int PerPage = 6;
    private static string T(string cn, string en) => GameEntry.Localization.Language == Language.English ? en : cn;

    public static TitleCollectionPanel Open(TMP_FontAsset font, Sprite[] mapIcons, Action close)
    {
        var data = WorldTimelineTableConfig.Create();
        var known = WorldTimelineDebugView.ReadCollection(data);
        var root = ExpeditionPaperUi.Canvas("TitleCollection");
        root.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.AddComponent<Image>().color = new Color(.09f,.10f,.09f,.90f);
        var panel = root.AddComponent<TitleCollectionPanel>();
        panel.art = Resources.Load<ExpeditionFormationStyle>("ExpeditionFormationStyle");
        panel.font = font;
        var shader = Resources.Load<Shader>("CollectionItemInk");
        if (shader != null) panel.itemInk = new Material(shader);
        panel.mapIcons = mapIcons;
        panel.close = close;
        panel.unlocked = new HashSet<string>(known);
        panel.atlas = Resources.Load<Texture2D>("CollectionGuardians");
        panel.BuildEntries(data);
        panel.SelectFirstOnPage();
        return panel;
    }

    private void BuildEntries(WorldTimelineDebugView.ScenarioData data)
    {
        var native = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        for (int i = 0; i < Guardians.Length; i++)
        {
            var g = Guardians[i];
            monsters.Add(new Entry { Key = "monster:" + g.Key, Name = T(g.Chinese,g.English),
                Portrait = i < 19 ? i : -1, Detail = WorldTimelineTableConfig.Localize(native[g.Skill].Name), Description = WorldTimelineTableConfig.Localize(native[g.Skill].Decs) });
        }
        foreach (var gear in ExpeditionEquipment.Definitions)
            items.Add(new Entry { Key = "gear:" + gear.Id, Name = gear.Name, Gear = gear.Id,
                Detail = gear.TriggerName + "   " + gear.Effect(1), Description = gear.TriggerRule });
        foreach (var gear in ExpeditionEquipment.Catalog.Where(g => g.Consumable && unlocked.Contains("legacy:" + g.Id)))
            items.Add(new Entry { Key = "legacy:" + gear.Id, Name = gear.Name, Detail = gear.Effect });
        var mapped = new HashSet<int>();
        void AddMap(int id, string name, string detail, int icon = 13)
        {
            if (id != 0 && mapped.Add(id)) items.Add(new Entry { Key = WorldTimelineDebugView.MapCollectionKey(id),
                Name = name, Detail = detail, MapIcon = icon });
        }
        foreach (var story in data.Stories.Items.Values.OrderBy(i => i.Id))
            AddMap(story.Id, story.Name, story.Description, story.HealthBonus > 0 ? 10 : 11);
        foreach (var location in data.Locations)
            AddMap(location.LootItemId, location.LootName, location.Name);
        foreach (var row in GameEntry.DataTable.GetDataTable<DRStartingCultivators>("StartingCultivators").GetAllDataRows())
            AddMap(row.ItemId, WorldTimelineTableConfig.Localize(row.WeaponName), WorldTimelineTableConfig.Localize(row.Name), 12);
        for (int i = 0; i < 3; i++)
            items.Add(new Entry { Key = "wild:" + i, MapIcon = 16+i, Name = WorldTimelineDebugView.TimelineText("Timeline_ExplorerRelic_" + i),
                Detail = WorldTimelineDebugView.TimelineText("Timeline_ExplorerRelic_Description_" + i) });
        foreach (var row in native.GetAllDataRows().Where(r => unlocked.Contains("item:" + -r.Id)))
            AddMap(-row.Id, WorldTimelineTableConfig.Localize(row.Name), T("行囊道具","Inventory item"));
    }

    private void Draw()
    {
        if (sheet != null)
        {
            sheet.gameObject.SetActive(false);
            Destroy(sheet.gameObject);
            ui.Dispose();
        }
        sheet = (RectTransform)new GameObject("CollectionPage",typeof(RectTransform)).transform;
        sheet.SetParent(transform,false);
        sheet.anchorMin = sheet.anchorMax = sheet.pivot = Vector2.one * .5f;
        sheet.sizeDelta = new Vector2(1660,946);
        ui = new ExpeditionPaperUi(sheet.gameObject,art != null ? art.HeadingFont(font) : font);
        ui.Box("Shadow",8,12,1660,946,new Color(0,0,0,.28f));
        ui.Box("Paper",0,0,1660,946,new Color32(239,231,209,255));
        if (art != null)
            Wash("PaperGrain",art.PaperTexture,new Rect(.08f,.61f,.34f,.28f),0,0,1660,946,Color.white);
        ui.Box("BookEdge",0,0,9,946,Ink);
        ui.Picture("TitleSeal",art?.Seal,56,42,78,78);
        ui.Text("Title",T("山河图鉴","Field Guide"),153,37,540,90,60,Ink);
        Nav("Close",T("返回标题","Back to title"),1415,48,183,56,close);
        ui.Line(58,145,1544);

        var entries = itemTab ? items : monsters;
        DrawPlate(entries[selected]);
        Nav("Monsters",T("怪物","Monsters"),708,172,134,55,() => SelectTab(false),!itemTab);
        Nav("Items",T("道具","Items"),850,172,134,55,() => SelectTab(true),itemTab);
        var progress = ui.Text("Progress",T("已收录","Discovered") + $"   {entries.Count(e => unlocked.Contains(e.Key))} / {entries.Count}",
            1176,185,414,41,25,FadedInk);
        progress.alignment = TextAlignmentOptions.Right;
        for (int i = 0; i < PerPage && page*PerPage+i < entries.Count; i++)
            DrawTile(entries[page*PerPage+i],page*PerPage+i,708+i%3*300,256+i/3*264);
        ui.Line(708,800,888);
        int pages = (entries.Count+PerPage-1)/PerPage;
        Nav("Previous",T("上一页","Previous"),710,824,150,62,() => Turn(-1)).interactable = page > 0;
        ui.Text("Page",$"{page+1}  /  {pages}",1038,826,230,62,28,FadedInk).alignment = TextAlignmentOptions.Center;
        Nav("Next",T("下一页","Next"),1446,824,150,62,() => Turn(1)).interactable = page+1 < pages;
        foreach (var label in sheet.GetComponentsInChildren<TMP_Text>())
            if (label.name == "PlateDescription" || !label.font.HasCharacters(label.text))
                label.font = font;
    }

    private void Wash(string name,Texture texture,Rect crop,int x,int y,int width,int height,Color tint)
    {
        if (texture == null) return;
        var image = ui.Rect(name,x,y,width,height).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.uvRect = crop;
        image.color = tint;
        image.raycastTarget = false;
    }

    private Button Nav(string name,string label,int x,int y,int w,int h,Action action,bool current = false)
    {
        var button = ui.Button(name,label,x,y,w,h,action);
        button.image.sprite = null;
        button.image.color = current ? Cinnabar : new Color(.35f,.32f,.25f,.07f);
        button.GetComponentInChildren<TMP_Text>().color = current ? ExpeditionPaperUi.Paper : Ink;
        return button;
    }

    private void DrawPlate(Entry entry)
    {
        bool known = unlocked.Contains(entry.Key);
        ui.Box("PlateEdge",56,174,610,710,new Color32(120,107,81,255));
        ui.Box("PlatePaper",58,176,606,706,new Color32(229,218,192,255));
        if (art != null)
            Wash("MountainWash",art.Landscape,new Rect(.28f,.29f,.46f,.70f),60,178,602,476,Color.white);
        DrawArt(entry,180,206,360,known);
        ui.Box("Caption",60,614,602,266,Ink);
        if (art != null)
            Wash("InkGrain",art.PaperTexture,new Rect(.56f,.57f,.36f,.34f),60,614,602,266,new Color(1,1,1,.54f));
        ui.Box("CaptionMark",91,647,5,43,new Color32(197,146,89,255));
        ui.Text("PlateName",known ? entry.Name : T("尚未收录","Undiscovered"),110,637,525,64,43,ExpeditionPaperUi.Paper);
        string summary = known ? entry.Detail : itemTab ? T("持有此道具后收录。","Acquire this item to record it.")
            : T("战胜对应的守关怪物后收录。","Defeat this guardian to record it.");
        ui.Text("PlateDetail",summary,93,713,538,entry.Description == null ? 146 : 45,25,new Color32(229,215,185,255));
        if (known && !string.IsNullOrEmpty(entry.Description))
            ui.Text("PlateDescription",entry.Description,93,762,530,105,22,new Color32(212,208,190,255));
    }

    private void DrawTile(Entry entry,int index,int x,int y)
    {
        bool known = unlocked.Contains(entry.Key);
        bool active = index == selected;
        ui.Box("TileBorder",x,y,280,243,active ? Cinnabar : new Color(.35f,.31f,.23f,.29f));
        ui.Box("TilePaper",x+2,y+2,276,239,new Color32(237,229,209,255));
        if (art != null)
            Wash("TileGrain",art.PaperTexture,new Rect(.09f,.61f,.32f,.29f),x+2,y+2,276,239,Color.white);
        DrawArt(entry,x+64,y+11,152,known);
        if (!known) ui.Box("LockedMask",x+2,y+2,276,239,new Color(.40f,.42f,.41f,.23f));
        ui.Box("TileRule",x+23,y+177,234,1,new Color(.34f,.30f,.24f,.31f));
        ui.Text("TileName",known ? entry.Name : T("未解锁","Undiscovered"),x+9,y+184,262,48,27,
            active && known ? Cinnabar : FadedInk).alignment = TextAlignmentOptions.Center;
        var target = ui.Box("Select"+index,x,y,280,243,Color.clear);
        var button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        button.onClick.AddListener(() => { if (selected != index) { selected = index; Draw(); } });
        UIFormLogic.BindButtonClickSound(button);
    }

    private void DrawArt(Entry entry,int x,int y,int size,bool known)
    {
        var tint = known ? Color.white : new Color(.28f,.30f,.28f,.48f);
        if (entry.Portrait >= 0 && atlas != null)
            Wash("Guardian",atlas,new Rect(entry.Portrait%5*.2f,(3-entry.Portrait/5)*.25f,.2f,.25f),x,y,size,size,tint);
        else if (entry.Gear > 0)
        {
            var icon = ui.Picture("Equipment",ui.ItemIcon(entry.Gear),x,y,size,size);
            icon.material = itemInk;
            icon.color = tint;
        }
        else if (entry.MapIcon >= 0 && mapIcons != null && entry.MapIcon < mapIcons.Length)
            ui.Picture("Relic",mapIcons[entry.MapIcon],x,y,size,size).color = tint;
        else
            ui.Text("Seal",known ? entry.Name.Substring(0,1) : "?",x,y,size,size,size*.4f,FadedInk)
                .alignment = TextAlignmentOptions.Center;
    }

    private void SelectFirstOnPage()
    {
        var entries = itemTab ? items : monsters;
        int start = page*PerPage;
        int first = entries.FindIndex(start,Math.Min(PerPage,entries.Count-start),e => unlocked.Contains(e.Key));
        selected = first >= 0 ? first : start;
        Draw();
    }
    private void SelectTab(bool next)
    {
        if (next == itemTab) return;
        itemTab = next;
        page = 0;
        SelectFirstOnPage();
    }

    private void Turn(int delta)
    {
        int next = Mathf.Clamp(page+delta,0,Math.Max(0,((itemTab ? items : monsters).Count-1)/PerPage));
        if (next == page) return;
        page = next;
        SelectFirstOnPage();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) close();
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) Turn(-1);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) Turn(1);
    }

    private void OnDestroy()
    {
        ui?.Dispose();
        if (itemInk != null) Destroy(itemInk);
    }
}
