using System.Collections.Generic;
using UnityEngine;

namespace Entity
{
    public class EnemyInfo
    {
        public List<OneEnemyInfo> InfoList = new List<OneEnemyInfo>();
    }

    public class OneEnemyInfo
    {
        public Vector2Int Pos;
        public int HeroID;
    }
}