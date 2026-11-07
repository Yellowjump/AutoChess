using static WorldTimelineDebug.WorldTimelineDebugView;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTimeline;
using WorldTimelineDebug;

public sealed class WorldTimelineMerchantCtrl : MonoBehaviour
{
    public const string PrefabPath = "Assets/GameMain/Prefeb/UIPrefab/world_timeline_merchant.prefab";
    [SerializeField] private TMP_Text title, intro;
    [SerializeField] private Button leave;
    [SerializeField] private ScrollRect stock;
    [SerializeField] private RectTransform rowTemplate;
    [SerializeField] private Sprite[] itemIcons, techniqueIcons;
    private readonly List<RectTransform> rows = new();
    private StoryEncounter shown;
    private WorldStorySystem story;
    private int leaveOption;

    private void Awake()
    {
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            if (text.text?.StartsWith("Timeline_", StringComparison.Ordinal) == true)
                text.text = TimelineText(text.text);
        leave.onClick.AddListener(() => story?.Choose(leaveOption));
    }

    private void OnDisable() { shown = null; story = null; }

    public void Refresh(WorldTimelineDebugView view, StoryEncounter encounter)
    {
        gameObject.SetActive(true);
        if (shown == encounter) return;
        shown = encounter;
        story = view.Simulation.Stories;
        title.text = encounter.Definition.Title;
        intro.text = encounter.Definition.Text;
        int index = 0;
        foreach (var option in encounter.Options)
        {
            if (option.ItemId == 0) { leaveOption = option.Id; continue; }
            if (index == rows.Count) rows.Add(Instantiate(rowTemplate, stock.content, false));
            var row = rows[index];
            row.anchoredPosition = new Vector2(0f, -index++ * 154f);
            var entry = view.DescribeMapLoot(option.ItemId);
            int nativeId = WorldTimelineBattleResolver.InventorySkillItem(option.ItemId, view.Simulation.State.StoryCombatSkillsEnabled);
            var items = UnityGameFramework.Runtime.GameEntry.DataTable.GetDataTable<DataTable.DRItem>("Item");
            int skillId = items?.HasDataRow(nativeId) == true ? items[nativeId].SkillID : 0;
            row.Find("Icon").GetComponent<Image>().sprite = WorldTimelineInventoryCtrl.ResolveInventoryIcon(
                option.ItemId, skillId, entry.Icon, itemIcons, techniqueIcons);
            row.Find("Name").GetComponent<TMP_Text>().text = entry.Name;
            row.Find("Kind").GetComponent<TMP_Text>().text = TimelineText(skillId == 0 ? "Timeline_UI_68" : "Timeline_UI_19");
            row.Find("Description").GetComponent<TMP_Text>().text = entry.Description;
            var button = row.Find("Buy").GetComponent<Button>();
            var reason = story.UnavailableReason(encounter.Actor, option);
            button.interactable = reason == null;
            button.GetComponentInChildren<TMP_Text>().text = story.ClaimedItems.Contains(option.ItemId)
                ? TimelineText("Timeline_MerchantSold") : reason ?? TimelineText("Timeline_MerchantBuy");
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { if (story.Choose(option.Id)) button.interactable = false; });
            row.gameObject.SetActive(true);
        }
        for (int i = index; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
        stock.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(stock.viewport.rect.height, index * 154f));
        stock.StopMovement();
        stock.content.anchoredPosition = Vector2.zero;
    }
}
