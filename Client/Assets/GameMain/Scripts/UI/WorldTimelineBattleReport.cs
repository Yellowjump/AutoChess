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
    private TMP_Text partyHealth;
    private readonly List<(TMP_Text Label, RectTransform Fill)> partyRows = new();
    private readonly List<(RectTransform Fill, float Start, float End, float Settled)> healthChanges = new();
    private float healthStarted;
    private bool treasureMode;
    private float revealStarted;
    private TMP_Text techniqueHeading;
    private ScrollRect techniqueScroll;
    private string regularTechniqueHeading;

    private void Awake()
    {
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            string key = label.text;
            if (key == "Timeline_BattleDrops") label.text = TimelineText(key, "").TrimEnd(' ', '·');
            else if (!string.IsNullOrEmpty(key) && (key.StartsWith("Timeline_", StringComparison.Ordinal) || key == "Attr_HuDun"))
                label.text = TimelineText(key);
        }
        var note = statsPage.transform.Find("Stats Note").GetComponent<TMP_Text>();
        note.text += TimelineText("Timeline_BattleStatsDamage") == "Damage"
            ? "  Shares: combatant / side, skill / combatant." : "  占比：角色占本方，技能与法器占角色。";
        note.enableAutoSizing = true;
        note.fontSizeMin = 13;
        var damageCell = statsRowTemplate.Find("Damage").GetComponent<TMP_Text>();
        CreateTrack(damageCell.rectTransform, 5f);
        damageCell.enableAutoSizing = true;
        damageCell.fontSizeMin = 12;
        damageCell.margin = new Vector4(0, 0, 0, 5);
        UIFormLogic.BindButtonClickSound(proceed);
        techniqueHeading = techniqueSection.transform.Find("Technique Heading").GetComponent<TMP_Text>();
        regularTechniqueHeading = techniqueHeading.text;
        var brushFont = Resources.Load<TMP_FontAsset>("InterfaceKai");
        if (brushFont != null) title.font = brushFont;
        var viewport = new GameObject("Technique Viewport", typeof(RectTransform), typeof(RectMask2D),
            typeof(Image), typeof(ScrollRect));
        viewport.transform.SetParent(techniqueSection.transform, false);
        viewport.GetComponent<Image>().color = Color.clear;
        var frame = (RectTransform)viewport.transform;
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = new Vector2(23, 14);
        frame.offsetMax = new Vector2(-23, -48);
        technique.rectTransform.SetParent(frame, false);
        technique.rectTransform.anchorMin = Vector2.up;
        technique.rectTransform.anchorMax = Vector2.one;
        technique.rectTransform.pivot = Vector2.up;
        technique.rectTransform.sizeDelta = Vector2.zero;
        technique.alignment = TextAlignmentOptions.TopLeft;
        techniqueScroll = viewport.GetComponent<ScrollRect>();
        techniqueScroll.viewport = frame;
        techniqueScroll.content = technique.rectTransform;
        techniqueScroll.horizontal = false;
        techniqueScroll.movementType = ScrollRect.MovementType.Clamped;
        partyHealth = Instantiate(health, healthSection.transform);
        partyHealth.name = "PartyHealth";
        var rect = partyHealth.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(20, 5);
        rect.offsetMax = new Vector2(-16, -5);
        partyHealth.alignment = TextAlignmentOptions.MidlineLeft;
        partyHealth.enableAutoSizing = true;
        partyHealth.fontSizeMin = 15;
        partyHealth.fontSizeMax = 22;
        partyHealth.gameObject.SetActive(false);
        UIFormLogic.BindButtonClickSound(statsToggle);
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
        bool vanguard = world.Nodes[battle.NodeId].IsExpeditionVanguard && !battle.IsTreasure;
        techniqueHeading.text = vanguard ? "战术回顾" : regularTechniqueHeading;
        string location = view.LocationName(world.Nodes[battle.NodeId]);
        if (battle.TrialItemId != 0) location += " · " + view.BattleDropName(battle.NodeId, battle.TrialItemId);
        caption.text = TimelineText("Timeline_UI_StoryCaption", world.Year, location);
        if(battle.SpiritStones>0)caption.text+=$"  ·  灵石 +{battle.SpiritStones}";
        bool comparison = false;
#if UNITY_EDITOR
        if (view.BattleComparisonLabel is string label)
        {
            comparison = true;
            caption.text = label + " · " + location;
        }
#endif
        if (treasureMode)
        {
            summaryHeight = 670f;
            statsToggle.gameObject.SetActive(false);
            ShowTreasure(view, battle);
            SetStatsOpen(false);
            return;
        }
        healthChanges.Clear();
        healthStarted = Time.unscaledTime;
        ShowPartyHealth(view, battle);
        if (health.gameObject.activeSelf)
        {
            var member = battle.PartyAtBattleEnd.FirstOrDefault(m => m.ActorId == world.PlayerActorId);
            health.text = $"{member.StartHealth} → {member.Health} / {member.MaxHealth}";
            if (battle.Health != member.Health || battle.MaxHealth != member.MaxHealth)
                health.text += $"  <size=65%>战后 {battle.Health}/{battle.MaxHealth}</size>";
            healthChanges.Add((healthFill.rectTransform, HealthRatio(member.StartHealth, member.MaxHealth),
                HealthRatio(member.Health, member.MaxHealth), HealthRatio(battle.Health, battle.MaxHealth)));
        }
        AnimateHealth(0f);
        showBacklash = battle.RitualBacklash;
        showCultivation = !comparison && (battle.Victory || battle.Experience > 0);
        experience.text = $"+{battle.Experience}";
        breakthrough.gameObject.SetActive(battle.Level > battle.PreviousLevel);
        breakthrough.text = TimelineText("Timeline_BattleBreakthrough", battle.PreviousLevel, battle.Level);
        showLoot = !comparison && (battle.Victory || battle.Items.Count > 0);
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
        if (world.Nodes[battle.NodeId].IsExpeditionVanguard)
        {
            var shield = battle.CombatOutput.FirstOrDefault(actor => actor.ActorId == view.Expedition?.CompanionId(0));
            var leader = battle.CombatOutput.FirstOrDefault(actor => actor.ActorId == world.PlayerActorId);
            progressLines.Add(shield == null ? "岩舟未上阵：可尝试用前排盾卫保护主角。"
                : $"岩舟承伤 {shield.DamageTaken:N0} · 护盾吸收 {shield.Skills.Sum(skill => skill.ShieldAbsorbed):N0}\n" +
                  $"主角损失气血 {leader?.HealthLost ?? 0:N0}（战后恢复前）");
            if (battle.Victory && view.Expedition?.HasRecruited(2) == false)
                progressLines.Add("阿翎条件达成：前往同伴关卡。");
        }
        var links = battle.CombatOutput.Where(actor => actor.Allied && actor.Artifacts != null).Select(actor => actor.Artifacts).ToArray();
        if (links.Any(link => link.SharedDamage > 0)) progressLines.Add($"镇砂代受 {links.Sum(link => link.SharedDamage):N0}");
        if (links.Any(link => link.OpeningSwordDamage > 0)) progressLines.Add($"养剑开场伤害 {links.Sum(link => link.OpeningSwordDamage):N0}");
        if (links.Any(link => link.HasRainTassel))
        {
            progressLines.Add($"听雨剑穗 · 触发 {links.Sum(x => x.RainVolleys)} 次 · 飞剑 {links.Sum(x => x.RainSwords)} 柄 · 伤害 {links.Sum(x => x.RainDamage):N0}");
            if (links.Any(x => x.TideShields > 0))
                progressLines.Add($"佩戴者听潮盾 · 生成 {links.Sum(x => x.TideShields)} 次 · 敌方击破 {links.Sum(x => x.TideBreaks)} 次 · 到期 {links.Sum(x => x.TideExpirations)} 次");
        }
        if (links.Any(link => link.Quakes > 0)) progressLines.Add($"震山钉 · 震击 {links.Sum(link => link.Quakes)} 次 · 命中 {links.Sum(link => link.QuakeHits)} 人次 · 伤害 {links.Sum(link => link.QuakeDamage):N0}");
        if (links.Any(link => link.SwordDamage > 0)) progressLines.Add($"藏锋飞剑伤害 {links.Sum(link => link.SwordDamage):N0}");
        if (links.Any(link => link.GrantedShield > link.KnotShield)) progressLines.Add($"法器护盾 {links.Sum(link => link.GrantedShield - link.KnotShield):N0}");
        if (links.Any(link => link.Pursuits > 0))
            progressLines.Add($"追击 {links.Sum(link => link.Pursuits)} 次 · 追加伤害 {links.Sum(link => link.Damage):N0}");
        if (links.Any(link => link.RelayHits > 0))
            progressLines.Add($"连环扣接力 {links.Sum(link => link.RelayHits)} 次 · 追加伤害 {links.Sum(link => link.RelayDamage):N0}");
        if (battle.FieldTreatment.Amount > 0)
        {
            string patient = battle.FieldTreatment.Role < 0 ? view.OriginalPlayerName : view.CompanionName(battle.FieldTreatment.Role).Split('·')[0].Trim();
            progressLines.Add($"行医囊 · {patient} 战后恢复 {battle.FieldTreatment.Amount} 气血");
        }
        var allies = battle.CombatOutput.Where(actor => actor.Allied).ToArray();
        if (allies.Any(actor => actor.CriticalHits > 0))
            progressLines.Add($"暴击命中 {allies.Sum(actor => actor.CriticalHits)} 段");
        if (allies.Any(actor => actor.MarksApplied > 0))
            progressLines.Add($"照隙镜 · 标记 {allies.Sum(actor => actor.MarksApplied)} 次 · 队友触发 {allies.Sum(actor => actor.MarksUsed)} 次");
        if (links.Any(link => link.EchoHits > 0))
            progressLines.Add($"回响玉简 · 回响 {links.Sum(link => link.EchoHits)} 次 · 追加伤害 {links.Sum(link => link.EchoDamage):N0}");
        if (links.Any(link => link.KnotShield > 0))
            progressLines.Add($"同袍结 · 护援 {links.Count(link => link.KnotShield > 0)} 次 · 护盾 {links.Sum(link => (long)link.KnotShield):N0}");
        if (links.Any(link => link.DoubleAfflictionHits > 0 || link.TripleAfflictionHits > 0))
            progressLines.Add($"三煞印 · 两种异常 {links.Sum(link => link.DoubleAfflictionHits)} 次 · 三种异常 {links.Sum(link => link.TripleAfflictionHits)} 次 · 追加伤害 {links.Sum(link => link.AfflictionDamage):N0}");
        if (links.Any(link => link.LastStandActive))
            progressLines.Add($"背水符 · 强化普攻 {links.Sum(link => link.LastStandHits)} 次 · 追加伤害 {links.Sum(link => link.LastStandDamage):N0}");
        if (links.Any(link => link.MirrorWeakens > 0))
            progressLines.Add($"折势镜 · 施加或刷新虚弱 {links.Sum(link => link.MirrorWeakens)} 次 · 叠加破绽 {links.Sum(link => link.MirrorOpenings)} 层");
        if (links.Any(link => link.ThunderBellJumps > 0))
            progressLines.Add($"鸣雷铃 · 跳雷 {links.Sum(link => link.ThunderBellJumps)} 次 · 伤害 {links.Sum(link => link.ThunderBellDamage):N0}");
        if (links.Any(link => link.MeridianLayers > 0 || link.MeridianBursts > 0))
            progressLines.Add($"截脉钉 · 施加流血 {links.Sum(link => link.MeridianLayers)} 层 · 截脉 {links.Sum(link => link.MeridianBursts)} 次 · 提前结算伤害 {links.Sum(link => link.MeridianBurstDamage):N0}");
        if (links.Any(link => link.TinderLayers > 0))
            progressLines.Add($"火绒囊 · 施加燃烧 {links.Sum(link => link.TinderLayers)} 层 · 持续伤害 {links.Sum(link => link.TinderDamage):N0}");
        if (links.Any(link => link.LampTransfers > 0))
            progressLines.Add($"同心灯传疗 {links.Sum(link => link.LampTransfers)} 次 · 恢复气血 {links.Sum(link => link.LampHealing):N0}");
        if (links.Any(link => link.ReprisalHits > 0))
            progressLines.Add($"偿锋印 {links.Sum(link => link.ReprisalHits)} 次 · 追加伤害 {links.Sum(link => link.ReprisalDamage):N0}");
        if (links.Any(link => link.Remedies > 0))
            progressLines.Add($"回春 {links.Sum(link => link.Remedies)} 次 · 治疗 {links.Sum(link => link.Healing + link.BonusHealing):N0}（含养血 {links.Sum(link => link.BonusHealing):N0}）");
        showTechnique = progressLines.Count > 0;
        float detailTop = showBacklash ? 400f : 366f;
        var lootRect = (RectTransform)rewards.transform;
        var techniqueRect = (RectTransform)techniqueSection.transform;
        lootRect.anchoredPosition = new Vector2(56, -detailTop);
        techniqueRect.anchoredPosition = new Vector2(showLoot ? 578 : 56, -detailTop);
        summaryHeight = showLoot || showTechnique ? detailTop + 280f : showBacklash ? 485f : 448f;
        technique.text = string.Join("\n", progressLines);
        technique.enableAutoSizing = false;
        technique.fontSize = 20;
        technique.lineSpacing = 8;
        technique.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(techniqueScroll.viewport.rect.height,
                technique.GetPreferredValues(technique.text, techniqueScroll.viewport.rect.width, Mathf.Infinity).y));
        techniqueScroll.StopMovement();
        technique.rectTransform.anchoredPosition = Vector2.zero;
        statsToggle.gameObject.SetActive(battle.CombatOutput.Count > 0);
        FillStats(view, battle);
        SetStatsOpen(false);
    }

    private void ShowPartyHealth(WorldTimelineDebugView view, BattleSettlement battle)
    {
        bool team = battle.PartyAtBattleEnd.Count > 1 || battle.SuccessorRole >= 0 || battle.Attrition && battle.Health == 0;
        foreach (Transform child in healthSection.transform)
            child.gameObject.SetActive(child == partyHealth.transform ? team : !team);
        foreach (var row in partyRows) row.Label.gameObject.SetActive(false);
        if (!team) return;
        int leader = view.Simulation.State.PlayerActorId;
        string heading = "上阵气血";
        int index = 0;
        float rowHeight = Mathf.Min(51f, 129f / Mathf.Max(1, battle.PartyAtBattleEnd.Count));
        foreach (var member in battle.PartyAtBattleEnd)
        {
            bool isLeader = member.ActorId == leader;
            string name = isLeader ? (battle.Attrition ? battle.PreviousLeaderRole < 0 ? view.OriginalPlayerName :
                view.CompanionName(battle.PreviousLeaderRole).Split('·')[0].Trim() : view.PlayerName) : view.CompanionName(ExpeditionParty.Role(member.ActorId)).Split('·')[0].Trim();
            string state;
            if (battle.Attrition)
                state = isLeader && battle.RitualBacklash && member.Health > 0 ? "反噬阵亡" : member.Health == 0 ? "阵亡 · 装备归队" : ExpeditionParty.Role(member.ActorId) == battle.SuccessorRole && !isLeader
                    ? "接任队长" : "存活";
            else if (isLeader)
                state = battle.Health == 0 ? "本世身亡" : member.Health == 0 ? $"获救 · 战后 {battle.Health}"
                    : battle.Health == member.Health ? "存活" : $"战后 {battle.Health}";
            else
                state = battle.Health == 0 || view.Simulation.State.IsEnded
                    ? (member.Health == 0 ? "倒下" : "存活")
                    : (member.Health == 0 ? "倒下 · 下场满血" : "下场满血");
            if (index == partyRows.Count)
            {
                var label = Instantiate(health, healthSection.transform);
                var track = CreateTrack(label.rectTransform, 6f);
                var rail = (RectTransform)track.parent;
                rail.offsetMin = new Vector2(146, 5);
                rail.offsetMax = new Vector2(-176, 11);
                partyRows.Add((label, track));
            }
            var row = partyRows[index++];
            var rect = row.Label.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(-44, rowHeight);
            rect.anchoredPosition = new Vector2(24, -46 - (index - 1) * rowHeight);
            row.Label.alignment = TextAlignmentOptions.MidlineLeft;
            row.Label.enableAutoSizing = true;
            row.Label.fontSizeMin = 14;
            row.Label.fontSizeMax = 20;
            row.Label.margin = new Vector4(0, 0, 0, 9);
            row.Label.text = $"<b>{name}</b><pos=146>{member.StartHealth} → <b>{member.Health}</b><size=80%> / {member.MaxHealth}</size>" +
                $"<pos=420><size=80%><color={(member.Health == 0 ? "#924D3A" : "#526659")}>{state}</color></size>";
            row.Label.gameObject.SetActive(true);
            float end = HealthRatio(member.Health, member.MaxHealth);
            healthChanges.Add((row.Fill, HealthRatio(member.StartHealth, member.MaxHealth), end, end));
        }
        if (battle.SuccessorRole >= 0 && !battle.PartyAtBattleEnd.Any(m => ExpeditionParty.Role(m.ActorId) == battle.SuccessorRole))
            heading += " · " + view.CompanionName(battle.SuccessorRole).Split('·')[0].Trim() + "从候补接任队长";
        partyHealth.rectTransform.anchorMin = new Vector2(0, 1);
        partyHealth.rectTransform.offsetMin = new Vector2(24, -42);
        partyHealth.rectTransform.offsetMax = new Vector2(-24, -12);
        partyHealth.text = heading;
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

    private static float HealthRatio(int hp, int maximum) => maximum > 0 ? Mathf.Clamp01((float)hp / maximum) : 0f;

    private static RectTransform CreateTrack(RectTransform parent, float height)
    {
        var track = new GameObject("Track", typeof(Image)).GetComponent<Image>();
        track.transform.SetParent(parent, false);
        track.color = new Color(.22f, .26f, .23f, .18f);
        track.raycastTarget = false;
        var rect = track.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.right;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(0, height);
        var fill = new GameObject("Fill", typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(rect, false);
        fill.color = new Color(.19f, .43f, .35f);
        fill.raycastTarget = false;
        var bar = fill.rectTransform;
        bar.anchorMin = Vector2.zero;
        bar.anchorMax = Vector2.one;
        bar.pivot = Vector2.zero;
        bar.offsetMin = bar.offsetMax = Vector2.zero;
        return bar;
    }

    private void AnimateHealth(float elapsed)
    {
        foreach (var change in healthChanges)
        {
            float value = elapsed <= .75f ? Mathf.SmoothStep(change.Start, change.End, elapsed / .75f)
                : Mathf.SmoothStep(change.End, change.Settled, (elapsed - .9f) / .45f);
            change.Fill.localScale = new Vector3(value, 1f, 1f);
        }
        if (elapsed >= 1.35f) healthChanges.Clear();
    }

    private void Update()
    {
        if (!treasureMode)
        {
            if (healthChanges.Count > 0) AnimateHealth(Time.unscaledTime - healthStarted);
            return;
        }
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
        long TotalDamage(ActorBattleOutput actor)
        {
            var links = actor.Artifacts;
            return actor.Skills.Sum(x => x.Damage) + (links?.Damage ?? 0) + (links?.RelayDamage ?? 0) + (links?.ReprisalDamage ?? 0) + (links?.TinderDamage ?? 0) + (links?.MeridianTickDamage ?? 0) + (links?.MeridianBurstDamage ?? 0) + (links?.ThunderBellDamage ?? 0) + (links?.LastStandDamage ?? 0) + (links?.AfflictionDamage ?? 0) + (links?.EchoDamage ?? 0) + (links?.SwordDamage ?? 0) + (links?.OpeningSwordDamage ?? 0) + (links?.QuakeDamage ?? 0) + (links?.RainDamage ?? 0);
        }
        var totals = battle.CombatOutput.GroupBy(a => a.Allied).ToDictionary(g => g.Key, g => g.Sum(TotalDamage));
        long actorDamage = 0, sideDamage = 0;
        Color sideColor = default;
        foreach (var actor in battle.CombatOutput.OrderByDescending(actor => actor.Allied).ThenByDescending(TotalDamage))
        {
            sideColor = actor.Allied ? new Color(.19f, .42f, .34f) : new Color(.62f, .32f, .23f);
            actorDamage = TotalDamage(actor);
            sideDamage = totals[actor.Allied];
            string side = TimelineText(actor.Allied ? "Timeline_BattleAllied" : "Timeline_BattleEnemy");
            string name = actor.ActorId == state.PlayerActorId ? (battle.Attrition ? battle.PreviousLeaderRole < 0 ? view.OriginalPlayerName : view.CompanionName(battle.PreviousLeaderRole) : view.PlayerName)
                : ExpeditionParty.IsCompanion(actor.ActorId) ? view.CompanionName(ExpeditionParty.Role(actor.ActorId))
                : actor.ActorId == state.ChosenActorId ? TimelineText("Timeline_UI_79")
                : state.Actors.ContainsKey(actor.ActorId) ? TimelineText("Timeline_UI_139") + actor.ActorId
                : TimelineText("Timeline_BattleGuardian", Math.Abs(actor.ActorId + battle.NodeId) + 1);
            var links = actor.Artifacts;
            SetRow(side + " · " + name + $"  承伤 {actor.DamageTaken:N0}", actorDamage, actor.Skills.Sum(x => x.Shield) + (links?.GrantedShield ?? 0),
                actor.Skills.Sum(x => x.ShieldAbsorbed),
                actor.Skills.Sum(x => x.Healing) + (links?.Healing ?? 0) + (links?.BonusHealing ?? 0) + (links?.RepeatHealing ?? 0) + (links?.LampHealing ?? 0), true);
            if (links?.RepeatHealing > 0) SetRow("    留春复疗",0,0,0,links.RepeatHealing,false);
            if (links?.SharedDamage > 0) SetRow($"    镇砂代受 {links.SharedDamage:N0}",0,0,0,0,false);
            if (links?.OpeningSwordDamage > 0) SetRow("    养剑开场",links.OpeningSwordDamage,0,0,0,false);
            if (links?.HasRainTassel == true) SetRow($"    听雨剑穗 ×{links.RainVolleys} · 出剑 {links.RainSwords} 柄",links.RainDamage,0,0,0,false);
            if (links?.Quakes > 0) SetRow($"    震山钉 ×{links.Quakes} · 命中 {links.QuakeHits} 人次",links.QuakeDamage,0,0,0,false);
            if (links?.SwordDamage > 0) SetRow("    藏锋飞剑",links.SwordDamage,0,0,0,false);
            if (links != null && links.GrantedShield > links.KnotShield) SetRow("    法器护盾",0,links.GrantedShield - links.KnotShield,0,0,false);
            if (links?.Openings > 0) SetRow($"    反击标记 ×{links.Openings}",0,0,0,0,false);
            if (links?.Pursuits > 0) SetRow($"    追击 ×{links.Pursuits}",links.Damage,0,0,0,false);
            if (links?.RelayHits > 0) SetRow($"    接力追击 ×{links.RelayHits}",links.RelayDamage,0,0,0,false);
            if (links?.MeridianLayers > 0) SetRow($"    截脉钉 · 流血 {links.MeridianLayers} 层",links.MeridianTickDamage,0,0,0,false);
            if (actor.CriticalHits > 0) SetRow($"    暴击命中 {actor.CriticalHits} 段",0,0,0,0,false);
            if (actor.MarksApplied > 0) SetRow($"    照隙镜 · 标记 {actor.MarksApplied} 次 · 队友触发 {actor.MarksUsed} 次",0,0,0,0,false);
            if (links?.EchoHits > 0) SetRow($"    回响玉简 ×{links.EchoHits}",links.EchoDamage,0,0,0,false);
            if (links?.KnotShield > 0) SetRow("    同袍结 · 同列护盾",0,links.KnotShield,0,0,false);
            if (links?.DoubleAfflictionHits > 0 || links?.TripleAfflictionHits > 0)
                SetRow($"    三煞印 · 两种 ×{links.DoubleAfflictionHits} · 三种 ×{links.TripleAfflictionHits}",links.AfflictionDamage,0,0,0,false);
            if (links?.LastStandActive == true) SetRow($"    背水符 · 强化普攻 {links.LastStandHits}/3",links.LastStandDamage,0,0,0,false);
            if (links?.MirrorWeakens > 0) SetRow($"    折势镜 · 虚弱 ×{links.MirrorWeakens} · 破绽 +{links.MirrorOpenings}",0,0,0,0,false);
            if (links?.ThunderBellJumps > 0) SetRow($"    鸣雷铃 · 跳雷 ×{links.ThunderBellJumps}",links.ThunderBellDamage,0,0,0,false);
            if (links?.MeridianBursts > 0) SetRow($"    截脉钉 · 提前结算 ×{links.MeridianBursts}",links.MeridianBurstDamage,0,0,0,false);
            if (links?.TinderLayers > 0) SetRow($"    火绒囊 · 燃烧 {links.TinderLayers} 层",links.TinderDamage,0,0,0,false);
            if (links?.LampTransfers > 0) SetRow($"    同心灯传疗 ×{links.LampTransfers}",0,0,0,links.LampHealing,false);
            if (links?.ReprisalHits > 0) SetRow($"    偿锋印 ×{links.ReprisalHits}",links.ReprisalDamage,0,0,0,false);
            if (links?.Remedies > 0) SetRow($"    回春 ×{links.Remedies}",0,0,0,links.Healing,false);
            if (links?.Recoveries > 0) SetRow($"    养血 ×{links.Recoveries}",0,0,0,links.BonusHealing,false);
            foreach (var skill in actor.Skills)
            {
                string skillName = (skill.ItemId == 0 || skill.SkillId == 1) ? TechniqueSkillName(skill.SkillId)
                    : itemTable?.HasDataRow(skill.ItemId) == true ? TimelineText(itemTable[skill.ItemId].Name)
                    : TechniqueSkillName(skill.SkillId);
                SetRow("    " + skillName, skill.Damage, skill.Shield, skill.ShieldAbsorbed, skill.Healing, false);
            }
        }
        for (int i = index; i < statsRows.Count; i++) statsRows[i].gameObject.SetActive(false);
        statsScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(statsScroll.viewport.rect.height, index * 44f));

        void SetRow(string label, long damage, long shield, long shieldAbsorbed, long healing, bool heading)
        {
            if (index == statsRows.Count) statsRows.Add(Instantiate(statsRowTemplate, statsScroll.content, false));
            var row = statsRows[index];
            row.anchoredPosition = new Vector2(0f, -44f * index++);
            var band = row.GetComponent<Image>();
            band.enabled = heading;
            band.color = new Color(sideColor.r, sideColor.g, sideColor.b, .14f);
            var nameCell = row.Find("Name").GetComponent<TMP_Text>();
            nameCell.text = label;
            nameCell.enableAutoSizing = true;
            nameCell.fontSizeMin = 14;
            nameCell.fontSizeMax = heading ? 21 : 18;
            nameCell.fontStyle = heading ? FontStyles.Bold : FontStyles.Normal;
            var cell = row.Find("Damage").GetComponent<TMP_Text>();
            long total = heading ? sideDamage : actorDamage;
            float share = total > 0 ? Mathf.Clamp01((float)damage / total) : 0f;
            cell.text = $"{damage:N0}<pos=155><size=80%><color=#576157>{share:0.#%}</color></size>";
            var fill = cell.transform.Find("Track/Fill").GetComponent<Image>();
            fill.color = heading ? sideColor : Color.Lerp(sideColor, Color.white, .24f);
            fill.rectTransform.localScale = new Vector3(share, 1f, 1f);
            fill.transform.parent.gameObject.SetActive(damage > 0 || heading);
            row.Find("Shield").GetComponent<TMP_Text>().text = $"{shield:N0} / {shieldAbsorbed:N0}";
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
