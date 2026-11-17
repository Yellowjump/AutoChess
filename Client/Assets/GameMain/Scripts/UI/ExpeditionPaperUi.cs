using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;

internal sealed class ExpeditionPaperUi
{
    public static readonly Color Ink=new Color(.18f,.16f,.12f), Muted=new Color(.43f,.39f,.31f),
        Paper=new Color(.97f,.94f,.87f), Jade=new Color(.30f,.43f,.34f), Red=new Color(.55f,.22f,.17f);
    private readonly RectTransform root;
    private readonly TMP_FontAsset font;
    private readonly ExpeditionFormationStyle style;
    private readonly Dictionary<int,Sprite> icons=new Dictionary<int,Sprite>();
    public ExpeditionPaperUi(GameObject root,TMP_FontAsset fallback)
    {
        this.root=(RectTransform)root.transform;
        style=Resources.Load<ExpeditionFormationStyle>("ExpeditionFormationStyle");
        font=style.HeadingFont(fallback);
    }
    public static GameObject Canvas(string name)
    {
        var root=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=31000;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        return root;
    }
    public RectTransform Rect(string name,float x,float y,float w,float h,Transform parent=null)
    {
        var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rect.gameObject.layer=5;
        rect.SetParent(parent??root,false);rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up;
        rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);return rect;
    }
    public Image Box(string name,float x,float y,float w,float h,Color color,Transform parent=null)
    {var image=Rect(name,x,y,w,h,parent).gameObject.AddComponent<Image>();image.color=color;return image;}
    public Image Page(string name,float x,float y,float w,float h)
    {
        var rect=Rect(name,x,y,w,h);
        var paper=rect.gameObject.AddComponent<RawImage>();paper.texture=style.PaperTexture;
        paper.uvRect=new Rect(.09f,.61f,.32f,.29f);
        var image=Box("PaperBorder",0,0,w,h,Color.white,rect);image.sprite=style.PaperFrame;
        image.type=Image.Type.Sliced;image.fillCenter=false;image.pixelsPerUnitMultiplier=1.5f;
        image.raycastTarget=false;return image;
    }
    public TMP_Text Text(string name,string text,float x,float y,float w,float h,float size,Color? color=null)
    {
        var label=Rect(name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=font;label.text=text;label.fontSize=size;label.color=color??Ink;
        label.raycastTarget=false;label.overflowMode=TextOverflowModes.Ellipsis;return label;
    }
    public Button Button(string name,string text,float x,float y,float w,float h,Action click,Color? fill=null)
    {
        var image=Box(name,x,y,w,h,fill??Color.white);image.sprite=style.PaperFrame;
        image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4;
        var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        var label=Text(name+"Label",text,6,4,w-12,h-8,28,fill.HasValue?Paper:Ink);
        label.transform.SetParent(image.transform,false);label.alignment=TextAlignmentOptions.Center;
        button.onClick.AddListener(()=>click());UIFormLogic.BindButtonClickSound(button);return button;
    }
    public Image Picture(string name,Sprite sprite,float x,float y,float w,float h,Transform parent=null)
    {
        var image=Rect(name,x,y,w,h,parent).gameObject.AddComponent<Image>();
        image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;image.enabled=sprite!=null;return image;
    }
    public void Line(float x,float y,float width)
    {Box("InkRule",x,y,width,1,new Color(.38f,.31f,.22f,.4f)).raycastTarget=false;}
    public void MarkSelection(Button button,bool selected)
    {
        button.targetGraphic.color=Color.white;
        var marker=button.transform.Find("SelectedCorners");
        if(marker==null && selected)
        {
            var rect=Rect("SelectedCorners",0,0,0,0,button.transform);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            rect.gameObject.AddComponent<CanvasRenderer>();
            var frame=rect.gameObject.AddComponent<ExpeditionInkFrame>();
            frame.color=Red;frame.CornersOnly=true;frame.raycastTarget=false;marker=rect;
        }
        if(marker!=null)marker.gameObject.SetActive(selected);
    }
    public Sprite ItemIcon(int id)
    {
        if(icons.TryGetValue(id,out var existing))return existing;
        var texture=Resources.Load<Texture2D>("TeamEquipmentIcons");
        int index=ExpeditionEquipment.Item(id).Icon;
        var sprite=Sprite.Create(texture,new Rect(index%5*texture.width/5f,(1-index/5)*texture.height/2f,
            texture.width/5f,texture.height/2f),new Vector2(.5f,.5f),100);
        icons.Add(id,sprite);return sprite;
    }
    public Sprite Portrait(int role)
    {
        string name=role switch{-1=>"Leader",0=>"Shield",1=>"Swordsman",2=>"Archer",_=>"Healer"};
        return Resources.Load<Sprite>("FormationReference/Detail"+name);
    }
    public void Dispose(){foreach(var sprite in icons.Values)UnityEngine.Object.Destroy(sprite);}
}
