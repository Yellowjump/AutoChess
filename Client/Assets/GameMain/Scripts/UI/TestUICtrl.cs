using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class TestUICtrl : UIFormLogic
{
    [SerializeField]
    private Button _btnTest;

    public override void OnInit(object userData)
    {
        _btnTest.onClick.AddListener(OnClickTestBtn);
        base.OnInit(userData);
    }

    private void OnClickTestBtn()
    {
        Log.Info("TestBtn OnClick");
    }
}
