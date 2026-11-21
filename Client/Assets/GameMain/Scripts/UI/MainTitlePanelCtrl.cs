using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameFramework.Event;
using GameFramework.Localization;
using Procedure;
using SelfEventArg;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimelineDebug;

public class MainTitlePanelCtrl : UIFormLogic
{
    [SerializeField] private RectTransform inkBackground;
    private float inkTime;

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        if (!Visible || inkBackground == null) return;
        inkTime = Mathf.Repeat(inkTime + realElapseSeconds, 24f);
        float phase = inkTime * (2f * Mathf.PI / 24f);
        Vector2 size = inkBackground.rect.size;
        inkBackground.localScale = Vector3.one * 1.05f;
        inkBackground.anchoredPosition = new Vector2(
            size.x * .018f * Mathf.Sin(phase),
            size.y * .007f * (1f - Mathf.Cos(phase)));
    }
    [SerializeField]
    private Button _btnContinue;
    [SerializeField]
    private Button _btnStart;
    [SerializeField]
    private Button _btnSetting;
    [SerializeField]
    private Button _btnQuit;
    private WorldTimelineDebugView worldTimeline;
    private TacticalTrialPanel tacticalTrial;
    private Button trialButton;
    private Button collectionButton;
    private TitleCollectionPanel collectionPanel;
    [SerializeField] private Sprite[] collectionItemIcons;
    [SerializeField] private OpeningCinematic openingCinematicPrefab;
    private OpeningCinematic openingCinematic;
    private bool restoreDebugger;
    [SerializeField] private Transform mTransSetting;
    [SerializeField] private Slider mVolumeMasterSlider;
    [SerializeField] private Slider mVolumeBgmSlider;
    [SerializeField] private Slider mVolumeSfxSlider;
    [SerializeField] private Slider mVolumeUISlider;
    [SerializeField] private TMP_Text masterVolumePercent;
    [SerializeField] private TMP_Text bgmVolumePercent;
    [SerializeField] private TMP_Text sfxVolumePercent;
    [SerializeField] private TMP_Text uiVolumePercent;
    [SerializeField] private TMP_Dropdown mLanguageDropDown;
    [SerializeField] private Button settingsClose;
    [SerializeField] private TMP_Text settingsCloseLabel;
    [SerializeField] private TMP_Text displayHeading;
    [SerializeField] private TMP_Text screenModeLabel;
    [SerializeField] private TMP_Text resolutionLabel;
    [SerializeField] private TMP_Dropdown mResolutionDropDown;
    private int curResolutionIndex;
    [SerializeField] private TMP_Dropdown mFullScreenModeDropDown;
    private FullScreenMode curFullScreenMode;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnContinue.onClick.AddListener(OnClickContinueBtn);
        _btnStart.onClick.AddListener(OnClickStartBtn);
        _btnSetting.onClick.AddListener(OnClickSettingBtn);
        _btnQuit.onClick.AddListener(OnClickQuitBtn);
        if (settingsClose != null) settingsClose.onClick.AddListener(CloseSettings);
        mLanguageDropDown.ClearOptions();
        mLanguageDropDown.AddOptions(new List<string> { "中文", "English" });
        mLanguageDropDown.onValueChanged.AddListener(OnLanguageDropdownValueChanged);
        mFullScreenModeDropDown.onValueChanged.AddListener(OnScreenFullModeChanged);
        mVolumeMasterSlider.value = HotGameEntry.Sound.GetMasterVolume();
        mVolumeSfxSlider.value = HotGameEntry.Sound.GetVolume("Sfx");
        mVolumeUISlider.value = HotGameEntry.Sound.GetVolume("UI");
        mVolumeBgmSlider.value = HotGameEntry.Sound.GetVolume("BGM");
        mVolumeMasterSlider.onValueChanged.AddListener(OnMasterVolumeSliderChange);
        mVolumeBgmSlider.onValueChanged.AddListener(OnBgmVolumeSliderChange);
        mVolumeSfxSlider.onValueChanged.AddListener(OnSFXVolumeSliderChange);
        mVolumeUISlider.onValueChanged.AddListener(OnMusicVolumeSliderChange);

        BindVolumePercent(mVolumeMasterSlider, masterVolumePercent);
        BindVolumePercent(mVolumeBgmSlider, bgmVolumePercent);
        BindVolumePercent(mVolumeSfxSlider, sfxVolumePercent);
        BindVolumePercent(mVolumeUISlider, uiVolumePercent);

        InitScreenResolution();
        CreateTrialButton();
        collectionButton = Instantiate(trialButton, transform);
        collectionButton.name = "CollectionButton";
        ((RectTransform)collectionButton.transform).anchoredPosition = new Vector2(-45,-139);
        collectionButton.onClick = new Button.ButtonClickedEvent();
        collectionButton.onClick.AddListener(OpenCollection);
        RefreshTrialLabel();
    }

    private void CreateTrialButton()
    {
        trialButton = Instantiate(_btnStart, transform);
        trialButton.name = "TacticalTrialButton";
        trialButton.onClick = new Button.ButtonClickedEvent();
        trialButton.onClick.AddListener(OpenTacticalTrial);
        var rect = (RectTransform)trialButton.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-45, -45);
        rect.sizeDelta = new Vector2(300, 80);
        trialButton.gameObject.SetActive(true);
        RefreshTrialLabel();
    }

    private void RefreshTrialLabel()
    {
        if (collectionButton != null)
            collectionButton.GetComponentInChildren<TMP_Text>(true).text =
                GameEntry.Localization.Language == Language.English ? "Field Guide" : "图鉴";
        if (trialButton == null) return;
        var label = trialButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = GameEntry.Localization.Language == Language.English ? "Mountain Gate Trial" : "山门试阵";
    }

    private void OpenCollection()
    {
        if (!CanUseTitleActions || IsConfirmationOpen) return;
        CloseSettings();
        var font = _btnStart.GetComponentInChildren<TMP_Text>(true).font;
        collectionPanel = TitleCollectionPanel.Open(font, collectionItemIcons, CloseCollection);
        Visible = false;
    }

    private void CloseCollection()
    {
        if (collectionPanel == null) return;
        collectionPanel.gameObject.SetActive(false);
        Destroy(collectionPanel.gameObject);
        collectionPanel = null;
        Visible = true;
        EventSystem.current?.SetSelectedGameObject(collectionButton.gameObject);
    }

    public void OpenTacticalTrial()
    {
        if (!CanUseTitleActions || IsConfirmationOpen) return;
        CloseSettings();
        var label = _btnStart.GetComponentInChildren<TMP_Text>(true);
        tacticalTrial = TacticalTrialPanel.Open(label != null ? label.font : TMP_Settings.defaultFontAsset, () =>
        {
            tacticalTrial = null;
            Visible = true;
            RefreshTrialLabel();
        });
        Visible = false;
    }

    private static void BindVolumePercent(Slider slider, TMP_Text label)
    {
        if (label == null) return;
        void Refresh(float value) => label.text = $"{Mathf.RoundToInt(value * 100f)}%";
        Refresh(slider.value);
        slider.onValueChanged.AddListener(Refresh);
    }

    private void InitScreenResolution()
    {
        Resolution[] resolutions = Screen.resolutions;
        List<string> resolutionStrList = ListPool<string>.Get();
        foreach (Resolution res in resolutions)
        {
            resolutionStrList.Add($"{res.width} × {res.height}");
        }
        mResolutionDropDown.ClearOptions();
        mResolutionDropDown.AddOptions(resolutionStrList);
        ListPool<string>.Release(resolutionStrList);
        curResolutionIndex = Mathf.Clamp(GameEntry.Setting.GetInt(ConstValue.SettingKeyScreenResolution,
            UnityExtension.GetCurrentResolutionIndex()), 0, Mathf.Max(0, resolutions.Length - 1));
        mResolutionDropDown.SetValueWithoutNotify(curResolutionIndex);
        mResolutionDropDown.interactable = resolutions.Length > 0;
        mResolutionDropDown.onValueChanged.AddListener(OnScreenResolutionChanged);
        curFullScreenMode = Screen.fullScreenMode;
    }

    public override void RefreshLocalizedText()
    {
        base.RefreshLocalizedText();
        RefreshTrialLabel();
        RefreshPaperTypography(mTransSetting);

        bool english = GameEntry.Localization.Language == Language.English;
        mLanguageDropDown.SetValueWithoutNotify(english ? 1 : 0);
        mLanguageDropDown.RefreshShownValue();
        if (settingsCloseLabel != null) settingsCloseLabel.text = GameEntry.Localization.GetString("Timeline_UI_14");
        if (displayHeading != null) displayHeading.text = english ? "Display & Language" : "显示与语言";
        if (screenModeLabel != null) screenModeLabel.text = english ? "Display Mode" : "屏幕模式";
        if (resolutionLabel != null) resolutionLabel.text = english ? "Resolution" : "分辨率";

        var stringList = ListPool<string>.Get();
        stringList.Add(GameEntry.Localization.GetString(EnumLanguage.FullScreenMode_ExclusiveFullScreen));
        stringList.Add(GameEntry.Localization.GetString(EnumLanguage.FullScreenMode_FullScreenWindow));
        stringList.Add(GameEntry.Localization.GetString(EnumLanguage.FullScreenMode_MaximizedWindow));
        stringList.Add(GameEntry.Localization.GetString(EnumLanguage.FullScreenMode_Windowed));
        mFullScreenModeDropDown.ClearOptions();
        mFullScreenModeDropDown.AddOptions(stringList);
        mFullScreenModeDropDown.SetValueWithoutNotify((int)curFullScreenMode);
        mFullScreenModeDropDown.RefreshShownValue();
        ListPool<string>.Release(stringList);
    }
    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
#if UNITY_STANDALONE_WIN && (!UNITY_EDITOR || UNITY_EDITOR_WIN)
        Zizouqi.SteamIntegration.SteamClientRuntime.ShowMainMenu();
#endif
        GameEntry.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnTimelineFormOpened);
        GameEntry.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnTimelineFormFailed);
        WorldTimelineDebugView.ResolveLanguageKey = WorldTimelineTableConfig.Localize;
        var hasData = WorldTimelineDebugView.HasUnfinishedRun;
        _btnContinue.gameObject.SetActive(hasData);
        GameEntry.UI.CloseUIForm(UICtrlName.LoadingPanel);
        mTransSetting.gameObject.SetActive(false);
    }

    private bool CanUseTitleActions => Visible && worldTimeline == null && openingCinematic == null && tacticalTrial == null && collectionPanel == null;

    private static bool IsConfirmationOpen => GameEntry.UI.HasUIForm(UICtrlName.ConfirmPanel) ||
        GameEntry.UI.IsLoadingUIForm(UICtrlName.ConfirmPanel);

    private void OnClickContinueBtn()
    {
        if (IsConfirmationOpen) return;
        Log.Info("OnClickContinueBtn OnClick");
        OpenWorldTimeline(true);
    }

    private void OnClickStartBtn()
    {
        if (!CanUseTitleActions || IsConfirmationOpen) return;
        if (!WorldTimelineDebugView.HasUnfinishedRun)
        {
            OpenWorldTimeline(false);
            return;
        }

        var confirmation = ReferencePool.Acquire<ConfirmPanelData>();
        confirmation.Content = WorldTimelineDebugView.TimelineText("Title_ConfirmNewGame");
        confirmation.ConfirmCallback = () => OpenWorldTimeline(false);
        GameEntry.UI.OpenUIForm(UICtrlName.ConfirmPanel, "tips", confirmation);
    }

    private void OnClickQuitBtn()
    {
        if (CanUseTitleActions && !IsConfirmationOpen) GameEntry.Shutdown(ShutdownType.Quit);
    }

#if UNITY_EDITOR
    internal bool CanClearWorldTimelineSaves => CanUseTitleActions;

    internal bool CanPreviewCinematics => CanUseTitleActions && openingCinematicPrefab != null && !IsConfirmationOpen;

    internal bool TryPreviewCinematic(bool ending)
    {
        if (!CanPreviewCinematics) return false;
        var pages = ending ? WorldTimelineDebugView.CreateEndingPages(
            guardianDisabled: true, springUsed: true, sluiceDrainsBoss: true) : null;
        if (!TryPlayCinematic(endingPages: pages)) return false;
        HotGameEntry.Debugger.ShowFullWindow = false;
        return true;
    }

    internal void ConfirmClearWorldTimelineSaves()
    {
        if (!CanClearWorldTimelineSaves || IsConfirmationOpen) return;

        HotGameEntry.Debugger.ShowFullWindow = false;
        var confirmation = ReferencePool.Acquire<ConfirmPanelData>();
        confirmation.Content = "确定清空当前轮回和全部前世记录？此操作无法撤销。";
        confirmation.ConfirmCallback = ClearWorldTimelineSaves;
        GameEntry.UI.OpenUIForm(UICtrlName.ConfirmPanel, "tips", confirmation);
    }

    private void ClearWorldTimelineSaves()
    {
        if (!CanClearWorldTimelineSaves) return;
        try
        {
            WorldTimelineDebugView.ClearSaves();
            Debug.Log("已清空轮回存档。点击「开始游戏」可重新测试首局开场。");
        }
        catch (System.Exception error) when (error is System.IO.IOException ||
            error is System.UnauthorizedAccessException || error is System.InvalidOperationException)
        {
            Debug.LogError($"清空轮回存档失败：{error}");
        }
        _btnContinue.gameObject.SetActive(WorldTimelineDebugView.HasUnfinishedRun);
    }
#endif

    private void OnClickSettingBtn()
    {
        if (!CanUseTitleActions || IsConfirmationOpen) return;
        mTransSetting.gameObject.SetActive(!mTransSetting.gameObject.activeSelf);
    }

    private void CloseSettings() => mTransSetting.gameObject.SetActive(false);

    private void OpenWorldTimeline(bool resume)
    {
        if (!CanUseTitleActions) return;
        CloseSettings();
        // MonoBehaviour implicitly converts to bool; name userData to select the data overload.
        WorldTimelineDebugView.OpenMapHud = view => GameEntry.UI.OpenUIForm(UICtrlName.WorldTimelinePanel, "top", userData: view);
        WorldTimelineDebugView.OpenPreparation = view => GameEntry.UI.OpenUIForm(UICtrlName.WorldTimelinePreparation, "top", userData: view);
        WorldTimelineDebugView.OpenStoryHud = view => GameEntry.UI.OpenUIForm(UICtrlName.WorldTimelineStory, "tips", userData: view);
        WorldTimelineDebugView.ScenarioFactory = WorldTimelineTableConfig.Create;
        WorldTimelineDebugView.BattleResolverFactory = () => new WorldTimelineBattleResolver();
        worldTimeline = WorldTimelineDebugView.OpenFromMenu(() =>
        {
            worldTimeline = null;
            Visible = true;
            _btnContinue.gameObject.SetActive(WorldTimelineDebugView.HasUnfinishedRun);
#if UNITY_STANDALONE_WIN && (!UNITY_EDITOR || UNITY_EDITOR_WIN)
            Zizouqi.SteamIntegration.SteamClientRuntime.ShowMainMenu();
#endif
        });
        if (resume && !worldTimeline.ResumeUnfinishedRun())
        {
            Destroy(worldTimeline.gameObject);
            worldTimeline = null;
            return;
        }
        if (worldTimeline.IsReady)
        {
            if (!resume && !(worldTimeline.CanBeginOpening && !WorldTimelineDebugView.HasUnfinishedRun &&
                TryPlayCinematic(EnterNewTimeline))) EnterNewTimeline();
        }
        else
        {
            Destroy(worldTimeline.gameObject);
            worldTimeline = null;
        }
    }

    private bool TryPlayCinematic(System.Action onComplete = null,
        (string Title, string Body, string Art)[] endingPages = null)
    {
        if (openingCinematicPrefab == null || openingCinematic != null) return false;

        var sound = HotGameEntry.Sound;
        var bgm = sound.GetSoundGroup("BGM");
        var sfx = sound.GetSoundGroup("Sfx");
        openingCinematic = Instantiate(openingCinematicPrefab);
        bool english = GameEntry.Localization.Language == Language.English;
        float musicVolume = bgm == null || bgm.Mute ? 0 : sound.MasterVolume * bgm.Volume;
        float effectsVolume = sfx == null || sfx.Mute ? 0 : sound.MasterVolume * sfx.Volume;
        bool playing = endingPages == null
            ? openingCinematic.TryPlay(english, musicVolume, effectsVolume, Complete)
            : openingCinematic.TryPlayEnding(endingPages, english, musicVolume, effectsVolume, Complete);
        if (playing)
        {
            var debugger = HotGameEntry.Debugger;
            restoreDebugger = debugger != null && debugger.ActiveWindow;
            if (restoreDebugger) debugger.ActiveWindow = false;
            EventSystem.current?.SetSelectedGameObject(null);
            sound.StopMusic();
            return true;
        }

        openingCinematic.Cancel();
        openingCinematic = null;
        return false;

        void Complete()
        {
            openingCinematic = null;
            RestoreDebugger();
            sound.PlayMusic(ProcedureTitle.TitleMusic);
            onComplete?.Invoke();
        }
    }

    private void EnterNewTimeline()
    {
        if (worldTimeline != null && !worldTimeline.TryBeginOpening())
            WorldTimelineDebugView.OpenPreparation?.Invoke(worldTimeline);
    }

    internal bool TryPlayTimelineEnding(WorldTimelineDebugView view)
    {
        if (view == null || view != worldTimeline || view.EndingPages == null) return false;
        return TryPlayCinematic(() =>
        {
            if (view != null) view.CloseEnding();
        }, view.EndingPages);
    }

    private void RestoreDebugger()
    {
        if (!restoreDebugger) return;
        restoreDebugger = false;
        var debugger = HotGameEntry.Debugger;
        if (debugger != null) debugger.ActiveWindow = true;
    }

    private void OnTimelineFormOpened(object sender, GameEventArgs args)
    {
        var opened = (OpenUIFormSuccessEventArgs)args;
        if (worldTimeline == null || !ReferenceEquals(opened.UserData, worldTimeline)) return;
        if (opened.UIForm.Logic is WorldTimelinePreparationCtrl || opened.UIForm.Logic is WorldTimelinePanelCtrl)
        {
            Visible = false;
#if UNITY_STANDALONE_WIN && (!UNITY_EDITOR || UNITY_EDITOR_WIN)
            Zizouqi.SteamIntegration.SteamClientRuntime.ShowSinglePlayer();
#endif
        }
    }

    private void OnTimelineFormFailed(object sender, GameEventArgs args)
    {
        var failed = (OpenUIFormFailureEventArgs)args;
        if (worldTimeline != null && Visible && ReferenceEquals(failed.UserData, worldTimeline))
            Destroy(worldTimeline.gameObject);
    }

    public override void OnClose(bool isShutdown, object userData)
    {
        GameEntry.Event?.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnTimelineFormOpened);
        GameEntry.Event?.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnTimelineFormFailed);
        if (openingCinematic != null) openingCinematic.Cancel();
        openingCinematic = null;
        RestoreDebugger();
        if (worldTimeline != null) Destroy(worldTimeline.gameObject);
        worldTimeline = null;
        if (collectionPanel != null) Destroy(collectionPanel.gameObject);
        collectionPanel = null;
        if (tacticalTrial != null) Destroy(tacticalTrial.gameObject);
        tacticalTrial = null;
        base.OnClose(isShutdown, userData);
    }
    private void OnLanguageDropdownValueChanged(int newValue)
    {
        var language = newValue == 1 ? Language.English : Language.ChineseSimplified;
        if (language == GameEntry.Localization.Language) return;
        if (!CanUseTitleActions || IsConfirmationOpen || !LocalizationExtensions.ApplyLanguage(language))
        {
            RefreshLocalizedText();
            return;
        }

        GameEntry.Setting.SetInt(ConstValue.SettingKeyLanguage, (int)language);
        GameEntry.Setting.Save();
    }

    private void OnScreenResolutionChanged(int newValue)
    {
        var resolution = UnityExtension.GetResolutionByIndex(newValue);
        if (!resolution.HasValue || newValue == curResolutionIndex) return;
        curResolutionIndex = newValue;
        GameEntry.Setting.SetInt(ConstValue.SettingKeyScreenResolution, curResolutionIndex);
        GameEntry.Setting.Save();
        Screen.SetResolution(resolution.Value.width, resolution.Value.height, curFullScreenMode);
    }

    private void OnScreenFullModeChanged(int newValue)
    {
        var mode = (FullScreenMode)newValue;
        if (mode == curFullScreenMode) return;
        curFullScreenMode = mode;
        GameEntry.Setting.SetInt(ConstValue.SettingFullScreenMode, newValue);
        GameEntry.Setting.Save();
        var resolution = UnityExtension.GetResolutionByIndex(curResolutionIndex);
        Screen.SetResolution(resolution?.width ?? Screen.width, resolution?.height ?? Screen.height, mode);
    }

    private void OnMasterVolumeSliderChange(float value)
    {
        HotGameEntry.Sound.SetMasterVolume(value);
    }
    private void OnBgmVolumeSliderChange(float value)
    {
        HotGameEntry.Sound.SetVolume("BGM", value);
    }
    private void OnSFXVolumeSliderChange(float value)
    {
        HotGameEntry.Sound.SetVolume("Sfx", value);
    }

    private void OnMusicVolumeSliderChange(float value)
    {
        HotGameEntry.Sound.SetVolume("UI", value);
    }
}
