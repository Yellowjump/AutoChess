using static WorldTimelineDebug.WorldTimelineDebugView;
﻿using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

public sealed class WorldTimelinePanelCtrl : UIFormLogic
{
    public enum DisplayState { Map, Battle, Preparation }
    [SerializeField] private CanvasGroup hud;
    [SerializeField] private GameObject headerSection, locationSection;
    [SerializeField] private GameObject[] mapSections;
    [SerializeField] private Button closeLocation;
    [SerializeField] private Button equipment;
    [SerializeField] private TMP_Text year, deadline, status, heroName, heroLevel, health, experience, lifespan;
    [SerializeField] private TMP_Text locationName, locationKind, locationHealth, locationAttack, locationSpirit, loot, hint, notice;
    [SerializeField] private Image healthFill, experienceFill, ritualFill, locationArt;
    [SerializeField] private Button move, cultivate, cultivateFive, wait, disguise, join;
    [SerializeField] private Button observeStep, observeToggle, speed, nextLife, returnMenu;
    [SerializeField] private Button overview, focusPlayer, focusChosen, zoomIn, zoomOut;
    [SerializeField] private Button roles, history, logs, growth, closePopup, previousPage, nextPage;
    [SerializeField] private TMP_Text observeLabel, speedLabel, popupTitle, popupText, pageLabel;
    [SerializeField] private GameObject popup, roleList, textPage;
    [SerializeField] private Button[] actorButtons;
    [SerializeField] private TMP_Text[] actorLabels;
    private WorldTimelineDebugView view;
    private float nextRefresh;
    private int popupTab, page;
    private DisplayState displayState;
    private bool locationOpen;
    public DisplayState CurrentDisplayState => displayState;
    public void RefreshDisplay() => SetDisplayState(ReadDisplayState());

    public bool CanInteractWithMap => view != null && displayState == DisplayState.Map
        && !view.IsBusy && !view.IsPreparing && !view.InventoryOpen && !popup.activeSelf;

    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.text.StartsWith("Timeline_", System.StringComparison.Ordinal)) label.text = TimelineText(label.text);
        equipment?.onClick.AddListener(OpenEquipment);
        closeLocation.onClick.AddListener(CloseLocationDetails);
        move.onClick.AddListener(() => view.MoveSelected());
        cultivate.onClick.AddListener(() => view.CultivateYears(1));
        cultivateFive.onClick.AddListener(() => view.CultivateYears(5));
        wait.onClick.AddListener(() => view.WaitYear());
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
        closePopup.onClick.AddListener(() => popup.SetActive(false));
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
        view.Closed += OnSessionClosed;
        locationOpen = false;
        SetHudVisible(true, true);
        SetDisplayState(ReadDisplayState());
        popup.SetActive(false);
        Refresh();
    }

    public override void OnClose(bool isShutdown, object userData)
    {
        if (view != null)
        {
            view.Closed -= OnSessionClosed;
            view.UsesPrefabHud = false;
        }
        view = null;
        base.OnClose(isShutdown, userData);
    }

    private void OnSessionClosed() => GameEntry.UI.CloseUIForm(UIForm);
    public void OpenEquipment()
    {
        if (view == null || view.IsPreparing || view.IsBusy || view.InventoryOpen) return;
        if (GameEntry.UI.HasUIForm(UICtrlName.WorldTimelineInventory) || GameEntry.UI.IsLoadingUIForm(UICtrlName.WorldTimelineInventory)) return;
        ClearHover();
        GameEntry.UI.OpenUIForm(UICtrlName.WorldTimelineInventory, "tips", userData: view);
    }

    private DisplayState ReadDisplayState() => view.IsPreparing ? DisplayState.Preparation
        : view.IsBusy ? DisplayState.Battle : DisplayState.Map;

    public void SetDisplayState(DisplayState state)
    {
        if (displayState != state)
        {
            locationOpen = false;
            popup.SetActive(false);
            ClearHover();
        }
        displayState = state;
        headerSection.SetActive(view == null || !view.InventoryOpen);
        bool showMap = state == DisplayState.Map && (view == null || !view.InventoryOpen);
        foreach (var section in mapSections) section.SetActive(showMap);
        locationSection.SetActive(showMap && locationOpen);
    }

    public void CloseLocationDetails()
    {
        locationOpen = false;
        locationSection.SetActive(false);
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
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .15f;
        Refresh();
    }

    private void Refresh()
    {
        var world = view.Simulation.State;
        var player = world.Actors[world.PlayerActorId];
        var node = view.SelectedLocation;
        int remaining = Math.Max(0, world.Ritual.DeadlineYear - world.Year);
        year.text = world.Year == 0 ? TimelineText("Timeline_UI_74") : TimelineText("Timeline_UI_Year", world.Year);
        bool showingBattle = HotGameEntry.HeroManager.IsTimelineBattle && !HotGameEntry.HeroManager.IsTimelineSimulation;
        deadline.text = showingBattle ? TimelineText("Timeline_UI_129") : TimelineText("Timeline_UI_Deadline", remaining);
        ritualFill.transform.parent.gameObject.SetActive(!showingBattle);
        ritualFill.fillAmount = Mathf.Clamp01(remaining / (float)world.Ritual.PeriodYears);
        status.text = view.CurrentStatus;
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
        lifespan.text = TimelineText("Timeline_UI_Lifespan", player.Age, player.Lifespan);
        locationName.text = view.LocationName(node);
        locationKind.text = $"{view.LocationKind(node)}   ({node.Q}, {node.R})";
        locationHealth.text = node.MonsterHealth > 0 ? node.MonsterHealth.ToString() : TimelineText("Timeline_UI_76");
        locationAttack.text = node.MonsterHealth > 0 ? node.MonsterAttack.ToString() : "—";
        locationSpirit.text = node.CultivationPerYear.ToString();
        locationArt.enabled = node.MonsterHealth > 0 || view.HasClearedGuardian(node);
        loot.text = view.LocationLoot(node);
        hint.text = view.LocationHint;
        notice.text = view.Notice;
        move.interactable = view.CanMoveSelected;
        bool waiting = view.Simulation.WaitingForPlayer && !view.IsBusy;
        cultivate.interactable = cultivateFive.interactable = wait.interactable = waiting;
        bool canJoin = waiting && view.Simulation.CanJoinChosen(world.PlayerActorId);
        join.interactable = canJoin;
        disguise.interactable = waiting && !canJoin;
        bool observing = !waiting && !world.IsEnded;
        observeStep.interactable = observeToggle.interactable = observing;
        nextLife.interactable = world.IsEnded;
        observeLabel.text = view.AutoObserving ? TimelineText("Timeline_UI_22") : TimelineText("Timeline_UI_21");
        speedLabel.text = TimelineText("Timeline_UI_ObservationSpeed", view.ObservationSpeed);
        if (popup.activeSelf) RefreshPopup();
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
        previousPage.gameObject.SetActive(popupTab != 0);
        nextPage.gameObject.SetActive(popupTab != 0);
        pageLabel.gameObject.SetActive(popupTab != 0);
        popupTitle.text = new[] { TimelineText("Timeline_UI_33"), TimelineText("Timeline_UI_18"), TimelineText("Timeline_UI_77"), TimelineText("Timeline_UI_78") }[popupTab];
        if (popupTab == 0)
        {
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
                actorLabels[i].text = TimelineText("Timeline_UI_ActorSummary", name, actor.Level, actor.Health, actor.Attack, point.Q, point.R);
                button.onClick.RemoveAllListeners();
                int id = actor.Id;
                button.onClick.AddListener(() =>
                {
                    view.FocusMapActor(id);
                    popup.SetActive(false);
                });
            }
            return;
        }
        string content = popupTab == 1 ? view.HistoryDetails : popupTab == 2 ? view.LogDetails : view.CultivationDetails;
        var lines = content.Split('\n');
        int count = Math.Max(1, (lines.Length + 11) / 12);
        page = Math.Min(page, count - 1);
        popupText.text = string.Join("\n", lines.Skip(page * 12).Take(12));
        pageLabel.text = $"{page + 1} / {count}";
        previousPage.interactable = page > 0;
        nextPage.interactable = page + 1 < count;
    }

    public void SelectNode(Vector2 screen)
    {
        if (!CanInteractWithMap) return;
        locationOpen = view.SelectAtScreen(screen);
        locationSection.SetActive(locationOpen);
        Refresh();
    }
    public void HoverNode(Vector2 screen) { if (CanInteractWithMap) view.HoverAtScreen(screen); }
    public void ClearHover() { if (view != null) view.ClearMapHover(); }
    public void Pan(Vector2 delta) { if (CanInteractWithMap) view.PanMap(delta); }
    public void Zoom(float factor) { if (CanInteractWithMap) view.ZoomMap(factor); }
}
