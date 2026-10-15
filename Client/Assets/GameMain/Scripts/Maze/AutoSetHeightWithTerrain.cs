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
            if (ListSetHeightTerrain == null || ListSetHeightTerrain.Count == 0||!HotGameEntry.HeroManager.HasInitTerrain())
            {
                return;
            }

            foreach (var oneTrans in ListSetHeightTerrain)
            {
                oneTrans.position = HotGameEntry.HeroManager.GetTerrainMatchHeight(oneTrans.position);
            }
        }
    }
}