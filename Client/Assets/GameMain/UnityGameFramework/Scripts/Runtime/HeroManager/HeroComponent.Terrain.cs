//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using GameFramework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using DataTable;
using Entity;
using Entity.Bullet;
using Maze;
using SkillSystem;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        public List<Terrain> TerrainList;
        private Rect terrainTotalSize = new Rect();
        public void InitTerrainList()
        {
            TerrainList = FindObjectsByType<Terrain>(sortMode: FindObjectsSortMode.None).ToList();
            if (TerrainList == null||TerrainList.Count==0)
            {
                Debug.LogWarning("No Terrain found in the scene. Please add a Terrain component.");
                return;
            }
            // 初始化最大和最小边界
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            // 遍历所有 Terrain
            foreach (var terrain in TerrainList)
            {
                TerrainData terrainData = terrain.terrainData;
            
                // 计算 Terrain 的左下角和右上角在世界空间中的坐标
                float terrainMinX = terrain.transform.position.x;
                float terrainMaxX = terrainMinX + terrainData.size.x;

                float terrainMinZ = terrain.transform.position.z;
                float terrainMaxZ = terrainMinZ + terrainData.size.z;

                // 更新总地形的边界
                minX = Mathf.Min(minX, terrainMinX);
                maxX = Mathf.Max(maxX, terrainMaxX);
                minZ = Mathf.Min(minZ, terrainMinZ);
                maxZ = Mathf.Max(maxZ, terrainMaxZ);
            }
            terrainTotalSize.xMin = minX;
            terrainTotalSize.xMax = maxX;
            terrainTotalSize.yMin = minZ;
            terrainTotalSize.yMax = maxZ;
        }

        public bool HasInitTerrain()
        {
            return TerrainList != null;
        }
        public Vector3 GetTerrainMatchHeight(Vector3 worldPos)
        {
            Terrain matchTerrain = GetMatchTerrain(worldPos);
            if (matchTerrain == null)
            {
                return worldPos;
            }

            var height = matchTerrain.SampleHeight(worldPos);
            worldPos.y = height;
            return worldPos;
        }
        private Terrain GetMatchTerrain(Vector3 targetPos)
        {
            if (TerrainList == null || TerrainList.Count == 0)
            {
                return null;
            }

            foreach (var oneTerrain in TerrainList)
            {
                var terrainPos = oneTerrain.GetPosition();
                var terrainSize = oneTerrain.terrainData.size;
                // 计算 Terrain 的边界
                float terrainMinX = terrainPos.x;
                float terrainMaxX = terrainMinX + terrainSize.x;

                float terrainMinZ = terrainPos.z;
                float terrainMaxZ = terrainMinZ + terrainSize.z;

                // 检查点是否在 Terrain 的范围内
                bool isInside = targetPos.x >= terrainMinX && targetPos.x <= terrainMaxX &&
                                targetPos.z >= terrainMinZ && targetPos.z <= terrainMaxZ;
                if (isInside)
                {
                    return oneTerrain;
                }
            }
            return null;
        }
    }
}