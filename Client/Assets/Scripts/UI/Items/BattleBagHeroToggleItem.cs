using System;
using DataTable;
using GameFramework.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class BattleBagHeroToggleItem:MonoBehaviour
{
    public Button BtnClick;
    public string HeroName;
    public TextMeshProUGUI TmpHeroName;
    public int HeroUID;
    public Action<BattleBagHeroToggleItem> OnClickPointCallback;
    public void Init()
    {
        BtnClick.onClick.AddListener(OnClickBtn);
    }

    public void Fresh()
    {
        TmpHeroName.text = HeroName;
    }
    private void OnClickBtn()
    {
        OnClickPointCallback?.Invoke(this);
    }
}