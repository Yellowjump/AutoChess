using System.Collections;
using System.Collections.Generic;
using Procedure;
using UnityEngine;
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
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnContinue.onClick.AddListener(OnClickContinueBtn);
        _btnStart.onClick.AddListener(OnClickStartBtn);
        _btnSetting.onClick.AddListener(OnClickSettingBtn);
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        var hasData = GameEntry.HeroManager.HasSaveData();
        _btnContinue.gameObject.SetActive(hasData);
        GameEntry.UI.CloseUIForm(UICtrlName.LoadingPanel);
    }

    private void OnClickContinueBtn()
    {
        Log.Info("OnClickContinueBtn OnClick");
        var titleProcedure = GameEntry.Procedure.GetProcedure<ProcedureTitle>() as ProcedureTitle;
        titleProcedure?.MoveToContinueGame();
    }
    private void OnClickStartBtn()
    {
         var titleProcedure = GameEntry.Procedure.GetProcedure<ProcedureTitle>() as ProcedureTitle;
         titleProcedure?.MoveToGame();
    }
    private void OnClickSettingBtn()
    {
        Log.Info("OnClickSettingBtn OnClick");
    }
}
