using static WorldTimelineDebug.WorldTimelineDebugView;
﻿using System;
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
    [SerializeField] private Button move, cultivate, cultivateFive, wait, disguise, join;
    [SerializeField] private Button observeStep, observeToggle, speed, nextLife, returnMenu;
    [SerializeField] private Button overview, focusPlayer, focusChosen, zoomIn, zoomOut;
    [SerializeField] private Button roles, history, logs, growth, closePopup, previousPage, nextPage;
    [SerializeField] private TMP_Text observeLabel, speedLabel, popupTitle, popupText, pageLabel;
    [SerializeField] private GameObject popup, roleList, textPage;
    [SerializeField] private WorldTimelineTechniqueRoute techniqueRoute;
    [SerializeField] private Button[] actorButtons;
    [SerializeField] private TMP_Text[] actorLabels;
    private WorldTimelineDebugView view;
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
        regularMove = moveLabel.text;
        regularCultivate = cultivateLabel.text;
        regularMoveColor = moveLabel.color;
        regularCultivateColor = cultivateLabel.color;
        regularCultivateFontSize = cultivateLabel.fontSize;
        BuildMapLegend();
        BuildExpeditionButton();
        BuildCompanionStageCard();
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
        cultivateFive.GetComponentInChildren<TMP_Text>(true).text = "成长来源";
        growth.GetComponentInChildren<TMP_Text>(true).text = "成长";
        cultivateFive.onClick.AddListener(() => OpenPopup(3));
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
        view.UsesPrefabHud = true;
        view.BattleFormationPresenter = PresentExpeditionFormation;
        view.TeamRewardPresenter=()=>ExpeditionRewardPanel.Present(view,moveLabel.font);
        view.EquipmentServicePresenter=()=>ExpeditionWorkshopPanel.Present(view,moveLabel.font);
        view.Closed += OnSessionClosed;
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
            view.TeamRewardPresenter=null;
            view.EquipmentServicePresenter=null;
            view.ExpeditionPanelOpen = false;
            view.CancelLanternForecast();
            view.Closed -= OnSessionClosed;
            view.UsesPrefabHud = false;
        }
        view = null;
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
        if(view.Expedition?.Equipment.Modern!=true)return;
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
        if (view?.Expedition != null)
        {
            expeditionButton.GetComponentInChildren<TMP_Text>().text = $"队伍·同伴  {view.Expedition.DeployedCount}/3";
            expeditionButton.interactable = !view.IsBusy && view.Simulation.WaitingForPlayer;
            recruitButton.GetComponentInChildren<TMP_Text>().text = view.PendingVanguard.HasValue
                ? "盾卫首战 · 查看关卡" : "同伴关卡 · 查看地图";
            recruitButton.interactable = expeditionButton.interactable;
        }
        foreach (var section in mapSections) section.SetActive(showMap);
        locationSection.SetActive(showMap && locationOpen);
        mapLegend.SetActive(showMap && view != null && !view.OpeningActive && !popup.activeSelf);
    }

    private void BuildExpeditionButton()
    {
        recruitButton = CreateMapButton("同伴关卡", -170, 360, new Color(.61f, .40f, .14f));
        expeditionButton = CreateMapButton("队伍·同伴", 180, 300, new Color(.19f, .38f, .32f));
        recruitButton.onClick.AddListener(() =>
        {
            if (!CanInteractWithMap || !view.Simulation.WaitingForPlayer) return;
            if (view.FocusVanguard()) { ShowLocationDetails(true); return; }
            var next = new[] { 0, 2, 3, 1 }.Where(candidate => view.Expedition.RecruitmentSites.ContainsKey(candidate) &&
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
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(x, -110);
            rect.sizeDelta = new Vector2(width, 64);
            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
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
        if(view?.Expedition?.Equipment.Enabled==true){OpenEquipment();return;}
        if (view?.Expedition == null || !CanInteractWithMap || !view.Simulation.WaitingForPlayer || expeditionPanel != null) return;
        ClearHover();
        expeditionPanel = ExpeditionPartyPanel.Open(view, moveLabel.font, false, view.SelectedNodeId,
            () => { expeditionPanel = null; RefreshDisplay(); }, role =>
            {
                if (view.FocusRecruitmentSite(role)) ShowLocationDetails(true);
            });
        RefreshDisplay();
    }

    private IEnumerator PresentExpeditionFormation(int nodeId)
    {
        var resolver = new WorldTimelineBattleResolver();
        yield return resolver.PreviewFormation(view.Simulation.State, nodeId,
            (actors, entities) => ExpeditionBattleFormation.Present(view, moveLabel.font, nodeId, actors, entities));
    }

    private void BuildMapLegend()
    {
        var card = MapLegendRect("关卡图例", (RectTransform)transform, new Vector2(28, -162), new Vector2(370, 173));
        mapLegend = card.gameObject;
        var paper = mapLegend.AddComponent<Image>();
        paper.color = new Color(.98f, .95f, .88f, .92f);
        paper.raycastTarget = false;
        var border = mapLegend.AddComponent<Outline>();
        border.effectColor = new Color(.37f, .31f, .21f, .35f);
        border.effectDistance = new Vector2(1f, -1f);
        mapLegend.SetActive(false);
        for (int i = 0; i < WorldTimelineMapMarker.Count; i++)
        {
            var kind = i == 0 ? MapMarkerKind.Companion : (MapMarkerKind)i;
            var position = new Vector2(12 + i / 5 * 178, -9 - i % 5 * 31);
            var icon = MapLegendRect(kind.ToString(), card, position, new Vector2(26, 26)).gameObject.AddComponent<Image>();
            icon.sprite = WorldTimelineMapMarker.GetSprite(kind);
            icon.raycastTarget = false;
            var label = MapLegendRect("Label", card, position + new Vector2(31, 0), new Vector2(143, 26))
                .gameObject.AddComponent<TextMeshProUGUI>();
            label.font = cultivateLabel.font;
            label.fontSize = 18;
            label.fontSizeMin = 14;
            label.fontSizeMax = 18;
            label.enableAutoSizing = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = new Color(.27f, .24f, .19f);
            label.raycastTarget = false;
            label.text = kind == MapMarkerKind.Companion ? "同伴关卡" : TimelineText(WorldTimelineMapMarker.LabelKey(kind));
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
        SetDisplayState(ReadDisplayState());
        bool forecastChanged = locationOpen && CanInteractWithMap && view.UpdateLanternForecast();
        if (!locationOpen || !CanInteractWithMap) view.CancelLanternForecast();
        if (!forecastChanged && Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .15f;
        Refresh();
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
        ritualFill.fillAmount = Mathf.Clamp01(remaining / (float)ritualDuration);
        status.text = view.CurrentStatus+(view.Expedition?.Equipment.Modern==true?$"  ·  灵石 {player.LegacySpiritStones}":"");
        returnMenu.interactable = !view.IsBusy;
        roles.interactable = history.interactable = logs.interactable = !view.InventoryOpen && !view.IsBusy;
        heroName.text = view.PlayerName;
        heroLevel.text = TimelineText("Timeline_UI_Realm", player.Level);
        health.text = $"{Math.Max(0, player.Health)} / {player.MaxHealth}";
        healthFill.fillAmount = Mathf.Clamp01(player.Health / (float)player.MaxHealth);
        var levels = view.Simulation.Cultivation;
        long required = levels.GetLevel(player.Level).ExperienceToNext;
        experience.text = required == 0 ? TimelineText("Timeline_UI_75") : $"{player.LevelExperience} / {required}";
        experienceFill.fillAmount = required == 0 ? 1 : Mathf.Clamp01(player.LevelExperience / (float)required);
        if (locationOpen) RefreshLocationDetails();
        var mapNotice = view.MapNotice;
        notice.text = mapNotice.Text;
        if (relayNotice != null)
            relayNotice.interactable = notice.raycastTarget = mapNotice.NodeId.HasValue && CanInteractWithMap;
        bool waiting = view.Simulation.WaitingForPlayer && !view.IsBusy;
        cultivateFive.interactable = waiting;
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

    private void RefreshLocationDetails()
    {
        var node = view.SelectedLocation;
        locationName.text = view.LocationName(node);
        locationKind.text = $"{view.LocationKind(node)}   ({node.Q}, {node.R})";
        locationKind.font = cultivateLabel.font;
        locationHealth.text = node.MonsterHealth > 0 ? node.MonsterHealth.ToString() : TimelineText("Timeline_UI_76");
        locationAttack.text = node.MonsterHealth > 0 ? node.MonsterAttack.ToString() : "—";
        locationSpirit.text = view.Expedition?.HasExplored(node.Id) == true ? "已领取"
            : "+" + (view.Expedition?.ExplorationExperience(view.Simulation.Cultivation) ?? 0);
        locationArt.enabled = !node.CompanionStageRole.HasValue && (node.MonsterHealth > 0 || view.HasClearedGuardian(node));
        companionStageCard.SetActive(node.CompanionStageRole.HasValue);
        if (node.CompanionStageRole is int role)
        {
            bool owned = view.Expedition.HasRecruited(role);
            bool local = view.Simulation.State.Actors[view.Simulation.State.PlayerActorId].NodeId == node.Id;
            companionStageText.text = "<b>" + view.CompanionName(role) + "</b>\n" +
                (owned ? "已完成招募\n" + (view.Expedition.CellOf(ExpeditionParty.FirstCompanionId + role) >= 0
                    ? "已上阵 · 可前往队伍调整站位" : "已加入候补 · 可前往队伍上阵") : view.RecruitmentConditions(role));
            companionStageRecruit.interactable = CanInteractWithMap && view.Expedition.CanRecruit(role);
            companionStageAction.text = owned ? "已招募" : !local ? "请先抵达此关卡" : node.MonsterHealth > 0
                ? "请先清除守军" : !view.Expedition.RecruitmentConditionMet(role) ? "招募条件未满足"
                : view.Expedition.DeployedCount >= ExpeditionParty.Capacity ? "招募至候补" : "招募入队";
            if (cultivateLabel.font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                cultivateLabel.font.TryAddCharacters(companionStageText.text + companionStageAction.text + locationKind.text);
            companionStageText.ForceMeshUpdate();
            companionStageAction.ForceMeshUpdate();
        }
        loot.text = view.LocationLoot(node);
        bool showingForecast = view.CanForecastLanternShoal;
        hint.font = showingForecast ? cultivateLabel.font : regularLocationHintFont;
        hint.text = showingForecast ? LanternForecastHint() : view.LocationHint;
        string recruitment = view.RecruitmentAt(node.Id);
        if (!string.IsNullOrEmpty(recruitment)) hint.text = recruitment + (showingForecast ? "\n" + hint.text : "");
        if (!string.IsNullOrEmpty(recruitment) || view.Expedition?.ProgressionEnabled == true)
        {
            hint.font = cultivateLabel.font;
            if (hint.font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                hint.font.TryAddCharacters(hint.text + "首次历练已领取");
            hint.ForceMeshUpdate();
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
        cultivateFive.gameObject.SetActive(!opening);
        wait.gameObject.SetActive(!opening || atStop);
        disguise.gameObject.SetActive(!opening);
        join.gameObject.SetActive(!opening);
        nextLife.gameObject.SetActive(!opening);
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
        roleList.SetActive(popupTab == 0);
        textPage.SetActive(popupTab != 0);
        if (popupTab == 0 && techniqueRoute != null) techniqueRoute.gameObject.SetActive(false);
        popupTitle.text = TimelineText(popupTab switch
        {
            0 => "Timeline_UI_33",
            1 => "Timeline_UI_18",
            2 => "Timeline_UI_77",
            TechniqueTab => "Timeline_Technique_Title",
            _ => "Timeline_UI_78"
        });
        if (popupTab == 3) popupTitle.text = "成长与属性";
        if (popupTab == 0)
        {
            previousPage.gameObject.SetActive(false);
            nextPage.gameObject.SetActive(false);
            pageLabel.gameObject.SetActive(false);
            var actors = view.Simulation.State.Actors.Values.Where(actor => actor.IsAlive).OrderBy(actor => actor.Id).ToArray();
            for (int i = 0; i < actorButtons.Length; i++)
            {
                var button = actorButtons[i];
                button.gameObject.SetActive(i < actors.Length);
                if (i >= actors.Length) continue;
                var actor = actors[i];
                var point = ActorMapProjection.Position(view.Simulation.State, actor);
                string name = actor.Id == view.Simulation.State.PlayerActorId ? view.PlayerName
                    : actor.Id == view.Simulation.State.ChosenActorId ? TimelineText("Timeline_UI_79") : TimelineText("Timeline_UI_PastLife", actor.Id);
                bool showRouteColor = actor.Id != view.Simulation.State.PlayerActorId;
                actorLabels[i].richText = showRouteColor;
                if (showRouteColor)
                {
                    var tint = WorldTimelineBoard.ActorRouteColor(actor.Id, view.Simulation.State.ChosenActorId);
                    name = $"<color=#{ColorUtility.ToHtmlStringRGB(tint)}>{name}</color>";
                }
                actorLabels[i].text = TimelineText("Timeline_UI_ActorSummary", name, actor.Level, actor.Health, actor.Attack, point.Q, point.R);
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
