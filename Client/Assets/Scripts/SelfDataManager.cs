using System.Collections.Generic;
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
}