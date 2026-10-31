//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using GameFramework;
using System.Collections.Generic;
using Entity;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using Zizouqi.CombatDots;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        public void InitOneEnemy(OneEnemyInfo oneInfo)
        {
            if (m_EntitiesByUid.Count == 0)
            {
                CombatBoardBridge.Clear();
            }
            EntityQizi qizi = HotGameEntry.HeroManager.GetNewEntityQizi();
            qizi.BelongCamp = CampType.Enemy;
            qizi.Init(oneInfo.HeroID);
            DirenList.Add(qizi);
            m_EntitiesByUid.Add(qizi.HeroUID, qizi);
            CombatBoardBridge.SetOccupant(oneInfo.Pos.y, oneInfo.Pos.x, qizi.HeroUID);
            qizi.LogicPosition = GetGeziPos(oneInfo.Pos.y, oneInfo.Pos.x);
            qizi.InitGObj();
        }

        public EntityQizi AddNewFriendHero(int heroID, int row = -1, int column = -1, List<int> itemList = null)
        {
            if (m_EntitiesByUid.Count == 0)
            {
                CombatBoardBridge.Clear();
            }
            EntityQizi qizi = HotGameEntry.HeroManager.GetNewEntityQizi();
            qizi.BelongCamp = CampType.Friend;
            qizi.Init(heroID,itemList);
            var emptyPos = new Vector2Int(column, row);
            if (row == -1)
            {
                emptyPos = GetEmptyFriendPos();
            }

            QiziCSList.Add(qizi);
            m_EntitiesByUid.Add(qizi.HeroUID, qizi);
            CombatBoardBridge.SetOccupant(emptyPos.y, emptyPos.x, qizi.HeroUID);
            qizi.LogicPosition = GetGeziPos(emptyPos.y, emptyPos.x);
            CombatAttributeBridge.SetDeploymentCell(qizi.CombatUnitHandle,
                emptyPos.y, emptyPos.x);
            //qizi.InitGObj();
            return qizi;
        }

        public void FreshFriendEntityPos()
        {
            foreach (var oneEntity in QiziCSList)
            {
                if (CombatBoardBridge.FindOccupant(oneEntity.HeroUID,
                        out int row, out int column))
                {
                    oneEntity.LogicPosition = GetGeziPos(row, column);
                }
            }
        }

        public void InitFriendGObj()
        {
            foreach (var oneEntity in QiziCSList)
            {
                oneEntity.InitGObj();
            }
        }

        public void ReleaseFriendGObj()
        {
            foreach (var oneEntity in QiziCSList)
            {
                oneEntity.RemoveGObj();
            }
        }

        private Vector2Int GetEmptyFriendPos()
        {
            return CombatBoardBridge.FindFirstEmpty(out int row, out int column)
                ? new Vector2Int(column, row) : Vector2Int.one;
        }

        public void GetEntityQiziList(CampType targetBelongType,CampType sourceCamp,ref List<EntityQizi> ret)
        {
            if (ret == null)
            {
                return;
            }

            ret.Clear();
            
            if ((sourceCamp == CampType.Friend && targetBelongType == CampType.Enemy) || (sourceCamp == CampType.Enemy && targetBelongType == CampType.Friend) || targetBelongType == CampType.Both)
            {
                AppendCombatGroup(CampType.Enemy, ret);
            }

            if ((sourceCamp == CampType.Friend && targetBelongType == CampType.Friend) || (sourceCamp == CampType.Enemy && targetBelongType == CampType.Enemy) || targetBelongType == CampType.Both)
            {
                AppendCombatGroup(CampType.Friend, ret);
            }
        }

        private void AppendCombatGroup(CampType group, List<EntityQizi> entities)
        {
            List<int> heroUids = ListPool<int>.Get();
            Zizouqi.CombatDots.CombatAttributeBridge.CollectGroupUids((int)group, heroUids);
            foreach (int uid in heroUids)
            {
                if (m_EntitiesByUid.TryGetValue(uid, out EntityQizi entity))
                {
                    entities.Add(entity);
                }
            }
            ListPool<int>.Release(heroUids);
        }

        public EntityQizi GetEntityByUID(int uid)
        {
            return m_EntitiesByUid.TryGetValue(uid, out EntityQizi qizi) ? qizi : null;
        }

    }
}
