using static WorldTimelineDebug.WorldTimelineDebugView;
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

public sealed class WorldTimelinePanelCtrl : UIFormLogic
{
    public enum DisplayState { Map, Battle, Preparation, Ending }
    [SerializeField] private CanvasGroup hud;
    [SerializeField] private GameObject headerSection, locationSection;
    [SerializeField] private GameObject[] mapSections;
    [SerializeField] private Button closeLocation;
    [SerializeField] private Button equipment;
    [SerializeField] private TMP_Text year, deadline, status, heroName, heroLevel, health, experience;
    [SerializeField] private TMP_Text locationName, locationKind, locationHealth, locationAttack, locationSpirit, loot, hint, notice;
    [SerializeField] private TMP_Text locationArrivals;
    [SerializeField] private ScrollRect locationArrivalScroll;
    [SerializeField] private Button relayNotice;
    [SerializeField] private Image healthFill, experienceFill, ritualFill, locationArt;
    [SerializeField] private Button move, cultivate, rest, wait, disguise, join;
    [SerializeField] private Button observeStep, observeToggle, speed, nextLife, returnMenu;
    [SerializeField] private Button overview, focusPlayer, focusChosen, zoomIn, zoomOut;
    [SerializeField] private Button roles, history, logs, growth, closePopup, previousPage, nextPage;
    [SerializeField] private TMP_Text observeLabel, speedLabel, popupTitle, popupText, pageLabel;
    [SerializeField] private GameObject popup, roleList, textPage;
    [SerializeField] private WorldTimelineTechniqueRoute techniqueRoute;
    [SerializeField] private Button[] actorButtons;
    [SerializeField] private TMP_Text[] actorLabels;
    private WorldTimelineDebugView view;
    private RectTransform hudFrame, journeyCard;
    private int displayedLeader = -2;
    private Image leaderPortrait;
    private Sprite originalPortrait;
    private readonly TMP_Text[] companionCards = new TMP_Text[ExpeditionParty.Capacity - 1];
    private bool restoreDebugger;
    private Vector2 lastHudSize;
    private RectTransform[] locationRows;
    private bool compactLocation;
    private TMP_Text rosterLabel, recruitmentLabel;
    private Button expeditionButton;
    private Button recruitButton;
    private GameObject companionStageCard;
    private TMP_Text companionStageText, companionStageAction;
    private Button companionStageRecruit;
    private ExpeditionPartyPanel expeditionPanel;
    private float nextRefresh;
    private int popupTab, page;
    private const int TechniqueTab = 4;
    private int selectedTechnique;
    private bool resumeTechniqueObservation;
    private DisplayState displayState;
    private bool locationOpen, offerLanternDetour;
    private int arrivalNodeId;
    private GameObject mapLegend;
    private readonly System.Collections.Generic.List<(MapMarkerKind? Kind, Image Background, TMP_Text Label)> legendRows = new();
    private MapMarkerKind? displayedFilter;
    private TMP_Text moveLabel, cultivateLabel;
    private TMP_FontAsset regularLocationHintFont;
    private TMP_Text nextLifeLabel;
    private string regularNextLife;
    private string regularMove, regularCultivate;
    private Color regularMoveColor, regularCultivateColor;
    private float regularCultivateFontSize;
    public DisplayState CurrentDisplayState => displayState;
    public void RefreshDisplay() => SetDisplayState(ReadDisplayState());

    public bool CanInteractWithMap => view != null && displayState == DisplayState.Map
        && !view.IsBusy && !view.IsPreparing && !view.InventoryOpen && !view.ExpeditionPanelOpen && !popup.activeSelf;

    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.text == "Timeline_UI_51") { label.text = "首次历练"; continue; }
            if (label.text == "BtnBag" || label.text.StartsWith("Timeline_", System.StringComparison.Ordinal))
                label.text = TimelineText(label.text);
        }
        equipment?.onClick.AddListener(OpenEquipment);
        relayNotice?.onClick.AddListener(() =>
        {
            if (!CanInteractWithMap || view.OpeningActive || !view.FocusRelayStep()) return;
            ShowLocationDetails(true);
        });
        closeLocation.onClick.AddListener(CloseLocationDetails);
        moveLabel = move.GetComponentInChildren<TMP_Text>(true);
        cultivateLabel = cultivate.GetComponentInChildren<TMP_Text>(true);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.text == "首次历练") label.font = cultivateLabel.font;
        locationSpirit.font = cultivateLabel.font;
        regularLocationHintFont = hint.font;
        nextLifeLabel = nextLife.GetComponentInChildren<TMP_Text>(true);
        regularNextLife = nextLifeLabel.text;
        regularMove = TimelineText("Timeline_UI_DetailMove");
        regularCultivate = cultivateLabel.text;
        regularMoveColor = moveLabel.color;
        regularCultivateColor = cultivateLabel.color;
        regularCultivateFontSize = cultivateLabel.fontSize;
        BuildMapLegend();
        BuildExpeditionButton();
        BuildCompanionStageCard();
        ArrangeMapHud();
        leaderPortrait = heroName.transform.parent.Find("CultivatorPortrait").GetComponent<Image>();
        originalPortrait = leaderPortrait.sprite;
        BindOpeningPreview(move, 1);
        BindOpeningPreview(cultivate, 2);
        move.onClick.AddListener(() =>
        {
            if (view.OpeningActive) view.SelectOpeningRoute(false);
            else if (!locationOpen || !view.LanternForecastPending) view.MoveSelected();
        });
        cultivate.onClick.AddListener(() =>
        {
            if (view.OpeningOfferRoutes) view.SelectOpeningRoute(true);
            else if (offerLanternDetour) view.TakeLanternDetour();
        });
        var restLabel = rest.GetComponentInChildren<TMP_Text>(true);
        restLabel.font = cultivateLabel.font;
        restLabel.text = $"休整一年\n全队气血 +{ExpeditionParty.RestPercent}%";
        rest.onClick.AddListener(() => view.RestYear());
        growth.GetComponentInChildren<TMP_Text>(true).text = "成长";

        wait.onClick.AddListener(() =>
        {
            if (view.OpeningActive) view.StayAtOpeningStop();
            else view.WaitYear();
        });
        disguise.onClick.AddListener(() => view.Disguise());
        join.onClick.AddListener(() => view.JoinChosen());
        observeStep.onClick.AddListener(() => view.ObserveYear());
        observeToggle.onClick.AddListener(() => view.ToggleObservation());
        speed.onClick.AddListener(() => view.CycleObservationSpeed());
        nextLife.onClick.AddListener(() => view.PrepareNextLife());
        returnMenu.onClick.AddListener(() => view.ReturnToMenu());
        overview.onClick.AddListener(() => view.OverviewMap());
        focusPlayer.onClick.AddListener(() => view.FocusMapActor(view.Simulation.State.PlayerActorId));
        focusChosen.onClick.AddListener(() =>
        {
            if (view.Simulation.State.ChosenActorId.HasValue) view.FocusMapActor(view.Simulation.State.ChosenActorId.Value);
        });
        zoomIn.onClick.AddListener(() => view.ZoomMap(.82f));
        zoomOut.onClick.AddListener(() => view.ZoomMap(1.22f));
        roles.onClick.AddListener(() => OpenPopup(0));
        history.onClick.AddListener(() => OpenPopup(1));
        logs.onClick.AddListener(() => OpenPopup(2));
        growth.onClick.AddListener(() => OpenPopup(3));
        closePopup.onClick.AddListener(ClosePopup);
        previousPage.onClick.AddListener(() => { page = Math.Max(0, page - 1); RefreshPopup(); });
        nextPage.onClick.AddListener(() => { page++; RefreshPopup(); });
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        view = userData as WorldTimelineDebugView;
        if (view == null || view.Simulation == null)
        {
            Debug.LogError("轮回主界面缺少有效的轮回会话。请使用 OpenUIForm(..., userData: view) 打开，且在创建 Simulation 后调用。");
            GameEntry.UI.CloseUIForm(UIForm);
            return;
        }
        restoreDebugger = HotGameEntry.Debugger != null && HotGameEntry.Debugger.ActiveWindow;
        if (restoreDebugger) HotGameEntry.Debugger.ActiveWindow = false;
        view.UsesPrefabHud = true;
        view.BattleFormationPresenter = PresentExpeditionFormation;
        view.PastInvitationPresenter = PresentPastInvitation;
        view.SuccessorPresenter = PresentSuccessor;
        view.TeamRewardPresenter=()=>ExpeditionRewardPanel.Present(view,moveLabel.font);
        view.EquipmentServicePresenter=()=>ExpeditionWorkshopPanel.Present(view,moveLabel.font);
        view.Closed += OnSessionClosed;
        RefreshMapLegend();
        locationOpen = false;
        arrivalNodeId = 0;
        selectedTechnique = 0;
        SetHudVisible(true, true);
        SetDisplayState(ReadDisplayState());
        popup.SetActive(false);
        Refresh();
    }

    public override void OnClose(bool isShutdown, object userData)
    {
        if (expeditionPanel != null) Destroy(expeditionPanel.gameObject);
        if (view != null)
        {
            view.BattleFormationPresenter = null;
            view.PastInvitationPresenter = null;
            view.SuccessorPresenter = null;
            view.TeamRewardPresenter=null;
            view.EquipmentServicePresenter=null;
            view.ExpeditionPanelOpen = false;
            view.CancelLanternForecast();
            view.Closed -= OnSessionClosed;
            view.UsesPrefabHud = false;
        }
        view = null;
        if (restoreDebugger && HotGameEntry.Debugger != null) HotGameEntry.Debugger.ActiveWindow = true;
        restoreDebugger = false;
        base.OnClose(isShutdown, userData);
    }

    public override void OnPause()
    {
        view?.CancelLanternForecast();
        base.OnPause();
    }

    private void OnSessionClosed() => GameEntry.UI.CloseUIForm(UIForm);
    public void OpenEquipment()
    {
        if (view == null || view.IsPreparing || view.IsBusy || view.InventoryOpen) return;
        if (!CanInteractWithMap || view.Expedition?.Equipment.Modern != true || !view.Expedition.Owns(view.Expedition.LeaderId)) return;
        ClearHover();
        ExpeditionEquipmentPanel.Open(view,moveLabel.font,RefreshDisplay);
    }

    internal bool OpenTechnique(WorldTimelineDebugView session, int techniqueId, bool resumeObservation)
    {
        if (view == null || view != session || view.IsPreparing || view.IsBusy || techniqueRoute == null ||
            !view.TryGetLearnedTechnique(techniqueId, out _, out _)) return false;
        selectedTechnique = techniqueId;
        resumeTechniqueObservation = resumeObservation;
        OpenPopup(TechniqueTab);
        return true;
    }

    private void ClosePopup()
    {
        popup.SetActive(false);
        if (popupTab != TechniqueTab || selectedTechnique == 0) return;
        int id = selectedTechnique;
        selectedTechnique = 0;
        WorldTimelineInventoryCtrl.ReturnFromTechnique(view, id, resumeTechniqueObservation);
    }

    private DisplayState ReadDisplayState()
    {
        if (view.IsPreparing) return DisplayState.Preparation;
        if (view.EndingPages != null) return DisplayState.Ending;
        var stories = view.Simulation.Stories;
        return IsVisibleBattle || view.PreparingExpeditionBattle || view.PendingBattleSettlement != null ||
            stories?.PendingChoice != null || stories?.PendingResult != null
            ? DisplayState.Battle : DisplayState.Map;
    }

    private static bool IsVisibleBattle => HotGameEntry.HeroManager.IsTimelineBattle && !HotGameEntry.HeroManager.IsTimelineSimulation;

    public void SetDisplayState(DisplayState state)
    {
        if (displayState != state)
        {
            locationOpen = false;
            arrivalNodeId = 0;
            popup.SetActive(false);
            if (selectedTechnique != 0)
            {
                selectedTechnique = 0;
                view?.ResumeInventoryObservation(resumeTechniqueObservation);
            }
            ClearHover();
        }
        displayState = state;
        SetHudVisible(state != DisplayState.Ending, state != DisplayState.Ending);
        headerSection.SetActive(view == null || !view.InventoryOpen && !view.ExpeditionPanelOpen);
        bool showMap = state == DisplayState.Map && (view == null || !view.InventoryOpen && !view.ExpeditionPanelOpen);
        expeditionButton.gameObject.SetActive(showMap && view?.Expedition != null && !popup.activeSelf);
        recruitButton.gameObject.SetActive(expeditionButton.gameObject.activeSelf);
        foreach (var section in mapSections) section.SetActive(showMap);
        locationSection.SetActive(showMap && locationOpen);
        journeyCard.gameObject.SetActive(showMap && !locationOpen && !string.IsNullOrWhiteSpace(notice.text));
        mapLegend.SetActive(showMap && view != null && !view.OpeningActive && !popup.activeSelf);
        if (mapLegend.activeSelf && displayedFilter != view.MapMarkerFilter) RefreshMapLegend();
    }

    private void BuildExpeditionButton()
    {
        recruitButton = CreateMapButton("同伴关卡", 212, 186, new Color(.61f, .40f, .14f));
        expeditionButton = CreateMapButton("队伍·同伴", 28, 178, new Color(.19f, .38f, .32f));
        rosterLabel = expeditionButton.GetComponentInChildren<TMP_Text>();
        recruitmentLabel = recruitButton.GetComponentInChildren<TMP_Text>();
        recruitButton.onClick.AddListener(() =>
        {
            if (!CanInteractWithMap || !view.Simulation.WaitingForPlayer) return;
            if (view.FocusVanguard()) { ShowLocationDetails(true); return; }
            var next = new[] { 0, 2, 3, 1, ExpeditionParty.SandSite, ExpeditionParty.TideSite }.Where(candidate => view.Expedition.RecruitmentSites.ContainsKey(candidate) &&
                !view.Expedition.HasRecruited(candidate)).Select(candidate => (int?)candidate).FirstOrDefault();
            if (next is int role && view.FocusRecruitmentSite(role)) ShowLocationDetails(true);
            else OpenExpedition();
        });
        expeditionButton.onClick.AddListener(OpenExpedition);

        Button CreateMapButton(string name, float x, float width, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -114);
            rect.sizeDelta = new Vector2(width, 52);
            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            go.layer = gameObject.layer;
            var label = Instantiate(moveLabel, rect);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.fontSize = 26; label.color = new Color(.96f, .91f, .78f);
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 26;
            BindButtonClickSound(button);
            return button;
        }
    }

    private void ArrangeMapHud()
    {
        var root = (RectTransform)transform;
        hudFrame = MapLegendRect("MapHud", root, Vector2.zero, Vector2.zero);
        hudFrame.anchorMin = hudFrame.anchorMax = hudFrame.pivot = new Vector2(.5f, .5f);
        // Leave the map input stretched across the actual viewport.
        foreach (var section in root.Cast<Transform>().ToArray())
            if (section != hudFrame && section.GetComponent<WorldTimelineMapInput>() == null)
                section.SetParent(hudFrame, false);
        ResizeHud();

        var commandDock = (RectTransform)move.transform.parent;
        commandDock.anchorMin = commandDock.anchorMax = commandDock.pivot = new Vector2(.5f, 0);
        commandDock.anchoredPosition = new Vector2(0, 28);
        commandDock.sizeDelta = new Vector2(914, 80);
        ArrangeRow(commandDock, 16, 12);
        var observationDock = (RectTransform)status.transform.parent;
        observationDock.anchorMin = observationDock.anchorMax = observationDock.pivot = commandDock.pivot;
        observationDock.anchoredPosition = new Vector2(0, 115);
        ArrangeRow(observationDock, 14, 5);
        status.gameObject.AddComponent<LayoutElement>().flexibleWidth = 2;
        status.enableAutoSizing = true;
        status.fontSizeMin = 16;
        status.fontSizeMax = status.fontSize;
        BuildJourneyCard();
        var playerCard = heroName.transform.parent;
        for (int i = 0; i < companionCards.Length; i++)
        {
            var strip = MapLegendRect("DeployedCompanion" + i, (RectTransform)playerCard,
                new Vector2(0, (i + 1) * 50), new Vector2(472, 46));
            strip.gameObject.AddComponent<Image>().color = new Color(.13f, .19f, .19f, .96f);
            var label = Instantiate(heroLevel, strip);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(16, 2);
            label.rectTransform.offsetMax = new Vector2(-16, -2);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSize = 22;
            label.raycastTarget = false;
            companionCards[i] = label;
            strip.gameObject.SetActive(false);
        }
        popup.transform.SetAsLastSibling();
        FitLabel(closeLocation.GetComponentInChildren<TMP_Text>());
        var rewardSpace = Vector2.up * 28;
        loot.rectTransform.sizeDelta += rewardSpace;
        hint.rectTransform.anchoredPosition -= rewardSpace;
        ((RectTransform)locationSection.transform).sizeDelta += rewardSpace;
        locationRows = locationSection.transform.Cast<RectTransform>()
            .Where(row => row.anchoredPosition.y <= -300).ToArray();
    }

    private void BuildJourneyCard()
    {
        journeyCard = MapLegendRect("Journey", hudFrame, new Vector2(-28, -114), new Vector2(416, 198));
        journeyCard.anchorMin = journeyCard.anchorMax = journeyCard.pivot = Vector2.one;
        var paper = journeyCard.gameObject.AddComponent<Image>();
        var source = locationSection.GetComponent<Image>();
        paper.sprite = source.sprite;
        paper.type = source.type;
        paper.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        paper.color = source.color;
        paper.raycastTarget = false;

        var title = Instantiate(locationName, journeyCard);
        title.text = "当前行程";
        title.rectTransform.anchoredPosition = new Vector2(26, -16);
        title.rectTransform.sizeDelta = new Vector2(360, 46);
        title.fontSize = 30;
        title.raycastTarget = false;

        var guide = notice.rectTransform;
        guide.SetParent(journeyCard, false);
        guide.anchorMin = Vector2.zero;
        guide.anchorMax = Vector2.one;
        guide.offsetMin = new Vector2(26, 18);
        guide.offsetMax = new Vector2(-26, -65);
        notice.alignment = TextAlignmentOptions.TopLeft;
        notice.enableAutoSizing = true;
        notice.fontSizeMin = 16;
        notice.fontSizeMax = 23;
    }

    private static void ArrangeRow(RectTransform row, int horizontalPadding, int verticalPadding)
    {
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
        layout.spacing = 8;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        foreach (var button in row.GetComponentsInChildren<Button>(true))
            FitLabel(button.GetComponentInChildren<TMP_Text>(true));
    }

    private static void FitLabel(TMP_Text label)
    {
        var rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6, 2);
        rect.offsetMax = new Vector2(-6, -2);
        label.fontSizeMax = label.fontSize;
        label.fontSizeMin = Mathf.Min(16, label.fontSize);
        label.enableAutoSizing = true;
        label.alignment = TextAlignmentOptions.Center;
    }

    private void ResizeHud()
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        if (size == lastHudSize || size.x <= 0 || size.y <= 0) return;
        lastHudSize = size;
        float scale = Mathf.Min(size.x / 1920f, size.y / 1080f);
        hudFrame.localScale = Vector3.one * scale;
        hudFrame.sizeDelta = size / scale;
    }

    private void SetLocationCompact(bool compact)
    {
        if (compactLocation == compact) return;
        compactLocation = compact;
        var offset = Vector2.up * (compact ? 172 : -172);
        foreach (var row in locationRows) row.anchoredPosition += offset;
        ((RectTransform)locationSection.transform).sizeDelta -= offset;
    }

    private void BuildCompanionStageCard()
    {
        var card = MapLegendRect("同伴关卡详情", (RectTransform)locationSection.transform,
            new Vector2(35, -130), new Vector2(346, 165));
        companionStageCard = card.gameObject;
        companionStageText = MapLegendRect("条件进度", card, Vector2.zero, new Vector2(346, 105))
            .gameObject.AddComponent<TextMeshProUGUI>();
        companionStageText.font = cultivateLabel.font;
        companionStageText.fontSize = 22;
        companionStageText.color = new Color(.22f, .30f, .27f);
        companionStageText.raycastTarget = false;
        var button = MapLegendRect("关卡招募", card, new Vector2(0, -113), new Vector2(346, 48));
        var background = button.gameObject.AddComponent<Image>();
        background.color = new Color(.19f, .38f, .32f);
        companionStageRecruit = button.gameObject.AddComponent<Button>();
        companionStageRecruit.targetGraphic = background;
        companionStageAction = Instantiate(moveLabel, button);
        companionStageAction.rectTransform.anchorMin = Vector2.zero;
        companionStageAction.rectTransform.anchorMax = Vector2.one;
        companionStageAction.rectTransform.offsetMin = companionStageAction.rectTransform.offsetMax = Vector2.zero;
        companionStageAction.font = cultivateLabel.font;
        companionStageAction.fontSize = 22;
        companionStageAction.color = new Color(.96f, .91f, .78f);
        companionStageAction.alignment = TextAlignmentOptions.Center;
        companionStageAction.raycastTarget = false;
        companionStageRecruit.onClick.AddListener(() =>
        {
            if (!CanInteractWithMap || view.SelectedLocation.CompanionStageRole is not int role) return;
            if (view.RecruitCompanion(role)) { RefreshLocationDetails(); RefreshDisplay(); }
        });
        BindButtonClickSound(companionStageRecruit);
        companionStageCard.SetActive(false);
    }

    public void OpenExpedition()
    {
        if (view?.Expedition == null || !CanInteractWithMap || !view.Simulation.WaitingForPlayer || expeditionPanel != null) return;
        ClearHover();
        expeditionPanel = ExpeditionPartyPanel.Open(view, moveLabel.font, false, view.SelectedNodeId,
            () => { expeditionPanel = null; RefreshDisplay(); }, role =>
            {
                if (view.FocusRecruitmentSite(role)) ShowLocationDetails(true);
            });
        RefreshDisplay();
    }

    private IEnumerator PresentPastInvitation(int id,Action<bool> choose)
    {
        var actor = view.Simulation.State.Actors[id];
        var former = view.Simulation.State.ExpeditionFor(id);
        var gear = former.Equipment;
        var bag = view.InvitationBag(id).ToArray();
        var lines = bag.GroupBy(i => i).OrderBy(g => g.Key.Definition).ThenBy(g => g.Key.Level)
            .Select(g => $"{ExpeditionEquipment.Definition(g.Key.Definition).Name}  {g.Key.Level}阶  ×{g.Count()}").ToArray();
        var root = ExpeditionPaperUi.Canvas("前世相遇");
        var ui = new ExpeditionPaperUi(root,moveLabel.font);
        bool? accepted = null;
        try
        {
            ui.Box("Backdrop",0,0,1920,1080,new Color(.08f,.09f,.08f,.85f));
            ui.Page("Invitation",290,175,1340,730);
            ui.Text("Heading","遇见" + view.ClearOwner(id),365,225,1200,76,48);
            ui.Line(365,318,1190);
            ui.Text("Member","加入的前世主角",365,355,520,52,33);
            ui.Text("Stats",$"{view.ClearOwner(id)}  ·  {actor.Level}级\n气血 {actor.Health} / {actor.MaxHealth}    攻击 {actor.CombatAttack}",365,425,575,101,29);
            var worn = gear.Instances.Where(i => i.Owner == 0).OrderBy(i => i.Slot)
                .Select(i => i.Definition.Name + " " + i.Level + "阶").ToList();
            if (gear.Innate(id) is ArtifactDefinition innate) worn.Insert(0,innate.Name + " " + gear.InnateLevel(id) + "阶");
            ui.Text("Loadout","随身穿戴  " + string.Join("、",worn),365,538,540,140,24);
            ui.Text("Assets","带入的全队资产",982,355,550,52,33);
            ui.Text("Stones",$"灵石 {gear.SpiritStones}    入囊装备 {bag.Length}件",982,425,560,50,28);
            var listed = ui.Text("Bag","",982,489,540,174,24);
            int page = 0;
            int pages = Math.Max(1,(lines.Length+4)/5);
            void Display() => listed.text = lines.Length == 0 ? "行囊空" : string.Join("\n",lines.Skip(page*5).Take(5));
            Display();
            if (pages > 1)
            {
                ui.Button("Previous","上一页",982,662,145,46,()=>{ page=(page+pages-1)%pages; Display(); });
                ui.Button("Next","下一页",1147,662,145,46,()=>{ page=(page+1)%pages; Display(); });
            }
            ui.Text("Companions","其余存活的关卡同伴会带着当前气血返回各自关卡。",365,706,1190,49,24);
            ui.Button("Decline","不邀请，迎战",785,795,310,68,()=>accepted=false);
            ui.Button("Accept","邀请主角入队",1120,795,390,68,()=>accepted=true,ExpeditionPaperUi.Jade);
            while (!accepted.HasValue) yield return null;
            choose(accepted.Value);
        }
        finally { ui.Dispose(); Destroy(root); }
    }

    private IEnumerator PresentSuccessor(System.Collections.Generic.IReadOnlyList<int> candidates, Action<int> choose)
    {
        var root = ExpeditionPaperUi.Canvas("选择接任队长");
        var ui = new ExpeditionPaperUi(root, moveLabel.font);
        int selected = -1;
        try
        {
            ui.Box("Backdrop",0,0,1920,1080,new Color(.08f,.09f,.08f,.85f));
            ui.Page("Succession",350,255,1220,570);
            ui.Text("Heading","选择接任队长",425,315,1060,72,50);
            ui.Text("Loss",view.PlayerName + "已阵亡，装备已放入行囊。选择一位存活的同伴继续带队。",425,411,1070,80,28);
            int columns = Math.Min(4, candidates.Count);
            float width = 1050f / columns;
            int height = candidates.Count > columns ? 100 : 204;
            for (int i = 0; i < candidates.Count; i++)
            {
                int role = candidates[i];
                var button = ui.Button("Successor" + role, "",425 + (i%columns)*width,525 + (i/columns)*(height+8),width-14,height,()=>{ selected=role; root.SetActive(false); },ExpeditionPaperUi.Jade);
                ui.Picture("Portrait",view.PastPortrait(view.Expedition,role) ?? ui.Portrait(role),8,12,70,height-28,button.transform);
                var label = button.GetComponentInChildren<TMP_Text>();
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(78,8); label.rectTransform.offsetMax = new Vector2(-8,-8);
                label.fontSize = 25;
                label.text = view.CompanionName(role) + "\n气血 " + view.Expedition.CompanionHealth(role) + "/" + view.Expedition.CompanionMaxHealth(role);
            }
            while (selected < 0) yield return null;
            choose(selected);
        }
        finally { ui.Dispose(); Destroy(root); }
    }

    private IEnumerator PresentExpeditionFormation(int nodeId)
    {
        var resolver = new WorldTimelineBattleResolver();
        yield return resolver.PreviewFormation(view.Simulation.State, nodeId,
            (actors, entities) => ExpeditionBattleFormation.Present(view, moveLabel.font, nodeId, actors, entities));
    }

    private void BuildMapLegend()
    {
        var card = MapLegendRect("关卡图例", (RectTransform)transform, new Vector2(28, -174), new Vector2(370, 203));
        mapLegend = card.gameObject;
        var paper = mapLegend.AddComponent<Image>();
        paper.color = new Color(.98f, .95f, .88f, .92f);
        paper.raycastTarget = true;
        var border = mapLegend.AddComponent<Outline>();
        border.effectColor = new Color(.37f, .31f, .21f, .35f);
        border.effectDistance = new Vector2(1f, -1f);
        mapLegend.SetActive(false);
        var ownership = MapLegendRect("首通归属",card,new Vector2(12,-165),new Vector2(346,30)).gameObject.AddComponent<TextMeshProUGUI>();
        ownership.font = cultivateLabel.font;
        ownership.fontSize = 16;
        ownership.color = ExpeditionPaperUi.Ink;
        ownership.text = "首通圆环 · 青色为当世，其余与队伍路线同色";
        ownership.raycastTarget = false;
        // Keep the existing legend order and use the spare cell for the reset action.
        for (int i = 0; i <= WorldTimelineMapMarker.Count; i++)
        {
            MapMarkerKind? kind = i == WorldTimelineMapMarker.Count ? null :
                i == 0 ? MapMarkerKind.Companion : (MapMarkerKind)i;
            var position = new Vector2(8 + i / 5 * 178, -8 - i % 5 * 31);
            var row = MapLegendRect(kind?.ToString() ?? "All", card, position, new Vector2(174, 29));
            var background = row.gameObject.AddComponent<Image>();
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() =>
            {
                if (!CanInteractWithMap || view.OpeningActive) return;
                view.MapMarkerFilter = view.MapMarkerFilter == kind ? null : kind;
                RefreshMapLegend();
            });
            BindButtonClickSound(button);
            if (kind.HasValue)
            {
                var icon = MapLegendRect("Icon", row, new Vector2(4, -2), new Vector2(25, 25)).gameObject.AddComponent<Image>();
                icon.sprite = WorldTimelineMapMarker.GetSprite(kind.Value);
                icon.raycastTarget = false;
            }
            var label = MapLegendRect("Label", row, new Vector2(kind.HasValue ? 35 : 0, 0), new Vector2(kind.HasValue ? 136 : 174, 29))
                .gameObject.AddComponent<TextMeshProUGUI>();
            label.font = cultivateLabel.font;
            label.fontSize = 18;
            label.fontSizeMin = 14;
            label.fontSizeMax = 18;
            label.enableAutoSizing = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = kind.HasValue ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = !kind.HasValue ? "显示全部" : kind == MapMarkerKind.Companion ? "同伴关卡" : TimelineText(WorldTimelineMapMarker.LabelKey(kind.Value));
            legendRows.Add((kind, background, label));
        }
    }

    private void RefreshMapLegend()
    {
        displayedFilter = view.MapMarkerFilter;
        foreach (var row in legendRows)
        {
            bool selected = row.Kind == displayedFilter;
            row.Background.color = selected ? new Color(.23f, .36f, .30f) : new Color(.83f, .79f, .68f, .24f);
            row.Label.color = selected ? new Color(.99f, .96f, .85f) : new Color(.27f, .24f, .19f);
        }
    }

    private static RectTransform MapLegendRect(string name, RectTransform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public void CloseLocationDetails()
    {
        locationOpen = false;
        arrivalNodeId = 0;
        locationSection.SetActive(false);
        view?.CancelLanternForecast();
        if (view != null) RefreshOpeningButtons(view.Simulation.WaitingForPlayer && !view.IsBusy);
    }

    private void SetHudVisible(bool visible, bool interactable)
    {
        hud.alpha = visible ? 1 : 0;
        hud.interactable = visible && interactable;
        hud.blocksRaycasts = visible;
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        if (view == null) return;
        ResizeHud();
        SetDisplayState(ReadDisplayState());
        bool forecastChanged = locationOpen && CanInteractWithMap && view.UpdateLanternForecast();
        if (!locationOpen || !CanInteractWithMap) view.CancelLanternForecast();
        if (!forecastChanged && Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .15f;
        Refresh();
    }

    private static void SetGauge(Image gauge, float fraction)
    {
        gauge.fillAmount = fraction;
        if (gauge.sprite == null)
            gauge.rectTransform.localScale = new Vector3(fraction, 1f, 1f);
    }

    private void Refresh()
    {
        var world = view.Simulation.State;
        var player = world.Actors[world.PlayerActorId];
        int remaining = Math.Max(0, world.Ritual.DeadlineYear - world.Year);
        year.text = world.Year == 0 ? TimelineText("Timeline_UI_74") : TimelineText("Timeline_UI_Year", world.Year);
        bool showingBattle = IsVisibleBattle;
        deadline.text = showingBattle ? TimelineText("Timeline_UI_129") : view.OpeningActive ? view.OpeningDeadlineLabel :
            TimelineText("Timeline_UI_Deadline", remaining);
        ritualFill.transform.parent.gameObject.SetActive(!showingBattle && !view.OpeningActive);
        int ritualDuration = world.Ritual.DeadlineYear == world.Ritual.InitialDeadlineYear
            ? world.Ritual.InitialDeadlineYear : world.Ritual.PeriodYears;
        SetGauge(ritualFill, Mathf.Clamp01(remaining / (float)ritualDuration));
        status.text = view.CurrentStatus+(view.Expedition?.Equipment.Modern==true?$"  ·  灵石 {player.LegacySpiritStones}":"");
        returnMenu.interactable = !view.IsBusy;
        roles.interactable = history.interactable = logs.interactable = !view.InventoryOpen && !view.IsBusy;
        RefreshCompanionCards(player);
        int leaderRole = view.Expedition?.LeaderRole ?? -1;
        if (displayedLeader != leaderRole)
        {
            displayedLeader = leaderRole;
            leaderPortrait.sprite = leaderRole < 0 ? originalPortrait : leaderRole >= ExpeditionParty.StageRoles ? view.PastPortrait(view.Expedition,leaderRole) : Resources.Load<Sprite>("FormationReference/Detail" +
                new[] { "Shield", "Swordsman", "Archer", "Healer" }[leaderRole]);
        }
        heroName.text = view.PlayerName;
        heroLevel.text = TimelineText("Timeline_UI_Realm", player.Level);
        health.text = $"{Math.Max(0, player.Health)} / {player.MaxHealth}";
        SetGauge(healthFill, Mathf.Clamp01(player.Health / (float)player.MaxHealth));
        var levels = view.Simulation.Cultivation;
        long required = levels.GetLevel(player.Level).ExperienceToNext;
        experience.text = required == 0 ? TimelineText("Timeline_UI_75") : $"{player.LevelExperience} / {required}";
        SetGauge(experienceFill, required == 0 ? 1 : Mathf.Clamp01(player.LevelExperience / (float)required));
        if (locationOpen) RefreshLocationDetails();
        var mapNotice = view.MapNotice;
        notice.text = mapNotice.Text;
        if (relayNotice != null)
            relayNotice.interactable = notice.raycastTarget = mapNotice.NodeId.HasValue && CanInteractWithMap && !view.OpeningActive;
        bool waiting = view.Simulation.WaitingForPlayer && !view.IsBusy;
        if (view.Expedition != null)
        {
            rosterLabel.text = $"队伍  {view.Expedition.DeployedCount}/{ExpeditionParty.Capacity}";
            recruitmentLabel.text = view.PendingVanguard.HasValue ? "定位首战关卡" : "查找同伴";
            expeditionButton.interactable = recruitButton.interactable = waiting;
        }
        focusChosen.interactable = world.ChosenActorId.HasValue && !view.IsBusy;
        bool observing = view.CanObserve || view.AutoObserving;
        observeStep.gameObject.SetActive(observing);
        observeToggle.gameObject.SetActive(observing);
        speed.gameObject.SetActive(observing);
        equipment?.gameObject.SetActive(player.IsAlive && view.Expedition?.Equipment.Modern == true);
        bool canJoin = waiting && view.Simulation.CanJoinChosen(world.PlayerActorId);
        join.interactable = canJoin;
        disguise.interactable = waiting && !canJoin;
        observeStep.interactable = view.CanObserve && !view.IsBusy && !view.AutoObserving;
        observeToggle.interactable = view.CanObserve && (!view.IsBusy || view.AutoObserving);
        bool canRecordLife = view.CanRecordPersonalLife;
        nextLife.interactable = !view.IsBusy && (world.IsEnded || canRecordLife);
        nextLifeLabel.text = canRecordLife ? TimelineText("Timeline_Sluice_NextLife") : regularNextLife;
        RefreshOpeningButtons(waiting);
        observeLabel.text = view.AutoObserving ? TimelineText("Timeline_UI_22") : TimelineText("Timeline_UI_21");
        speedLabel.text = TimelineText("Timeline_UI_ObservationSpeed", view.ObservationSpeed);
        if (popup.activeSelf) RefreshPopup();
    }

    private void RefreshCompanionCards(ActorState player)
    {
        int index = 0;
        var party = view.Expedition;
        if (party != null)
        {
            var cells = party.Cells;
            for (int cell = 0; cell < cells.Count; cell++)
            {
                if (!ExpeditionParty.IsCompanion(cells[cell])) continue;
                int role = ExpeditionParty.Role(cells[cell]);
                var label = companionCards[index++];
                string readiness = player.IsAlive && !view.Simulation.State.IsEnded
                    ? $"气血 {party.CompanionHealth(role)}/{party.CompanionMaxHealth(role)}" : "本世已结束";
                label.text = $"{(cell < 3 ? "前排" : "后排")}   {view.CompanionName(role)}     {readiness}";
                label.transform.parent.gameObject.SetActive(true);
            }
        }
        for (; index < companionCards.Length; index++)
            companionCards[index].transform.parent.gameObject.SetActive(false);
    }

    private void RefreshLocationDetails()
    {
        var node = view.SelectedLocation;
        locationName.text = view.LocationName(node);
        locationKind.text = $"{view.LocationKind(node)}   ({node.Q}, {node.R})";
        locationKind.font = cultivateLabel.font;
        locationHealth.text = node.MonsterHealth > 0 ? node.MonsterHealth.ToString() : TimelineText("Timeline_UI_76");
        locationAttack.text = node.MonsterHealth > 0 ? node.MonsterAttack.ToString() : "—";
        locationSpirit.text = node.FirstClear.HasValue || view.Expedition?.HasExplored(node.Id) == true ? "已领取"
            : "+" + (view.Expedition?.ExplorationExperience(view.Simulation.Cultivation) ?? 0);
        locationArt.enabled = !node.CompanionStageRole.HasValue && (node.MonsterHealth > 0 || view.HasClearedGuardian(node));
        companionStageCard.SetActive(node.CompanionStageRole.HasValue);
        SetLocationCompact(!locationArt.enabled && !companionStageCard.activeSelf);
        if (node.CompanionStageRole is int role)
        {
            bool owned = view.Expedition.HasRecruited(role);
            bool local = view.Simulation.State.Actors[view.Simulation.State.PlayerActorId].NodeId == node.Id;
            companionStageText.text = "<b>" + view.CompanionName(role) + "</b>\n" +
                (view.Expedition.IsFallen(role) ? "已阵亡 · 本世无法再次招募" : owned ? "已完成招募\n" + (view.Expedition.CellOf(view.Expedition.FieldId(view.Expedition.SiteMember(role))) >= 0
                    ? "已上阵 · 战前可调整站位" : "已加入候补 · 战前可安排上阵") : view.RecruitmentConditions(role));
            int? stageOwner = view.Simulation.State.StageOwner(role);
            bool claimedByOther = stageOwner.HasValue && stageOwner != view.Expedition.LeaderId;
            int? returned = view.Simulation.State.ReturnedStageHealth(role);
            if (returned > 0) companionStageText.text += "\n归来气血 " + returned;
            if (claimedByOther) companionStageText.text = view.CompanionName(role) + "\n已随" + view.ClearOwner(stageOwner.Value) + "同行";
            if (returned == 0) companionStageText.text = view.CompanionName(role) + "\n已阵亡";
            companionStageRecruit.interactable = CanInteractWithMap && view.Expedition.CanRecruit(role);
            companionStageAction.text = returned == 0 ? "已阵亡" : claimedByOther ? "已被招募" : view.Expedition.IsFallen(role) ? "已阵亡" : owned ? "已招募" : !local ? "请先抵达此关卡" : node.MonsterHealth > 0
                ? "请先清除守军" : !view.Expedition.RecruitmentConditionMet(role) ? "招募条件未满足"
                : view.Expedition.DeployedCount >= ExpeditionParty.Capacity ? "招募至候补" : "招募入队";
        }
        loot.font = cultivateLabel.font;
        loot.enableAutoSizing = true;
        loot.fontSizeMin = 14;
        loot.fontSizeMax = 22;
        loot.text = view.MapRewardDetails(node);
        bool showingForecast = view.CanForecastLanternShoal;
        hint.font = showingForecast ? cultivateLabel.font : regularLocationHintFont;
        hint.text = showingForecast ? LanternForecastHint() : view.LocationHint;
        string recruitment = view.RecruitmentAt(node.Id);
        if (!string.IsNullOrEmpty(recruitment)) hint.text = recruitment + (showingForecast ? "\n" + hint.text : "");
        if (!string.IsNullOrEmpty(recruitment) || view.Expedition?.ProgressionEnabled == true)
        {
            hint.font = cultivateLabel.font;
        }
        if (locationArrivals != null && locationArrivalScroll != null)
        {
            string arrivals = view.LocationArrivals(node);
            if (locationArrivals.text != arrivals)
            {
                locationArrivals.text = arrivals;
                locationArrivals.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(locationArrivalScroll.viewport.rect.height, Mathf.Ceil(locationArrivals.preferredHeight)));
            }
            if (arrivalNodeId != node.Id)
            {
                arrivalNodeId = node.Id;
                locationArrivalScroll.StopMovement();
                locationArrivals.rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }

    private string LanternForecastHint()
    {
        if (view.LanternForecastPending) return TimelineText("Timeline_LanternForecastPending");
        if (view.LanternForecastHealth is not int health) return TimelineText("Timeline_LanternForecastUnavailable");
        if (health == 0) return TimelineText("Timeline_LanternForecastLoss");
        return TimelineText(view.LanternForecastHighRisk ? "Timeline_LanternForecastRiskyWin" :
            "Timeline_LanternForecastWin", health);
    }

    private void RefreshOpeningButtons(bool waiting)
    {
        bool opening = view.OpeningActive;
        bool offerRoutes = view.OpeningOfferRoutes;
        bool atStop = view.OpeningAtStop;
        bool highRisk = locationOpen && view.LanternForecastHighRisk && view.CanMoveSelected;
        int? detourYears = highRisk ? view.LanternDetourYears : null;
        if (offerLanternDetour != detourYears.HasValue)
        {
            offerLanternDetour = detourYears.HasValue;
            cultivateLabel.enableAutoSizing = offerLanternDetour;
            if (offerLanternDetour)
            {
                cultivateLabel.fontSizeMin = regularCultivateFontSize * .65f;
                cultivateLabel.fontSizeMax = regularCultivateFontSize;
            }
            else cultivateLabel.fontSize = regularCultivateFontSize;
        }
        moveLabel.text = opening ? view.OpeningPrimaryLabel : highRisk
            ? TimelineText("Timeline_LanternForecastFight") : regularMove;
        cultivateLabel.text = offerRoutes ? TimelineText("Timeline_Opening_Detour", view.OpeningDetourYears)
            : detourYears is int years ? TimelineText("Timeline_LanternForecastDetour", years - 1) : regularCultivate;
        moveLabel.color = offerRoutes || highRisk ? new Color(1f, .71f, .54f) : regularMoveColor;
        cultivateLabel.color = offerRoutes || detourYears.HasValue ? WorldTimelineBoard.OpeningDetourColor : regularCultivateColor;
        cultivate.gameObject.SetActive(offerRoutes || detourYears.HasValue);
        rest.gameObject.SetActive(!opening && view.Simulation.WaitingForPlayer && view.Expedition?.AttritionEnabled == true);
        rest.interactable = view.CanRest;
        wait.gameObject.SetActive(view.Simulation.WaitingForPlayer && (!opening || atStop));
        disguise.gameObject.SetActive(!opening && view.Simulation.WaitingForPlayer && !view.Simulation.CanJoinChosen(view.Simulation.State.PlayerActorId));
        join.gameObject.SetActive(!opening && view.Simulation.WaitingForPlayer && view.Simulation.CanJoinChosen(view.Simulation.State.PlayerActorId));
        nextLife.gameObject.SetActive(!opening && (view.Simulation.State.IsEnded || view.CanRecordPersonalLife));
        move.gameObject.SetActive(opening || view.Simulation.WaitingForPlayer);
        move.interactable = opening ? view.CanAdvanceOpening(false)
            : view.CanMoveSelected && (!locationOpen || !view.LanternForecastPending);
        cultivate.interactable = offerRoutes ? view.CanAdvanceOpening(true) : waiting && detourYears.HasValue;
        wait.interactable = waiting && (!opening || atStop);
    }

    private void BindOpeningPreview(Button button, int route)
    {
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        Add(EventTriggerType.PointerEnter, route);
        Add(EventTriggerType.PointerExit, 0);

        void Add(EventTriggerType type, int preview)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => view?.PreviewOpeningRoute(preview));
            trigger.triggers.Add(entry);
        }
    }

    private void OpenPopup(int tab)
    {
        popupTab = tab;
        page = 0;
        view.ClearMapHover();
        popup.SetActive(true);
        RefreshPopup();
    }

    private void RefreshPopup()
    {
        bool historyRows = popupTab == 1 && view.Simulation.Histories.Count > 0;
        bool actorRows = popupTab == 0 || historyRows;
        roleList.SetActive(actorRows);
        textPage.SetActive(!actorRows);
        if (actorRows && techniqueRoute != null) techniqueRoute.gameObject.SetActive(false);
        popupTitle.text = TimelineText(popupTab switch
        {
            0 => "Timeline_UI_33",
            1 => "Timeline_UI_18",
            2 => "Timeline_UI_77",
            TechniqueTab => "Timeline_Technique_Title",
            _ => "Timeline_UI_78"
        });
        if (popupTab == 3) popupTitle.text = "成长与属性";
        if (actorRows)
        {
            var world = view.Simulation.State;
            var actors = historyRows ? view.Simulation.Histories.Select(h => world.Actors[h.ActorId]).ToArray()
                : world.Actors.Values.Where(actor => actor.OnWorldMap).OrderBy(actor => actor.Id).ToArray();
            var inactive = historyRows ? view.InactiveHistoryNames() : Array.Empty<string>();
            int rowCount = actors.Length + inactive.Length;
            int pages = Math.Max(1,(rowCount + actorButtons.Length - 1)/actorButtons.Length);
            page = Math.Min(page,pages-1);
            previousPage.gameObject.SetActive(pages>1);
            nextPage.gameObject.SetActive(pages>1);
            pageLabel.gameObject.SetActive(pages>1);
            previousPage.interactable = page>0;
            nextPage.interactable = page+1<pages;
            pageLabel.text = $"{page+1} / {pages}";
            for (int i = 0; i < actorButtons.Length; i++)
            {
                var button = actorButtons[i];
                int index = page*actorButtons.Length+i;
                button.gameObject.SetActive(index < rowCount);
                if (index >= rowCount) continue;
                if (index >= actors.Length)
                {
                    button.interactable = false;
                    actorLabels[i].text = TimelineText("Timeline_UI_108") + inactive[index-actors.Length];
                    continue;
                }
                var actor = actors[index];
                button.interactable = actor.OnWorldMap;
                string name = actor.Id == view.Simulation.State.PlayerActorId ? view.PlayerName
                    : actor.Id == view.Simulation.State.ChosenActorId ? TimelineText("Timeline_UI_79") : view.ClearOwner(actor.Id);
                bool showRouteColor = actor.Id != view.Simulation.State.PlayerActorId;
                actorLabels[i].richText = showRouteColor;
                if (showRouteColor)
                {
                    var tint = WorldTimelineBoard.ActorRouteColor(actor.Id, view.Simulation.State.ChosenActorId);
                    name = $"<color=#{ColorUtility.ToHtmlStringRGB(tint)}>{name}</color>";
                }
                actorLabels[i].font = cultivateLabel.font;
                actorLabels[i].enableAutoSizing = true;
                actorLabels[i].fontSizeMin = 15;
                actorLabels[i].fontSizeMax = 21;
                actorLabels[i].text = actor.OnWorldMap
                    ? $"{name} · {actor.Level}级 · 气血 {actor.Health}/{actor.MaxHealth}\n{view.MapActorLocation(actor)}"
                    : name + " · " + view.MapActorLocation(actor);
                button.onClick.RemoveAllListeners();
                BindButtonClickSound(button);
                int id = actor.Id;
                button.onClick.AddListener(() =>
                {
                    view.FocusMapActor(id);
                    popup.SetActive(false);
                });
            }
            return;
        }
        bool showingTechnique = popupTab == TechniqueTab;
        if (techniqueRoute != null) techniqueRoute.gameObject.SetActive(showingTechnique);
        popupText.gameObject.SetActive(!showingTechnique);
        int count;
        if (showingTechnique)
        {
            if (!view.TryGetLearnedTechnique(selectedTechnique, out var art, out var progress))
            {
                ClosePopup();
                return;
            }
            const int stride = WorldTimelineTechniqueRoute.VisibleStages - 1;
            count = Math.Max(1, (art.Stages.Count - 2 + stride) / stride);
            page = Math.Min(page, count - 1);
            var state = view.Simulation.State;
            var player = state.Actors[state.PlayerActorId];
            techniqueRoute.Show(art, page * stride, progress, player, FormatTechniqueRequirement);
        }
        else
        {
            string content = popupTab == 1 ? view.HistoryDetails : popupTab == 2 ? view.LogDetails : view.CultivationDetails;
            var lines = content.Split('\n');
            count = Math.Max(1, (lines.Length + 11) / 12);
            page = Math.Min(page, count - 1);
            popupText.text = string.Join("\n", lines.Skip(page * 12).Take(12));
        }
        bool showPagination = !showingTechnique || count > 1;
        previousPage.gameObject.SetActive(showPagination);
        nextPage.gameObject.SetActive(showPagination);
        pageLabel.gameObject.SetActive(showPagination);
        pageLabel.text = $"{page + 1} / {count}";
        previousPage.interactable = page > 0;
        nextPage.interactable = page + 1 < count;
    }

    private string FormatTechniqueRequirement(TechniqueStage stage, int progress)
    {
        switch (stage.Requirement)
        {
            case TechniqueRequirement.BattleExperience:
                return TimelineText("Timeline_Technique_Experience", progress, stage.Value);
            case TechniqueRequirement.SkillKills:
                return TimelineText("Timeline_Technique_Kills", progress, stage.Value);
            case TechniqueRequirement.Item:
                var story = view.Simulation.Stories;
                string item = story != null && story.Config.Items.TryGetValue(stage.Value, out var definition)
                    ? definition.Name : TimelineText("Timeline_UI_RelicId", stage.Value);
                var world = view.Simulation.State;
                bool held = world.Actors[world.PlayerActorId].Items.Contains(stage.Value);
                return TimelineText("Timeline_Technique_Item", item) + "  ·  " +
                    TimelineText(held ? "Timeline_Technique_ItemHeld" : "Timeline_Technique_ItemMissing");
            default:
                return TimelineText("Timeline_Technique_Starting");
        }
    }

    public void SelectNode(Vector2 screen)
    {
        if (!CanInteractWithMap) return;
        ShowLocationDetails(view.SelectAtScreen(screen));
    }

    private void ShowLocationDetails(bool open)
    {
        locationOpen = open;
        locationSection.SetActive(open);
        if (!open || !view.CanForecastLanternShoal) view.CancelLanternForecast();
        if (open) RefreshLocationDetails();
        RefreshOpeningButtons(view.Simulation.WaitingForPlayer && !view.IsBusy);
    }
    public void HoverNode(Vector2 screen)
    {
        if (!CanInteractWithMap) return;
        view.HoverAtScreen(screen);
        if (view.OpeningActive) view.PreviewOpeningRouteAt(screen);
    }
    public void ClearHover()
    {
        if (view == null) return;
        view.ClearMapHover();
        view.PreviewOpeningRoute(0);
    }
    public void Pan(Vector2 delta) { if (CanInteractWithMap) view.PanMap(delta); }
    public void Zoom(float factor) { if (CanInteractWithMap) view.ZoomMap(factor); }
}
