using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class WorldTimelineInventoryAuthoring
{
    public const string PrefabPath="Assets/GameMain/Prefeb/UIPrefab/world_timeline_inventory.prefab";
    private const string Icons="Assets/GameMain/Image/WorldTimeline/Inventory/item_icons.png";
    private static Sprite paper,slate;
    private static TMP_FontAsset font;
    private static readonly Color Ink=new Color(.17f,.22f,.20f), Ivory=new Color(.95f,.89f,.75f), Jade=new Color(.25f,.46f,.37f);
    [MenuItem("Tools/World Timeline/Build Inventory Prefab %&F2")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先退出 Play。");
        AssetDatabase.ImportAsset(Icons,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(Icons);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.wrapMode=TextureWrapMode.Clamp;importer.GetSourceTextureWidthAndHeight(out int w,out int h);
        var slices=new SpriteMetaData[16];
        for(int i=0;i<16;i++)slices[i]=new SpriteMetaData{name="Item"+i.ToString("D2"),pivot=new Vector2(.5f,.5f),
            rect=new Rect(i%4*w/4f+2,(3-i/4)*h/4f+2,w/4f-4,h/4f-4)};
#pragma warning disable 0618
        importer.spritesheet=slices;
#pragma warning restore 0618
        importer.SaveAndReimport();
        var icons=AssetDatabase.LoadAllAssetsAtPath(Icons).OfType<Sprite>().OrderBy(x=>x.name).ToArray();
        var portraits=AssetDatabase.LoadAllAssetsAtPath("Assets/GameMain/Image/WorldTimeline/Selection/cultivator_portraits.png").OfType<Sprite>().OrderBy(x=>x.name).ToArray();
        var theme=AssetDatabase.LoadAllAssetsAtPath("Assets/GameMain/Image/WorldTimeline/cultivation_ui_atlas.png").OfType<Sprite>().ToArray();
        paper=theme.Single(x=>x.name=="Paper");slate=theme.Single(x=>x.name=="Slate");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GameMain/Fonts/SIMHEI SDF.asset");
        var root=new GameObject("world_timeline_inventory",typeof(RectTransform),typeof(CanvasGroup));root.layer=5;
        var rootRect=(RectTransform)root.transform;rootRect.anchorMin=Vector2.zero;rootRect.anchorMax=Vector2.one;rootRect.sizeDelta=Vector2.zero;
        var binding=new SerializedObject(root.AddComponent<WorldTimelineInventoryCtrl>());
        try
        {
            var background=Image(root.transform,"宣纸幕",paper,0,0,1920,1080);background.raycastTarget=true;
            background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;
            background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
            var content=Rect(root.transform,"Layout_1920x1080",0,0,1920,1080);
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,.5f);content.anchoredPosition=Vector2.zero;
            var fitter=new SerializedObject(root.AddComponent<WorldTimelinePreparationLayout>());
            fitter.FindProperty("content").objectReferenceValue=content;fitter.ApplyModifiedPropertiesWithoutUndo();
            Label(content,"题签","本 世 行 囊",50,105,380,32,21,Ink);
            Label(content,"Title","随 身 装 备",580,98,760,46,38,Ink,TextAlignmentOptions.Center);
            Bind(binding,"close",Button(content,"返回山河",1640,100,230,42,"返回山河",22));
            var left=Image(content,"本世修士",paper,36,150,400,840).transform;
            Bind(binding,"portrait",Image(left,"Portrait",portraits[0],30,28,340,420,false));
            Bind(binding,"heroName",Label(left,"Name","青篁",30,465,340,50,37,Ink,TextAlignmentOptions.Center));
            Bind(binding,"level",Label(left,"Level","第 1 层",30,527,340,35,24,Ink,TextAlignmentOptions.Center));
            string[] statLabels={"气血","攻击","战斗攻击"};string[] statFields={"health","attack","combatAttack"};
            for(int i=0;i<3;i++)
            {
                Label(left,"Label"+i,statLabels[i],32,590+i*54,155,35,23,Ink);
                Bind(binding,statFields[i],Label(left,"Value"+i,"—",190,590+i*54,175,35,24,Ink,TextAlignmentOptions.Right));
            }
            Label(left,"Rule","持有即生效",35,766,330,32,22,Ink,TextAlignmentOptions.Center);
            var middle=Image(content,"物品目录",paper,454,150,900,840).transform;
            var filters=binding.FindProperty("filters");var fills=binding.FindProperty("filterFills");filters.arraySize=fills.arraySize=4;
            string[] labels={"全部","兵器","遗物","功法"};
            for(int i=0;i<4;i++)
            {
                var button=Button(middle,"分类_"+labels[i],30+i*213,24,198,58,labels[i],25);
                filters.GetArrayElementAtIndex(i).objectReferenceValue=button;fills.GetArrayElementAtIndex(i).objectReferenceValue=button.targetGraphic;
            }
            Bind(binding,"quantity",Label(middle,"持有数量","随身 1 件 · 数量不限",35,105,830,40,26,Ink,TextAlignmentOptions.Center));
            var slots=binding.FindProperty("slots");slots.arraySize=12;
            for(int i=0;i<12;i++)
            {
                var card=Image(middle,"物品卡_"+i,paper,28+i%4*212,160+i/4*197,198,186);
                var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card;card.raycastTarget=true;
                var outline=card.gameObject.AddComponent<Outline>();outline.effectColor=Jade;outline.effectDistance=new Vector2(3,-3);outline.enabled=i==0;
                var icon=Image(card.transform,"Icon",icons[i],15,12,168,130,false);
                var name=Label(card.transform,"Name","物品名称",12,149,174,28,21,Ink,TextAlignmentOptions.Center);
                var count=Label(card.transform,"Count","",133,14,48,25,17,Ink,TextAlignmentOptions.Right);
                var slot=slots.GetArrayElementAtIndex(i);Bind(slot,"button",button);Bind(slot,"icon",icon);Bind(slot,"name",name);Bind(slot,"count",count);Bind(slot,"selected",outline);
            }
            var empty=Rect(middle,"Empty",70,295,760,220);Bind(binding,"empty",empty.gameObject);
            Bind(binding,"emptyText",Label(empty,"Text","此类物品尚未获得",0,75,760,60,29,Ink,TextAlignmentOptions.Center));empty.gameObject.SetActive(false);
            Bind(binding,"previousPage",Button(middle,"上一页",190,765,160,48,"上一页",22));
            Bind(binding,"pageLabel",Label(middle,"Page","1 / 1",365,773,170,35,24,Ink,TextAlignmentOptions.Center));
            Bind(binding,"nextPage",Button(middle,"下一页",550,765,160,48,"下一页",22));
            var right=Image(content,"物品详情面板",slate,1374,150,510,840).transform;
            Label(right,"EmptyHint","选择一件物品\n查看详细记载",55,325,400,120,27,Ivory,TextAlignmentOptions.Center);
            var detail=Image(right,"详情内容",slate,0,0,510,840);Bind(binding,"detail",detail.gameObject);
            Bind(binding,"detailIcon",Image(detail.transform,"Icon",icons[0],45,35,420,300,false));
            Bind(binding,"itemName",Label(detail.transform,"Name","青竹剑",45,365,420,54,35,Ivory,TextAlignmentOptions.Center));
            Bind(binding,"itemKind",Label(detail.transform,"Kind","兵器",45,432,420,32,23,Ivory,TextAlignmentOptions.Center));
            Bind(binding,"description",Label(detail.transform,"Description","随身兵器。",45,495,420,180,24,Ivory));
            Bind(binding,"origin",Label(detail.transform,"Origin","初始随身",45,703,420,55,21,Ivory));
            Bind(binding,"ownership",Label(detail.transform,"State","随身持有",45,771,420,30,21,Ivory));
            Label(content,"Footer","兵器、遗物与功法均在此查看。",350,1014,1220,35,23,Ink,TextAlignmentOptions.Center);
            Array(binding,"itemIcons",icons);Array(binding,"portraits",portraits);
            binding.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
        }
        finally{Object.DestroyImmediate(root);}
        InstallMapButton();Debug.Log("随身装备预制体与地图入口已保存："+PrefabPath);
    }
    private static void InstallMapButton()
    {
        const string path="Assets/GameMain/Prefeb/UIPrefab/world_timeline_panel.prefab";
        var contents=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var bind=new SerializedObject(contents.GetComponent<WorldTimelinePanelCtrl>());
            var growth=(Button)bind.FindProperty("growth").objectReferenceValue;
            var equipment=(Button)bind.FindProperty("equipment").objectReferenceValue;
            if(equipment==null){equipment=Object.Instantiate(growth,growth.transform.parent);equipment.name="Equipment";}
            var rect=(RectTransform)growth.transform;rect.sizeDelta=new Vector2(98,42);
            var growthLabel=growth.GetComponentInChildren<TMP_Text>();growthLabel.text="修行";growthLabel.rectTransform.sizeDelta=new Vector2(82,34);
            var equipRect=(RectTransform)equipment.transform;equipRect.anchoredPosition=rect.anchoredPosition+new Vector2(109,0);equipRect.sizeDelta=new Vector2(98,42);
            var label=equipment.GetComponentInChildren<TMP_Text>();label.text="装备";label.rectTransform.sizeDelta=new Vector2(82,34);
            Bind(bind,"equipment",equipment);bind.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(contents,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(contents);}
    }
    private static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
    }
    private static Image Image(Transform parent,string name,Sprite sprite,float x,float y,float w,float h,bool sliced=true)
    {
        var image=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;image.preserveAspect=!sliced;
        image.type=sliced&&sprite!=null&&sprite.border!=Vector4.zero?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.pixelsPerUnitMultiplier=3.5f;return image;
    }
    private static TMP_Text Label(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
    {
        var text=Rect(parent,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;text.text=value;text.color=color;
        text.alignment=alignment;text.raycastTarget=false;text.enableWordWrapping=true;text.overflowMode=TextOverflowModes.Ellipsis;return text;
    }
    private static Button Button(Transform parent,string name,float x,float y,float w,float h,string value,int size)
    {
        var image=Image(parent,name,null,x,y,w,h);image.color=new Color(.13f,.18f,.20f);image.raycastTarget=true;
        var outline=image.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(1,-1);outline.effectColor=new Color(.72f,.58f,.36f);
        var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
        Label(image.transform,"Label",value,8,4,w-16,h-8,size,Ivory,TextAlignmentOptions.Center);return button;
    }
    private static void Array(SerializedObject obj,string name,Sprite[] values)
    {var prop=obj.FindProperty(name);prop.arraySize=values.Length;for(int i=0;i<values.Length;i++)prop.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
    private static void Bind(SerializedObject obj,string name,Object value)=>obj.FindProperty(name).objectReferenceValue=value;
    private static void Bind(SerializedProperty obj,string name,Object value)=>obj.FindPropertyRelative(name).objectReferenceValue=value;
}
