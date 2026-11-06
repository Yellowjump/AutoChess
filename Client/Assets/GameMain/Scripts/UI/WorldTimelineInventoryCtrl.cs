using static WorldTimelineDebug.WorldTimelineDebugView;
using System;
using System.Collections.Generic;
using System.Linq;
using DataTable;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimelineDebug;

public sealed class WorldTimelineInventoryCtrl : UIFormLogic
{
    private sealed class GrantedItemSelection
    {
        public WorldTimelineDebugView View;
        public int ItemId;
    }

    public static void ShowGrantedItem(WorldTimelineDebugView view, int itemId)
    {
        string form = UICtrlName.WorldTimelineInventory;
        if (GameEntry.UI.GetUIForm(form)?.Logic is WorldTimelineInventoryCtrl inventory && inventory.view == view)
            inventory.FocusGrantedItem(itemId);
        else if (!GameEntry.UI.IsLoadingUIForm(form))
            GameEntry.UI.OpenUIForm(form, "tips", userData: new GrantedItemSelection { View = view, ItemId = itemId });
    }

    private void FocusGrantedItem(int itemId)
    {
        filter = 0;
        selectedId = -itemId;
        var items = view.ReadEquipment();
        page = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Id != selectedId) continue;
            page = i / slots.Length;
            break;
        }
        Refresh(true);
    }

    [Serializable] private sealed class Slot
    {
        public Button button;
        public Image icon;
        public TMP_Text name, count;
        public Outline selected;
    }
    [SerializeField] private Slot[] slots;
    [SerializeField] private Sprite[] itemIcons, portraits;
    [SerializeField] private Button[] filters;
    [SerializeField] private Image[] filterFills;
    [SerializeField] private TMP_Text heroName, level, health, attack, combatAttack, quantity, pageLabel, emptyText;
    [SerializeField] private TMP_Text itemName, itemKind, description, origin, ownership;
    [SerializeField] private Image portrait, detailIcon;
    [SerializeField] private GameObject detail, empty;
    [SerializeField] private Button close, previousPage, nextPage;
    private WorldTimelineDebugView view;
    private List<WorldTimelineDebugView.EquipmentEntry> visible = new List<WorldTimelineDebugView.EquipmentEntry>();
    private int filter, page, selectedId, fingerprint;
    private float nextRefresh;
    private bool closing, resumeObservation;
    public int Page => page;
    public int VisibleItemCount => visible.Count;
    public WorldTimelineDebugView Session => view;

    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.text.StartsWith("Timeline_", System.StringComparison.Ordinal)) label.text = TimelineText(label.text);
        close.onClick.AddListener(Close);
        previousPage.onClick.AddListener(() => { page = Math.Max(0,page-1); Refresh(true); });
        nextPage.onClick.AddListener(() => { page++; Refresh(true); });
        for (int i=0;i<filters.Length;i++)
        { int category=i; filters[i].onClick.AddListener(() => { filter=category;page=0;selectedId=0;Refresh(true); }); }
        for (int i=0;i<slots.Length;i++)
        {
            int slot=i;
            slots[i].button.onClick.AddListener(() =>
            {
                int index=page*slots.Length+slot;
                if(index<visible.Count){selectedId=visible[index].Id;Refresh(true);}
            });
        }
    }
    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        var selection = userData as GrantedItemSelection;
        closing=false; view=selection?.View ?? userData as WorldTimelineDebugView;
        if(view?.Simulation==null || view.IsPreparing){Close();return;}
        view.InventoryOpen=true;resumeObservation=view.PauseInventoryObservation();view.ClearMapHover();view.Closed+=Close;
        RefreshMapDisplay();
        page=filter=selectedId=0;
        if (selection != null) FocusGrantedItem(selection.ItemId);
        else Refresh(true);
    }
    public override void OnClose(bool isShutdown,object userData)
    {
        if(view!=null){view.Closed-=Close;view.InventoryOpen=false;view.ResumeInventoryObservation(resumeObservation);}
        if(!isShutdown)RefreshMapDisplay();
        view=null;visible.Clear();base.OnClose(isShutdown,userData);
    }
    public override void OnUpdate(float elapseSeconds,float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds,realElapseSeconds);
        if(view==null)return;
        if(view.IsPreparing){Close();return;}
        if(Time.unscaledTime<nextRefresh)return;
        nextRefresh=Time.unscaledTime+.25f;Refresh(false);
    }
    private void Close(){if(closing)return;closing=true;GameEntry.UI.CloseUIForm(UIForm);}
    private void Refresh(bool force)
    {
        if(view?.Simulation==null)return;
        var actor=view.Simulation.State.Actors[view.Simulation.State.PlayerActorId];
        int hash=unchecked(actor.Health*397 ^ actor.MaxHealth*31 ^ actor.Attack ^ actor.CombatAttack ^ actor.Level*17);
        foreach(int id in actor.Items)hash=unchecked(hash*31+id);
        if(!force && hash==fingerprint)return;fingerprint=hash;
        heroName.text=view.PlayerName;level.text=TimelineText("Timeline_UI_Level", actor.Level);
        health.text=actor.Health+" / "+actor.MaxHealth;attack.text=actor.Attack.ToString();combatAttack.text=actor.CombatAttack.ToString();
        int portraitIndex=view.InventoryPortraitIndex;
        portrait.sprite=portraitIndex>=0 && portraitIndex<portraits.Length?portraits[portraitIndex]:null;
        portrait.enabled=portrait.sprite!=null;
        var all=view.ReadEquipment();
        var nativeItemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        foreach (var entry in all)
        {
            if (entry.Id >= 0 || nativeItemTable == null || !nativeItemTable.HasDataRow(-entry.Id)) continue;
            var item = nativeItemTable[-entry.Id];
            entry.Name = GameEntry.Localization.GetString(item.Name);
            if (!string.IsNullOrEmpty(item.Decs))
                entry.Description = HotGameEntry.HeroManager.FormatItemDesc(GameEntry.Localization.GetString(item.Decs), 0);
        }
        visible=all.Where(item=>filter==0 || (int)item.Kind==filter).ToList();
        quantity.text=TimelineText("Timeline_UI_InventoryCount", all.Sum(item=>item.Count));
        int pages=Math.Max(1,(visible.Count+slots.Length-1)/slots.Length);page=Math.Min(page,pages-1);
        var onPage=visible.Skip(page*slots.Length).Take(slots.Length).ToArray();
        if(!onPage.Any(item=>item.Id==selectedId))selectedId=onPage.FirstOrDefault()?.Id??0;
        for(int i=0;i<slots.Length;i++)
        {
            var slot=slots[i];slot.button.gameObject.SetActive(i<onPage.Length);
            if(i>=onPage.Length)continue;
            var item=onPage[i];slot.name.text=item.Name;slot.count.text=item.Count>1?"×"+item.Count:"";
            slot.icon.sprite=Icon(item.Icon);slot.selected.enabled=item.Id==selectedId;
        }
        for(int i=0;i<filterFills.Length;i++)filterFills[i].color=i==filter?new Color(.26f,.46f,.38f):new Color(.13f,.18f,.20f);
        pageLabel.text=$"{page+1} / {pages}";previousPage.interactable=page>0;nextPage.interactable=page+1<pages;
        empty.SetActive(visible.Count==0);emptyText.text=filter==0?TimelineText("Timeline_UI_80"):TimelineText("Timeline_UI_48");
        var selected=visible.FirstOrDefault(item=>item.Id==selectedId);detail.SetActive(selected!=null);
        if(selected==null)return;
        itemName.text=selected.Name;itemKind.text=new[]{TimelineText("Timeline_UI_13"),TimelineText("Timeline_UI_15"),TimelineText("Timeline_UI_68"),TimelineText("Timeline_UI_19")}[(int)selected.Kind];
        detailIcon.sprite=Icon(selected.Icon);description.text=selected.Description;origin.text=selected.Origin;
        if (view.Simulation.State.StoryCombatSkillsEnabled)
        {
            int skillItemId = WorldTimelineBattleResolver.StorySkillItem(selected.Id);
            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            if (skillItemId > 0 && itemTable != null && itemTable.HasDataRow(skillItemId))
            {
                var skillItem = itemTable[skillItemId];
                string effect = HotGameEntry.HeroManager.FormatItemDesc(GameEntry.Localization.GetString(skillItem.Decs), 0);
                if (effect != selected.Description) description.text += $"\n\n{effect}";
            }
        }
        ownership.text=selected.Kind==WorldTimelineDebugView.EquipmentKind.Art?TimelineText("Timeline_UI_81"):TimelineText("Timeline_UI_72");
        if(selected.Count>1)ownership.text+=TimelineText("Timeline_UI_ItemQuantity", selected.Count);
    }
    private Sprite Icon(int index)=>itemIcons.Length==0?null:itemIcons[Mathf.Clamp(index,0,itemIcons.Length-1)];
    private static void RefreshMapDisplay()
    { (GameEntry.UI.GetUIForm(UICtrlName.WorldTimelinePanel)?.Logic as WorldTimelinePanelCtrl)?.RefreshDisplay(); }
}
