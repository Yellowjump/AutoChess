using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Maze
{
    public class AutoSetHeightWithTerrain:MonoBehaviour
    {
        public List<Transform> ListSetHeightTerrain;

        public void SetTransformHeightWithTerrain()
        {
            if (ListSetHeightTerrain == null || ListSetHeightTerrain.Count == 0||!GameEntry.HeroManager.HasInitTerrain())
            {
                return;
            }

            foreach (var oneTrans in ListSetHeightTerrain)
            {
                oneTrans.position = GameEntry.HeroManager.GetTerrainMatchHeight(oneTrans.position);
            }
        }
    }
}