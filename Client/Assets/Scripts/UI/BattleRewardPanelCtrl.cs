using System.Collections;
using System.Collections.Generic;
using Procedure;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using SelfEventArg;
using UnityEngine.Pool;

public class BattleRewardPanelCtrl : UIFormLogic
{
    [SerializeField]
    private Button _btnContinue;
    [SerializeField]
    private BattleRewardItem _rewardItemTemp;
    [SerializeField] private Transform _itemParent;
    
    private ObjectPool<BattleRewardItem> _rewardItemPool;
    private List<BattleRewardItem> _curShowItemList = new() ;
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnContinue.onClick.AddListener(OnClickContinueBtn);
        _rewardItemPool ??= new ObjectPool<BattleRewardItem>(() =>
        {
            GameObject ob = Instantiate(_rewardItemTemp.gameObject, _itemParent);
            BattleRewardItem ri = ob.GetComponent<BattleRewardItem>();
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
        _btnContinue.gameObject.SetActive(false);
        _itemParent.gameObject.SetActive(true);
        List<int> rewardItemIDList = ListPool<int>.Get();
        rewardItemIDList.Add(1);
        rewardItemIDList.Add(2);
        rewardItemIDList.Add(3);
        for (int itemIndex = 0; itemIndex < rewardItemIDList.Count; itemIndex++)
        {
            var oneItem = _rewardItemPool.Get();
            oneItem.ItemID = rewardItemIDList[itemIndex];
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

    private void OnClickRewardItem(BattleRewardItem battleRewardItem)
    {
        var containItem = SelfDataManager.Instance.ItemBag.ContainsKey(battleRewardItem.ItemID);
        if (!containItem)
        {
            SelfDataManager.Instance.ItemBag.Add(battleRewardItem.ItemID,1);
        }
        else
        {
            SelfDataManager.Instance.ItemBag[battleRewardItem.ItemID]++;
        }
        //关闭奖励选项
        _itemParent.gameObject.SetActive(false);
        _btnContinue.gameObject.SetActive(true);
    }
    private void OnClickContinueBtn()
    {
        GameEntry.Event.Fire(this,PassPointEventArgs.Create());
        GameEntry.UI.CloseUIForm(UIForm);
    }
}
