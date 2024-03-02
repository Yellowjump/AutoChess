using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class UIguanli : UIFormLogic
{


    [SerializeField]
    private Button _btnShuaxin;

    [SerializeField]
    private Button _btnShouqi;

    [SerializeField]
    private Canvas _cavJiemian;
    protected override void OnInit(object userData)
    {
        transform.position = Vector3.zero;
        _btnShuaxin.onClick.AddListener(OnClickBtnShuaxin);
        _btnShouqi.onClick.AddListener(OnClickBtnShouqi);
        base.OnInit(userData);
    }

    private void OnClickBtnShouqi()
    {
        _cavJiemian.transform.SetPositionY(-_cavJiemian.transform.position.y);
        Log.Info("TestBtn OnClick");
    }
    private void OnClickBtnShuaxin()
    {
        Log.Info("TestBtn OnClick");
    }
}
