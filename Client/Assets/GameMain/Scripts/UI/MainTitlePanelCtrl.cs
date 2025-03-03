using System.Collections;
using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameFramework.Localization;
using Procedure;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class MainTitlePanelCtrl : UIFormLogic
{
    [SerializeField]
    private Button _btnContinue;
    [SerializeField]
    private Button _btnStart;
    [SerializeField]
    private Button _btnSetting;

    [SerializeField] private Transform mTransSetting;
    [SerializeField] private Slider mVolumeMasterSlider;
    [SerializeField] private Slider mVolumeBgmSlider;
    [SerializeField] private Slider mVolumeSfxSlider;
    [SerializeField] private Slider mVolumeUISlider;
    [SerializeField] private TMP_Dropdown mLanguageDropDown;
    private Language currentLanguage;
    private Language selectLanguage;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnContinue.onClick.AddListener(OnClickContinueBtn);
        _btnStart.onClick.AddListener(OnClickStartBtn);
        _btnSetting.onClick.AddListener(OnClickSettingBtn);
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
        mVolumeMasterSlider.value = GameEntry.Sound.GetMasterVolume();
        mVolumeSfxSlider.value = GameEntry.Sound.GetVolume("Sfx");
        mVolumeUISlider.value = GameEntry.Sound.GetVolume("UI");
        mVolumeBgmSlider.value = GameEntry.Sound.GetVolume("BGM");
        mVolumeMasterSlider.onValueChanged.AddListener(OnMasterVolumeSliderChange);
        mVolumeBgmSlider.onValueChanged.AddListener(OnBgmVolumeSliderChange);
        mVolumeSfxSlider.onValueChanged.AddListener(OnSFXVolumeSliderChange);
        mVolumeUISlider.onValueChanged.AddListener(OnMusicVolumeSliderChange);
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        var hasData = GameEntry.HeroManager.HasSaveData();
        _btnContinue.gameObject.SetActive(hasData);
        GameEntry.UI.CloseUIForm(UICtrlName.LoadingPanel);
        mTransSetting.gameObject.SetActive(false);
    }

    private void OnClickContinueBtn()
    {
        Log.Info("OnClickContinueBtn OnClick");
        GameEntry.Sound.PlayUISound((int)EnumSound.BtnSfx);
        var titleProcedure = GameEntry.Procedure.GetProcedure<ProcedureTitle>() as ProcedureTitle;
        titleProcedure?.MoveToContinueGame();
    }
    private void OnClickStartBtn()
    {
        GameEntry.Sound.PlayUISound((int)EnumSound.BtnSfx);
         var titleProcedure = GameEntry.Procedure.GetProcedure<ProcedureTitle>() as ProcedureTitle;
         titleProcedure?.MoveToGame();
    }
    private void OnClickSettingBtn()
    {
        GameEntry.Sound.PlayUISound((int)EnumSound.BtnSfx);
        mTransSetting.gameObject.SetActive(!mTransSetting.gameObject.activeSelf);
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
        GameEntry.UI.OpenUIForm(UICtrlName.ConfirmPanel, "middle",newConfirmData);
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
        GameEntry.Sound.SetMasterVolume(value);
    }
    private void OnBgmVolumeSliderChange(float value)
    {
        GameEntry.Sound.SetVolume("BGM", value);
    }
    private void OnSFXVolumeSliderChange(float value)
    {
        GameEntry.Sound.SetVolume("Sfx", value);
    }

    private void OnMusicVolumeSliderChange(float value)
    {
        GameEntry.Sound.SetVolume("UI", value);
    }
}
