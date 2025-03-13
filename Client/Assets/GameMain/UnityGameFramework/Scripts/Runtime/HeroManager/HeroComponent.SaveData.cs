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
        public class SaveMazePoint:IReference
        {
            public int areaID;
            public AreaPoint.PointPassState state;
            public int levelID;
            public bool CanSee;
            public void Clear()
            {
                areaID = 0;

                state = AreaPoint.PointPassState.Lock;
                levelID = 0;
                CanSee = false;
            }
        }
        [Serializable]
        public class ItemSaveData:IReference
        {
            public int itemID;
            public int count;
            public void Clear()
            {
                itemID = 0;
                count = 0;
            }
        }
        [Serializable]
        public class SaveHeroData:IReference
        {
            public int heroID;
            public List<int> equipItem;
            public Vector2Int pos;
            public void Clear()
            {
                
            }
        }
        public class SaveData:IReference
        {
            public string Version;
            public int RandomSeed;
            public int RandomCount;
            public int CoinNum;
            // ditu
            public List<SaveMazePoint> MazeData;
            public List<ItemSaveData> Bag;
            public List<SaveHeroData> HeroList;
            public void Clear()
            {
                Version = string.Empty;
                RandomSeed = 0;
                RandomCount = 0;
                CoinNum = 0;
                if (MazeData != null)
                {
                    foreach (var onePoint in MazeData)
                    {
                        ReferencePool.Release(onePoint);
                    }
                    ListPool<SaveMazePoint>.Release(MazeData);
                    MazeData = null;
                }
                if (Bag != null)
                {
                    foreach (var oneItem in Bag)
                    {
                        ReferencePool.Release(oneItem);
                    }
                    ListPool<ItemSaveData>.Release(Bag);
                    Bag = null;
                }
                if (HeroList != null)
                {
                    foreach (var oneHero in HeroList)
                    {
                        ReferencePool.Release(oneHero);
                    }
                    ListPool<SaveHeroData>.Release(HeroList);
                    HeroList = null;
                }
            }
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
            SaveData newSaveData = ReferencePool.Acquire<SaveData>();
            newSaveData.Version = Application.version;
            newSaveData.RandomSeed = Utility.Random.Seed;
            newSaveData.RandomCount = Utility.Random.NextCount;
            newSaveData.MazeData = ListPool<SaveMazePoint>.Get();
            foreach (var onePoint in CurAreaList)
            {
                var newPointData = ReferencePool.Acquire<SaveMazePoint>();
                newPointData.areaID = onePoint.Index;
                newPointData.state = onePoint.CurPassState;
                newPointData.CanSee = onePoint.CanSee;
                newPointData.levelID = onePoint.CurLevelID;
                newSaveData.MazeData.Add(newPointData);
            }
            newSaveData.Bag = ListPool<ItemSaveData>.Get();
            
            foreach (var oneItemData in ItemBagList)
            {
                ItemSaveData itemSaveData = null;
                foreach (var oneBagItem in newSaveData.Bag)
                {
                    if (oneBagItem.itemID == oneItemData.ItemID)
                    {
                        itemSaveData = oneBagItem;
                        break;
                    }
                }

                if (itemSaveData == null)
                {
                    itemSaveData = ReferencePool.Acquire<ItemSaveData>();
                    itemSaveData.itemID = oneItemData.ItemID;
                    itemSaveData.count = 1;
                    newSaveData.Bag.Add(itemSaveData);
                }
                else
                {
                    itemSaveData.count++;
                }
            }
            
            newSaveData.HeroList = ListPool<SaveHeroData>.Get();
            foreach (var oneHero in QiziCSList)
            {
                var newHero = ReferencePool.Acquire<SaveHeroData>();
                newHero.heroID = oneHero.HeroID;
                newHero.pos = oneHero.SavePos;
                newHero.equipItem = ListPool<int>.Get();
                foreach (var item in oneHero.EquipItemList)
                {
                    newHero.equipItem.Add(item.ItemID);
                }
                newSaveData.HeroList.Add(newHero);
            }
            newSaveData.CoinNum = CoinNum;
            string json = Utility.Json.ToJson(newSaveData);
            ReferencePool.Release(newSaveData);
            GameEntry.Setting.SetString(ConstValue.SettingKeyGameRecord,json);
            GameEntry.Setting.Save();
            //File.WriteAllText(SaveDataFilePath, json);
        }
        /// <summary>
        /// 是否存在存档
        /// </summary>
        /// <returns></returns>
        public bool HasSaveData()
        {
            /*if (!File.Exists(SaveDataFilePath))
            {
                Debug.LogWarning("Save file not found.");
                return false;
            }*/
            return GameEntry.Setting.HasSetting(ConstValue.SettingKeyGameRecord);
        }
        // 读取数据的方法
        public SaveData Load()
        {
            /*if (!File.Exists(SaveDataFilePath))
            {
                Debug.LogWarning("Save file not found.");
                return null;
            }
            string json = File.ReadAllText(SaveDataFilePath);*/
            var json = GameEntry.Setting.GetString(ConstValue.SettingKeyGameRecord);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            SaveData data = Utility.Json.ToObject<SaveData>(json);
            return data;
        }

        public void DeleteGameRecord()
        {
            GameEntry.Setting.RemoveSetting(ConstValue.SettingKeyGameRecord);
            GameEntry.Setting.Save();
        }

        private void ClearBagList()
        {
            foreach (var oneItemData in ItemBagList)
            {
                ReferencePool.Release(oneItemData);
            }
            ItemBagList.Clear();
            ItemUniqueIndex = 0;
        }
    }
}