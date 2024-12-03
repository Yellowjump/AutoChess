using System;
using System.Collections.Generic;
using DataTable;
using Entity;
using GameFramework;
using GameFramework.Event;
using Maze;
using SelfEventArg;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

public class SelfDataManager
{
    private static SelfDataManager _instance;
    public static SelfDataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new SelfDataManager();
                _instance.Init();
            }
            return _instance;
        }
    }
    public List<AreaPoint> CurAreaList;
    public AreaPoint CurAreaPoint;
    public int CoinNum;
    public List<EntityQizi> SelfHeroList = new List<EntityQizi>();
    public Dictionary<int, int> ItemBag = new Dictionary<int, int>();

    private void Init()
    {
        GameEntry.Event.Subscribe(CMDGetItemEventArgs.EventId,OnCMDGetItem);
    }

    public void InitDataFormData(HeroComponent.SaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        var levelConfigTable = GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
        CurAreaList ??= new List<AreaPoint>();
        CurAreaList.Clear();
        foreach (var onePointData in saveData.MazeData)
        {
            var newPoint = new AreaPoint(onePointData.pos);
            newPoint.CanSee = onePointData.CanSee;
            if (levelConfigTable.HasDataRow(onePointData.levelID))
            {
                var levelData = levelConfigTable[onePointData.levelID];
                newPoint.CurType = (MazePointType)levelData.MazePointType;
            }
            else
            {
                Log.Error($"No LevelConfig ID:{onePointData.levelID}");
            }

            newPoint.CurPassState = onePointData.state;
            newPoint.CurLevelID = onePointData.levelID;
            CurAreaList.Add(newPoint);
        }

        foreach (var onePointData in saveData.MazeData)
        {
            var curPoint = GetPoint(onePointData.pos.x, onePointData.pos.y);
            foreach (var linkPos in onePointData.linkPos)
            {
                var linkPoint = GetPoint(linkPos.x, linkPos.y);
                curPoint.LinkPointObsolete.Add(linkPoint);
            }
        }
        ItemBag.Clear();
        foreach (var oneItemData in saveData.Bag)
        {
            ItemBag.Add(oneItemData.itemID,oneItemData.count);
        }

        foreach (var oneHeroData in saveData.HeroList)
        {
            var newFriendHero = GameEntry.HeroManager.AddNewFriendHero(oneHeroData.heroID, oneHeroData.pos.y, oneHeroData.pos.x);
            newFriendHero.EquipItemList.AddRange(oneHeroData.equipItem);
            SelfHeroList.Add(newFriendHero);
        }
        CoinNum = saveData.CoinNum;
    }
    private void OnCMDGetItem(object sender, GameEventArgs e)
    {
        CMDGetItemEventArgs ne = (CMDGetItemEventArgs)e;
        if (ne == null)
        {
            return;
        }
        AddOneItem(ne.ItemID, ne.ItemNum);
    }

    public void AddOneItem(int id,int changeNum)
    {
        var containItem = ItemBag.ContainsKey(id);
        if (!containItem)
        {
            if (changeNum > 0)
            {
                ItemBag.Add(id,changeNum);
            }
        }
        else
        {
            var curNum = ItemBag[id];
            if (curNum + changeNum <= 0)
            {
                ItemBag.Remove(id);
            }
            else
            {
                ItemBag[id] = curNum + changeNum;
            }
        }
    }

    public bool TryCraftItem(int itemID)
    {
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        if (itemTable.HasDataRow(itemID))
        {
            var itemData = itemTable[itemID];
            var needItemList = itemData.CraftList;
            if (MeetCraftItemNeed(needItemList))
            {
                foreach (var idAndNum in needItemList)
                {
                    AddOneItem(idAndNum.Item1,-idAndNum.Item2);
                }
                AddOneItem(itemID,1);
                return true;
            }
        }
        return false;
    }

    private bool MeetCraftItemNeed(List<(int, int)> needItem)
    {
        if (ItemBag == null || needItem == null || needItem.Count==0)
        {
            return false;
        }

        foreach (var idAndNum in needItem)
        {
            if ((ItemBag.ContainsKey(idAndNum.Item1) &&ItemBag[idAndNum.Item1] >= idAndNum.Item2) == false)
            {
                return false;
            }
        }
        return true;
    }

    public bool TryEquipItem(int heroUID,int itemID)
    {
        if (!ItemBag.ContainsKey(itemID))
        {
            return false;
        }

        if (ItemBag[itemID] <= 0)
        {
            return false;
        }
        EntityQizi targetHero = null;
        foreach (var oneHero in SelfHeroList)
        {
            if (oneHero.HeroUID == heroUID)
            {
                targetHero = oneHero;
                break;
            }
        }

        if (targetHero.EquipItemList == null)
        {
            return false;
        }

        if (targetHero.EquipItemList.Count >= 5)//todo 可能不同角色的装备上限不同
        {
            return false;
        }

        AddOneItem(itemID, -1);
        targetHero.EquipItemList.Add(itemID);
        targetHero.OnChangeEquipItem();
        return true;
    }

    public bool TryRemoveEquip(int heroUID, int itemID,int equipIndex)
    {
        EntityQizi targetHero = null;
        foreach (var oneHero in SelfHeroList)
        {
            if (oneHero.HeroUID == heroUID)
            {
                targetHero = oneHero;
                break;
            }
        }

        if (targetHero.EquipItemList == null)
        {
            return false;
        }

        if (targetHero.EquipItemList.Count <= equipIndex)
        {
            return false;
        }

        if (targetHero.EquipItemList[equipIndex] != itemID)
        {
            return false;
        }
        targetHero.EquipItemList.RemoveAt(equipIndex);
        targetHero.OnChangeEquipItem();
        AddOneItem(itemID,1);
        return true;
    }
    public void PassCurPoint()
    {
        if (CurAreaPoint == null)
        {
            return;
        }
        CurAreaPoint.CurPassState = AreaPoint.PointPassState.Pass;
        foreach (var linkPointIndex in CurAreaPoint.LinkPointList)
        {
            var linkPoint = GetPoint(linkPointIndex);
            if (linkPoint != null&&linkPoint.CurPassState == AreaPoint.PointPassState.Lock)
            {
                linkPoint.CurPassState = AreaPoint.PointPassState.Unlock;
                linkPoint.CanSee = true;
            }
        }
        GameEntry.Event.Fire(this,MapFreshEventArgs.Create());
    }
    public AreaPoint GetPoint(int x, int y)
    {
        foreach (var onePoint in CurAreaList)
        {
            if (onePoint.PosObsolete.x == x && onePoint.PosObsolete.y == y)
            {
                return onePoint;
            }
        }
        return null;
    }
    public AreaPoint GetPoint(int index)
    {
        foreach (var onePoint in CurAreaList)
        {
            if (onePoint.Index == index)
            {
                return onePoint;
            }
        }
        return null;
    }

    public void TryAddCoin(int changeNum)
    {
        CoinNum += changeNum;
        CoinNum = Math.Max(0, CoinNum);
        GameEntry.Event.Fire(this,FreshCoinNumArg.Create());
    }
    public int GetOneRandomLevelIDFormType(MazePointType pointType)
    {
        var levelConfigTable = GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
        List<DRLevelConfig> allMeetList = ListPool<DRLevelConfig>.Get();
        foreach (var oneLevelConfig in levelConfigTable.GetAllDataRows())
        {
            if (oneLevelConfig.MazePointType == (int)pointType)
            {
                allMeetList.Add(oneLevelConfig);
            }
        }
        var levelIndex = Utility.Random.GetRandom(allMeetList.Count);
        var retID = allMeetList[levelIndex].Id;
        ListPool<DRLevelConfig>.Release(allMeetList);
        return retID;
    }
}