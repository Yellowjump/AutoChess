using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

public sealed class ExpeditionEquipmentPanel : MonoBehaviour
{
    private WorldTimelineDebugView view;
    private ExpeditionPaperUi ui;
    private int selectedActor,selectedKey,selectedSlot,page;
    private Action closed;
    private bool restoreDebugger,resumeObservation;
    private TMP_Text stats,characterName,itemName,itemEffect,itemNext,itemCost,itemDescription,feedback,pages;
    private Image portrait,detailIcon;
    private Button action;
    private readonly List<Button> members=new List<Button>();
    private readonly List<GameObject> dynamicUi=new List<GameObject>();
    private ExpeditionEquipment Gear=>view.Expedition.Equipment;
    public int SelectedActor=>selectedActor;
    public static ExpeditionEquipmentPanel Open(WorldTimelineDebugView view,TMP_FontAsset font,Action closed)
    {
        var root=ExpeditionPaperUi.Canvas("同行装备");
        var panel=root.AddComponent<ExpeditionEquipmentPanel>();panel.view=view;panel.closed=closed;
        panel.selectedActor=view.Expedition.LeaderId;panel.ui=new ExpeditionPaperUi(root,font);
        panel.restoreDebugger=HotGameEntry.Debugger!=null&&HotGameEntry.Debugger.ActiveWindow;
        if(panel.restoreDebugger)HotGameEntry.Debugger.ActiveWindow=false;
        panel.resumeObservation=view.PauseInventoryObservation();view.InventoryOpen=true;view.Closed+=panel.Close;
        try{panel.Build();panel.Refresh();}catch{Destroy(root);throw;}return panel;
    }
    private string Name(int actor)=>actor==view.Expedition.LeaderId?view.PlayerName:
        view.CompanionName(ExpeditionParty.Role(actor)).Split('·')[0].Trim();
    private void Build()
    {
        ui.Box("Backdrop",0,0,1920,1080,new Color(.09f,.10f,.08f,.86f));ui.Page("EquipmentLedger",40,35,1840,1010);
        ui.Text("Title","同行行囊",96,64,560,80,56).fontStyle=FontStyles.Bold;
        ui.Text("Subtitle",$"一器伴此生，行装随君配    ·    灵石 {Gear.SpiritStones}",98,147,1260,43,27,ExpeditionPaperUi.Muted);
        ui.Button("Close","返回地图",1650,78,169,60,Close);
        ui.Line(93,200,1730);
        for(int i=0;i<5;i++)
        {
            int role=i-1,actor=i==0?view.Expedition.LeaderId:view.Expedition.CompanionId(role);
            var button=ui.Button("EquipMember"+i,"",91,230+i*147,274,128,()=>{
                selectedActor=actor;selectedKey=Gear.InnateKey(actor);selectedSlot=0;Refresh();});
            bool owned=view.Expedition.Owns(actor);button.interactable=owned;members.Add(button);
            ui.Picture("MemberPortrait",ui.Portrait(role),9,6,65,115,button.transform);
            var label=ui.Text("MemberName",Name(actor),88,24,179,43,32);label.transform.SetParent(button.transform,false);
            var state=ui.Text("MemberState",owned?(i==0?"主角":new[]{"盾卫","剑客","弓手","医者"}[role]):"尚未结识",
                89,75,179,33,24,ExpeditionPaperUi.Muted);state.transform.SetParent(button.transform,false);
        }
        characterName=ui.Text("CharacterName","",435,229,440,61,43);
        stats=ui.Text("CharacterStats","",435,307,430,90,28);
        portrait=ui.Picture("CharacterPortrait",null,667,390,217,260);
        ui.Text("SlotsHeading","通用行装 · 六个槽位",435,656,450,43,29);
        ui.Text("SlotsHint","任选六个槽位 · 同伴间自由转交",435,958,455,27,20,ExpeditionPaperUi.Muted);
        ui.Text("BagTitle","共享行囊",945,229,400,58,38).fontStyle=FontStyles.Bold;
        ui.Button("PreviousBagPage","上页",1450,233,105,46,()=>{page--;Refresh();});
        pages=ui.Text("BagPages","",1564,239,100,40,24);pages.alignment=TextAlignmentOptions.Center;
        ui.Button("NextBagPage","下页",1675,233,105,46,()=>{page++;Refresh();});
        ui.Line(945,705,835);
        detailIcon=ui.Picture("SelectedItemIcon",null,945,742,150,150);
        itemName=ui.Text("SelectedItemName","",1110,741,640,48,34);
        itemEffect=ui.Text("SelectedItemEffect","",1110,797,640,40,27);
        itemNext=ui.Text("SelectedItemNextLevel","",1110,842,640,40,27,ExpeditionPaperUi.Jade);
        itemCost=ui.Text("SelectedItemUpgradeCost","",1110,888,650,34,23,ExpeditionPaperUi.Muted);
        itemDescription=ui.Text("SelectedItemDescription","",1110,925,650,27,21,ExpeditionPaperUi.Muted);
        action=ui.Button("EquipOrUse","装入所选槽位",1465,953,310,60,Act,ExpeditionPaperUi.Red);
        feedback=ui.Text("EquipmentFeedback","本命法器固定随身；通用道具可自由穿戴、卸下与转交。",435,984,1010,34,23,ExpeditionPaperUi.Muted);
        selectedKey=Gear.InnateKey(selectedActor);
    }
    private void Act()
    {
        var item=Gear.Instance(selectedKey);if(item==null)return;
        bool remove=item.Owner>=0;
        bool done=remove?view.UnequipTeamItem(selectedActor,item.Slot):view.EquipArtifact(selectedActor,item.Id,selectedSlot);
        feedback.text=done?(remove?"已卸下，原有等级保留在行囊中。":"已装入槽位，原道具已返还行囊。"):"当前无法操作。";Refresh();
    }
    private void Track(GameObject item)=>dynamicUi.Add(item);
    private void ClearDynamic(){foreach(var obj in dynamicUi){obj.SetActive(false);Destroy(obj);}dynamicUi.Clear();}
    private void Refresh()
    {
        ClearDynamic();
        var actor=selectedActor==view.Expedition.LeaderId?view.Simulation.State.Actors[selectedActor]:
            view.Expedition.CreateCompanion(ExpeditionParty.Role(selectedActor),view.SelectedNodeId);
        characterName.text=Name(selectedActor);stats.text=$"气血  {actor.Health} / {actor.MaxHealth}\n攻击  {actor.CombatAttack}";
        portrait.sprite=ui.Portrait(selectedActor==view.Expedition.LeaderId?-1:ExpeditionParty.Role(selectedActor));portrait.enabled=portrait.sprite!=null;
        for(int i=0;i<members.Count;i++)ui.MarkSelection(members[i],selectedActor==(i==0?view.Expedition.LeaderId:view.Expedition.CompanionId(i-1)));
        var innate=Gear.Innate(selectedActor);int level=Gear.InnateLevel(selectedActor);
        var fixedCard=ui.Button("InnateArtifact","",435,410,215,223,()=>{selectedKey=Gear.InnateKey(selectedActor);Refresh();});Track(fixedCard.gameObject);
        ui.MarkSelection(fixedCard,selectedKey==Gear.InnateKey(selectedActor));
        var fixedLabel=ui.Text("InnateType","本命法器 · 固定",449,424,190,33,21,ExpeditionPaperUi.Red);Track(fixedLabel.gameObject);
        Track(ui.Picture("InnateIcon",ui.ItemIcon(innate.Id),470,458,137,92).gameObject);
        Track(ui.Text("InnateName",innate.Name,455,554,190,35,29).gameObject);
        Track(ui.Text("InnateLevel",$"{level} / {innate.MaxLevel} 阶 · 仅可升级",450,594,193,29,18,ExpeditionPaperUi.Muted).gameObject);
        for(int s=0;s<ExpeditionEquipment.CommonSlots;s++)
        {
            int slot=s;float x=435+s%3*148,y=710+s/3*128;var worn=Gear.Worn(selectedActor,s);
            var card=ui.Button("EquippedSlot"+s,"",x,y,135,116,()=>{
                selectedSlot=slot;if(worn!=null)selectedKey=worn.Id;Refresh();});Track(card.gameObject);ui.MarkSelection(card,s==selectedSlot);
            Track(ui.Text("SlotType"+s,"行装 "+(s+1),x+11,y+5,114,24,20,ExpeditionPaperUi.Muted).gameObject);
            if(worn!=null)Track(ui.Picture("WornIcon"+s,ui.ItemIcon(worn.DefinitionId),x+35,y+30,65,39).gameObject);
            var label=ui.Text("WornName"+s,worn?.Definition.Name??"空位",x+5,y+72,125,24,20);label.alignment=TextAlignmentOptions.Center;Track(label.gameObject);
            var lv=ui.Text("WornLevel"+s,worn==null?"不限道具类型":$"{worn.Level} / {worn.Definition.MaxLevel} 阶",x+5,y+98,125,18,17,ExpeditionPaperUi.Muted);lv.alignment=TextAlignmentOptions.Center;Track(lv.gameObject);
        }
        var bag=Gear.Instances.Where(i=>i.Owner<0).ToArray();int total=Math.Max(1,(bag.Length+5)/6);page=Mathf.Clamp(page,0,total-1);pages.text=$"{page+1} / {total}";
        if(bag.Length==0)Track(ui.Text("EmptyBag","行囊暂空，战斗择宝或前往商人补充行装。",960,410,780,90,29,ExpeditionPaperUi.Muted).gameObject);
        var visible=bag.Skip(page*6).Take(6).ToArray();
        for(int i=0;i<visible.Length;i++)
        {
            var item=visible[i];float x=945+i%3*281,y=316+i/3*186;
            var card=ui.Button("BagItem"+item.Id,"",x,y,263,168,()=>{selectedKey=item.Id;Refresh();});Track(card.gameObject);ui.MarkSelection(card,item.Id==selectedKey);
            ui.Picture("Icon",ui.ItemIcon(item.DefinitionId),10,20,104,116,card.transform);
            var title=ui.Text("Name",item.Definition.Name,121,28,136,42,28);title.transform.SetParent(card.transform,false);
            var count=ui.Text("Level",$"{item.Level} / {item.Definition.MaxLevel} 阶",121,84,136,30,23,ExpeditionPaperUi.Muted);count.transform.SetParent(card.transform,false);
        }
        var selected=Gear.UpgradeDefinition(selectedKey);int selectedLevel=Gear.UpgradeLevel(selectedKey);var instance=Gear.Instance(selectedKey);
        if(selectedKey<0&&selectedKey!=Gear.InnateKey(selectedActor)){selectedKey=Gear.InnateKey(selectedActor);selected=Gear.Innate(selectedActor);selectedLevel=level;}
        itemName.text=selected==null?"选择一件行装":$"{selected.Name}  {selectedLevel} / {selected.MaxLevel} 阶";
        itemEffect.text=selected==null?"":"当前  "+selected.Effect(selectedLevel);
        bool capped=selected!=null&&selectedLevel>=selected.MaxLevel;
        itemNext.text=selected==null?"":capped?"已满阶 · 无法继续升级":"下一阶  "+selected.Effect(selectedLevel+1);
        itemCost.text=selected==null||capped?"":$"商人升阶 {selected.UpgradePrice(selectedLevel)} 灵石 · 事件可获免费升阶";
        detailIcon.sprite=selected==null?null:ui.ItemIcon(selected.Id);detailIcon.enabled=selected!=null;
        itemDescription.text=selected==null?"":selected.Innate?"本命法器固定随身，不可替换或卸下。":"不限类型与职业，卸下与转交均保留等级。";
        bool remove=instance?.Owner>=0;action.interactable=instance!=null && (!remove || instance.Owner==(selectedActor==view.Expedition.LeaderId?0:ExpeditionParty.Role(selectedActor)+1));
        action.GetComponentInChildren<TMP_Text>().text=selected?.Innate==true?"本命法器 · 仅可升级":remove?"卸下此道具":"装入行装 "+(selectedSlot+1);
    }
    public void Close(){gameObject.SetActive(false);Destroy(gameObject);}
    private void OnDestroy()
    {
        if(view!=null){view.InventoryOpen=false;view.Closed-=Close;view.ResumeInventoryObservation(resumeObservation);}
        if(restoreDebugger&&HotGameEntry.Debugger!=null)HotGameEntry.Debugger.ActiveWindow=true;
        ui?.Dispose();if(view?.UsesPrefabHud==true)closed?.Invoke();
    }
}
