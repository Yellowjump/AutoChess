using System;
using DataTable;
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
    [SerializeField] private RectTransform paperFrame;
    private Action _confirmCallback;
    private Action _cancelCallback;
    private CanvasGroup fade;
    private Vector2 restingPosition;
    private float elapsed;
    private bool closing;
    private Action closeCallback;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        fade = gameObject.AddComponent<CanvasGroup>();
        restingPosition = paperFrame.anchoredPosition;
        _btnConfirm.onClick.AddListener(OnClickConfirmBtn);
        _btnConfirmSingle.onClick.AddListener(OnClickConfirmBtn);
        _btnCancel.onClick.AddListener(OnClickCancelBtn);
    }

    public override void RefreshLocalizedText()
    {
        base.RefreshLocalizedText();
        RefreshPaperTypography(transform);
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        elapsed = 0f;
        closing = false;
        fade.alpha = 0f;
        fade.interactable = false;
        paperFrame.anchoredPosition = restingPosition + Vector2.down * 8f;
        HotGameEntry.Sound.PlayUISound((int)EnumSound.ConfirmTipShow);
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
        BeginClose(_confirmCallback);
    }
    private void OnClickCancelBtn()
    {
        BeginClose(_cancelCallback);
    }

    private void BeginClose(Action callback)
    {
        if (closing || !Visible) return;
        closing = true;
        closeCallback = callback;
        elapsed = (1f - fade.alpha) * 0.10f;
        fade.interactable = false;
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        elapsed += realElapseSeconds;
        if (closing)
        {
            fade.alpha = 1f - Mathf.Clamp01(elapsed / 0.10f);
            if (fade.alpha > 0f) return;

            var callback = closeCallback;
            Close();
            callback?.Invoke();
            return;
        }

        if (fade.interactable) return;
        float progress = Mathf.Clamp01(elapsed / 0.15f);
        fade.alpha = progress;
        float remaining = 1f - progress;
        paperFrame.anchoredPosition = restingPosition + Vector2.down * (8f * remaining * remaining);
        fade.interactable = progress >= 1f;
    }

    public override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        _confirmCallback = null;
        _cancelCallback = null;
        closeCallback = null;
    }
}
