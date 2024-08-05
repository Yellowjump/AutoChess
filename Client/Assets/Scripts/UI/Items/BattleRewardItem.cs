using System;
using DataTable;
using GameFramework.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class BattleRewardItem:MonoBehaviour
{
    public Image Icon;
    public TextMeshProUGUI Name;
    public Button BtnClick;
    public int ItemID;
    public Action<BattleRewardItem> OnClickPointCallback;
    private LoadAssetCallbacks _loadIconCallback;
    public void Init()
    {
        BtnClick.onClick.AddListener(OnClickBtn);
        _loadIconCallback = new LoadAssetCallbacks(OnIconLoadSuccessCallback);
    }

    public void Fresh()
    {
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        if (!itemTable.HasDataRow(ItemID))
        {
            Log.Error($"Item Table not Contain {ItemID}");
            return;
        }

        var itemData = itemTable[ItemID];
        Name.text = itemData.Name;
        GameEntry.Resource.LoadAsset("Assets/Image/Icons/ItemIcon/axe.png",typeof(Sprite),_loadIconCallback);
    }

    private void OnIconLoadSuccessCallback(string assetName, object asset, float duration, object userData)
    {
        Sprite sp = asset as Sprite;
        if (sp != null)
        {
            Icon.sprite = sp;
        }
        gameObject.SetActive(true);
    }
    private void OnClickBtn()
    {
        OnClickPointCallback.Invoke(this);
    }
}