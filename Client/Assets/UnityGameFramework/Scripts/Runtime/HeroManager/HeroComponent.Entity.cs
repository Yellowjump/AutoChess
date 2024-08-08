//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System.Collections.Generic;
using Entity;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent 
    {
        public int QiziCurUniqueIndex = 0;
        public void InitOneEnemy(OneEnemyInfo oneInfo)
        {
            EntityQizi qizi = GameEntry.HeroManager.GetNewEntityQizi();
            qizi.BelongCamp = CampType.Enemy;
            qizi.Init(oneInfo.HeroID);
            qizi.rowIndex =oneInfo.Pos.y;
            qizi.columnIndex = oneInfo.Pos.x;
            DirenList.Add(qizi);
            qige[qizi.rowIndex][qizi.columnIndex] = qizi.HeroUID;
            qizi.LogicPosition =GetGeziPos(qizi.rowIndex, qizi.columnIndex);
            qizi.GObj.transform.rotation = Quaternion.Euler(new Vector3(0, -180, 0));
        }

        public EntityQizi AddNewFriendHero(int heroID)
        {
            EntityQizi qizi = GameEntry.HeroManager.GetNewEntityQizi();
            qizi.BelongCamp = CampType.Friend;
            qizi.Init(heroID);
            var emptyPos = GetEmptyFriendPos();
            qizi.rowIndex =emptyPos.y;
            qizi.columnIndex = emptyPos.x;
            QiziCSList.Add(qizi);
            qige[qizi.rowIndex][qizi.columnIndex] = qizi.HeroUID;
            qizi.LogicPosition =GetGeziPos(qizi.rowIndex, qizi.columnIndex);
            return qizi;
        }

        private Vector2Int GetEmptyFriendPos()
        {
            for (int y = 0; y < qige.Length; y++)
            {
                for (int x = 0; x < qige[y].Length; x++)
                {
                    if (qige[y][x] == -1)
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
            return Vector2Int.one;
        }
        public bool GetNearestTarget(EntityQizi source, CampType targetCamp, out EntityQizi target)
        {
            target = null;
            List<EntityQizi> waitCheckList = ListPool<EntityQizi>.Get();
            if ((source.BelongCamp == CampType.Friend&&targetCamp == CampType.Enemy)||(source.BelongCamp == CampType.Enemy&&targetCamp == CampType.Friend)||targetCamp == CampType.Both)
            {
                waitCheckList.AddRange(DirenList);
            }

            if ((source.BelongCamp == CampType.Friend && targetCamp == CampType.Friend) || (source.BelongCamp == CampType.Enemy && targetCamp == CampType.Enemy) || targetCamp == CampType.Both)
            {
                waitCheckList.AddRange(QiziCSList);
            }
            float minDistanceSquare = float.MaxValue;
            foreach (var oneQizi in waitCheckList)
            {
                if (oneQizi.IsValid == false)
                {
                    continue;
                }
                var newDistanceSquare = oneQizi.GetDistanceSquare(source);
                if (newDistanceSquare< minDistanceSquare)
                {
                    target = oneQizi;
                    minDistanceSquare = newDistanceSquare;
                }
            }
            if (Utility.TruncateFloat(minDistanceSquare,4) <source.gongjiDistence*source.gongjiDistence)
            {
                return true;
            }
            return false;
        }
    }
}
