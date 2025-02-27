using System;
using DataTable;
using GameFramework.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class BattleRewardItem:MonoBehaviour
{
    public BattleBagItem item;
    public TextMeshProUGUI Rarity;
    public TextMeshProUGUI Desc;
    public TextMeshProUGUI Name;
    public Button BtnClick;
    public int ItemID;
    public Action<BattleRewardItem> OnClickPointCallback;
    public void Init()
    {
        BtnClick.onClick.AddListener(OnClickBtn);
        item.Init();
    }

    public void Fresh()
    {
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        if (!itemTable.HasDataRow(ItemID))
        {
            Log.Error($"Item Table not Contain {ItemID}");
            return;
        }

        item.ItemID = ItemID;
        item.Fresh();
        var itemData = itemTable[ItemID];
        Name.text = itemData.Name;
        Desc.text = itemData.Decs;
        Rarity.color = ConstValue.RarityColorList[itemData.Rarity];
        Rarity.text = ConstValue.RarityNameList[itemData.Rarity];
    }
    private void OnClickBtn()
    {
        OnClickPointCallback.Invoke(this);
    }

    public void OnRelease()
    {
        item.OnRelease();
    }
}