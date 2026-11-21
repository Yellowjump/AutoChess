using System;
using System.Collections.Generic;
using System.Linq;
using SkillSystem;
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
    private int selectedActor,selectedKey,page;
    private int memberPage;
    private long? inspectedItem;
    private int selectedSlot=-1;
    private bool slotFocused;
    private Action closed;
    private bool restoreDebugger,resumeObservation;
    private TMP_Text stats,characterName,itemName,itemEffect,itemNext,itemCost,itemDescription,feedback,pages;
    private Image portrait,detailIcon;
    private Button action;
    private GameObject transferDialog;
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
        if (view.Expedition.RoleCount >= 5)
            ui.Button("MemberPage","切换同伴",94,930,263,56,()=>{memberPage=1-memberPage;Refresh();});
        for(int i=0;i<=view.Expedition.RoleCount;i++)
        {
            int role=i-1,actor=i==0?view.Expedition.LeaderId:view.Expedition.CompanionId(role);
            var button=ui.Button("EquipMember"+i,"",91,230+(i%5)*139,274,128,()=>{
                inspectedItem=null;selectedActor=actor;selectedKey=Gear.InnateKey(actor);selectedSlot=-1;slotFocused=false;Refresh();});
            if (i > 0 && role == view.Expedition.LeaderRole) button.gameObject.SetActive(false);
            bool owned=view.Expedition.Owns(actor);button.interactable=owned;members.Add(button);
            ui.Picture("MemberPortrait",view.PastPortrait(view.Expedition,view.Expedition.CombatRole(actor)) ?? ui.Portrait(view.Expedition.CombatRole(actor)),9,6,65,115,button.transform);
            var label=ui.Text("MemberName",Name(actor),88,24,179,43,32);label.transform.SetParent(button.transform,false);
            var state=ui.Text("MemberState",owned?(i==0?"主角":role>=ExpeditionParty.StageRoles?"前世主角":new[]{"盾卫","剑客","弓手","医者"}[role]):view.Expedition.IsFallen(role)?"已阵亡":"尚未结识",
                89,75,179,33,24,ExpeditionPaperUi.Muted);state.transform.SetParent(button.transform,false);
        }
        characterName=ui.Text("CharacterName","",435,229,440,61,43);
        stats=ui.Text("CharacterStats","",435,300,430,90,24);
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
        action=ui.Button("EquipOrUse","",1465,953,310,60,Act,ExpeditionPaperUi.Red);
        feedback=ui.Text("EquipmentFeedback","同一角色的同类效果只触发一次；反击标记只刷新时间。",435,984,1010,34,23,ExpeditionPaperUi.Muted);
        selectedKey=Gear.InnateKey(selectedActor);
    }
    private void Act()
    {
        var item=Gear.Instance(selectedKey);
        if(inspectedItem.HasValue || item==null || transferDialog!=null)return;
        if(item.Owner>=0 && OwnerActor(item)==selectedActor)
        {
            ApplyItem(item,item.Slot);
            return;
        }
        int slot=Gear.FirstFreeSlot(selectedActor);
        if(slot<0 && selectedSlot<0)
        {
            ShowSlotPicker(item);
            return;
        }
        if(slot<0 || item.Owner>=0)
            ShowEquipDialog(item,slot<0?selectedSlot:slot);
        else ApplyItem(item,slot);
    }
    private int OwnerActor(ArtifactInstance item)=>view.Expedition.FieldId(item.Owner-1);
    private string OwnerLabel(ArtifactInstance item)=>item.Owner<0?GameEntry.Localization.GetString("Gear_Unworn"):
        GameEntry.Localization.GetString("Gear_WornBy",Name(OwnerActor(item)));

    private void ShowSlotPicker(ArtifactInstance item)
    {
        transferDialog=ui.Box("SlotPicker",0,0,1920,1080,new Color(0,0,0,.6f)).gameObject;
        var paper=ui.Box("SlotPickerPaper",510,325,900,430,ExpeditionPaperUi.Paper,transferDialog.transform);
        var title=ui.Text("SelectSlot",GameEntry.Localization.GetString("Gear_SelectReplacement"),40,30,820,80,32);
        title.transform.SetParent(paper.transform,false);
        for(int i=0;i<ExpeditionEquipment.CommonSlots;i++)
        {
            int slot=i;
            var worn=Gear.Worn(selectedActor,slot);
            var choice=ui.Button("PickSlot"+slot,$"{slot+1}  {worn.Definition.Name}",30+i%3*285,120+i/3*82,265,70,()=>
            {
                CloseTransferDialog();
                selectedSlot=slot;
                ShowEquipDialog(item,slot);
            });
            choice.transform.SetParent(paper.transform,false);
        }
        var cancel=ui.Button("CancelPick",GameEntry.Localization.GetString("Confirm_Cancel"),345,327,210,65,CloseTransferDialog);
        cancel.transform.SetParent(paper.transform,false);
    }

    private void ShowEquipDialog(ArtifactInstance item,int slot)
    {
        int owner=item.Owner,actor=selectedActor;
        var replaced=Gear.Worn(actor,slot);
        string prompt;
        if(replaced==null)
            prompt=GameEntry.Localization.GetString("Gear_TransferPrompt",item.Definition.Name,Name(OwnerActor(item)),Name(actor));
        else if(owner>=0)
            prompt=GameEntry.Localization.GetString("Gear_TransferReplace",item.Definition.Name,Name(OwnerActor(item)),Name(actor),slot+1,replaced.Definition.Name);
        else
            prompt=GameEntry.Localization.GetString("Gear_Replace",Name(actor),slot+1,replaced.Definition.Name,item.Definition.Name);
        transferDialog=ui.Box("TransferDialog",0,0,1920,1080,new Color(0,0,0,.6f)).gameObject;
        var paper=ui.Box("TransferPaper",510,365,900,350,ExpeditionPaperUi.Paper,transferDialog.transform);
        var message=ui.Text("TransferMessage",prompt,45,30,810,195,34);
        message.enableAutoSizing=true;
        message.fontSizeMin=22;
        message.fontSizeMax=34;
        message.transform.SetParent(paper.transform,false);
        var cancel=ui.Button("CancelTransfer",GameEntry.Localization.GetString("Confirm_Cancel"),200,239,210,65,CloseTransferDialog);
        cancel.transform.SetParent(paper.transform,false);
        var confirm=ui.Button("ConfirmTransfer",GameEntry.Localization.GetString("Confirm_Confirn"),490,239,210,65,()=>
        {
            CloseTransferDialog();
            if(selectedActor==actor && item.Owner==owner && Gear.Worn(actor,slot)==replaced)ApplyItem(item,slot);
            else Refresh();
        },ExpeditionPaperUi.Red);
        confirm.transform.SetParent(paper.transform,false);
    }
    private void CloseTransferDialog()
    {
        transferDialog.SetActive(false);
        Destroy(transferDialog);
        transferDialog=null;
    }
    private void ApplyItem(ArtifactInstance item,int slot)
    {
        bool remove=item.Owner>=0 && OwnerActor(item)==selectedActor;
        bool done=remove?view.UnequipTeamItem(selectedActor,item.Slot):view.EquipArtifact(selectedActor,item.Id,slot,item.Owner>=0);
        if(done)slotFocused=false;
        feedback.text=GameEntry.Localization.GetString(done?(remove?"Gear_Removed":"Gear_Equipped"):"Gear_Unavailable");
        Refresh();
    }
    private void Track(GameObject item)=>dynamicUi.Add(item);
    private void ClearDynamic(){foreach(var obj in dynamicUi){obj.SetActive(false);Destroy(obj);}dynamicUi.Clear();}
    private void Refresh()
    {
        ClearDynamic();
        var actor=selectedActor==view.Expedition.LeaderId?view.Simulation.State.Actors[selectedActor]:
            view.Expedition.CreateCompanion(ExpeditionParty.Role(selectedActor),view.SelectedNodeId);
        characterName.text=Name(selectedActor);stats.text=$"气血  {actor.Health} / {actor.MaxHealth}\n攻击  {actor.CombatAttack}";
        if (view.Simulation.State.CriticalArtifactsEnabled)
            stats.text += $"\n暴击率 {CriticalFighter.BaseChance}%   暴击伤害 {CriticalFighter.DamagePercent(view.Expedition.Equipment.BattleTriggers(selectedActor))}%";
        portrait.sprite=view.PastPortrait(view.Expedition,view.Expedition.CombatRole(selectedActor)) ?? ui.Portrait(view.Expedition.CombatRole(selectedActor));portrait.enabled=portrait.sprite!=null;
        for(int i=0;i<members.Count;i++) members[i].gameObject.SetActive(i/5==memberPage && (i==0 || i-1!=view.Expedition.LeaderRole));
        for(int i=0;i<members.Count;i++)ui.MarkSelection(members[i],selectedActor==(i==0?view.Expedition.LeaderId:view.Expedition.CompanionId(i-1)));
        var innate=Gear.Innate(selectedActor);int level=Gear.InnateLevel(selectedActor);
        if (innate != null)
        {
        var fixedCard=ui.Button("InnateArtifact","",435,410,215,223,()=>{inspectedItem=null;selectedKey=Gear.InnateKey(selectedActor);slotFocused=false;Refresh();});Track(fixedCard.gameObject);
        ui.MarkSelection(fixedCard,!inspectedItem.HasValue&&!slotFocused&&selectedKey==Gear.InnateKey(selectedActor));
        var fixedLabel=ui.Text("InnateType","本命 · "+ItemSkills.ArtifactLabel(innate.Id),449,424,190,33,21,ExpeditionPaperUi.Red);Track(fixedLabel.gameObject);
        Track(ui.Picture("InnateIcon",ui.ItemIcon(innate.Id),470,458,137,92).gameObject);
        Track(ui.Text("InnateName",innate.Name,455,554,190,35,29).gameObject);
        Track(ui.Text("InnateLevel",$"{level} / {innate.MaxLevel} 阶 · 仅可升级",450,594,193,29,18,ExpeditionPaperUi.Muted).gameObject);
        }
        for(int s=0;s<ExpeditionEquipment.CommonSlots;s++)
        {
            int slot=s;float x=435+s%3*148,y=710+s/3*128;var worn=Gear.Worn(selectedActor,s);
            var card=ui.Button("EquippedSlot"+s,"",x,y,135,116,()=>{
                inspectedItem=null;selectedSlot=slot;slotFocused=true;if(worn!=null)selectedKey=worn.Id;Refresh();});Track(card.gameObject);ui.MarkSelection(card,slotFocused&&s==selectedSlot);
            Track(ui.Text("SlotType"+s,worn==null?"行装 "+(s+1):ItemSkills.ArtifactLabel(worn.DefinitionId),x+11,y+5,114,24,20,ExpeditionPaperUi.Muted).gameObject);
            if(worn!=null)Track(ui.Picture("WornIcon"+s,ui.ItemIcon(worn.DefinitionId),x+35,y+30,65,39).gameObject);
            var label=ui.Text("WornName"+s,worn?.Definition.Name??"空位",x+5,y+72,125,24,20);label.alignment=TextAlignmentOptions.Center;Track(label.gameObject);
            var lv=ui.Text("WornLevel"+s,worn==null?"不限道具类型":$"{worn.Level} / {worn.Definition.MaxLevel} 阶",x+5,y+98,125,18,17,ExpeditionPaperUi.Muted);lv.alignment=TextAlignmentOptions.Center;Track(lv.gameObject);
        }
        var leader=view.Simulation.State.Actors[view.Expedition.LeaderId];
        IReadOnlyList<WorldTimelineDebugView.EquipmentEntry> carried=WorldTimelineInventoryCtrl.ReadCarriedEquipment(view, leader);
        var bag=Gear.Instances;int total=Math.Max(1,(bag.Count+carried.Count+5)/6);page=Mathf.Clamp(page,0,total-1);pages.text=$"{page+1} / {total}";
        if(bag.Count+carried.Count==0)Track(ui.Text("EmptyBag","行囊暂空，战斗择宝或前往商人补充行装。",960,410,780,90,29,ExpeditionPaperUi.Muted).gameObject);
        var visible=bag.Skip(page*6).Take(6).ToArray();
        for(int i=0;i<visible.Length;i++)
        {
            var item=visible[i];float x=945+i%3*281,y=316+i/3*186;
            var card=ui.Button("BagItem"+item.Id,"",x,y,263,168,()=>{inspectedItem=null;selectedKey=item.Id;slotFocused=false;Refresh();});Track(card.gameObject);ui.MarkSelection(card,!inspectedItem.HasValue&&!slotFocused&&item.Id==selectedKey);
            ui.Picture("Icon",ui.ItemIcon(item.DefinitionId),10,20,104,116,card.transform);
            if(item.Owner>=0 && OwnerActor(item)!=selectedActor)
                ui.Picture("OwnerPortrait",view.PastPortrait(view.Expedition,item.Owner-1) ?? ui.Portrait(item.Owner-1),8,6,48,56,card.transform);
            var title=ui.Text("Name",item.Definition.Name,121,28,136,42,28);title.transform.SetParent(card.transform,false);
            var count=ui.Text("Level",$"{item.Level} / {item.Definition.MaxLevel} 阶",121,84,136,30,23,ExpeditionPaperUi.Muted);count.transform.SetParent(card.transform,false);
            var owner=ui.Text("Owner",ItemSkills.ArtifactLabel(item.DefinitionId)+" · "+OwnerLabel(item),12,125,239,34,21,ExpeditionPaperUi.Muted);
            owner.transform.SetParent(card.transform,false);
        }
        for(int cell=visible.Length;cell<6;cell++)
        {
            int index=page*6+cell-bag.Count;
            if(index<0 || index>=carried.Count)break;
            var entry=carried[index];
            string kind=entry.IsStartingSkill?"起手功法":entry.TechniqueId!=0?"已习功法":"随身道具";
            string type = string.IsNullOrEmpty(entry.SkillTypeName) ? "" : entry.SkillTypeName + " · ";
            var card=ui.Button("CarriedItem"+entry.SelectionId,$"{type}{entry.Name}  ×{entry.Count}\n{view.PlayerName} · {kind}",
                945+cell%3*281,316+cell/3*186,263,168,()=>{inspectedItem=entry.SelectionId;slotFocused=false;Refresh();});
            Track(card.gameObject);ui.MarkSelection(card,inspectedItem==entry.SelectionId);
        }
        var inspected=carried.FirstOrDefault(entry=>entry.SelectionId==inspectedItem);
        action.gameObject.SetActive(inspected==null);
        if(inspected!=null)
        {
            itemName.text=inspected.Name;itemEffect.text=string.Join(" · ",new[]{inspected.SkillTypeName,view.PlayerName,"无需装备"}.Where(part=>!string.IsNullOrEmpty(part)));
            itemNext.text=itemCost.text=itemDescription.text="";detailIcon.enabled=false;
            var description=ui.Text("CarriedEffect",inspected.Description,945,850,830,135,28);
            description.enableAutoSizing=true;description.fontSizeMin=18;Track(description.gameObject);
            return;
        }
        inspectedItem=null;
        var selected=Gear.UpgradeDefinition(selectedKey);int selectedLevel=Gear.UpgradeLevel(selectedKey);var instance=Gear.Instance(selectedKey);
        if(selectedKey<0&&selectedKey!=Gear.InnateKey(selectedActor)){selectedKey=Gear.InnateKey(selectedActor);selected=Gear.Innate(selectedActor);selectedLevel=level;}
        itemName.text=selected==null?"选择一件行装":$"{selected.Name}  {selectedLevel} / {selected.MaxLevel} 阶";
        itemEffect.text=selected==null?"":ItemSkills.ArtifactLabel(selected.Id)+" · "+selected.Effect(selectedLevel);
        if (instance?.DefinitionId == ExpeditionEquipment.GrowingSwordCase)
            itemEffect.text += instance.SwordClears == 2 ? " · 已养成" : $" · 养剑 {instance.SwordClears}/2";
        bool capped=selected!=null&&selectedLevel>=selected.MaxLevel;
        itemNext.text=selected==null?"":capped?"已满阶 · 无法继续升级":"下一阶  "+selected.Effect(selectedLevel+1);
        itemCost.text=selected==null||capped?"":$"商人升阶 {selected.UpgradePrice(selectedLevel)} 灵石 · 事件可获免费升阶";
        detailIcon.sprite=selected==null?null:ui.ItemIcon(selected.Id);detailIcon.enabled=selected!=null;
        itemDescription.enableAutoSizing=true;itemDescription.fontSizeMin=16;
        itemDescription.text=selected==null?"":selected.TriggerName+" · "+selected.TriggerRule;
        bool remove=instance!=null && instance.Owner>=0 && OwnerActor(instance)==selectedActor;
        action.interactable=instance!=null;
        if (instance?.Definition.Personal == true) itemDescription.text = "仅原主可触发 · " + selected.TriggerRule;
        action.GetComponentInChildren<TMP_Text>().text=GameEntry.Localization.GetString(
            instance==null && selected?.Innate==true?"Gear_Innate":remove?"Gear_Unequip":"Gear_Equip");
    }
    public void Close(){gameObject.SetActive(false);Destroy(gameObject);}
    private void OnDestroy()
    {
        if(view!=null){view.InventoryOpen=false;view.Closed-=Close;view.ResumeInventoryObservation(resumeObservation);}
        if(restoreDebugger&&HotGameEntry.Debugger!=null)HotGameEntry.Debugger.ActiveWindow=true;
        ui?.Dispose();if(view?.UsesPrefabHud==true)closed?.Invoke();
    }
}
