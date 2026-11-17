using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Entity;
using SkillSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

// Paper military roster over the existing arena, using real combat cells and models.
public sealed class ExpeditionBattleFormation : MonoBehaviour
{
    private WorldTimelineDebugView view;
    private TMP_FontAsset heading;
    private ExpeditionFormationStyle style;
    private ExpeditionFormationVfx vfx;
    private int selected, nodeId;
    private bool playerFirst, confirmed, restoreDebugger;
    private readonly Dictionary<int, EntityQizi> models = new Dictionary<int, EntityQizi>();
    private readonly Dictionary<int, ActorState> actors = new Dictionary<int, ActorState>();
    private readonly List<Card> cards = new List<Card>();
    private readonly List<(RectTransform Rect, EntityQizi Unit)> tags = new List<(RectTransform, EntityQizi)>();
    private readonly List<(RectTransform Rect, EntityQizi Unit)> handles = new List<(RectTransform, EntityQizi)>();
    private readonly Button[] slots = new Button[6];
    private TMP_Text instruction, count, dragLabel, detailName, detailRole, health, attack, ability, details, historyNote;
    private Button bench;
    private readonly Dictionary<int,int> benchedCells=new Dictionary<int,int>();
    private Image portrait;
    private Material portraitInk;
    private readonly List<Material> paperMaterials = new List<Material>();
    private readonly Dictionary<string,Sprite> referenceSprites = new Dictionary<string,Sprite>();
    private readonly List<Sprite> cardPortraits = new List<Sprite>();
    private RectTransform root;
    private string feedback;
    private ExpeditionParty Party => view.Expedition;
    private const float PageEdge = 1226, HeaderHeight = 84, RosterTop = 745;
    private static readonly Color Ink=new Color(.15f,.13f,.10f), Muted=new Color(.35f,.33f,.27f),
        Paper=new Color(.96f,.93f,.87f), Jade=new Color(.36f,.44f,.37f),
        Cinnabar=new Color(.55f,.20f,.16f), Rule=new Color(.43f,.36f,.27f,.62f),
        Brass=new Color(.82f,.69f,.45f);
    private sealed class Card
    {
        public int Id;
        public Button Button;
        public Image Stamp, Selection;
    }
    public bool Confirmed => confirmed;

    public static IEnumerator Present(WorldTimelineDebugView view, TMP_FontAsset font, int node,
        IReadOnlyList<ActorState> actors, IReadOnlyList<EntityQizi> entities)
    {
        var go=new GameObject("战场布阵",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=30000;
        var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1672,941); scaler.matchWidthOrHeight=.5f;
        var panel=go.AddComponent<ExpeditionBattleFormation>();
        panel.view=view;panel.nodeId=node;panel.root=(RectTransform)go.transform;
        panel.style=Resources.Load<ExpeditionFormationStyle>("ExpeditionFormationStyle");
        panel.heading=panel.style!=null?panel.style.HeadingFont(font):font;
        panel.selected=view.Simulation.State.PlayerActorId;
        panel.playerFirst=entities[actors.ToList().FindIndex(a=>a.Id==panel.selected)].BelongCamp==CampType.Friend;
        for(int i=0;i<actors.Count;i++){panel.models.Add(actors[i].Id,entities[i]);panel.actors.Add(actors[i].Id,actors[i]);}
        panel.restoreDebugger=HotGameEntry.Debugger!=null&&HotGameEntry.Debugger.ActiveWindow;
        if(panel.restoreDebugger)HotGameEntry.Debugger.ActiveWindow=false;
        view.ExpeditionPanelOpen=true;
        try
        {
            panel.vfx=new ExpeditionFormationVfx(panel.playerFirst);panel.Build();panel.Refresh();
            while(!panel.confirmed&&view!=null)
            {
                HotGameEntry.HeroManager.UpdateNoBattle(Time.deltaTime,Time.unscaledDeltaTime);
                foreach(var unit in entities)if(unit.HpBar?.HpBarObj!=null)unit.HpBar.HpBarObj.SetActive(unit.GObj.activeSelf);
                yield return null;
            }
        }
        finally{if(go!=null){go.SetActive(false);Destroy(go);}}
    }

    private void Build()
    {
        // The opaque book margins are later siblings, blocking battlefield clicks beneath them.
        foreach(int id in actors.Keys.Where(Party.Owns))
        {
            int actorId=id;
            var handle=Button("Piece"+id,"",0,0,90,145,()=>Select(actorId),Color.clear);
            AddDrag(handle,()=>{Select(actorId);return true;});
            handles.Add(((RectTransform)handle.transform,models[id]));
        }
        for(int i=0;i<6;i++)
        {
            int cell=i;
            slots[i]=Button("FormationCell"+i,"",0,0,144,84,()=>PlaceSelected(cell),Color.clear);
            AddDrag(slots[i],()=>{int id=Party.Cells[cell];if(id!=0)Select(id);return id!=0;});
        }
        foreach(int id in actors.Keys)
        {
            int actorId=id;
            bool own = Party.Owns(id);
            var tag=Button((own?"Ally":"Opponent")+id,Name(id),0,0,own?97:136,35,()=>Select(actorId),Paper);
            UsePaperFrame(tag, own ? Color.white : new Color(.92f,.90f,.84f));
            var text=tag.GetComponentInChildren<TMP_Text>();
            text.fontSize=22;
            if(own)text.fontStyle=FontStyles.Bold;
            if(Party.Owns(id))AddDrag(tag,()=>{Select(actorId);return true;});
            tags.Add(((RectTransform)tag.transform,models[id]));
        }
        ReferencePicture("Header", "Header", 0, 0, 1672, HeaderHeight, true);
        var location=Label("Location",view.LocationName(view.Simulation.State.Nodes[nodeId]),373,17,225,48,32);
        location.enableAutoSizing=true;location.fontSizeMin=23;location.fontSizeMax=32;
        count=Label("Count","",1410,17,245,46,32);
        count.alignment=TextAlignmentOptions.Right;
        count.enableAutoSizing=true;
        count.fontSizeMin=24;
        count.fontSizeMax=32;

        ReferencePicture("DetailsPage", "DetailsPaper", PageEdge, HeaderHeight, 446, RosterTop-HeaderHeight, true);
        portrait=Picture(root,"SelectedPortrait",null,1449,112,223,482);
        portrait.preserveAspect=false;
        portraitInk=PaperMaterial(new Vector4(.13f,.13f,0,.01f));
        portrait.material=portraitInk;
        detailName=Label("SelectedName","",1266,122,186,75,58);
        detailName.enableAutoSizing=true;detailName.fontSizeMin=31;detailName.fontSizeMax=58;
        detailName.fontStyle=FontStyles.Bold;
        ReferencePicture("RoleStamp","RoleStamp",1260,197,98,42);
        detailRole=Label("SelectedRole","",1264,201,90,36,29,Paper);
        detailRole.alignment=TextAlignmentOptions.Center;
        health=Label("Health","",1267,259,190,36,25);
        Line("HealthRule",1268,293,155,1);
        attack=Label("Attack","",1267,305,170,37,26);
        Line("AttackRule",1268,339,155,1);
        ReferencePicture("AbilityWash","AbilityWash",1256,383,153,39);
        ability=Label("AbilityName","",1270,381,235,45,32);
        ability.fontStyle=FontStyles.Bold;
        details=Label("Details","",1267,435,210,88,24);
        details.enableAutoSizing=true;details.fontSizeMin=21;details.fontSizeMax=24;
        bench=Button("Bench","下阵候补",1271,527,178,50,ToggleSelectedDeployment,Color.white);
        bench.image.sprite=style.PaperFrame;bench.image.type=Image.Type.Sliced;bench.image.pixelsPerUnitMultiplier=4;
        ReferencePicture("HistoryBook","Archive",1245,617,111,82);
        Line("HistoryRuleTop",1355,628,303,1);
        Line("HistoryRuleBottom",1260,694,397,1);
        historyNote=Label("HistoryNote","",1367,639,284,54,25);
        historyNote.alignment=TextAlignmentOptions.MidlineLeft;

        ReferencePicture("RosterBackground","RosterPaper",0,RosterTop,1672,196,true);
        int n=0;
        foreach(int id in actors.Keys.Where(Party.Owns))BuildCard(id,235+n++*200);
        if(n<5)Label("RecruitHint","循地图寻访\n结识更多同行",253+n*200,810,165,63,24,Muted);
        var confirm=Button("Confirm","",1270,782,387,86,()=>confirmed=true,Color.white);
        confirm.image.sprite=ReferenceSprite("Confirm");
        instruction=Label("Instructions","",22,709,1168,32,20,Paper);
        instruction.gameObject.AddComponent<Shadow>().effectColor=new Color(.1f,.1f,.08f,.8f);
        dragLabel=Label("Dragging","",0,0,235,47,29,Cinnabar);
        dragLabel.gameObject.SetActive(false);
    }
    private void BuildCard(int id,float x)
    {
        var button=Button("Member"+(id==Party.LeaderId?0:ExpeditionParty.Role(id)+1),"",x,770,194,138,()=>Select(id),new Color(1,1,1,.12f));
        var rect=(RectTransform)button.transform;
        Picture(rect,"Portrait",Portrait(id),7,10,102,114).preserveAspect=false;
        var name=Label("Name",Name(id),122,15,69,42,31);name.transform.SetParent(rect,false);
        name.fontStyle=FontStyles.Bold;
        name.enableAutoSizing=true;name.fontSizeMin=21;name.fontSizeMax=29;
        var role=Label("Role",RoleName(id),122,59,69,33,24,Muted);role.transform.SetParent(rect,false);
        var stamp=Picture(rect,"StatusStamp",ReferenceSprite("Deployed"),122,94,61,27);
        Frame(rect,Rule);
        var selection=Picture(rect,"SelectedCorners",ReferenceSprite("SelectedCorners"),0,0,194,138);
        selection.preserveAspect=false;
        cards.Add(new Card{Id=id,Button=button,Selection=selection,Stamp=stamp});
        AddDrag(button,()=>{Select(id);return true;});
    }
    private void AddDrag(Button button,Func<bool> begin)
    {
        var drag=button.gameObject.AddComponent<ExpeditionFormationDrag>();drag.Begin=begin;drag.Move=Drag;drag.End=Drop;
    }
    private string Name(int id)
    {
        if(id==Party.LeaderId)return view.PlayerName;
        if(ExpeditionParty.IsCompanion(id))return view.CompanionName(ExpeditionParty.Role(id)).Split('·')[0].Trim();
        return view.Simulation.State.HistoricalExpeditions.ContainsKey(id)?"前世修士":"敌方守军";
    }
    private string RoleName(int id)=>ExpeditionParty.Role(id) switch
    {0=>"盾卫",1=>"剑客",2=>"弓手",3=>"医者",_=>Party.Owns(id)?"主角":"敌方"};
    private Sprite Portrait(int id)
    {
        var full=DetailPortrait(id);
        if(full==null)return null;
        Rect bounds=full.rect;
        // Crop the new illustration so the card and detail show the same pose.
        Rect crop=ExpeditionParty.Role(id) switch
        {
            2=>new Rect(.4f,.67f,.56f,.29f),
            3=>new Rect(.35f,.66f,.5f,.30f),
            _=>new Rect(.25f,.70f,.5f,.28f)
        };
        Rect face=new Rect(bounds.x+bounds.width*crop.x,bounds.y+bounds.height*crop.y,
            bounds.width*crop.width,bounds.height*crop.height);
        var sprite=Sprite.Create(full.texture,face,new Vector2(.5f,.5f),full.pixelsPerUnit);
        cardPortraits.Add(sprite);
        return sprite;
    }
    private Sprite DetailPortrait(int id)
    {
        if(id==Party.LeaderId)return ReferenceSprite("DetailLeader");
        return ExpeditionParty.Role(id) switch
        {
            0=>ReferenceSprite("DetailShield"),
            1=>ReferenceSprite("DetailSwordsman"),
            2=>ReferenceSprite("DetailArcher"),
            3=>ReferenceSprite("DetailHealer"),
            _=>null
        };
    }
    private void Select(int id){selected=id;feedback=null;Refresh();}
    private void ToggleSelectedDeployment()
    {
        if(!Party.Owns(selected)||selected==Party.LeaderId)return;
        int previous=Party.CellOf(selected);
        if(previous>=0)
        {
            if(view.BenchCompanion(selected)){benchedCells[selected]=previous;feedback="已下阵候补 · 点击上阵可回到空闲阵位";Refresh();}
            return;
        }
        if(Party.DeployedCount>=ExpeditionParty.Capacity){feedback="阵容已满 · 先让一位同伴下阵";Refresh();return;}
        int cell=benchedCells.TryGetValue(selected,out int remembered)&&Party.Cells[remembered]==0?remembered:-1;
        if(cell<0)
        {
            var order=ExpeditionParty.Role(selected)<2?new[]{1,0,2,4,3,5}:new[]{4,3,5,1,0,2};
            cell=order.FirstOrDefault(c=>Party.Cells[c]==0);
        }
        PlaceSelected(cell);
    }
    public bool PlaceSelected(int cell)
    {
        bool placed=Party.Owns(selected)&&view.PlaceCompanion(selected,cell);
        feedback=placed?"阵位已定 · 准备好后，开始战斗":"只可调整本世队伍 · 至多三人上阵，主角须在场";
        Refresh();return placed;
    }
    private void Refresh()
    {
        var manager=HotGameEntry.HeroManager;
        foreach(var pair in models.Where(p=>Party.Owns(p.Key)))manager.ReleaseFootprint(pair.Value);
        foreach(var pair in models.Where(p=>Party.Owns(p.Key)))
        {
            int cell=Party.CellOf(pair.Key);var unit=pair.Value;unit.GObj.SetActive(cell>=0);
            if(cell<0){unit.LogicPosition=new Vector3(0,-1000,0);continue;}
            var position=ExpeditionBattleSetup.Position(cell,playerFirst);
            if(!manager.TryPlace(unit,position))throw new InvalidOperationException("布阵位置被占用。");
            unit.SavePos=position;unit.LogicPosition=manager.GetGeziPos(position.y,position.x);
        }
        foreach(var card in cards)
        {
            bool deployed=Party.CellOf(card.Id)>=0;
            card.Stamp.sprite=ReferenceSprite(deployed?"Deployed":"Reserve");
            card.Selection.gameObject.SetActive(card.Id==selected);
            card.Button.targetGraphic.color=card.Id==selected?new Color(1,1,1,.37f):new Color(1,1,1,.10f);
        }
        count.text=$"本世上阵  {Party.DeployedCount} / 3";
        instruction.text=feedback??(view.Simulation.State.Nodes[nodeId].IsExpeditionVanguard?view.VanguardFormationHint:
            "前排迎敌 · 后排策应    拖动棋子布阵，点击敌人查看详情");
        var chosen=actors[selected];detailName.text=Name(selected);detailRole.text=RoleName(selected);
        portrait.sprite=DetailPortrait(selected);
        portrait.enabled=portrait.sprite!=null;
        if(portrait.sprite!=null && portraitInk!=null)
        {
            Vector4 uv=DataUtility.GetOuterUV(portrait.sprite);
            portraitInk.SetVector("_UvArea",new Vector4(uv.x,uv.y,uv.z-uv.x,uv.w-uv.y));
        }
        health.text=$"气血  {chosen.Health} / {chosen.MaxHealth}";
        attack.text=$"攻击  {chosen.CombatAttack}";
        int role=ExpeditionParty.Role(selected);
        ability.text=role switch{0=>"近身牵制",1=>"近战突破",2=>"远程输出",3=>"悬壶济世",_=>Party.Owns(selected)?"本世修行":"知己知彼"};
        details.text=role switch
        {
            0=>"前排接敌，吸引敌人。\n定期起盾，护住同伴。",
            1=>"攻击附带流血。\n配合盾卫，集中突破一侧。",
            2=>"射程六格，气血较低。\n后排策应，远程输出。",
            3=>"每三秒治疗附近\n最虚弱的同伴。",
            _=>Party.Owns(selected)?"携当前装备与功法入阵。\n前后站位决定接敌顺序。":"敌方站位已定。\n可查看敌情，不能调动。"
        };
        bool historical=view.Simulation.State.HistoricalExpeditions.Values.Any(p=>p.Owns(selected));
        historyNote.text=historical?"前世队伍\n按旧阵自动参战":Party.Owns(selected)?"此战队伍与阵位\n将随本世一同留存":"审敌之势\n再定本世之阵";
        bool own=Party.Owns(selected),deployedSelected=Party.CellOf(selected)>=0,full=Party.DeployedCount>=ExpeditionParty.Capacity;
        bench.interactable=own&&selected!=Party.LeaderId&&(deployedSelected||!full);
        bench.GetComponentInChildren<TMP_Text>().text=!own?"敌方阵容":selected==Party.LeaderId?"主角须在场":
            deployedSelected?"下阵候补":full?"阵容已满":"上阵";
        vfx.Refresh(Party,selected);
        if(heading.atlasPopulationMode!=AtlasPopulationMode.Static)
            heading.TryAddCharacters(string.Concat(GetComponentsInChildren<TMP_Text>(true).Select(t=>t.text)));
    }
    private Vector2 Project(Vector3 world)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root,Camera.main.WorldToScreenPoint(world),null,out var point);return point;
    }
    private void LateUpdate()
    {
        if(view==null||Camera.main==null)return;
        for(int i=0;i<6;i++)
        {
            var pos=ExpeditionBattleSetup.Position(i,playerFirst);
            Anchor((RectTransform)slots[i].transform,Project(HotGameEntry.HeroManager.GetGeziPos(pos.y,pos.x)));
        }
        foreach(var tag in tags)
        {
            tag.Rect.gameObject.SetActive(tag.Unit.GObj.activeSelf);
            Anchor(tag.Rect,Project(tag.Unit.LogicPosition+Vector3.up*1.2f));
        }
        foreach(var handle in handles)
        {
            handle.Rect.gameObject.SetActive(handle.Unit.GObj.activeSelf);
            Anchor(handle.Rect,Project(handle.Unit.LogicPosition+Vector3.up*.55f));
        }
    }
    private static void Anchor(RectTransform rect,Vector2 point)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=point;}
    private int DropCell(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screen,null,out var point);
        var topLeft=point+new Vector2(root.rect.width*.5f,-root.rect.height*.5f);
        if(topLeft.x>=PageEdge||-topLeft.y>=RosterTop||-topLeft.y<=HeaderHeight)return -1;
        for(int i=0;i<6;i++)if(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)slots[i].transform,screen))return i;
        return -1;
    }
    private void Drag(Vector2 screen)
    {
        dragLabel.gameObject.SetActive(true);dragLabel.text=Name(selected);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screen,null,out var point);
        Anchor(dragLabel.rectTransform,point+new Vector2(88,32));vfx.Refresh(Party,selected,DropCell(screen));
    }
    private void Drop(Vector2 screen)
    {
        dragLabel.gameObject.SetActive(false);
        int cell=DropCell(screen);if(cell>=0){PlaceSelected(cell);return;}
        feedback="请将同伴放入战场中的圆形阵位";Refresh();
    }
    private void OnDestroy()
    {
        vfx?.Dispose();
        foreach(var material in paperMaterials)Destroy(material);
        foreach(var sprite in cardPortraits)Destroy(sprite);
        if(view!=null)view.ExpeditionPanelOpen=false;
        if(restoreDebugger&&HotGameEntry.Debugger!=null)HotGameEntry.Debugger.ActiveWindow=true;
    }
    private RectTransform Rect(string name,float x,float y,float w,float h,Transform parent=null)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;
        var rect=(RectTransform)go.transform;rect.SetParent(parent??transform,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);return rect;
    }
    private RectTransform Box(string name,float x,float y,float w,float h,Color color)
    {var r=Rect(name,x,y,w,h);r.gameObject.AddComponent<Image>().color=color;return r;}
    private void Line(string name,float x,float y,float w,float h)
    {Box(name,x,y,w,h,Rule).GetComponent<Image>().raycastTarget=false;}
    private Sprite ReferenceSprite(string name)
    {
        if(referenceSprites.TryGetValue(name,out var sprite))return sprite;
        sprite=Resources.Load<Sprite>("FormationReference/"+name);
        if(sprite==null)throw new InvalidOperationException("布阵参考素材未载入："+name);
        referenceSprites.Add(name,sprite);
        return sprite;
    }
    private Image ReferencePicture(string name,string asset,float x,float y,float width,float height,bool blocksPointer=false)
    {
        var image=Picture(root,name,ReferenceSprite(asset),x,y,width,height);
        image.preserveAspect=false;
        image.raycastTarget=blocksPointer;
        return image;
    }
    private Material PaperMaterial(Vector4 feather,bool brush=false)
    {
        var shader=Resources.Load<Shader>("FormationPaper");
        if(shader==null||!shader.isSupported)return null;
        var material=new Material(shader);
        material.SetVector("_Fade",feather);
        material.SetFloat("_Brush",brush?1:0);
        paperMaterials.Add(material);
        return material;
    }
    private void UsePaperFrame(Button button,Color color)=>UsePaperFrame((Image)button.targetGraphic,color);
    private void UsePaperFrame(Image image,Color color)
    {
        if(style?.PaperFrame==null)return;
        image.sprite=style.PaperFrame;
        image.color=color;
        image.type=Image.Type.Sliced;
        image.pixelsPerUnitMultiplier=4;
    }
    private Image Picture(Transform parent,string name,Sprite sprite,float x,float y,float w,float h)
    {
        var image=Rect(name,x,y,w,h,parent).gameObject.AddComponent<Image>();
        image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;return image;
    }
    private TMP_Text Label(string name,string text,float x,float y,float w,float h,float size,Color? color=null)
    {
        var t=Rect(name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=heading;t.text=text;t.fontSize=size;
        t.color=color??Ink;t.raycastTarget=false;t.overflowMode=TextOverflowModes.Ellipsis;return t;
    }
    private Button Button(string name,string text,float x,float y,float w,float h,Action click,Color color)
    {
        var r=Box(name,x,y,w,h,color);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
        b.navigation=new Navigation{mode=Navigation.Mode.None};
        var t=Label(name+"Label",text,0,0,w-12,h-8,26);t.transform.SetParent(r,false);
        t.rectTransform.anchoredPosition=new Vector2(6,-4);t.alignment=TextAlignmentOptions.Center;
        b.onClick.AddListener(()=>click());UIFormLogic.BindButtonClickSound(b);return b;
    }
    private ExpeditionInkFrame Frame(RectTransform parent,Color color)
    {
        var rect=Rect("InkFrame",0,0,parent.rect.width,parent.rect.height,parent);
        rect.anchorMin=Vector2.zero;
        rect.anchorMax=Vector2.one;
        rect.offsetMin=rect.offsetMax=Vector2.zero;
        rect.gameObject.AddComponent<CanvasRenderer>();
        var frame=rect.gameObject.AddComponent<ExpeditionInkFrame>();frame.color=color;frame.raycastTarget=false;return frame;
    }
}

// Crisp vector rules and restrained cinnabar corner marks.
public sealed class ExpeditionInkFrame : MaskableGraphic
{
    private bool cornersOnly;
    public bool DoubleRule;
    public bool CornersOnly{get=>cornersOnly;set{if(cornersOnly!=value){cornersOnly=value;SetVerticesDirty();}}}
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var r=rectTransform.rect;
        Draw(2,cornersOnly?4:DoubleRule?2.5f:1.5f);
        if(DoubleRule||cornersOnly)Draw(7,1.5f);
        void Draw(float pad,float width)
        {
            float left=r.xMin+pad,right=r.xMax-pad,bottom=r.yMin+pad,top=r.yMax-pad;
            float h=cornersOnly?22:right-left,v=cornersOnly?22:top-bottom;
            Quad(left,top-width,h,width);Quad(right-h,bottom,h,width);
            Quad(left,bottom,width,v);Quad(right-width,top-v,width,v);
            if(cornersOnly){Quad(right-h,top-width,h,width);Quad(left,bottom,h,width);Quad(left,top-v,width,v);Quad(right-width,bottom,width,v);}
        }
        void Quad(float x,float y,float w,float h)
        {
            int i=mesh.currentVertCount;
            mesh.AddVert(new Vector3(x,y),color,Vector2.zero);mesh.AddVert(new Vector3(x,y+h),color,Vector2.zero);
            mesh.AddVert(new Vector3(x+w,y+h),color,Vector2.zero);mesh.AddVert(new Vector3(x+w,y),color,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
    }
}
public sealed class ExpeditionFormationDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Func<bool> Begin; public Action<Vector2> Move,End; private bool dragging;
    public void OnBeginDrag(PointerEventData data){dragging=Begin?.Invoke()==true;if(dragging)Move?.Invoke(data.position);}
    public void OnDrag(PointerEventData data){if(dragging)Move?.Invoke(data.position);}
    public void OnEndDrag(PointerEventData data){if(dragging)End?.Invoke(data.position);dragging=false;}
}
