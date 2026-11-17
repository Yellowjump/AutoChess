using static WorldTimelineDebug.WorldTimelineDebugView;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Sprites;
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
    private (string Title, string Body, string Art)[] shownEnding;
    private int endingPage;
    private TMP_Text proceedLabel;
    private string storyProceedText;
    private static readonly int InkProgress = Shader.PropertyToID("_InkProgress");
    private static readonly int ArtUv = Shader.PropertyToID("_ArtUv");
    private Material inkMaterial;
    private CanvasGroup presentationInput;
    private CanvasGroup[] contentGroups;
    private Image artFrame;
    private float presentationProgress;
    private bool wantsPresentation;

    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        presentationInput = presentation.AddComponent<CanvasGroup>();
        artFrame = art.transform.parent.GetComponent<Image>();
        contentGroups = presentation.transform.Cast<Transform>()
            .Where(child => child != artFrame.transform)
            .Select(child => child.gameObject.AddComponent<CanvasGroup>()).ToArray();
        var shader = Resources.Load<Shader>("StoryInkReveal");
        if (shader != null && shader.isSupported)
        {
            inkMaterial = new Material(shader);
            art.material = inkMaterial;
        }
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
        proceedLabel = proceed.GetComponentInChildren<TMP_Text>(true);
        storyProceedText = proceedLabel.text;
        proceed.onClick.AddListener(() =>
        {
            if (view?.EndingPages is { } pages)
            {
                shownEnding = null;
                if (++endingPage >= pages.Length) view.CloseEnding();
                if (view.EndingPages == null)
                {
                    proceedLabel.text = storyProceedText;
                    SetPresentation(false);
                }
                else ShowEndingPage(pages);
                return;
            }
            view?.Simulation?.Stories?.Continue();
            proceed.interactable = false;
        });
    }
    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        view = userData as WorldTimelineDebugView;
        if (view == null) { GameEntry.UI.CloseUIForm(UIForm); return; }
        view.UsesStoryHud = true; view.Closed += Close;
        shown = null; shownResult = null; shownBattle = null; shownEnding = null;
        HidePresentationImmediately();
        Refresh();
    }
    public override void OnClose(bool isShutdown, object userData)
    {
        HidePresentationImmediately();
        if (view != null) { view.UsesStoryHud = false; view.Closed -= Close; }
        if (battleReport != null) battleReport.gameObject.SetActive(false);
        if (merchant != null) merchant.gameObject.SetActive(false);
        view = null; base.OnClose(isShutdown, userData);
    }
    private void Close() => GameEntry.UI.CloseUIForm(UIForm);

    private void OnDestroy()
    {
        if (inkMaterial != null) Destroy(inkMaterial);
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        Refresh();
        if (!presentation.activeSelf || (wantsPresentation && presentationProgress >= 1f)) return;
        presentationProgress = Mathf.MoveTowards(presentationProgress, wantsPresentation ? 1f : 0f,
            realElapseSeconds / (wantsPresentation ? 0.30f : 0.22f));
        ApplyPresentation();
        if (!wantsPresentation && presentationProgress == 0f) presentation.SetActive(false);
    }

    private void SetPresentation(bool visible)
    {
        if (wantsPresentation == visible) return;
        wantsPresentation = visible;
        if (visible) presentation.SetActive(true);
        ApplyPresentation();
    }

    private void HidePresentationImmediately()
    {
        wantsPresentation = false;
        presentationProgress = 0f;
        presentation.SetActive(false);
    }

    private void ApplyPresentation()
    {
        float content = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.20f, 0.95f, presentationProgress));
        foreach (var group in contentGroups) group.alpha = content;
        presentationInput.interactable = wantsPresentation && presentationProgress >= 1f;

        float ink = Mathf.SmoothStep(0f, 1f, presentationProgress);
        artFrame.canvasRenderer.SetAlpha(ink);
        if (inkMaterial != null)
        {
            if (art.sprite != null) inkMaterial.SetVector(ArtUv, DataUtility.GetOuterUV(art.sprite));
            inkMaterial.SetFloat(InkProgress, ink);
        }
        else art.canvasRenderer.SetAlpha(ink);
    }

    private void Refresh()
    {
        var battle = view?.PendingBattleSettlement;
        if (battle != null)
        {
            if (battle == shownBattle) return;
            shownBattle = battle;
            shown = null;
            shownResult = null;
            HidePresentationImmediately();
            if (battleReport == null) battleReport = Instantiate(battleReportPrefab, transform, false);
            battleReport.Show(view, battle);
            return;
        }
        if (battleReport != null && battleReport.gameObject.activeSelf) battleReport.gameObject.SetActive(false);
        shownBattle = null;
        if (view?.EndingPages is { } ending)
        {
            if (shownEnding == ending) return;
            endingPage = 0;
            shownEnding = ending;
            var titleMenu = FindFirstObjectByType<MainTitlePanelCtrl>(FindObjectsInactive.Include);
            if (titleMenu != null && titleMenu.TryPlayTimelineEnding(view)) HidePresentationImmediately();
            else ShowEndingPage(ending);
            return;
        }
        if (shownEnding != null)
        {
            shownEnding = null;
            shown = null;
            shownResult = null;
            proceedLabel.text = storyProceedText;
        }
        var story = view?.Simulation?.Stories;
        var context = story?.PendingChoice ?? story?.PendingResult?.Encounter;
        var result = story?.PendingResult;
        if (context?.Definition.IsMerchant == true)
        {
            HidePresentationImmediately();
            if (merchant == null) merchant = Instantiate(merchantPrefab, transform, false);
            merchant.Refresh(view, context);
            return;
        }
        if (merchant != null && merchant.gameObject.activeSelf) merchant.gameObject.SetActive(false);
        SetPresentation(context != null);
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

    private void ShowEndingPage((string Title, string Body, string Art)[] ending)
    {
        shownEnding = ending;
        if (merchant != null && merchant.gameObject.activeSelf) merchant.gameObject.SetActive(false);
        SetPresentation(true);
        var page = ending[endingPage];
        title.text = page.Title;
        body.text = page.Body;
        caption.text = TimelineText("Timeline_Ending_Caption", view.Simulation.State.Year);
        resultLabel.text = TimelineText("Timeline_Ending_Page", endingPage + 1, ending.Length);
        string background = page.Art switch { "Spring" => "Well", "Sluice" => "Bridge",
            "Book" or "Road" => "BridgeRestored", _ => "Gate" };
        art.sprite = backgrounds.FirstOrDefault(x => x.key == background)?.sprite;
        art.enabled = art.sprite != null;
        foreach (var option in options) option.gameObject.SetActive(false);
        proceed.gameObject.SetActive(true);
        proceed.interactable = true;
        proceedLabel.text = TimelineText(endingPage + 1 == ending.Length ? "Timeline_Ending_Close" : "Timeline_Ending_Next");
    }
}
