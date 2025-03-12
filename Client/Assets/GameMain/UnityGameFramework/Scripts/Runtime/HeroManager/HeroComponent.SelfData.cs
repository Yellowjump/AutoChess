using System;
using System.Collections.Generic;
using DataTable;
using Entity;
using GameFramework;
using GameFramework.Event;
using Maze;
using SelfEventArg;
using SkillSystem;
using UnityEngine.Pool;
using UnityEngine.Serialization;

namespace UnityGameFramework.Runtime
{
    public class OneItemData:IReference
    {
        public int ItemID;
        public int UniqueID;
        public void Clear()
        {
            ItemID = 0;
            UniqueID = 0;
        }
    }
    public partial class HeroComponent
    {
        public List<AreaPoint> CurAreaList;
        public AreaPoint CurAreaPoint;
        public int CoinNum;
        public List<EntityQizi> SelfHeroList = new List<EntityQizi>();
        public List<OneItemData> ItemBagList = new List<OneItemData>();
        public int ItemUniqueIndex = 0;
        public void InitDataFormData(HeroComponent.SaveData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            var levelConfigTable = GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
            CurAreaList ??= new List<AreaPoint>();
            CurAreaList.Clear();
            foreach (var onePointData in saveData.MazeData)
            {
                var newPoint = new AreaPoint(onePointData.pos);
                newPoint.CanSee = onePointData.CanSee;
                if (levelConfigTable.HasDataRow(onePointData.levelID))
                {
                    var levelData = levelConfigTable[onePointData.levelID];
                    newPoint.CurType = (MazePointType)levelData.MazePointType;
                }
                else
                {
                    Log.Error($"No LevelConfig ID:{onePointData.levelID}");
                }

                newPoint.CurPassState = onePointData.state;
                newPoint.CurLevelID = onePointData.levelID;
                CurAreaList.Add(newPoint);
            }

            foreach (var onePointData in saveData.MazeData)
            {
                var curPoint = GetPoint(onePointData.pos.x, onePointData.pos.y);
                foreach (var linkPos in onePointData.linkPos)
                {
                    var linkPoint = GetPoint(linkPos.x, linkPos.y);
                    curPoint.LinkPointObsolete.Add(linkPoint);
                }
            }

            GameEntry.HeroManager.InitAreaPointCamera();
            ItemBagList.Clear();
            ItemUniqueIndex = 0;
            foreach (var oneItemData in saveData.Bag)
            {
                AddOneItemToBag(oneItemData.itemID, oneItemData.count);
            }

            foreach (var oneHeroData in saveData.HeroList)
            {
                var newFriendHero = GameEntry.HeroManager.AddNewFriendHero(oneHeroData.heroID, oneHeroData.pos.y, oneHeroData.pos.x);
                foreach (var itemID in oneHeroData.equipItem)
                {
                    newFriendHero.EquipItemList.Add(AddEquipItemToEquip(itemID));
                }
                SelfHeroList.Add(newFriendHero);
            }

            CoinNum = saveData.CoinNum;
        }

        private void OnCMDGetItem(object sender, GameEventArgs e)
        {
            CMDGetItemEventArgs ne = (CMDGetItemEventArgs)e;
            if (ne == null)
            {
                return;
            }

            AddOneItemToBag(ne.ItemID, ne.ItemNum);
        }

        public void AddOneItemToBag(OneItemData itemData)
        {
            if (ItemBagList != null)
            {
                ItemBagList.Add(itemData);
            }
        }
        public void AddOneItemToBag(int id, int changeNum)
        {
            if (changeNum > 0)
            {
                for (int countIndex = 0; countIndex < changeNum; countIndex++)
                {
                    var oneNewItemData = ReferencePool.Acquire<OneItemData>();
                    oneNewItemData.ItemID = id;
                    oneNewItemData.UniqueID = ++ItemUniqueIndex;
                    ItemBagList.Add(oneNewItemData);
                }
            }
            else
            {
                List<OneItemData> tempList = ListPool<OneItemData>.Get();
                tempList.AddRange(ItemBagList);
                var hasRemoveCount = 0;
                foreach (var oneItemData in tempList)
                {
                    if (oneItemData.ItemID == id)
                    {
                        ItemBagList.Remove(oneItemData);
                        ReferencePool.Release(oneItemData);
                        hasRemoveCount++;
                        if (hasRemoveCount >= -changeNum)
                        {
                            break;
                        }
                    }
                }
                if (hasRemoveCount < -changeNum)
                {
                    Log.Error($"Try Remove More Item itemID:{{id}} Count {changeNum}");
                }
                ListPool<OneItemData>.Release(tempList);
            }
        }
        public OneItemData AddEquipItemToEquip(int itemID)
        {
            var oneNewItemData = ReferencePool.Acquire<OneItemData>();
            oneNewItemData.ItemID = itemID;
            oneNewItemData.UniqueID = ++ItemUniqueIndex;
            return oneNewItemData;
        }

        public bool TryCraftItem(int itemID)
        {
            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            if (itemTable.HasDataRow(itemID))
            {
                var itemData = itemTable[itemID];
                var needItemList = itemData.CraftList;
                if (MeetCraftItemNeed(needItemList))
                {
                    foreach (var idAndNum in needItemList)
                    {
                        AddOneItemToBag(idAndNum.Item1, -idAndNum.Item2);
                    }

                    AddOneItemToBag(itemID, 1);
                    return true;
                }
            }

            return false;
        }

        public int OwnTargetItemCount(int itemID)
        {
            if (ItemBagList == null || ItemBagList.Count == 0)
            {
                return 0;
            }

            var retCount = 0;
            foreach (var oneItemData in ItemBagList)
            {
                if (oneItemData.ItemID == itemID)
                {
                    retCount++;
                }
            }
            return retCount;
        }

        public OneItemData GetBagItemDataByUID(int uniqueID)
        {
            if (ItemBagList == null || ItemBagList.Count == 0)
            {
                return null;
            }
            foreach (var oneItemData in ItemBagList)
            {
                if (oneItemData.UniqueID == uniqueID)
                {
                    return oneItemData;
                }
            }

            return null;
        }
        private bool MeetCraftItemNeed(List<(int, int)> needItem)
        {
            if (ItemBagList == null || needItem == null || needItem.Count == 0)
            {
                return false;
            }
            
            foreach (var idAndNum in needItem)
            {
                if (OwnTargetItemCount(idAndNum.Item1)< idAndNum.Item2)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryEquipItem(int heroUID, OneItemData itemData)
        {
            if (itemData == null)
            {
                return false;
            }
            EntityQizi targetHero = null;
            foreach (var oneHero in SelfHeroList)
            {
                if (oneHero.HeroUID == heroUID)
                {
                    targetHero = oneHero;
                    break;
                }
            }

            if (targetHero.EquipItemList == null)
            {
                return false;
            }

            if (targetHero.EquipItemList.Count >= 5) //todo 可能不同角色的装备上限不同
            {
                return false;
            }

            ItemBagList.Remove(itemData);
            itemData.UniqueID = ++ItemUniqueIndex;//重新赋值uid，用作判断 装备时间 先后
            targetHero.EquipItemList.Add(itemData);
            targetHero.EquipItemList.Sort(CompareDRItem);
            targetHero.OnChangeEquipItem();
            return true;
        }
        // 比较函数
        private int CompareDRItem(OneItemData a, OneItemData b)
        {
            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            if (itemTable == null)
            {
                return 0;
            }

            var itemDataA = itemTable[a.ItemID];
            var itemDataB = itemTable[b.ItemID];
            var skillTable = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
            // 第一规则：SkillID==0的排在前面
            int skillA = itemDataA.SkillID == 0 ? 0 : 1;
            int skillB = itemDataB.SkillID == 0 ? 0 : 1;
            if (skillA != skillB)
                return skillA - skillB;

            // 第二规则：按 SkillType 排序
            if (itemDataA.SkillID != 0 && itemDataB.SkillID != 0)
            {
                var typeA = skillTable[itemDataA.SkillID].SkillType;
                var typeB = skillTable[itemDataB.SkillID].SkillType;
                int typeOrderA = GetSkillTypeOrder(typeA);
                int typeOrderB = GetSkillTypeOrder(typeB);
                if (typeOrderA != typeOrderB)
                    return typeOrderA - typeOrderB;
            }
            // 第三规则：按 UID 排序
            return a.UniqueID - b.UniqueID;
        }
        int GetSkillTypeOrder(int type)
        {
            return type switch
            {
                (int)SkillType.PassiveSkill => 0,
                (int)SkillType.NoAnimSkill => 1,
                (int)SkillType.NormalSkill => 2,
                _ => int.MaxValue // 其他未定义类型排在最后
            };
        }
        public bool TryRemoveEquip(int heroUID, int equipItemUID)
        {
            EntityQizi targetHero = null;
            foreach (var oneHero in SelfHeroList)
            {
                if (oneHero.HeroUID == heroUID)
                {
                    targetHero = oneHero;
                    break;
                }
            }

            if (targetHero.EquipItemList == null)
            {
                return false;
            }

            OneItemData targetItem = null;
            foreach (var oneEquipItem in targetHero.EquipItemList)
            {
                if (oneEquipItem.UniqueID == equipItemUID)
                {
                    targetItem = oneEquipItem;
                    break;
                }
            }
            targetHero.EquipItemList.Remove(targetItem);
            targetHero.OnChangeEquipItem();
            AddOneItemToBag(targetItem);
            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns>false表示通关。true表示 还有关卡</returns>
        public bool PassCurPoint()
        {
            if (CurAreaPoint == null)
            {
                return false;
            }

            CurAreaPoint.CurPassState = AreaPoint.PointPassState.Pass;
            foreach (var linkPointIndex in CurAreaPoint.LinkPointList)
            {
                var linkPoint = GetPoint(linkPointIndex);
                if (linkPoint != null && linkPoint.CurPassState == AreaPoint.PointPassState.Lock)
                {
                    linkPoint.CurPassState = AreaPoint.PointPassState.Unlock;
                    linkPoint.CanSee = true;
                }
            }

            GameEntry.Event.Fire(this, MapFreshEventArgs.Create());
            if (!CurAreaList.Exists(point => point.CurPassState == AreaPoint.PointPassState.Unlock))
            {
                return false;
                //通关
            }

            return true;
        }

        public AreaPoint GetPoint(int x, int y)
        {
            foreach (var onePoint in CurAreaList)
            {
                if (onePoint.PosObsolete.x == x && onePoint.PosObsolete.y == y)
                {
                    return onePoint;
                }
            }

            return null;
        }

        public AreaPoint GetPoint(int index)
        {
            foreach (var onePoint in CurAreaList)
            {
                if (onePoint.Index == index)
                {
                    return onePoint;
                }
            }

            return null;
        }

        public void TryAddCoin(int changeNum)
        {
            CoinNum += changeNum;
            CoinNum = Math.Max(0, CoinNum);
            GameEntry.Event.Fire(this, FreshCoinNumArg.Create());
        }

        public int GetOneRandomLevelIDFormType(MazePointType pointType)
        {
            var levelConfigTable = GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
            List<DRLevelConfig> allMeetList = ListPool<DRLevelConfig>.Get();
            foreach (var oneLevelConfig in levelConfigTable.GetAllDataRows())
            {
                if (oneLevelConfig.MazePointType == (int)pointType)
                {
                    allMeetList.Add(oneLevelConfig);
                }
            }

            var levelIndex = Utility.Random.GetRandom(allMeetList.Count);
            var retID = allMeetList[levelIndex].Id;
            ListPool<DRLevelConfig>.Release(allMeetList);
            return retID;
        }
    }
}