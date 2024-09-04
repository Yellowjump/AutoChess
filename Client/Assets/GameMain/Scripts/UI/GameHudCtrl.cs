using System.Collections;
using System.Collections.Generic;
using GameFramework.Event;
using Procedure;
using Procedure.GameStates;
using SelfEventArg;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class GameHudCtrl : UIFormLogic
{
    [SerializeField]
    private TextMeshProUGUI CoinText;
    [SerializeField]
    private Button _btnBag;
    [SerializeField]
    private Button _btnHeroEquip;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnBag.onClick.AddListener(OnClickBagBtn);
        _btnHeroEquip.onClick.AddListener(OnClickEquipBtn);
        GameEntry.Event.Subscribe(FreshCoinNumArg.EventId,OnFreshCoinNum);
    }

    private void OnClickBagBtn()
    {
        Log.Info("OnClickContinueBtn OnClick");
        var form = GameEntry.UI.GetUIForm(UICtrlName.GameBagPanel);
        if (form == null)
        {
            GameEntry.UI.OpenUIForm(UICtrlName.GameBagPanel, "middle",BattleBagPanelCtrl.BagPanelShowState.Craft);
        }
        else
        {
            GameEntry.Event.Fire(this, BagPanelCheckToCraftEventArgs.Create());
        }
    }
    private void OnClickEquipBtn()
    {
        var form = GameEntry.UI.GetUIForm(UICtrlName.GameBagPanel);
        if (form == null)
        {
            GameEntry.UI.OpenUIForm(UICtrlName.GameBagPanel, "middle",BattleBagPanelCtrl.BagPanelShowState.HeroEquip);
        }
        else
        {
            GameEntry.Event.Fire(this, BagPanelCheckToEquipEventArgs.Create());
        }
    }
    private void OnFreshCoinNum(object sender, GameEventArgs e)
    {
        FreshCoinNumArg ee = (FreshCoinNumArg)e;
        if (ee == null)
        {
            return;
        }
        CoinText.text = SelfDataManager.Instance.CoinNum.ToString();
    }
}
