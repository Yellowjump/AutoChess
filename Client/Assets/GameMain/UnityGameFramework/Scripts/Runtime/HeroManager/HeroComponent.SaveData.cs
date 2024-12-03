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
using DataTable;
using Entity.Bullet;
using Maze;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        public string SaveDataFolderPath => Application.persistentDataPath + "/SaveData";
        public string SaveDataFilePath => SaveDataFolderPath + "/SaveData.txt";

        /// <summary>
        /// 存储的地图数据
        /// </summary>
        [Serializable]
        public class SaveMazePoint
        {
            public Vector2Int pos;
            public List<Vector2Int> linkPos;
            public AreaPoint.PointPassState state;
            public int levelID;
            public bool CanSee;
        }
        [Serializable]
        public class oneItem
        {
            public int itemID;
            public int count;
        }
        [Serializable]
        public class SaveHeroData
        {
            public int heroID;
            public List<int> equipItem;
            public Vector2Int pos;
        }
        public class SaveData
        {
            public string Version;
            public int RandomSeed;
            public int RandomCount;
            public int CoinNum;
            // ditu
            public List<SaveMazePoint> MazeData;
            public List<oneItem> Bag;
            public List<SaveHeroData> HeroList;
        }
        /// <summary>
        /// 存档
        /// </summary>
        public void Save()
        {
            // 确保保存目录存在
            if (!Directory.Exists(SaveDataFolderPath))
            {
                Directory.CreateDirectory(SaveDataFolderPath);
            }
            //创建存档对象
            SaveData newSaveData = new SaveData();
            newSaveData.Version = Application.version;
            newSaveData.RandomSeed = Utility.Random.Seed;
            newSaveData.RandomCount = Utility.Random.NextCount;
            newSaveData.MazeData = new List<SaveMazePoint>();
            foreach (var onePoint in SelfDataManager.Instance.CurAreaList)
            {
                if (onePoint.CurType == MazePointType.Empty)
                {
                    continue;
                }
                var newPointData = new SaveMazePoint();
                newPointData.pos = onePoint.PosObsolete;
                newPointData.linkPos = new List<Vector2Int>();
                foreach (var linkPoint in onePoint.LinkPointObsolete)
                {
                    newPointData.linkPos.Add(linkPoint.PosObsolete);
                }

                newPointData.state = onePoint.CurPassState;
                newPointData.CanSee = onePoint.CanSee;
                newPointData.levelID = onePoint.CurLevelID;
                newSaveData.MazeData.Add(newPointData);
            }
            newSaveData.Bag = new();
            foreach (var keyValue in SelfDataManager.Instance.ItemBag)
            {
                oneItem oneItem = new oneItem()
                {
                    itemID = keyValue.Key,
                    count = keyValue.Value
                };
                newSaveData.Bag.Add(oneItem);
            }
            
            newSaveData.HeroList = new List<SaveHeroData>();
            foreach (var oneHero in SelfDataManager.Instance.SelfHeroList)
            {
                var newHero = new SaveHeroData();
                newHero.heroID = oneHero.HeroID;
                newHero.pos = oneHero.SavePos;
                newHero.equipItem = new List<int>();
                foreach (var itemID in oneHero.EquipItemList)
                {
                    newHero.equipItem.Add(itemID);
                }
                newSaveData.HeroList.Add(newHero);
            }
            newSaveData.CoinNum = SelfDataManager.Instance.CoinNum;
            string json = Utility.Json.ToJson(newSaveData);
            File.WriteAllText(SaveDataFilePath, json);
        }
        /// <summary>
        /// 是否存在存档
        /// </summary>
        /// <returns></returns>
        public bool HasSaveData()
        {
            if (!File.Exists(SaveDataFilePath))
            {
                Debug.LogWarning("Save file not found.");
                return false;
            }
            return true;
        }
        // 读取数据的方法
        public SaveData Load()
        {
            if (!File.Exists(SaveDataFilePath))
            {
                Debug.LogWarning("Save file not found.");
                return null;
            }
            string json = File.ReadAllText(SaveDataFilePath);
            SaveData data = Utility.Json.ToObject<SaveData>(json);
            return data;
        }
    }
}