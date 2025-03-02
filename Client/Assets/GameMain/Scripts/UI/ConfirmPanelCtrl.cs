using System;
using GameFramework;
using Procedure;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class ConfirmPanelData:IReference
{
    public Action ConfirmCallback;
    public Action CancelCallback;
    public string Content;
    public bool ShowSingleConfirmBtn;
    public void Clear()
    {
        ConfirmCallback = null;
        CancelCallback = null;
        Content = string.Empty;
        ShowSingleConfirmBtn = false;
    }
}
public class ConfirmPanelCtrl : UIFormLogic
{
    [SerializeField] private Button _btnConfirm;
    [SerializeField] private Button _btnCancel;
    [SerializeField] private Button _btnConfirmSingle;
    [SerializeField] private TextMeshProUGUI _txtContent;
    private Action _confirmCallback;
    private Action _cancelCallback;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnConfirm.onClick.AddListener(OnClickConfirmBtn);
        _btnConfirmSingle.onClick.AddListener(OnClickConfirmBtn);
        _btnCancel.onClick.AddListener(OnClickCancelBtn);
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        if (userData is ConfirmPanelData confirmData)
        {
            _confirmCallback = confirmData.ConfirmCallback;
            _cancelCallback = confirmData.CancelCallback;
            _txtContent.text = confirmData.Content;
            _btnConfirm.gameObject.SetActive(!confirmData.ShowSingleConfirmBtn);
            _btnCancel.gameObject.SetActive(!confirmData.ShowSingleConfirmBtn);
            _btnConfirmSingle.gameObject.SetActive(confirmData.ShowSingleConfirmBtn);
            ReferencePool.Release(confirmData);
        }
        else
        {
            Log.Error("Open ConfirmPanel need ConfirmPanelData");
        }
    }

    private void OnClickConfirmBtn()
    {
        _confirmCallback?.Invoke();
        Close();
    }
    private void OnClickCancelBtn()
    {
        _cancelCallback?.Invoke();
        Close();
    }

    public override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        _confirmCallback = null;
        _cancelCallback = null;
    }
}
