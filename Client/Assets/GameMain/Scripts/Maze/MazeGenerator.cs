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
        public Vector2Int PosObsolete { get; set; }
        public int Index;
        public Vector3 Pos{ get; set; }
        public GameObject LevelGObj;
        public AreaPointType AreaPointType;
        public MazePointType CurType { get; set; }
        public int CurLevelID;
        public Vector3 CameraPosOffset;
        public Quaternion CameraRotation;
        public List<AreaPoint> LinkPointObsolete { get; set; }
        public List<int> LinkPointList { get; set; }
        public bool CanSee = false;//能看见
        public PointPassState CurPassState = PointPassState.Lock;
        public AreaPoint(Vector2Int posObsolete)
        {
            PosObsolete = posObsolete;
            CurType = MazePointType.Empty;
            LinkPointObsolete = new List<AreaPoint>();
        }

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
    public class MazeGenerator
    {
        public int Width = 8;
        public int Height = 7;
        private AreaPoint[,] grid;
        private List<AreaPoint> mainPath;
        private List<AreaPoint> usedPoints = new List<AreaPoint>();
        private List<Vector2Int> directions;
        private List<int> mainPathWeight = new List<int>(){10,2,10,2,20,3,1,3};
        public MazeGenerator()
        {
            directions = new List<Vector2Int>
            {
                new Vector2Int(1, 0), // 右
                new Vector2Int(-1, 0), // 左
                new Vector2Int(0, 1), // 上
                new Vector2Int(0, -1), // 下
                new Vector2Int(1, 1), // 右上
                new Vector2Int(1, -1), // 右下
                new Vector2Int(-1, 1), // 左上
                new Vector2Int(-1, -1) // 左下
            };
        }

        public List<AreaPoint> GenerateMaze()
        {
            grid = new AreaPoint[Width, Height];
            InitializeGrid();

            mainPath = new List<AreaPoint>();
            GenerateMainPath(0, 0);

            GenerateSecondaryPath();
            GenerateSecondaryPath();

            AssignTypesToPoints();

            return FlattenGrid();
        }

        private void InitializeGrid()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    grid[x, y] = new AreaPoint(new Vector2Int(x, y));
                }
            }
        }

        private bool GenerateMainPathRecursive(int x, int y)
        {
            if (x == Width - 1 && y == Height - 1)
            {
                mainPath.Add(grid[x, y]);
                usedPoints.Add(grid[x, y]);
                return true;
            }

            var newDirectionsList = new List<Vector2Int>();
            newDirectionsList.AddRange(directions);
            ShuffleWithWeights(newDirectionsList,mainPathWeight);
            foreach (var direction in newDirectionsList)
            {
                if ((x == Width - 1 && direction.y <= 0)||(y==Height-1&&direction.x<=0))
                {
                    continue;
                }
                int newX = x + direction.x;
                int newY = y + direction.y;

                if (newX >= 0 && newY >= 0 && newX < Width && newY < Height && !mainPath.Contains(grid[newX, newY])&&!IsCrossingPath(new Vector2Int(x, y), new Vector2Int(newX, newY), direction))
                {
                    mainPath.Add(grid[x, y]);
                    usedPoints.Add(grid[x, y]);
                    grid[x, y].LinkPointObsolete.Add(grid[newX, newY]);
                    grid[newX, newY].LinkPointObsolete.Add(grid[x, y]);

                    if (GenerateMainPathRecursive(newX, newY))
                    {
                        return true;
                    }

                    // Backtrack
                    mainPath.Remove(grid[x, y]);
                    usedPoints.Remove(grid[x, y]);
                    grid[x, y].LinkPointObsolete.Remove(grid[newX, newY]);
                    grid[newX, newY].LinkPointObsolete.Remove(grid[x, y]);
                }
            }

            return false;
        }

        private void GenerateMainPath(int startX, int startY)
        {
            if (!GenerateMainPathRecursive(startX, startY))
            {
                throw new Exception("Failed to generate main path");
            }
        }

        private void GenerateSecondaryPath()
        {
            int startIndex = Utility.Random.GetRandom(0, mainPath.Count / 2);
            int endIndex = Utility.Random.GetRandom(mainPath.Count / 2, mainPath.Count);

            AreaPoint startPoint = mainPath[startIndex];
            AreaPoint endPoint = mainPath[endIndex];

            int x = startPoint.PosObsolete.x;
            int y = startPoint.PosObsolete.y;
            List<AreaPoint> secondaryPath = new List<AreaPoint> { startPoint };

            while (true)
            {
                var newDirectionsList = new List<Vector2Int>();
                newDirectionsList.AddRange(directions);
                Shuffle(newDirectionsList);

                bool moved = false;

                foreach (var direction in newDirectionsList)
                {
                    int newX = x + direction.x;
                    int newY = y + direction.y;

                    if (newX >= 0 && newY >= 0 && newX < Width && newY < Height && !secondaryPath.Contains(grid[newX, newY]) && !usedPoints.Contains(grid[newX, newY]))
                    {
                        if (IsCrossingPath(new Vector2Int(x, y), new Vector2Int(newX, newY), direction))
                            continue;

                        secondaryPath.Add(grid[newX, newY]);
                        usedPoints.Add(grid[newX, newY]);
                        grid[x, y].LinkPointObsolete.Add(grid[newX, newY]);
                        grid[newX, newY].LinkPointObsolete.Add(grid[x, y]);
                        x = newX;
                        y = newY;
                        moved = true;
                        break;
                    }
                }

                if (!moved || (secondaryPath.Count > 1 && (x == endPoint.PosObsolete.x && y == endPoint.PosObsolete.y))) break; // Break if no move is possible or reach the end point
            }
        }

        private bool IsCrossingPath(Vector2Int from, Vector2Int to, Vector2Int direction)
        {
            if (Math.Abs(direction.x) == 1 && Math.Abs(direction.y) == 1)
            {
                Vector2Int adjacent1 = new Vector2Int(from.x + direction.x, from.y);
                Vector2Int adjacent2 = new Vector2Int(from.x, from.y + direction.y);

                if (IsPointUsed(adjacent1) && IsPointUsed(adjacent2))
                {
                    if (IsLinked(adjacent1, adjacent2))
                        return true;
                }
            }

            return false;
        }

        private bool IsPointUsed(Vector2Int point)
        {
            return usedPoints.Any(p => p.PosObsolete.x == point.x && p.PosObsolete.y == point.y);
        }

        private bool IsLinked(Vector2Int point1, Vector2Int point2)
        {
            AreaPoint p1 = usedPoints.FirstOrDefault(p => p.PosObsolete.x == point1.x && p.PosObsolete.y == point1.y);
            AreaPoint p2 = usedPoints.FirstOrDefault(p => p.PosObsolete.x == point2.x && p.PosObsolete.y == point2.y);

            return p1 != null && p2 != null && p1.LinkPointObsolete.Contains(p2);
        }

        private void AssignTypesToPoints()
        {
            foreach (var point in mainPath)
            {
                if (point.LinkPointObsolete.Count == 0)
                {
                    Log.Error("should be Empty");
                }
                if (point.PosObsolete.x == 0 && point.PosObsolete.y == 0)
                {
                    point.CurType = MazePointType.Store;
                    point.CanSee = true;
                    point.CurPassState = AreaPoint.PointPassState.Unlock;
                }
                else if (point.PosObsolete.x == Width - 1 && point.PosObsolete.y == Height - 1)
                {
                    point.CurType = MazePointType.BossBattle;
                    point.CanSee = true;
                }
                else
                {
                    point.CanSee = false;
                    point.CurType = (MazePointType)Utility.Random.GetRandom(2, Enum.GetValues(typeof(MazePointType)).Length - 2);
                }
            }

            foreach (var point in usedPoints)
            {
                if (point.CurType == MazePointType.Empty && !mainPath.Contains(point))
                {
                    point.CanSee = false;
                    point.CurType = (MazePointType)Utility.Random.GetRandom(2, Enum.GetValues(typeof(MazePointType)).Length - 2);
                }
            }
        }
        private void ShuffleWithWeights<T>(IList<T> list, IList<int> weights)
        {
            if (list.Count != weights.Count)
            {
                throw new ArgumentException("List and weights must have the same length.");
            }

            // Create a list to hold weighted indices
            List<int> weightedIndices = new List<int>();

            // Populate weighted indices based on weights
            for (int i = 0; i < weights.Count; i++)
            {
                for (int j = 0; j < weights[i]; j++)
                {
                    weightedIndices.Add(i);
                }
            }

            int n = list.Count;
            for (int i = 0; i < n - 1; i++)
            {
                // Get a random index from i to n-1 inclusive
                int k = Utility.Random.GetRandom(weightedIndices.Count);

                // Swap elements using weighted indices
                int index = weightedIndices[k];
                (list[index], list[i]) = (list[i], list[index]);
                weightedIndices.RemoveAll((item) => item == index);
                for (var index1 = 0; index1 < weightedIndices.Count; index1++)
                {
                    var item = weightedIndices[index1];
                    if (item == i)
                    {
                        weightedIndices[index1] = index;
                    }
                }
            }
        }
        private void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Utility.Random.GetRandom(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private List<AreaPoint> FlattenGrid()
        {
            List<AreaPoint> pointList = new List<AreaPoint>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (grid[x, y].CurType == MazePointType.Empty)
                    {
                        continue;
                    }
                    pointList.Add(grid[x, y]);
                }
            }

            foreach (var onePoint in pointList)
            {
                onePoint.CurLevelID = SelfDataManager.Instance.GetOneRandomLevelIDFormType(onePoint.CurType);
            }
            return pointList;
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
                onePoint.CurLevelID = SelfDataManager.Instance.GetOneRandomLevelIDFormType(onePoint.CurType);
            }
            return pointList;
        }
    }
}