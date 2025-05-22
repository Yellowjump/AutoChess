using System;
using System.Collections.Generic;
using System.Linq;
using DataTable;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;
using SelfEventArg;
using UnityEngine.Pool;

namespace Maze
{
    /// <summary>
    /// 地图点的类型，对应的是 地图上图标的不同，UnKnown可以是 战斗，商店，宝箱，或者事件,点击之后随机后确定。
    /// </summary>
    public enum MazePointType
    {
        /// <summary>
        /// 小怪
        /// </summary>
        SmallBattle = 0,
        /// <summary>
        /// 精英怪
        /// </summary>
        EliteBattle = 1,
        /// <summary>
        /// 关底boss
        /// </summary>
        BossBattle = 2,
        UnKnown = 3,
        Store = 4,
        /// <summary>
        /// 宝箱
        /// </summary>
        Chest = 5,
        Event = 6,
        Empty = 7 // Represent empty points in the maze
    }
    // 地图点类型，对应的是areaPoint表中地图的设定，多个起点，一个终点
    public enum AreaPointType
    {
        Start,
        End,
        Battle,
        Event,
        Empty,
    }
    public class AreaPoint:IReference
    {
        [Serializable]
        public enum PointPassState
        {
            Lock,//锁定
            Unlock,//可进入
            Pass,//已通过
        }
        public int Index;//areaPoint表ID
        public Vector3 Pos{ get; set; }
        public GameObject LevelGObj;
        public GameObject SourceObj;
        public AreaPointType AreaPointType;
        public MazePointType CurType { get; set; }
        public int CurLevelID;
        public Vector3 CameraPosOffset;
        public Quaternion CameraRotation;
        public List<int> LinkPointList { get; set; }
        public bool CanSee = false;//能看见
        public PointPassState CurPassState = PointPassState.Lock;
        public AreaPoint()
        {
            LinkPointList = ListPool<int>.Get();
        }

        public void Clear()
        {
            if (LinkPointList != null)
            {
                ListPool<int>.Release(LinkPointList);
            }
            LevelGObj = null;
        }
    }
    //从表中路径数据生成 细化地图
    public class MazeGeneratorFromAreaPointTable
    {
        public List<AreaPoint> InitMap()
        {
            List<AreaPoint> pointList = ListPool<AreaPoint>.Get();
            var areaPointTable = GameEntry.DataTable.GetDataTable<DRAreaPoint>("AreaPoint");
            foreach (var oneAreaPointData in areaPointTable.GetAllDataRows())
            {
                var onePoint = ReferencePool.Acquire<AreaPoint>();
                onePoint.Index = oneAreaPointData.Id;
                onePoint.Pos = oneAreaPointData.Position;
                onePoint.CameraPosOffset = oneAreaPointData.CameraPosRelate;
                onePoint.CameraRotation = new Quaternion(oneAreaPointData.CameraRotate.x,oneAreaPointData.CameraRotate.y,oneAreaPointData.CameraRotate.z,oneAreaPointData.CameraRotate.w);
                onePoint.LinkPointList.AddRange(oneAreaPointData.LinkArea);
                onePoint.CurPassState = AreaPoint.PointPassState.Lock;
                onePoint.AreaPointType = (AreaPointType)oneAreaPointData.AreaPointType;
                if (oneAreaPointData.RandomLevelConfigID != null && oneAreaPointData.RandomLevelConfigID.Length > 0)
                {
                    var randomLevelID = oneAreaPointData.RandomLevelConfigID[Utility.Random.GetRandom(oneAreaPointData.RandomLevelConfigID.Length)];
                    onePoint.CurLevelID = randomLevelID;
                    var levelConfigTable = GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
                    if (levelConfigTable.HasDataRow(onePoint.CurLevelID))
                    {
                        onePoint.CurType = (MazePointType)levelConfigTable[onePoint.CurLevelID].MazePointType;
                    }
                }
                else
                {
                    switch ((AreaPointType)oneAreaPointData.AreaPointType)
                    {
                        case AreaPointType.Battle:
                            onePoint.CurType = MazePointType.SmallBattle;
                            break;
                        case AreaPointType.End:
                            onePoint.CurType = MazePointType.BossBattle;
                            break;
                        case AreaPointType.Event:
                            onePoint.CurType = MazePointType.Event;
                            break;
                        case AreaPointType.Empty:
                            onePoint.CurType = (MazePointType)Utility.Random.GetRandom(2, Enum.GetValues(typeof(MazePointType)).Length - 2);
                            break;
                    }
                }
                pointList.Add(onePoint);
            }
            //选出start点
            var startList = pointList.FindAll(item => item.AreaPointType == AreaPointType.Start);
            if (startList.Count == 0)
            {
                Log.Error("No Start Point");
                return pointList;
            }

            var finalStartRandom = Utility.Random.GetRandom(startList.Count);
            var startPoint = startList[finalStartRandom];
            startPoint.CurType = MazePointType.Store;
            startPoint.CanSee = true;
            startPoint.CurPassState = AreaPoint.PointPassState.Unlock;
            
            //遍历其他点，具体他们的信息
            foreach (var onePoint in startList)
            {
                if (onePoint != startPoint)
                {
                    onePoint.CurType = (MazePointType)Utility.Random.GetRandom(2, Enum.GetValues(typeof(MazePointType)).Length - 2);
                }
            }
            foreach (var onePoint in pointList)
            {
                if (onePoint.CurLevelID == 0)
                {
                    onePoint.CurLevelID = GameEntry.HeroManager.GetOneRandomLevelIDFormType(onePoint.CurType);
                }
            }
            return pointList;
        }
    }
}