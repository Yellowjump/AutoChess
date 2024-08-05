using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] private Transform _itemParent;
    
    private ObjectPool<BattleBagItem> _rewardItemPool;
    private List<BattleBagItem> _curShowItemList = new() ;
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnContinue.onClick.AddListener(OnClickContinueBtn);
        _rewardItemPool ??= new ObjectPool<BattleBagItem>(() =>
        {
            GameObject ob = Instantiate(_bagItemTemp.gameObject, _itemParent);
            BattleBagItem ri = ob.GetComponent<BattleBagItem>();
            if (ri != null)
            {
                ri.OnClickPointCallback = OnClickRewardItem;
                ri.Init();
            }
            return ri;
        }, (item) => {item.gameObject.SetActive(false);}, (item) => {item.gameObject.SetActive(false);}, (item) => { Destroy(item.gameObject); });
    }

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        var bagDic = SelfDataManager.Instance.ItemBag;
        
        foreach (var keyValue in bagDic.OrderBy((ky)=>ky.Key))
        {
            var oneItem = _rewardItemPool.Get();
            oneItem.ItemID =keyValue.Key;
            oneItem.itemNum = keyValue.Value;
            oneItem.Fresh();
            _curShowItemList.Add(oneItem);
        }
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        foreach (var item in _curShowItemList)
        {
            _rewardItemPool?.Release(item);
        }
        _curShowItemList.Clear();
    }

    private void OnClickRewardItem(BattleBagItem battleRewardItem)
    {
        
    }
    private void OnClickContinueBtn()
    {
        GameEntry.UI.CloseUIForm(UIForm);
    }
}
