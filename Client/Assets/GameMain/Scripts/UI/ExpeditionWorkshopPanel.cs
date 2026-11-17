using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

public sealed class ExpeditionWorkshopPanel : MonoBehaviour
{
    private WorldTimelineDebugView view;
    private ExpeditionPaperUi ui;
    private int tab,selected,page;
    private TMP_Text purse,subtitle,heading,effect,next,cost,feedback,pageLabel;
    private Image icon;
    private Button confirm,shopTab,upgradeTab;
    private readonly List<GameObject> rows=new List<GameObject>();
    private ExpeditionEquipment Gear=>view.Expedition.Equipment;
    public static IEnumerator Present(WorldTimelineDebugView view,TMP_FontAsset font)
    {
        var root=ExpeditionPaperUi.Canvas(view.EquipmentServiceMerchant?"行商与淬炼":"机缘淬炼");
        var panel=root.AddComponent<ExpeditionWorkshopPanel>();panel.view=view;panel.ui=new ExpeditionPaperUi(root,font);
        panel.tab=view.EquipmentServiceMerchant?0:1;panel.selected=panel.tab==0?5:-1;
        bool restore=HotGameEntry.Debugger!=null&&HotGameEntry.Debugger.ActiveWindow;
        if(restore)HotGameEntry.Debugger.ActiveWindow=false;
        view.InventoryOpen=true;view.Closed+=view.CloseEquipmentService;
        try
        {
            panel.Build();panel.Refresh();
            while(view!=null && view.EquipmentServiceOpen)yield return null;
        }
        finally
        {
            if(view!=null){view.InventoryOpen=false;view.Closed-=view.CloseEquipmentService;}
            if(root!=null){root.SetActive(false);Destroy(root);}
            if(restore&&HotGameEntry.Debugger!=null)HotGameEntry.Debugger.ActiveWindow=true;
        }
    }
    private void Build()
    {
        ui.Box("Backdrop",0,0,1920,1080,new Color(.09f,.10f,.08f,.89f));ui.Page("WorkshopLedger",80,55,1760,970);
        ui.Text("Title",view.EquipmentServiceMerchant?"山行商铺":"机缘 · 淬炼",145,98,900,82,57).fontStyle=FontStyles.Bold;
        purse=ui.Text("SpiritStones","",1260,111,480,55,35,ExpeditionPaperUi.Jade);purse.alignment=TextAlignmentOptions.Right;
        subtitle=ui.Text("Subtitle","",147,190,1380,65,28,ExpeditionPaperUi.Muted);
        ui.Button("LeaveWorkshop",view.EquipmentServiceMerchant?"离开商铺":"继续前行",1510,924,248,63,view.CloseEquipmentService);
        ui.Line(145,272,1610);
        if(view.EquipmentServiceMerchant)
        {
            shopTab=ui.Button("ShopTab","购置行装",146,294,224,57,()=>{tab=0;page=0;selected=5;Refresh();});
            upgradeTab=ui.Button("UpgradeTab","法器与道具升级",389,294,296,57,()=>{tab=1;page=0;selected=-1;Refresh();});
        }
        else ui.Text("EventBenefit","灵石 +12 · 自选一件免费升阶",149,296,690,54,34,ExpeditionPaperUi.Jade);
        ui.Button("WorkshopPrevious","上页",731,303,88,43,()=>{page--;Refresh();});
        pageLabel=ui.Text("WorkshopPage","",827,309,85,34,23);pageLabel.alignment=TextAlignmentOptions.Center;
        ui.Button("WorkshopNext","下页",919,303,88,43,()=>{page++;Refresh();});
        ui.Line(1041,295,1);
        icon=ui.Picture("WorkshopItemIcon",null,1259,328,270,195);
        heading=ui.Text("WorkshopItemName","",1114,545,626,67,44);heading.alignment=TextAlignmentOptions.Center;
        effect=ui.Text("CurrentEffect","",1112,633,628,52,29);effect.alignment=TextAlignmentOptions.Center;
        next=ui.Text("UpgradePreview","",1111,697,630,75,29,ExpeditionPaperUi.Jade);next.alignment=TextAlignmentOptions.Center;
        cost=ui.Text("WorkshopCost","",1110,787,630,48,28,ExpeditionPaperUi.Muted);cost.alignment=TextAlignmentOptions.Center;
        confirm=ui.Button("WorkshopConfirm","",1200,848,445,65,Act,ExpeditionPaperUi.Red);
        feedback=ui.Text("WorkshopFeedback","",148,927,1290,65,26,ExpeditionPaperUi.Muted);
    }
    private string Owner(int role)=>role==0?view.PlayerName:view.CompanionName(role-1).Split('·')[0].Trim();
    private IEnumerable<int> Targets()
    {
        yield return -1;
        for(int r=0;r<4;r++)if(view.Expedition.HasRecruited(r))yield return -r-2;
        foreach(var item in Gear.Instances)yield return item.Id;
    }
    private void Refresh()
    {
        foreach(var row in rows){row.SetActive(false);Destroy(row);}rows.Clear();
        purse.text=$"灵石  {Gear.SpiritStones}";
        subtitle.text=view.EquipmentServiceTitle+"  /  "+(tab==0?"行装不限职业与槽位，购得后收入共享行囊":
            view.EquipmentServiceMerchant?"选择一件本命法器或行装，消耗灵石升阶":"此处机缘仅此一次；为任意一位同伴的法器或道具免费升一阶");
        if(shopTab!=null){ui.MarkSelection(shopTab,tab==0);ui.MarkSelection(upgradeTab,tab==1);}
        int[] keys=tab==0?ExpeditionEquipment.Definitions.Where(d=>!d.Innate).Select(d=>d.Id).ToArray():Targets().ToArray();
        if(!keys.Contains(selected))selected=keys.FirstOrDefault();
        int total=Math.Max(1,(keys.Length+5)/6);page=Mathf.Clamp(page,0,total-1);pageLabel.text=$"{page+1}/{total}";
        var visible=keys.Skip(page*6).Take(6).ToArray();
        for(int i=0;i<visible.Length;i++)
        {
            int key=visible[i];var d=tab==0?ExpeditionEquipment.Definition(key):Gear.UpgradeDefinition(key);
            int level=tab==0?1:Gear.UpgradeLevel(key);float x=147+i%2*440,y=387+i/2*170;
            var row=ui.Button("WorkshopItem"+key,"",x,y,419,151,()=>{selected=key;Refresh();});rows.Add(row.gameObject);ui.MarkSelection(row,selected==key);
            ui.Picture("Icon",ui.ItemIcon(d.Id),13,16,111,110,row.transform);
            var title=ui.Text("Name",d.Name,141,17,265,39,29);title.transform.SetParent(row.transform,false);
            string owner=tab==0?$"售价 {d.Price} 灵石":key<0?Owner(-key-1)+" · 本命":
                Gear.Instance(key).Owner<0?"共享行囊":Owner(Gear.Instance(key).Owner)+" · 行装 "+(Gear.Instance(key).Slot+1);
            var where=ui.Text("Owner",owner,141,65,267,30,22,ExpeditionPaperUi.Muted);where.transform.SetParent(row.transform,false);
            var lv=ui.Text("Level",$"{level} / {d.MaxLevel} 阶"+(level>=d.MaxLevel?" · 已满阶":""),141,105,260,30,23,ExpeditionPaperUi.Jade);lv.transform.SetParent(row.transform,false);
        }
        var item=tab==0?ExpeditionEquipment.Definition(selected):Gear.UpgradeDefinition(selected);
        if(item==null){confirm.interactable=false;return;}
        int current=tab==0?1:Gear.UpgradeLevel(selected);bool max=current>=item.MaxLevel;
        icon.sprite=ui.ItemIcon(item.Id);icon.enabled=true;heading.text=item.Name+$"  {current} / {item.MaxLevel} 阶";
        effect.text="当前  "+item.Effect(current);
        next.text=tab==0?"通用行装 · 任意同伴 · 任意行装槽":max?"已达升阶上限":$"下一阶  {item.Effect(current+1)}";
        int price=tab==0?item.Price:item.UpgradePrice(current);
        bool free=tab==1&&!view.EquipmentServiceMerchant;
        cost.text=free?(view.EventUpgradeAvailable?"机缘赠予 · 本次免费":"本次免费升阶已使用"):tab==1&&max?"满阶后无法继续升级":$"需要 {price} 灵石";
        confirm.interactable=tab==0?Gear.SpiritStones>=price&&Gear.Instances.Count<ExpeditionEquipment.MaxInstances:
            Gear.CanUpgrade(selected)&&(free?view.EventUpgradeAvailable:Gear.SpiritStones>=price);
        confirm.GetComponentInChildren<TMP_Text>().text=tab==0?(confirm.interactable?"购入行囊":"灵石不足"):max?"已满阶":
            free?(view.EventUpgradeAvailable?"免费升一阶":"机缘已用尽"):confirm.interactable?"消耗灵石 · 升阶":"灵石不足";
    }
    private void Act()
    {
        var d=tab==0?ExpeditionEquipment.Definition(selected):Gear.UpgradeDefinition(selected);
        bool done=tab==0?view.BuyArtifact(selected):view.UpgradeArtifact(selected);
        feedback.text=done?tab==0?d.Name+"已收入共享行囊。":d.Name+"升阶成功，属性已生效。":"当前无法操作。";Refresh();
    }
    private void OnDestroy(){ui?.Dispose();}
}
