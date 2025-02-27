using System.Collections;
using System.Collections.Generic;
using DataTable;
using GameFramework;
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
    public override void OnInit(object userData)
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
        }, (item) => {item.gameObject.SetActive(true);}, (item) => {item.OnRelease();item.gameObject.SetActive(false);}, (item) => { Destroy(item.gameObject); });
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        _btnContinue.gameObject.SetActive(false);
        _itemParent.gameObject.SetActive(true);
        List<int> rewardItemIDList = ListPool<int>.Get();
        var levelID = SelfDataManager.Instance.CurAreaPoint.CurLevelID;
        var levelConfigTable = GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
        if (!levelConfigTable.HasDataRow(levelID))
        {
            Log.Error($"level Table not Contain {levelID}");
            return;
        }

        var rewardID = levelConfigTable[levelID].RewardID;
        var rewardConfigTable = GameEntry.DataTable.GetDataTable<DRRewardConfig>("RewardConfig");
        if (!rewardConfigTable.HasDataRow(rewardID))
        {
            Log.Error($"rewardConfigTable Table not Contain {rewardID}");
            return;
        }
        var itemAndWeightList = rewardConfigTable[rewardID].RewardItemAndRandomWeight;
        if (itemAndWeightList.Count < 3)
        {
            Log.Error($"rewardConfigTable ID:{rewardID} Has No Num Reward");
            return;
        }
        List<int> idList = ListPool<int>.Get();
        List<int> weightList = ListPool<int>.Get();
        foreach (var idAndWeight in itemAndWeightList)
        {
            idList.Add(idAndWeight.Item1);
            weightList.Add(idAndWeight.Item2);
        }
        Utility.ShuffleWithWeight(idList,weightList);
        
        rewardItemIDList.Add(idList[0]);
        rewardItemIDList.Add(idList[1]);
        rewardItemIDList.Add(idList[2]);
        for (int itemIndex = 0; itemIndex < rewardItemIDList.Count; itemIndex++)
        {
            var oneItem = _rewardItemPool.Get();
            oneItem.ItemID = rewardItemIDList[itemIndex];
            oneItem.Fresh();
            _curShowItemList.Add(oneItem);
        }
        ListPool<int>.Release(rewardItemIDList);
        ListPool<int>.Release(idList);
        ListPool<int>.Release(weightList);
    }

    public override void OnClose(bool isShutdown, object userData)
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
        SelfDataManager.Instance.AddOneItem(battleRewardItem.ItemID, 1);
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
