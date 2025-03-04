using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameMain.Scripts.UI.Items;
using SelfEventArg;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class EventStoreCtrl : UIFormLogic
{
    [SerializeField]
    private Button _btnLoseLevel;
    [SerializeField]
    private StoreItem _storeItemTemplete;
    [SerializeField] private Transform _storeItemParent;
    [SerializeField]
    private Transform _releaseItemPa;
    private ObjectPool<StoreItem> _itemPool;
    private List<StoreItem> _curStoreItemList = new();
    [SerializeField]
    private ItemTip _itemTip;
    private const int _tipPosOffsetX = 250;
    private const int _tipPosOffsetY = 150;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnLoseLevel.onClick.AddListener(OnClickBtnLevel);
        _itemPool ??= new ObjectPool<StoreItem>(() =>
            {
                GameObject ob = Instantiate(_storeItemTemplete.gameObject, _releaseItemPa);
                StoreItem ri = ob.GetComponent<StoreItem>();
                if (ri != null)
                {
                    ri.OnClickPointCallback = OnClickItem;
                    ri.OnPointEnterCallback = OnPointItemEnter;
                    ri.OnPointExitCallback = OnPointItemExit;
                    ri.Init();
                }
                return ri;
            }, (item) => {item.gameObject.SetActive(false);}, (item) => {item.gameObject.SetActive(false);item.OnRelease();item.transform.SetParent(_releaseItemPa);},
            (item) => { Destroy(item.gameObject); });
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        _itemTip.gameObject.SetActive(false);
        // 把item表里的全放进来 暂时，后续 可以从 关卡奖励积累 的 获取
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        //从所有Item中随机选择12个
        List<DRItem> itemList = ListPool<DRItem>.Get();
        itemList.AddRange(itemTable.GetAllDataRows());
        Utility.Shuffle(itemList);
        for (int i = 0; i < ConstValue.StoreItemNum; i++)
        {
            var oneItemData = itemList[i];
            var oneItem = _itemPool.Get();
            _curStoreItemList.Add(oneItem);
            oneItem.transform.SetParent(_storeItemParent);
            oneItem.ItemID =oneItemData.Id;
            oneItem.Fresh();
        }
    }

    public override void OnClose(bool isShutdown, object userData)
    {
        foreach (var oneItem in _curStoreItemList)
        {
            _itemPool.Release(oneItem);
        }
        _curStoreItemList.Clear();
        base.OnClose(isShutdown, userData);
    }

    private void OnClickBtnLevel()
    {
        GameEntry.Event.Fire(this,EventCompleteToMapEventArg.Create());
    }
    private void OnClickItem(StoreItem storeItem)
    {
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        if (itemTable.HasDataRow(storeItem.ItemID))
        {
            var itemData = itemTable[storeItem.ItemID];
            if (SelfDataManager.Instance.CoinNum < itemData.StoreCoin)
            {
                // Coin 不足
                return;
            }

            storeItem.SetBtnInteractFalse();
            SelfDataManager.Instance.TryAddCoin(-itemData.StoreCoin);
            SelfDataManager.Instance.AddOneItem(storeItem.ItemID,1);
        }
    }
    private void OnPointItemEnter(StoreItem battleBagItem)
    {
        _itemTip.ItemID = battleBagItem.ItemID;
        _itemTip.gameObject.SetActive(true);
        var xOffset = battleBagItem.transform.position.x > 0 ? -_tipPosOffsetX : _tipPosOffsetX;
        var yOffset = battleBagItem.transform.position.y > 0 ? -_tipPosOffsetY : _tipPosOffsetY;
        _itemTip.gameObject.transform.position = battleBagItem.transform.position + new Vector3(xOffset,yOffset,0);
        _itemTip.FreshTip();
    }

    private void OnPointItemExit(StoreItem battleBagItem)
    {
        _itemTip.gameObject.SetActive(false);
    }
}
