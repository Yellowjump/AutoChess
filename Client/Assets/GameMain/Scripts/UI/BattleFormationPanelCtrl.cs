using System.Collections;
using System.Collections.Generic;
using Procedure;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using SelfEventArg;

public class BattleFormationPanelCtrl : UIFormLogic
{
    [SerializeField]
    private Button _btnContinue;

    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnContinue.onClick.AddListener(OnClickContinueBtn);
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        _btnContinue.gameObject.SetActive(true);
    }

    public override void OnReveal()
    {
        base.OnReveal();
        _btnContinue.gameObject.SetActive(true);
    }

    public override void OnCover()
    {
        base.OnCover();
        _btnContinue.gameObject.SetActive(false);
    }

    private void OnClickContinueBtn()
    {
        GameEntry.Event.Fire(this,FormationToBattleEventArgs.Create());
    }
}
