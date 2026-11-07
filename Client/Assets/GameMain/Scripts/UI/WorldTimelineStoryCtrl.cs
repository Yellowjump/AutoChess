using static WorldTimelineDebug.WorldTimelineDebugView;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

public sealed class WorldTimelineStoryCtrl : UIFormLogic
{
    [Serializable] private sealed class Background { public string key; public Sprite sprite; }
    [SerializeField] private GameObject presentation;
    [SerializeField] private TMP_Text title, body, caption, resultLabel;
    [SerializeField] private Image art;
    [SerializeField] private Button[] options;
    [SerializeField] private TMP_Text[] optionLabels;
    [SerializeField] private Button proceed;
    [SerializeField] private Background[] backgrounds;
    [SerializeField] private WorldTimelineBattleReport battleReportPrefab;
    [SerializeField] private WorldTimelineMerchantCtrl merchantPrefab;
    private WorldTimelineBattleReport battleReport;
    private WorldTimelineMerchantCtrl merchant;
    private WorldTimelineDebugView view;
    private StoryEncounter shown;
    private StoryResolution shownResult;
    private BattleSettlement shownBattle;

    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.text.StartsWith("Timeline_", System.StringComparison.Ordinal)) label.text = TimelineText(label.text);
        for (int i = 0; i < options.Length; i++)
        {
            int slot = i;
            options[i].onClick.AddListener(() =>
            {
                var story = view?.Simulation?.Stories;
                if (story?.PendingChoice == null || slot >= story.PendingChoice.Options.Count) return;
                if (story.Choose(story.PendingChoice.Options[slot].Id))
                    foreach (var button in options) button.interactable = false;
            });
        }
        proceed.onClick.AddListener(() => { view?.Simulation?.Stories?.Continue(); proceed.interactable = false; });
    }
    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        view = userData as WorldTimelineDebugView;
        if (view == null) { GameEntry.UI.CloseUIForm(UIForm); return; }
        view.UsesStoryHud = true; view.Closed += Close;
        shown = null; shownResult = null; shownBattle = null; presentation.SetActive(false); Refresh();
    }
    public override void OnClose(bool isShutdown, object userData)
    {
        if (view != null) { view.UsesStoryHud = false; view.Closed -= Close; }
        if (battleReport != null) battleReport.gameObject.SetActive(false);
        if (merchant != null) merchant.gameObject.SetActive(false);
        view = null; base.OnClose(isShutdown, userData);
    }
    private void Close() => GameEntry.UI.CloseUIForm(UIForm);
    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    { base.OnUpdate(elapseSeconds, realElapseSeconds); Refresh(); }
    private void Refresh()
    {
        var battle = view?.PendingBattleSettlement;
        if (battle != null)
        {
            if (battle == shownBattle) return;
            shownBattle = battle;
            shown = null;
            shownResult = null;
            presentation.SetActive(false);
            if (battleReport == null) battleReport = Instantiate(battleReportPrefab, transform, false);
            battleReport.Show(view, battle);
            return;
        }
        if (battleReport != null && battleReport.gameObject.activeSelf) battleReport.gameObject.SetActive(false);
        shownBattle = null;
        var story = view?.Simulation?.Stories;
        var context = story?.PendingChoice ?? story?.PendingResult?.Encounter;
        var result = story?.PendingResult;
        if (context?.Definition.IsMerchant == true)
        {
            presentation.SetActive(false);
            if (merchant == null) merchant = Instantiate(merchantPrefab, transform, false);
            merchant.Refresh(view, context);
            return;
        }
        if (merchant != null && merchant.gameObject.activeSelf) merchant.gameObject.SetActive(false);
        presentation.SetActive(context != null);
        if (context == null) { shown = null; shownResult = null; return; }
        if (context == shown && result == shownResult) return;
        shown = context; shownResult = result;
        title.text = context.Definition.Title;
        caption.text = TimelineText("Timeline_UI_StoryCaption", view.Simulation.State.Year, view.PlayerName);
        body.text = result == null ? context.Definition.Text : result.Text;
        resultLabel.text = result == null ? TimelineText("Timeline_UI_46") : TimelineText("Timeline_UI_47");
        string background = context.Definition.Background;
        if (background == "Bridge" && story.Flags.Contains("bridge_repaired")) background = "BridgeRestored";
        art.sprite = backgrounds.FirstOrDefault(x => x.key == background)?.sprite;
        art.enabled = art.sprite != null;
        for (int i = 0; i < options.Length; i++)
        {
            options[i].gameObject.SetActive(result == null && i < context.Options.Count);
            if (result != null || i >= context.Options.Count) continue;
            var option = context.Options[i];
            string reason = story.UnavailableReason(context.Actor, option);
            options[i].interactable = reason == null;
            string costs = option.TimeYears > 0 ? TimelineText("Timeline_UI_StoryTimeCost", option.TimeYears) : "";
            if (option.HealthCost > 0) costs += TimelineText("Timeline_UI_StoryHealthCost", option.HealthCost);
            optionLabels[i].text = option.Text + costs + (reason == null ? "" : "\n" + reason);
        }
        proceed.gameObject.SetActive(result != null); proceed.interactable = true;
    }
}
