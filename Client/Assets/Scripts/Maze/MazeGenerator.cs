using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;
using Random = UnityEngine.Random;

namespace Maze
{
    public enum MazePointType
    {
        Start,
        End,
        SmallBattle,
        EliteBattle,
        Rest,
        Event,
        Empty // Represent empty points in the maze
    }

    public class MazePoint
    {
        public Vector2Int Pos { get; set; }
        public MazePointType CurType { get; set; }
        public List<MazePoint> LinkPoint { get; set; }

        public MazePoint(Vector2Int pos)
        {
            Pos = pos;
            CurType = MazePointType.Empty;
            LinkPoint = new List<MazePoint>();
        }
    }

    public class MazeGenerator
    {
        private const int Width = 8;
        private const int Height = 7;
        private MazePoint[,] grid;
        private List<MazePoint> mainPath;
        private List<MazePoint> usedPoints = new List<MazePoint>();
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

        public List<MazePoint> GenerateMaze()
        {
            grid = new MazePoint[Width, Height];
            InitializeGrid();

            mainPath = new List<MazePoint>();
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
                    grid[x, y] = new MazePoint(new Vector2Int(x, y));
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
                if ((x == Width - 1 && direction.y < 0)||(y==Height-1&&direction.x<0))
                {
                    continue;
                }
                int newX = x + direction.x;
                int newY = y + direction.y;

                if (newX >= 0 && newY >= 0 && newX < Width && newY < Height && !mainPath.Contains(grid[newX, newY])&&!IsCrossingPath(new Vector2Int(x, y), new Vector2Int(newX, newY), direction))
                {
                    mainPath.Add(grid[x, y]);
                    usedPoints.Add(grid[x, y]);
                    grid[x, y].LinkPoint.Add(grid[newX, newY]);
                    grid[newX, newY].LinkPoint.Add(grid[x, y]);

                    if (GenerateMainPathRecursive(newX, newY))
                    {
                        return true;
                    }

                    // Backtrack
                    mainPath.Remove(grid[x, y]);
                    usedPoints.Remove(grid[x, y]);
                    grid[x, y].LinkPoint.Remove(grid[newX, newY]);
                    grid[newX, newY].LinkPoint.Remove(grid[x, y]);
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

            MazePoint startPoint = mainPath[startIndex];
            MazePoint endPoint = mainPath[endIndex];

            int x = startPoint.Pos.x;
            int y = startPoint.Pos.y;
            List<MazePoint> secondaryPath = new List<MazePoint> { startPoint };

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
                        grid[x, y].LinkPoint.Add(grid[newX, newY]);
                        grid[newX, newY].LinkPoint.Add(grid[x, y]);
                        x = newX;
                        y = newY;
                        moved = true;
                        break;
                    }
                }

                if (!moved || (secondaryPath.Count > 1 && (x == endPoint.Pos.x && y == endPoint.Pos.y))) break; // Break if no move is possible or reach the end point
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
            return usedPoints.Any(p => p.Pos.x == point.x && p.Pos.y == point.y);
        }

        private bool IsLinked(Vector2Int point1, Vector2Int point2)
        {
            MazePoint p1 = usedPoints.FirstOrDefault(p => p.Pos.x == point1.x && p.Pos.y == point1.y);
            MazePoint p2 = usedPoints.FirstOrDefault(p => p.Pos.x == point2.x && p.Pos.y == point2.y);

            return p1 != null && p2 != null && p1.LinkPoint.Contains(p2);
        }

        private void AssignTypesToPoints()
        {
            foreach (var point in mainPath)
            {
                if (point.LinkPoint.Count == 0)
                {
                    Log.Error("should be Empty");
                }
                if (point.Pos.x == 0 && point.Pos.y == 0)
                {
                    point.CurType = MazePointType.Start;
                }
                else if (point.Pos.x == Width - 1 && point.Pos.y == Height - 1)
                {
                    point.CurType = MazePointType.End;
                }
                else
                {
                    point.CurType = (MazePointType)Utility.Random.GetRandom(2, Enum.GetValues(typeof(MazePointType)).Length - 1);
                }
            }

            foreach (var point in usedPoints)
            {
                if (point.CurType == MazePointType.Empty && !mainPath.Contains(point))
                {
                    if (point.LinkPoint.Count == 0)
                    {
                        Log.Error("should be Empty");
                    }
                    point.CurType = (MazePointType)Utility.Random.GetRandom(2, Enum.GetValues(typeof(MazePointType)).Length - 1);
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

        private List<MazePoint> FlattenGrid()
        {
            List<MazePoint> pointList = new List<MazePoint>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    pointList.Add(grid[x, y]);
                }
            }

            return pointList;
        }
    }
}