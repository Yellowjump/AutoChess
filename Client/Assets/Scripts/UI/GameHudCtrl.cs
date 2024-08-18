using System.Collections;
using System.Collections.Generic;
using Procedure;
using SelfEventArg;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class GameHudCtrl : UIFormLogic
{
    [SerializeField]
    private Button _btnBag;
    [SerializeField]
    private Button _btnHeroEquip;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnBag.onClick.AddListener(OnClickBagBtn);
        _btnHeroEquip.onClick.AddListener(OnClickEquipBtn);
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
}
