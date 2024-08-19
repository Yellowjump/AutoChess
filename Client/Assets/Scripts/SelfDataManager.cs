using System.Collections.Generic;
using DataTable;
using Entity;
using GameFramework.Event;
using Maze;
using SelfEventArg;
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
    public List<MazePoint> CurMazeList;
    public MazeGenerator CurMaze;
    public List<EntityQizi> SelfHeroList = new List<EntityQizi>();
    public Dictionary<int, int> ItemBag = new Dictionary<int, int>();

    private void Init()
    {
        GameEntry.Event.Subscribe(CMDGetItemEventArgs.EventId,OnCMDGetItem);
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
}