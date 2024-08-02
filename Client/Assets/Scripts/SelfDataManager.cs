using System.Collections.Generic;
using Entity;
using Maze;

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
            }
            return _instance;
        }
    }
    public List<MazePoint> CurMazeList;
    public MazeGenerator CurMaze;
    public List<EntityQizi> SelfHeroList = new List<EntityQizi>();
}