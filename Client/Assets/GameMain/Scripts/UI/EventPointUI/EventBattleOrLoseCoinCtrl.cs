using System.Collections;
using System.Collections.Generic;
using Procedure;
using SelfEventArg;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class EventBattleOrLoseCoinCtrl : UIFormLogic
{
    [SerializeField]
    private Button _btnLoseCoin;
    [SerializeField]
    private Button _btnBattle;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnLoseCoin.onClick.AddListener(OnClickBtnLoseCoin);
        _btnBattle.onClick.AddListener(OnClickBtnStartBattle);
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
    }

    private void OnClickBtnLoseCoin()
    {
        SelfDataManager.Instance.TryAddCoin(-30);
        GameEntry.Event.Fire(this,EventCompleteToMapEventArg.Create());
    }
    private void OnClickBtnStartBattle()
    {
        GameEntry.Event.Fire(this,EventChangeToBattleEventArg.Create(1001));
    }
}
