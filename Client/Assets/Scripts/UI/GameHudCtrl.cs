using System.Collections;
using System.Collections.Generic;
using Procedure;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class GameHudCtrl : UIFormLogic
{
    [SerializeField]
    private Button _btnBag;
    
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnBag.onClick.AddListener(OnClickBagBtn);
    }

    private void OnClickBagBtn()
    {
        Log.Info("OnClickContinueBtn OnClick");
        GameEntry.UI.OpenUIForm(UICtrlName.GameBagPanel, "middle");
    }
}
