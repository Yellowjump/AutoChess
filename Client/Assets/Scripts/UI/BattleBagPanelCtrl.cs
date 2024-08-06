using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DataTable;
using Procedure;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using SelfEventArg;
using TMPro;
using UnityEngine.Pool;
using UnityEngine.Serialization;

public class BattleBagPanelCtrl : UIFormLogic
{
    [SerializeField]
    private Button _btnContinue;
    [SerializeField]
    private Button _btnCraft;
    [SerializeField]
    private BattleBagItem _craftItem;
    [SerializeField] private Transform _craftParent;
    [SerializeField] private TextMeshProUGUI _craftItemName;
    [SerializeField] private TextMeshProUGUI _craftItemDesc;
    [SerializeField] private Transform _joinCraftItemParent;
    [SerializeField]
    private BattleBagItem _bagItemTemp;
    [FormerlySerializedAs("_itemParent")] [SerializeField] private Transform _bagItemParent;
    
    private ObjectPool<BattleBagItem> _itemPool;
    [SerializeField]
    private Transform _releaseItemPa;
    private List<BattleBagItem> _curShowBagItemList = new();
    private List<BattleBagItem> _joinCraftItemList = new();
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnContinue.onClick.AddListener(OnClickContinueBtn);
        _btnCraft.onClick.AddListener(OnClickCraftBtn);
        _itemPool ??= new ObjectPool<BattleBagItem>(() =>
        {
            GameObject ob = Instantiate(_bagItemTemp.gameObject, _releaseItemPa);
            BattleBagItem ri = ob.GetComponent<BattleBagItem>();
            if (ri != null)
            {
                ri.OnClickPointCallback = OnClickItem;
                ri.Init();
            }
            return ri;
        }, (item) => {item.gameObject.SetActive(false);}, (item) => {item.gameObject.SetActive(false);item.transform.SetParent(_releaseItemPa);}, (item) => { Destroy(item.gameObject); });
        _craftItem.Init();
        _craftItem.CurItemType = BattleBagItem.ItemType.CraftResult;
        _craftItem.OnClickPointCallback = OnClickItem;
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        ShowBagItem();
    }

    private void ShowBagItem()
    {
        _btnCraft.interactable = false;
        _craftItem.ItemID = 0;
        _craftParent.gameObject.SetActive(false);
        var bagDic = SelfDataManager.Instance.ItemBag;
        
        foreach (var keyValue in bagDic.OrderBy((ky)=>ky.Key))
        {
            var oneItem = _itemPool.Get();
            oneItem.transform.SetParent(_bagItemParent);
            oneItem.ItemID =keyValue.Key;
            oneItem.itemNum = keyValue.Value;
            oneItem.CurItemType = BattleBagItem.ItemType.Bag;
            oneItem.Fresh();
            _curShowBagItemList.Add(oneItem);
        }
    }
    public override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        ClearAllCurItem();
    }

    private void ClearAllCurItem()
    {
        foreach (var item in _curShowBagItemList)
        {
            _itemPool?.Release(item);
        }
        _curShowBagItemList.Clear();
        foreach (var item in _joinCraftItemList)
        {
            _itemPool?.Release(item);
        }
        _joinCraftItemList.Clear();
    }
    private void OnClickItem(BattleBagItem battleBagItem)
    {
        if (battleBagItem.CurItemType == BattleBagItem.ItemType.Bag)
        {
            OnClickBagItem(battleBagItem);
        }
        else if (battleBagItem.CurItemType == BattleBagItem.ItemType.InJoinCraft)
        {
            OnClickJoinCraftItem(battleBagItem);
        }
        OnJoinCraftItemChanged();
    }

    private void OnClickBagItem(BattleBagItem battleBagItem)
    {
        var itemID = battleBagItem.ItemID;
        var itemBagNum = battleBagItem.itemNum;
        var joinItem = _joinCraftItemList.FindLast((item) => item.ItemID == itemID);
        if (joinItem == null)
        {
            joinItem = _itemPool.Get();
            joinItem.CurItemType = BattleBagItem.ItemType.InJoinCraft;
            joinItem.transform.SetParent(_joinCraftItemParent);
            joinItem.ItemID =itemID;
            joinItem.itemNum = 1;
            joinItem.Fresh();
            _joinCraftItemList.Add(joinItem);
        }
        else
        {
            joinItem.itemNum++;
            joinItem.FreshNum();
        }

        if (itemBagNum <= 1)
        {
            _curShowBagItemList.Remove(battleBagItem);
            _itemPool.Release(battleBagItem);
        }
        else
        {
            battleBagItem.itemNum--;
            battleBagItem.FreshNum();
        }
    }
    private void OnClickJoinCraftItem(BattleBagItem craftItem)
    {
        var itemID = craftItem.ItemID;
        
        if(craftItem.itemNum<=1)
        {
            _joinCraftItemList.Remove(craftItem);
            _itemPool.Release(craftItem);
        }
        else
        {
            craftItem.itemNum--;
            craftItem.FreshNum();
        }
        var bagItem = _curShowBagItemList.FindLast((item) => item.ItemID == itemID);
        if (bagItem == null)
        {
            bagItem = _itemPool.Get();
            bagItem.CurItemType = BattleBagItem.ItemType.Bag;
            bagItem.transform.SetParent(_bagItemParent);
            bagItem.ItemID =itemID;
            bagItem.itemNum = 1;
            bagItem.Fresh();
            _curShowBagItemList.Add(bagItem);
        }
        else
        {
            bagItem.itemNum++;
            bagItem.FreshNum();
        }
    }

    private void OnJoinCraftItemChanged()
    {
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        int matchID = -1;
        foreach (var oneItem in itemTable)
        {
            if (oneItem.CraftList.Count != 0)
            {
                if (MeetCraftList(oneItem.CraftList))
                {
                    matchID = oneItem.Id;
                }
            }
        }

        if (matchID != -1)
        {
            if (matchID != _craftItem.ItemID)
            {
                _craftItem.ItemID = matchID;
                _craftItemName.text = itemTable[matchID].Name;
                _craftItemDesc.text = itemTable[matchID].Name;
                _craftItem.Fresh();
                _craftParent.gameObject.SetActive(true);
                _btnCraft.interactable = true;
            }
        }
        else
        {
            _craftItem.ItemID = 0;
            _craftParent.gameObject.SetActive(false);
            _btnCraft.interactable = false;
        }
        
    }

    private bool MeetCraftList(List<(int, int)> needItem)
    {
        if (_joinCraftItemList == null || needItem == null || needItem.Count==0|| _joinCraftItemList.Count != needItem.Count)
        {
            return false;
        }

        foreach (var idAndNum in needItem)
        {
            if (_joinCraftItemList.Exists((item) => item.ItemID == idAndNum.Item1 && item.itemNum == idAndNum.Item2) == false)
            {
                return false;
            }
        }
        return true;
    }
    private void OnClickContinueBtn()
    {
        GameEntry.UI.CloseUIForm(UIForm);
    }
    private void OnClickCraftBtn()
    {
        if (_craftItem != null && _craftItem.ItemID != 0)
        {
            if (SelfDataManager.Instance.TryCraftItem(_craftItem.ItemID))
            {
                //打开 tip 界面通知获取物品（要不要做呢）
                
                //刷新界面
                FreshItem();
            }
        }
    }

    public void FreshItem()
    {
        ClearAllCurItem();
        ShowBagItem();
    }
}
