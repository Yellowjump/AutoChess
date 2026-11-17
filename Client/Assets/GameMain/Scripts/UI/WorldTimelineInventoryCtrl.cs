using static WorldTimelineDebug.WorldTimelineDebugView;
using System;
using System.Collections.Generic;
using System.Linq;
using DataTable;
using Entity;
using SkillSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimelineDebug;

public sealed class WorldTimelineInventoryCtrl : UIFormLogic
{
    private sealed class InventorySelection
    {
        public WorldTimelineDebugView View;
        public int ItemId;
        public int TechniqueId;
        public bool ResumeObservation;
    }

    public static void ShowGrantedItem(WorldTimelineDebugView view, int itemId)
    {
        string form = UICtrlName.WorldTimelineInventory;
        if (GameEntry.UI.GetUIForm(form)?.Logic is WorldTimelineInventoryCtrl inventory && inventory.view == view)
            inventory.FocusGrantedItem(itemId);
        else if (!GameEntry.UI.IsLoadingUIForm(form))
            GameEntry.UI.OpenUIForm(form, "tips", userData: new InventorySelection { View = view, ItemId = itemId });
    }

    public static void ReturnFromTechnique(WorldTimelineDebugView view, int techniqueId, bool resumeObservation)
    {
        GameEntry.UI.OpenUIForm(UICtrlName.WorldTimelineInventory, "tips", userData: new InventorySelection
        {
            View = view, TechniqueId = techniqueId, ResumeObservation = resumeObservation
        });
    }

    private void FocusGrantedItem(int itemId)
    {
        filter = 0;
        Refresh(true);
        int index = visible.FindIndex(entry => entry.Id == -itemId);
        if (index >= 0)
        {
            selectedId = visible[index].SelectionId;
            Refresh(true);
        }
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
    [SerializeField] private Sprite[] techniqueIcons;
    [SerializeField] private Button[] filters;
    [SerializeField] private Image[] filterFills;
    [SerializeField] private TMP_Text heroName, level, health, attack, combatAttack, quantity, emptyText;
    [SerializeField] private TMP_Text itemName, itemKind, description, origin;
    [SerializeField] private Image portrait, detailIcon;
    [SerializeField] private GameObject detail, empty;
    [SerializeField] private Button close;
    [SerializeField] private ScrollRect itemScroll;
    private readonly List<Slot> cards = new List<Slot>();
    private WorldTimelineDebugView view;
    private List<WorldTimelineDebugView.EquipmentEntry> visible = new List<WorldTimelineDebugView.EquipmentEntry>();
    private int filter, fingerprint;
    private int pendingTechnique;
    private long selectedId;
    private float nextRefresh;
    private bool closing, resumeObservation;
    public int VisibleItemCount => visible.Count;
    public WorldTimelineDebugView Session => view;

    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.text == "BtnBag" || label.text?.StartsWith("Timeline_", System.StringComparison.Ordinal) == true)
                label.text = TimelineText(label.text);
        close.onClick.AddListener(Close);
        for (int i = 0; i < filters.Length; i++)
        {
            int category = i;
            filters[i].onClick.AddListener(() =>
            {
                if (filter == category) return;
                filter = category;
                selectedId = 0;
                Refresh(true);
                ResetScroll();
            });
        }
        foreach (var slot in slots) RegisterCard(slot);
    }

    private void RegisterCard(Slot slot)
    {
        int index = cards.Count;
        cards.Add(slot);
        BindButtonClickSound(slot.button);
        slot.button.onClick.AddListener(() =>
        {
            if (index >= visible.Count || view == null) return;
            var entry = visible[index];
            if (entry.TechniqueId != 0 && view.TryGetLearnedTechnique(entry.TechniqueId, out _, out _) &&
                GameEntry.UI.GetUIForm(UICtrlName.WorldTimelinePanel)?.Logic is WorldTimelinePanelCtrl)
            {
                pendingTechnique = entry.TechniqueId;
                Close();
                return;
            }
            selectedId = entry.SelectionId;
            Refresh(true);
        });
    }

    private void EnsureCards(int count)
    {
        while (cards.Count < count)
        {
            var button = Instantiate(slots[0].button, itemScroll.content);
            var card = button.transform;
            RegisterCard(new Slot
            {
                button = button,
                icon = card.Find("Icon").GetComponent<Image>(),
                name = card.Find("Name").GetComponent<TMP_Text>(),
                count = card.Find("Count").GetComponent<TMP_Text>(),
                selected = button.GetComponent<Outline>()
            });
        }
    }

    private void ResetScroll()
    {
        itemScroll.StopMovement();
        itemScroll.content.anchoredPosition = Vector2.zero;
        itemScroll.verticalNormalizedPosition = 1f;
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        var selection = userData as InventorySelection;
        closing=false; view=selection?.View ?? userData as WorldTimelineDebugView;
        pendingTechnique = 0;
        if(view?.Simulation==null || view.IsPreparing){Close();return;}
        view.InventoryOpen=true;resumeObservation=view.PauseInventoryObservation();view.ClearMapHover();view.Closed+=Close;
        resumeObservation |= selection?.ResumeObservation == true;
        RefreshMapDisplay();
        filter=0;selectedId=0;
        if (selection?.TechniqueId > 0)
        {
            filter = (int)EquipmentKind.Art;
            Refresh(true);
            int index = visible.FindIndex(entry => entry.TechniqueId == selection.TechniqueId);
            if (index >= 0)
            {
                selectedId = visible[index].SelectionId;
                Refresh(true);
            }
        }
        else if (selection != null) FocusGrantedItem(selection.ItemId);
        else Refresh(true);
        ResetScroll();
    }
    public override void OnClose(bool isShutdown,object userData)
    {
        var session = view;
        if (session != null)
        {
            session.Closed -= Close;
            session.InventoryOpen = false;
        }
        if(!isShutdown)RefreshMapDisplay();
        view=null;visible.Clear();base.OnClose(isShutdown,userData);
        if (session != null && (!isShutdown && pendingTechnique != 0 &&
            GameEntry.UI.GetUIForm(UICtrlName.WorldTimelinePanel)?.Logic is WorldTimelinePanelCtrl panel &&
            panel.OpenTechnique(session, pendingTechnique, resumeObservation)))
            return;
        session?.ResumeInventoryObservation(resumeObservation);
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
        foreach(var art in actor.Techniques)hash=unchecked(hash*31+art.Key*17+art.Value.Rank);
        if(!force && hash==fingerprint)return;fingerprint=hash;
        heroName.text=view.PlayerName;level.text=TimelineText("Timeline_UI_Level", actor.Level);
        health.text=actor.Health+" / "+actor.MaxHealth;attack.text=actor.Attack.ToString();combatAttack.text=actor.CombatAttack.ToString();
        int portraitIndex=view.InventoryPortraitIndex;
        portrait.sprite=portraitIndex>=0 && portraitIndex<portraits.Length?portraits[portraitIndex]:null;
        portrait.enabled=portrait.sprite!=null;
        var all=view.ReadEquipment();
        var nativeItemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        var skills = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
        foreach (var entry in all)
        {
            if (entry.TechniqueId != 0 || nativeItemTable == null) continue;
            int nativeId = WorldTimelineBattleResolver.InventorySkillItem(entry.Id, view.Simulation.State.StoryCombatSkillsEnabled);
            if (nativeId == 0 || !nativeItemTable.HasDataRow(nativeId)) continue;
            var item = nativeItemTable[nativeId];
            if (entry.IsStartingSkill)
            {
                if (item.SkillID == 0 || skills == null || !skills.HasDataRow(item.SkillID)) continue;
                bool replaced = view.Simulation.State.TechniqueCombatEnabled && actor.Techniques.ContainsKey(1) &&
                    (SkillType)skills[item.SkillID].SkillType == SkillType.NormalSkill;
                if (replaced) continue;
                entry.Name = item.SkillID == 1 ? TechniqueSkillName(1) : TimelineText(item.Name);
                entry.Icon = 12;
            }
            if (item.SkillID != 0)
            {
                entry.SkillItemId = nativeId;
                entry.Kind = entry.Id is TimelineCaveHazards.EmberHeart or TimelineCaveHazards.IceBead
                    ? EquipmentKind.Relic : EquipmentKind.Art;
            }
            if (entry.Id < 0)
            {
                entry.Name = GameEntry.Localization.GetString(item.Name);
                if (!string.IsNullOrEmpty(item.Decs))
                    entry.Description = HotGameEntry.HeroManager.FormatTimelineItemDesc(TimelineText(item.Decs), actor);
            }
        }
        var carried = all.Where(item => !item.IsStartingSkill || item.SkillItemId != 0).ToArray();
        visible=carried.Where(item=>filter==0 || (int)item.Kind==filter).ToList();
        quantity.text=TimelineText("Timeline_UI_InventoryCount", carried.Sum(item=>item.Count));
        EnsureCards(visible.Count);
        if (!visible.Any(item => item.SelectionId == selectedId)) selectedId = visible.FirstOrDefault()?.SelectionId ?? 0;
        float height = Mathf.Max(itemScroll.viewport.rect.height, 16f + ((visible.Count + 3) / 4) * 197f);
        itemScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        for (int i = 0; i < cards.Count; i++)
        {
            var slot = cards[i];
            slot.button.gameObject.SetActive(i < visible.Count);
            if (i >= visible.Count) continue;
            ((RectTransform)slot.button.transform).anchoredPosition = new Vector2(8 + i % 4 * 212, -8 - i / 4 * 197);
            var item = visible[i];
            slot.name.text = item.Name;
            slot.count.text = item.Count > 1 ? "×" + item.Count : "";
            slot.icon.sprite = Icon(item, actor, nativeItemTable);
            slot.selected.enabled = item.SelectionId == selectedId;
        }
        for(int i=0;i<filterFills.Length;i++)filterFills[i].color=i==filter?new Color(.26f,.46f,.38f):new Color(.13f,.18f,.20f);
        empty.SetActive(visible.Count==0);emptyText.text=filter==0?TimelineText("Timeline_UI_80"):TimelineText("Timeline_UI_48");
        var selected=visible.FirstOrDefault(item=>item.SelectionId==selectedId);detail.SetActive(selected!=null);
        if(selected==null)return;
        itemName.text=selected.Name;itemKind.text=TimelineText(selected.Kind==EquipmentKind.Art?"Timeline_UI_19":"Timeline_UI_68");
        detailIcon.sprite=Icon(selected, actor, nativeItemTable);description.text=selected.Description;origin.text=selected.Origin;
        int skillId = InventorySkillId(selected, actor, nativeItemTable);
        bool bell = selected.Id == 9104 || selected.Id == -9104;
        bool bellShieldReplaced = bell && view.Simulation.State.TechniqueCombatEnabled && actor.Techniques.ContainsKey(1);
        if (bellShieldReplaced && selected.Id < 0) description.text = "";
        if (!bellShieldReplaced && (selected.SkillItemId != 0 || selected.TechniqueId != 0))
        {
            string template = WorldTimelineTechniqueRoute.DescriptionTemplate(skillId);
            if (template == null && selected.SkillItemId != 0 && selected.Id > 0)
                template = TimelineText(nativeItemTable[selected.SkillItemId].Decs);
            if (!string.IsNullOrEmpty(template))
            {
                string effect = HotGameEntry.HeroManager.FormatTimelineItemDesc(template, actor);
                if (selected.Id < 0 || string.IsNullOrEmpty(description.text)) description.text = effect;
                else if (effect != description.text) description.text += $"\n\n{effect}";
            }
        }
        if (view.Simulation.State.SwordRecallEnabled && bell)
        {
            if (!string.IsNullOrEmpty(description.text)) description.text += "\n\n";
            description.text += TimelineText("Timeline_SwordRecall_Description");
        }
        int flavorItemId = selected.Id < 0 ? selected.SkillItemId : selected.Id;
        string flavor = DetailFlavor(skillId, flavorItemId);
        if (!string.IsNullOrEmpty(flavor))
            description.text += $"\n\n<size=80%><color=#A9A08A>{flavor}</color></size>";
    }
    private static int InventorySkillId(EquipmentEntry entry, WorldTimeline.ActorState actor,
        GameFramework.DataTable.IDataTable<DRItem> items) =>
        entry.SkillItemId != 0 ? items[entry.SkillItemId].SkillID
            : actor.Techniques.TryGetValue(entry.TechniqueId, out var art) ? art.SkillId : 0;

    private Sprite Icon(EquipmentEntry entry, WorldTimeline.ActorState actor, GameFramework.DataTable.IDataTable<DRItem> items)
        => ResolveInventoryIcon(entry.Id, InventorySkillId(entry, actor, items), entry.Icon, itemIcons, techniqueIcons);

    internal static Sprite ResolveInventoryIcon(int itemId, int skillId, int fallback, Sprite[] itemIcons, Sprite[] techniqueIcons)
    {
        if (skillId == 0) skillId = Math.Abs(itemId);
        int index = itemId == 9104 || itemId == -9104 ? 3 : skillId switch
        {
            1 => 0,
            1001 => 1,
            8 => 2,
            >= 9121 and <= 9124 => 4 + skillId - 9121,
            >= 9131 and <= 9134 => 8 + skillId - 9131,
            >= 9141 and <= 9144 => 12 + skillId - 9141,
            9112 => 16,
            9111 => 17,
            3 => 18,
            4 => 19,
            9103 => 20,
            >= 9151 and <= 9153 => 21 + skillId - 9151,
            9102 => 24,
            _ => -1
        };
        if (index >= 0 && techniqueIcons != null && index < techniqueIcons.Length && techniqueIcons[index] != null)
            return techniqueIcons[index];
        return itemIcons.Length == 0 ? null : itemIcons[Mathf.Clamp(fallback, 0, itemIcons.Length - 1)];
    }
    private static void RefreshMapDisplay()
    { (GameEntry.UI.GetUIForm(UICtrlName.WorldTimelinePanel)?.Logic as WorldTimelinePanelCtrl)?.RefreshDisplay(); }
}
