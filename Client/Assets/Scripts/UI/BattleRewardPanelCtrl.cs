using System.Collections;
using System.Collections.Generic;
using Procedure;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using SelfEventArg;

public class BattleRewardPanelCtrl : UIFormLogic
{
    [SerializeField]
    private Button _btnContinue;
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnContinue.onClick.AddListener(OnClickContinueBtn);
    }

    private void OnClickContinueBtn()
    {
        GameEntry.Event.Fire(this,PassPointEventArgs.Create());
        GameEntry.UI.CloseUIForm(UIForm);
    }
}
