using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework;
using UnityEngine;
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

        public List<MazePoint> GenerateMaze()
        {
            grid = new MazePoint[Width, Height];
            InitializeGrid();

            List<MazePoint> mainPath = new List<MazePoint>();
            if (!GenerateMainPath(0, 0, mainPath))
            {
                throw new Exception("Failed to generate a valid maze");
            }

            // Generate extra paths
            GenerateExtraPaths(mainPath, 2);

            // Ensure all points are accessible
            EnsureAllPointsAccessible();

            // Assign types to maze points
            AssignTypesToPoints(mainPath);

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

        private bool GenerateMainPath(int x, int y, List<MazePoint> path)
        {
            if (x == Width - 1 && y == Height - 1)
            {
                path.Add(grid[x, y]);
                return true;
            }

            if (x < 0 || y < 0 || x >= Width || y >= Height || path.Contains(grid[x, y]))
            {
                return false;
            }

            path.Add(grid[x, y]);

            List<Vector2Int> directions = new List<Vector2Int>
            {
                new Vector2Int(1, 0), // Right
                new Vector2Int(-1, 0), // Left
                new Vector2Int(0, 1), // Down
                new Vector2Int(0, -1) // Up
            };

            Shuffle(directions);

            foreach (var direction in directions)
            {
                int newX = x + direction.x;
                int newY = y + direction.y;

                if (GenerateMainPath(newX, newY, path))
                {
                    grid[x, y].LinkPoint.Add(grid[newX, newY]);
                    grid[newX, newY].LinkPoint.Add(grid[x, y]);
                    return true;
                }
            }

            path.Remove(grid[x, y]);
            return false;
        }

        private void GenerateExtraPaths(List<MazePoint> mainPath, int maxExtraPaths)
        {
            int extraPaths = 0;
            HashSet<MazePoint> visited = new HashSet<MazePoint>(mainPath);

            while (extraPaths < maxExtraPaths)
            {
                MazePoint start = mainPath[Utility.Random.GetRandom(mainPath.Count)];
                List<MazePoint> extraPath = new List<MazePoint>();
                if (GenerateExtraPath(start.Pos.x, start.Pos.y, extraPath, visited))
                {
                    foreach (var point in extraPath)
                    {
                        if (!mainPath.Contains(point))
                        {
                            mainPath.Add(point);
                            visited.Add(point);
                        }
                    }

                    extraPaths++;
                }
            }
        }

        private bool GenerateExtraPath(int x, int y, List<MazePoint> path, HashSet<MazePoint> visited)
        {
            if (x == Width - 1 && y == Height - 1)
            {
                path.Add(grid[x, y]);
                return true;
            }

            if (x < 0 || y < 0 || x >= Width || y >= Height || path.Contains(grid[x, y]) || visited.Contains(grid[x, y]))
            {
                return false;
            }

            path.Add(grid[x, y]);

            List<Vector2Int> directions = new List<Vector2Int>
            {
                new Vector2Int(1, 0), // Right
                new Vector2Int(-1, 0), // Left
                new Vector2Int(0, 1), // Down
                new Vector2Int(0, -1) // Up
            };

            Shuffle(directions);

            foreach (var direction in directions)
            {
                int newX = x + direction.x;
                int newY = y + direction.y;

                if (GenerateExtraPath(newX, newY, path, visited))
                {
                    grid[x, y].LinkPoint.Add(grid[newX, newY]);
                    grid[newX, newY].LinkPoint.Add(grid[x, y]);
                    return true;
                }
            }

            path.Remove(grid[x, y]);
            return false;
        }

        private void EnsureAllPointsAccessible()
        {
            HashSet<MazePoint> visited = new HashSet<MazePoint>();
            Queue<MazePoint> queue = new Queue<MazePoint>();

            queue.Enqueue(grid[0, 0]);
            visited.Add(grid[0, 0]);

            while (queue.Count > 0)
            {
                MazePoint current = queue.Dequeue();

                foreach (var neighbor in GetNeighbors(current.Pos))
                {
                    if (!visited.Contains(neighbor))
                    {
                        current.LinkPoint.Add(neighbor);
                        neighbor.LinkPoint.Add(current);
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (!visited.Contains(grid[x, y]))
                    {
                        var randomNeighbor = GetNeighbors(grid[x, y].Pos).FirstOrDefault(p => visited.Contains(p));
                        if (randomNeighbor != null)
                        {
                            grid[x, y].LinkPoint.Add(randomNeighbor);
                            randomNeighbor.LinkPoint.Add(grid[x, y]);
                            visited.Add(grid[x, y]);
                        }
                    }
                }
            }
        }

        private List<MazePoint> GetNeighbors(Vector2Int pos)
        {
            List<MazePoint> neighbors = new List<MazePoint>();

            if (pos.x > 0) neighbors.Add(grid[pos.x - 1, pos.y]);
            if (pos.x < Width - 1) neighbors.Add(grid[pos.x + 1, pos.y]);
            if (pos.y > 0) neighbors.Add(grid[pos.x, pos.y - 1]);
            if (pos.y < Height - 1) neighbors.Add(grid[pos.x, pos.y + 1]);

            return neighbors;
        }

        private void AssignTypesToPoints(List<MazePoint> mainPath)
        {
            foreach (var point in mainPath)
            {
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

            foreach (var point in grid)
            {
                if (point.CurType == MazePointType.Empty && !mainPath.Contains(point))
                {
                    point.CurType = (MazePointType)Utility.Random.GetRandom(2, Enum.GetValues(typeof(MazePointType)).Length - 1);
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