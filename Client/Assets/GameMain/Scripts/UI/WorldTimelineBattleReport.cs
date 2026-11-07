using static WorldTimelineDebug.WorldTimelineDebugView;
using System;
using System.Collections.Generic;
using System.Linq;
using DataTable;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

public sealed class WorldTimelineBattleReport : MonoBehaviour
{
    public const string PrefabPath = "Assets/GameMain/Prefeb/UIPrefab/world_timeline_battle_report.prefab";
    [SerializeField] private TMP_Text title, caption, health, experience, breakthrough, backlash, noDrops, technique;
    [SerializeField] private TMP_Text kicker;
    [SerializeField] private Image accent, healthFill;
    [SerializeField] private GameObject rewards, cultivation, techniqueSection;
    [SerializeField] private RectTransform card, itemTemplate;
    [SerializeField] private ScrollRect lootScroll;
    [SerializeField] private Button proceed;
    [SerializeField] private Button statsToggle;
    [SerializeField] private TMP_Text statsToggleText;
    [SerializeField] private ScrollRect statsScroll;
    [SerializeField] private RectTransform statsRowTemplate;
    [SerializeField] private GameObject statsPage, healthSection;
    [SerializeField] private GameObject treasurePage;
    [SerializeField] private RectTransform treasureArt, treasureRowTemplate;
    [SerializeField] private CanvasGroup treasureSparks;
    [SerializeField] private ScrollRect treasureScroll;
    [SerializeField] private Sprite[] itemIcons, techniqueIcons;
    private readonly List<RectTransform> rows = new();
    private readonly List<RectTransform> statsRows = new();
    private readonly List<RectTransform> treasureRows = new();
    private Action confirm;
    private bool statsOpen, showCultivation, showLoot, showTechnique, showBacklash;
    private float summaryHeight;
    private bool treasureMode;
    private float revealStarted;

    private void Awake()
    {
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            string key = label.text;
            if (key == "Timeline_BattleDrops") label.text = TimelineText(key, "").TrimEnd(' ', '·');
            else if (!string.IsNullOrEmpty(key) && (key.StartsWith("Timeline_", StringComparison.Ordinal) || key == "Attr_HuDun"))
                label.text = TimelineText(key);
        }
        proceed.onClick.AddListener(() =>
        {
            proceed.interactable = false;
            confirm?.Invoke();
        });
        statsToggle.onClick.AddListener(() => SetStatsOpen(!statsOpen));
    }

    public void Show(WorldTimelineDebugView view, BattleSettlement battle)
    {
        confirm = view.ContinueBattleSettlement;
        gameObject.SetActive(true);
        proceed.interactable = true;
        treasureMode = battle.IsTreasure;
        Color color = battle.Victory ? new Color(.20f, .40f, .33f) : new Color(.58f, .24f, .20f);
        if (treasureMode) color = new Color(.62f, .40f, .12f);
        kicker.text = TimelineText(treasureMode ? "Timeline_TreasureKicker" : "Timeline_BattleSettlement");
        title.color = accent.color = healthFill.color = color;
        title.text = TimelineText(treasureMode ? "Timeline_TreasureOpened"
            : battle.Victory ? "Timeline_BattleVictory" : "Timeline_BattleDefeat");
        var world = view.Simulation.State;
        summaryHeight = battle.Victory || battle.Items.Count > 0 || battle.Techniques.Count > 0 ? 670f : 455f;
        string location = view.LocationName(world.Nodes[battle.NodeId]);
        if (battle.TrialItemId != 0) location += " · " + view.BattleDropName(battle.NodeId, battle.TrialItemId);
        caption.text = TimelineText("Timeline_UI_StoryCaption", world.Year, location);
        if (treasureMode)
        {
            summaryHeight = 670f;
            statsToggle.gameObject.SetActive(false);
            ShowTreasure(view, battle);
            SetStatsOpen(false);
            return;
        }
        health.text = $"{battle.Health} / {battle.MaxHealth}";
        float remaining = battle.MaxHealth > 0 ? Mathf.Clamp01(battle.Health / (float)battle.MaxHealth) : 0f;
        healthFill.rectTransform.localScale = new Vector3(remaining, 1f, 1f);
        showBacklash = battle.RitualBacklash;
        showCultivation = battle.Victory || battle.Experience > 0;
        experience.text = $"+{battle.Experience}";
        breakthrough.gameObject.SetActive(battle.Level > battle.PreviousLevel);
        breakthrough.text = TimelineText("Timeline_BattleBreakthrough", battle.PreviousLevel, battle.Level);
        showLoot = battle.Victory || battle.Items.Count > 0;
        ShowLoot(view, battle);
        bool hasLightning = battle.TrialItemId != 0 || battle.Lightning.Applications > 0 ||
            battle.Lightning.Transfers > 0 || battle.Lightning.Jumps > 0;
        var progressLines = battle.Techniques.Select(art =>
        {
            string name = TechniqueName(art.Id);
            if (art.Rank > art.PreviousRank)
                return $"<color=#87601F>{TimelineText("Timeline_BattleTechniqueRank", name, art.Rank, TechniqueSkillName(art.SkillId))}</color>";
            return TimelineText(art.Requirement == TechniqueRequirement.SkillKills
                ? "Timeline_BattleTechniqueKills" : "Timeline_BattleTechniqueXP", name, art.Progress);
        }).ToList();
        if (hasLightning)
        {
            progressLines.Add(TimelineText("Timeline_BattleLightningCounts",
                battle.Lightning.Applications, battle.Lightning.Transfers, battle.Lightning.Jumps));
        }
        showTechnique = progressLines.Count > 0;
        technique.text = string.Join("\n", progressLines);
        statsToggle.gameObject.SetActive(battle.CombatOutput.Count > 0);
        FillStats(view, battle);
        SetStatsOpen(false);
    }

    private void SetStatsOpen(bool open)
    {
        statsOpen = open;
        bool summary = !open && !treasureMode;
        healthSection.SetActive(summary);
        cultivation.SetActive(summary && showCultivation);
        rewards.SetActive(summary && showLoot);
        techniqueSection.SetActive(summary && showTechnique);
        backlash.gameObject.SetActive(summary && showBacklash);
        statsPage.SetActive(open);
        treasurePage.SetActive(treasureMode);
        statsToggleText.text = TimelineText(open ? "Timeline_UI_60" : "Timeline_BattleStats");
        card.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, open ? 670f : summaryHeight);
        if (open)
        {
            statsScroll.StopMovement();
            statsScroll.content.anchoredPosition = Vector2.zero;
        }
    }

    private void Update()
    {
        if (!treasureMode) return;
        float elapsed = Time.unscaledTime - revealStarted;
        float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .36f));
        treasureArt.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, reveal);
        treasureSparks.alpha = Mathf.Lerp(1f, .44f, reveal) + .14f * Mathf.Sin(elapsed * 4f) * reveal;
    }

    private void ShowTreasure(WorldTimelineDebugView view, BattleSettlement battle)
    {
        revealStarted = Time.unscaledTime;
        treasureArt.localScale = Vector3.one * .94f;
        treasureSparks.alpha = 1f;
        int i = 0;
        foreach (var group in battle.Items.GroupBy(id => id))
        {
            if (i == treasureRows.Count) treasureRows.Add(Instantiate(treasureRowTemplate, treasureScroll.content, false));
            var row = treasureRows[i];
            var entry = view.DescribeMapLoot(group.Key, battle.NodeId);
            row.anchoredPosition = new Vector2(0f, -i++ * 132f);
            row.Find("Name").GetComponent<TMP_Text>().text = $"{entry.Name} ×{group.Count()}";
            row.Find("Description").GetComponent<TMP_Text>().text = entry.Description;
            row.Find("Icon").GetComponent<Image>().sprite = LootIcon(view, entry);
            row.gameObject.SetActive(true);
        }
        for (int j = i; j < treasureRows.Count; j++) treasureRows[j].gameObject.SetActive(false);
        treasureScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(treasureScroll.viewport.rect.height, i * 132f));
        treasureScroll.StopMovement();
        treasureScroll.content.anchoredPosition = Vector2.zero;
    }

    private Sprite LootIcon(WorldTimelineDebugView view, WorldTimelineDebugView.EquipmentEntry entry)
    {
        var nativeItems = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        int nativeId = WorldTimelineBattleResolver.InventorySkillItem(entry.Id, view.Simulation.State.StoryCombatSkillsEnabled);
        int skillId = nativeItems?.HasDataRow(nativeId) == true ? nativeItems[nativeId].SkillID : 0;
        return WorldTimelineInventoryCtrl.ResolveInventoryIcon(entry.Id, skillId, entry.Icon, itemIcons, techniqueIcons);
    }

    private void FillStats(WorldTimelineDebugView view, BattleSettlement battle)
    {
        int index = 0;
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        var state = view.Simulation.State;
        foreach (var actor in battle.CombatOutput.OrderByDescending(actor => actor.Allied))
        {
            string side = TimelineText(actor.Allied ? "Timeline_BattleAllied" : "Timeline_BattleEnemy");
            string name = actor.ActorId == state.PlayerActorId ? view.PlayerName
                : actor.ActorId == state.ChosenActorId ? TimelineText("Timeline_UI_79")
                : state.Actors.ContainsKey(actor.ActorId) ? TimelineText("Timeline_UI_139") + actor.ActorId
                : TimelineText("Timeline_BattleGuardian", Math.Abs(actor.ActorId + battle.NodeId) + 1);
            SetRow(side + " · " + name, actor.Skills.Sum(x => x.Damage), actor.Skills.Sum(x => x.Shield),
                actor.Skills.Sum(x => x.Healing), true);
            foreach (var skill in actor.Skills)
            {
                string skillName = (skill.ItemId == 0 || skill.SkillId == 1) ? TechniqueSkillName(skill.SkillId)
                    : itemTable?.HasDataRow(skill.ItemId) == true ? TimelineText(itemTable[skill.ItemId].Name)
                    : TechniqueSkillName(skill.SkillId);
                SetRow("    " + skillName, skill.Damage, skill.Shield, skill.Healing, false);
            }
        }
        for (int i = index; i < statsRows.Count; i++) statsRows[i].gameObject.SetActive(false);
        statsScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(statsScroll.viewport.rect.height, index * 38f));

        void SetRow(string label, long damage, long shield, long healing, bool heading)
        {
            if (index == statsRows.Count) statsRows.Add(Instantiate(statsRowTemplate, statsScroll.content, false));
            var row = statsRows[index];
            row.anchoredPosition = new Vector2(0f, -38f * index++);
            row.GetComponent<Image>().enabled = heading;
            row.Find("Name").GetComponent<TMP_Text>().text = label;
            row.Find("Damage").GetComponent<TMP_Text>().text = damage.ToString("N0");
            row.Find("Shield").GetComponent<TMP_Text>().text = shield.ToString("N0");
            row.Find("Healing").GetComponent<TMP_Text>().text = healing.ToString("N0");
            row.gameObject.SetActive(true);
        }
    }

    private void ShowLoot(WorldTimelineDebugView view, BattleSettlement battle)
    {
        int index = 0;
        var nativeItems = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        foreach (var group in battle.Items.GroupBy(id => id))
        {
            if (index == rows.Count) rows.Add(Instantiate(itemTemplate, lootScroll.content, false));
            var row = rows[index];
            row.anchoredPosition = new Vector2(0f, -index * 64f);
            var name = row.Find("Name").GetComponent<TMP_Text>();
            name.text = view.BattleDropName(battle.NodeId, group.Key);
            row.Find("Count").GetComponent<TMP_Text>().text = $"×{group.Count()}";
            var nativeId = WorldTimelineBattleResolver.InventorySkillItem(group.Key, view.Simulation.State.StoryCombatSkillsEnabled);
            var nativeItem = nativeItems?.HasDataRow(nativeId) == true ? nativeItems[nativeId] : null;
            name.color = nativeItem != null && nativeItem.Rarity > 1
                ? new Color(.53f, .37f, .12f) : new Color(.15f, .22f, .20f);
            row.Find("Icon").GetComponent<Image>().sprite = LootIcon(view, view.DescribeMapLoot(group.Key, battle.NodeId));
            row.gameObject.SetActive(true);
            index++;
        }
        for (int i = index; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
        noDrops.gameObject.SetActive(index == 0);
        lootScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(lootScroll.viewport.rect.height, index * 64f));
        lootScroll.StopMovement();
        lootScroll.content.anchoredPosition = Vector2.zero;
    }
}
