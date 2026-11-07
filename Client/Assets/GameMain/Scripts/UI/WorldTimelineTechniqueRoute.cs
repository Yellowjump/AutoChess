using System;
using DataTable;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using static WorldTimelineDebug.WorldTimelineDebugView;

public sealed class WorldTimelineTechniqueRoute : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    public const int VisibleStages = 3;
    [SerializeField] private Image[] frames, icons, links, fills;
    [SerializeField] private TMP_Text[] stageLabels, stageStates, linkLabels;
    [SerializeField] private Sprite[] skillIcons;
    [SerializeField] private TMP_Text heading, detailTitle, detailState, detailCondition, detailEffect;
    [SerializeField] private Image detailIcon;

    private static readonly Color Jade = new Color(.20f, .43f, .35f);
    private static readonly Color Brass = new Color(.71f, .52f, .29f);
    private static readonly Color Stone = new Color(.48f, .45f, .39f);
    private TechniqueDefinition route;
    private TechniqueProgress progress;
    private Func<TechniqueStage, int, string> formatCondition;
    private ActorState actor;
    private int first, count, hovered = -1;

    public void Show(TechniqueDefinition definition, int firstIndex, TechniqueProgress current,
        ActorState player, Func<TechniqueStage, int, string> condition)
    {
        if (route != definition || first != firstIndex) hovered = -1;
        route = definition;
        first = firstIndex;
        progress = current;
        actor = player;
        formatCondition = condition;
        count = Math.Min(VisibleStages, route.Stages.Count - first);
        heading.text = TechniqueName(route.Id);
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i].gameObject.SetActive(i < count);
            if (i >= count) continue;
            int rank = first + i + 1;
            var stage = route.Stages[rank - 1];
            frames[i].color = i == hovered ? Brass : rank == progress?.Rank ? Jade : Stone;
            icons[i].sprite = Icon(stage.SkillId);
            icons[i].color = rank <= progress?.Rank ? Color.white : new Color(.63f, .62f, .58f);
            stageLabels[i].text = TimelineText("Timeline_Technique_Stage", rank, TechniqueSkillName(stage.SkillId));
            stageStates[i].text = Status(rank);
            stageStates[i].color = rank == progress?.Rank ? Jade : Stone;
        }
        for (int i = 0; i < links.Length; i++)
        {
            links[i].gameObject.SetActive(i + 1 < count);
            if (i + 1 >= count) continue;
            int targetRank = first + i + 2;
            var next = route.Stages[targetRank - 1];
            int amount = Amount(next, targetRank);
            float ratio = next.Value == 0 ? 0f : Mathf.Clamp01(amount / (float)next.Value);
            var fillRect = fills[i].rectTransform;
            fillRect.anchorMax = new Vector2(ratio, 1f);
            linkLabels[i].text = next.Requirement == TechniqueRequirement.Item
                ? TimelineText(amount > 0 ? "Timeline_Technique_ItemHeld" : "Timeline_Technique_ItemMissing")
                : formatCondition(next, amount);
        }
        int selected = hovered >= 0 ? hovered : Mathf.Clamp((progress?.Rank ?? 1) - first - 1, 0, count - 1);
        int detailRank = first + selected + 1;
        var detail = route.Stages[detailRank - 1];
        detailIcon.sprite = Icon(detail.SkillId);
        detailTitle.text = TimelineText("Timeline_Technique_Stage", detailRank, TechniqueSkillName(detail.SkillId));
        detailState.text = Status(detailRank);
        detailCondition.text = detailRank == progress?.Rank && progress.NextStage is TechniqueStage nextStage
            ? formatCondition(nextStage, Amount(nextStage, detailRank + 1))
            : formatCondition(detail, Amount(detail, detailRank));
        detailEffect.text = DescribeSkill(detail.SkillId);
        string flavor = DetailFlavor(detail.SkillId);
        if (!string.IsNullOrEmpty(flavor))
            detailEffect.text += $"\n\n<size=80%><color=#746A57>{flavor}</color></size>";
    }

    private int Amount(TechniqueStage stage, int rank)
    {
        if (progress?.Rank >= rank) return stage.Value;
        if (stage.Requirement == TechniqueRequirement.Item)
        {
                foreach (int item in actor.Items)
                if (item == stage.Value) return stage.Value;
            return 0;
        }
        return progress?.Rank == rank - 1 ? progress.Progress : 0;
    }

    private string Status(int rank) => TimelineText(rank == progress?.Rank ? "Timeline_Technique_Current"
        : rank < progress?.Rank ? "Timeline_Technique_Unlocked" : "Timeline_Technique_Unlearned");

    private Sprite Icon(int skill) => skillIcons[skill == 1001 ? 1 : skill == 8 ? 2 : 0];

    private string DescribeSkill(int id)
    {
        string template = DescriptionTemplate(id);
        if (template == null) return TimelineText("Timeline_Technique_SkillNumber", id);
        return System.Text.RegularExpressions.Regex.Replace(
            HotGameEntry.HeroManager.FormatTimelineItemDesc(template, actor), "</?color[^>]*>", "");
    }

    internal static string DescriptionTemplate(int id)
    {
        switch (id)
        {
            case 1: return TimelineText(HeroComponent.TimelineSlashDescriptionKey);
            case 8: return TimelineText("Timeline_Technique_FlightDescription");
            case 9104: return TimelineText("Timeline_Technique_BellShieldDescription");
            case 1001:
                var table = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
                return table != null && table.HasDataRow(id)
                    ? TimelineText("Timeline_Technique_LongReachDescription").Replace("{1}", table[id].IntParam1.ToString())
                    : null;
            default: return null;
        }
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        for (int i = 0; i < count; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(frames[i].rectTransform, eventData.position,
                eventData.enterEventCamera))
            {
                SetHover(i);
                return;
            }
        SetHover(-1);
    }

    public void OnPointerExit(PointerEventData eventData) => SetHover(-1);
    private void OnDisable() => hovered = -1;

    private void SetHover(int index)
    {
        if (hovered == index || route == null) return;
        hovered = index;
        Refresh();
    }
}
