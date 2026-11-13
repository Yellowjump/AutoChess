using System.Collections;
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
using UnityEngine.Serialization;
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
    [SerializeField, FormerlySerializedAs("_btnWorldTimeline"), FormerlySerializedAs("_btnTestHotFixing")]
    private Button _btnReplayOpening;
    private WorldTimelineDebugView worldTimeline;
    [SerializeField] private OpeningCinematic openingCinematicPrefab;
    private OpeningCinematic openingCinematic;
    private bool restoreDebugger;
    [SerializeField] private Transform mTransSetting;
    [SerializeField] private Slider mVolumeMasterSlider;
    [SerializeField] private Slider mVolumeBgmSlider;
    [SerializeField] private Slider mVolumeSfxSlider;
    [SerializeField] private Slider mVolumeUISlider;
    [SerializeField] private TMP_Dropdown mLanguageDropDown;
    private Language currentLanguage;
    private Language selectLanguage;
    [SerializeField] private TMP_Dropdown mResolutionDropDown;
    private int curResolutionIndex;
    private int selectResolutionIndex;
    [SerializeField] private TMP_Dropdown mFullScreenModeDropDown;
    private FullScreenMode  curFullScreenMode;
    private FullScreenMode  selectFullScreenMode;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnContinue.onClick.AddListener(OnClickContinueBtn);
        _btnStart.onClick.AddListener(OnClickStartBtn);
        _btnSetting.onClick.AddListener(OnClickSettingBtn);
        if (_btnReplayOpening != null)
            _btnReplayOpening.onClick.AddListener(OnClickReplayOpening);
        mLanguageDropDown.ClearOptions();
        var stringList = ListPool<string>.Get();
        stringList.Add(GameEntry.Localization.GetString(EnumLanguage.Language_Chinese));
        stringList.Add(GameEntry.Localization.GetString(EnumLanguage.Language_English));
        mLanguageDropDown.AddOptions(stringList);
        currentLanguage = (Language)GameEntry.Setting.GetInt(ConstValue.SettingKeyLanguage, (int)Language.ChineseSimplified);
        selectLanguage = currentLanguage;
        int dropDownValue = 0;
        switch (currentLanguage)
        {
            case Language.ChineseSimplified:
                dropDownValue = 0;
                break;
            case Language.English:
                dropDownValue = 1;
                break;
        }
        mLanguageDropDown.value = dropDownValue;
        mLanguageDropDown.onValueChanged.AddListener(OnLanguageDropdownValueChanged);
        ListPool<string>.Release(stringList);
        mVolumeMasterSlider.value = HotGameEntry.Sound.GetMasterVolume();
        mVolumeSfxSlider.value = HotGameEntry.Sound.GetVolume("Sfx");
        mVolumeUISlider.value = HotGameEntry.Sound.GetVolume("UI");
        mVolumeBgmSlider.value = HotGameEntry.Sound.GetVolume("BGM");
        mVolumeMasterSlider.onValueChanged.AddListener(OnMasterVolumeSliderChange);
        mVolumeBgmSlider.onValueChanged.AddListener(OnBgmVolumeSliderChange);
        mVolumeSfxSlider.onValueChanged.AddListener(OnSFXVolumeSliderChange);
        mVolumeUISlider.onValueChanged.AddListener(OnMusicVolumeSliderChange);

        InitScreenResolution();
        InitScreenFullMode();
    }

    private void InitScreenResolution()
    {
        Resolution[] resolutions = Screen.resolutions;
        List<string> resolutionStrList = ListPool<string>.Get();
        foreach (Resolution res in resolutions)
        {
            Debug.Log($"支持的分辨率: {res.width} × {res.height}");
            resolutionStrList.Add($"{res.width} × {res.height}");
        }
        mResolutionDropDown.ClearOptions();
        mResolutionDropDown.AddOptions(resolutionStrList);
        ListPool<string>.Release(resolutionStrList);
        curResolutionIndex = GameEntry.Setting.GetInt(ConstValue.SettingKeyScreenResolution,  0);
        curFullScreenMode = (FullScreenMode)GameEntry.Setting.GetInt(ConstValue.SettingFullScreenMode, (int)FullScreenMode.Windowed);
        selectResolutionIndex = curResolutionIndex;
        mResolutionDropDown.value = curResolutionIndex;
        mResolutionDropDown.onValueChanged.AddListener(OnScreenResolutionChanged);
    }

    private void InitScreenFullMode()
    {
        var stringList = ListPool<string>.Get();
        stringList.Add(GameEntry.Localization.GetString(EnumLanguage.FullScreenMode_ExclusiveFullScreen));
        stringList.Add(GameEntry.Localization.GetString(EnumLanguage.FullScreenMode_FullScreenWindow));
        stringList.Add(GameEntry.Localization.GetString(EnumLanguage.FullScreenMode_MaximizedWindow));
        stringList.Add(GameEntry.Localization.GetString(EnumLanguage.FullScreenMode_Windowed));
        mFullScreenModeDropDown.ClearOptions();
        mFullScreenModeDropDown.AddOptions(stringList);
        curFullScreenMode = (FullScreenMode)GameEntry.Setting.GetInt(ConstValue.SettingFullScreenMode, (int)FullScreenMode.Windowed);
        selectFullScreenMode = curFullScreenMode;
        mFullScreenModeDropDown.value = (int)curFullScreenMode;
        mFullScreenModeDropDown.onValueChanged.AddListener(OnScreenFullModeChanged);
        ListPool<string>.Release(stringList);
    }
    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        GameEntry.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnTimelineFormOpened);
        GameEntry.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnTimelineFormFailed);
        var hasData = WorldTimelineDebugView.HasUnfinishedRun;
        _btnContinue.gameObject.SetActive(hasData);
        if (_btnReplayOpening != null)
        {
            _btnReplayOpening.gameObject.SetActive(openingCinematicPrefab != null);
            _btnReplayOpening.GetComponentInChildren<TMP_Text>(true).text =
                currentLanguage == Language.English ? "Watch Opening" : "观看开场";
        }
        GameEntry.UI.CloseUIForm(UICtrlName.LoadingPanel);
        mTransSetting.gameObject.SetActive(false);
    }

    private void OnClickContinueBtn()
    {
        Log.Info("OnClickContinueBtn OnClick");
        HotGameEntry.Sound.PlayUISound((int)EnumSound.BtnSfx);
        OpenWorldTimeline(true);
    }
    private void OnClickStartBtn()
    {
        HotGameEntry.Sound.PlayUISound((int)EnumSound.BtnSfx);
        OpenWorldTimeline(false);
    }
    private void OnClickSettingBtn()
    {
        HotGameEntry.Sound.PlayUISound((int)EnumSound.BtnSfx);
        mTransSetting.gameObject.SetActive(!mTransSetting.gameObject.activeSelf);
    }

    private void OnClickReplayOpening()
    {
        HotGameEntry.Sound.PlayUISound((int)EnumSound.BtnSfx);
        if (worldTimeline == null) TryPlayOpeningCinematic();
    }

    private void OpenWorldTimeline(bool resume)
    {
        if (worldTimeline != null || openingCinematic != null) return;
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
        });
        if (resume && !worldTimeline.ResumeUnfinishedRun())
        {
            Destroy(worldTimeline.gameObject);
            worldTimeline = null;
            return;
        }
        if (worldTimeline.IsReady)
        {
            if (!resume && !(worldTimeline.PreparationHistoryCount == 0 && !WorldTimelineDebugView.HasUnfinishedRun &&
                TryPlayOpeningCinematic(EnterNewTimeline))) EnterNewTimeline();
        }
        else
        {
            Destroy(worldTimeline.gameObject);
            worldTimeline = null;
        }
    }

    private bool TryPlayOpeningCinematic(System.Action onComplete = null)
    {
        if (openingCinematicPrefab == null || openingCinematic != null) return false;

        var sound = HotGameEntry.Sound;
        var bgm = sound.GetSoundGroup("BGM");
        var sfx = sound.GetSoundGroup("Sfx");
        openingCinematic = Instantiate(openingCinematicPrefab);
        if (openingCinematic.TryPlay(currentLanguage == Language.English,
            bgm == null || bgm.Mute ? 0 : sound.MasterVolume * bgm.Volume,
            sfx == null || sfx.Mute ? 0 : sound.MasterVolume * sfx.Volume,
            () =>
            {
                openingCinematic = null;
                RestoreDebugger();
                sound.PlayMusic(ProcedureTitle.TitleMusic);
                onComplete?.Invoke();
            }))
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
    }

    private void EnterNewTimeline()
    {
        if (worldTimeline != null)
            WorldTimelineDebugView.OpenPreparation?.Invoke(worldTimeline);
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
        GameEntry.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnTimelineFormOpened);
        GameEntry.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnTimelineFormFailed);
        if (openingCinematic != null) openingCinematic.Cancel();
        openingCinematic = null;
        RestoreDebugger();
        if (worldTimeline != null) Destroy(worldTimeline.gameObject);
        worldTimeline = null;
        base.OnClose(isShutdown, userData);
    }
    private void OnLanguageDropdownValueChanged(int newValue)
    {
        var newLanguage = Language.ChineseSimplified;
        switch (newValue)
        {
            case 0:
                newLanguage = Language.ChineseSimplified;
                break;
            case 1:
                newLanguage = Language.English;
                break;
        }

        if (newLanguage == currentLanguage)
        {
            return;
        }
        selectLanguage = newLanguage;
        ConfirmPanelData newConfirmData = ReferencePool.Acquire<ConfirmPanelData>();
        newConfirmData.Content = GameEntry.Localization.GetString(EnumLanguage.ChangeLanguageTip);
        newConfirmData.ShowSingleConfirmBtn = false;
        newConfirmData.ConfirmCallback = OnConfirmChangeLanguageClick;
        newConfirmData.CancelCallback = OnCancelChangeLanguageClick;
        GameEntry.UI.OpenUIForm(UICtrlName.ConfirmPanel, "tips", newConfirmData);
    }

    private void OnScreenResolutionChanged(int newValue)
    {
        selectResolutionIndex = newValue;
        var resolution = UnityExtension.GetResolutionByIndex(selectResolutionIndex);
        if (resolution.HasValue &&selectResolutionIndex != curResolutionIndex)
        {
            curResolutionIndex = selectResolutionIndex;
            GameEntry.Setting.SetInt(ConstValue.SettingKeyScreenResolution, curResolutionIndex);
            GameEntry.Setting.Save();
            Screen.SetResolution(resolution.Value.width, resolution.Value.height, curFullScreenMode);
        }
    }

    private void OnScreenFullModeChanged(int newValue)
    {
        selectFullScreenMode = (FullScreenMode)newValue;
        if (selectFullScreenMode != curFullScreenMode)
        {
            curFullScreenMode = selectFullScreenMode;
            GameEntry.Setting.SetInt(ConstValue.SettingFullScreenMode, (int)selectFullScreenMode);
            GameEntry.Setting.Save();
            var resolution = UnityExtension.GetResolutionByIndex(curResolutionIndex);
            if (resolution.HasValue)
            {
                Screen.SetResolution(resolution.Value.width, resolution.Value.height, curFullScreenMode);
            }
        }
    }
    private void OnConfirmChangeLanguageClick()
    {
        currentLanguage = selectLanguage;
        GameEntry.Setting.SetInt(ConstValue.SettingKeyLanguage, (int)selectLanguage);
        GameEntry.Setting.Save();
        GameEntry.Shutdown(ShutdownType.Restart);
    }
    private void OnCancelChangeLanguageClick()
    {
        selectLanguage = currentLanguage;
        int dropDownValue = 0;
        switch (currentLanguage)
        {
            case Language.ChineseSimplified:
                dropDownValue = 0;
                break;
            case Language.English:
                dropDownValue = 1;
                break;
        }
        mLanguageDropDown.onValueChanged.RemoveAllListeners();
        mLanguageDropDown.value = dropDownValue;
        mLanguageDropDown.onValueChanged.AddListener(OnLanguageDropdownValueChanged);
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
