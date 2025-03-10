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
            qizi.rowIndex = oneInfo.Pos.y;
            qizi.columnIndex = oneInfo.Pos.x;
            DirenList.Add(qizi);
            qige[qizi.rowIndex][qizi.columnIndex] = qizi.HeroUID;
            qizi.LogicPosition = GetGeziPos(qizi.rowIndex, qizi.columnIndex);
            qizi.InitGObj();
        }

        public EntityQizi AddNewFriendHero(int heroID, int row = -1, int column = -1)
        {
            EntityQizi qizi = GameEntry.HeroManager.GetNewEntityQizi();
            qizi.BelongCamp = CampType.Friend;
            qizi.Init(heroID);
            var emptyPos = new Vector2Int(column, row);
            if (row == -1)
            {
                emptyPos = GetEmptyFriendPos();
            }

            qizi.rowIndex = emptyPos.y;
            qizi.columnIndex = emptyPos.x;
            QiziCSList.Add(qizi);
            qige[qizi.rowIndex][qizi.columnIndex] = qizi.HeroUID;
            qizi.LogicPosition = GetGeziPos(qizi.rowIndex, qizi.columnIndex);
            //qizi.InitGObj();
            return qizi;
        }

        public void FreshFriendEntityPos()
        {
            foreach (var oneEntity in QiziCSList)
            {
                oneEntity.LogicPosition = GetGeziPos(oneEntity.rowIndex, oneEntity.columnIndex);
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

        public List<EntityQizi> GetEnemyList(CampType ownerCamp)
        {
            return ownerCamp == CampType.Friend ? DirenList : QiziCSList;
        }

        public bool GetNearestTarget(EntityBase source, CampType targetCamp, out EntityQizi target, int skillRange)
        {
            target = null;
            List<EntityQizi> waitCheckList = ListPool<EntityQizi>.Get();
            if ((source.BelongCamp == CampType.Friend && targetCamp == CampType.Enemy) || (source.BelongCamp == CampType.Enemy && targetCamp == CampType.Friend) || targetCamp == CampType.Both)
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
                if (newDistanceSquare < minDistanceSquare)
                {
                    target = oneQizi;
                    minDistanceSquare = newDistanceSquare;
                }
            }

            ListPool<EntityQizi>.Release(waitCheckList);
            if (Utility.TruncateFloat(minDistanceSquare, 3) < skillRange * skillRange)
            {
                return true;
            }

            return false;
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
                ret.AddRange(DirenList);
            }

            if ((sourceCamp == CampType.Friend && targetBelongType == CampType.Friend) || (sourceCamp == CampType.Enemy && targetBelongType == CampType.Enemy) || targetBelongType == CampType.Both)
            {
                ret.AddRange(QiziCSList);
            }
        }

        public EntityQizi GetEntityByUID(int uid)
        {
            foreach (var oneEntity in QiziCSList)
            {
                if (oneEntity.HeroUID == uid)
                {
                    return oneEntity;
                }
            }

            foreach (var oneEntity in DirenList)
            {
                if (oneEntity.HeroUID == uid)
                {
                    return oneEntity;
                }
            }

            return null;
        }

        #region 求最多散点覆盖圆圆心坐标

        /// <summary>
        /// 极角区间数据类，使用对象池管理
        /// </summary>
        private class AngleInterval : IReference
        {
            public double StartAngle;
            public double EndAngle;
            public bool IsSplit;

            public void Clear()
            {
                StartAngle = 0;
                EndAngle = 0;
                IsSplit = false;
            }
        }
        public class AngleEvent : IReference,IComparable<AngleEvent>
        {
            public double Angle;
            public bool IsStart; // true=覆盖开始，false=覆盖结束

            public AngleEvent()
            {
                
            }
            public AngleEvent Init(double angle, bool isStart)
            {
                Angle = angle;
                IsStart = isStart;
                return this;
            }
            public AngleEvent(double angle, bool isStart)
            {
                Angle = angle;
                IsStart = isStart;
            }

            public int CompareTo(AngleEvent other) => Angle.CompareTo(other.Angle);
            public void Clear()
            {
                Angle = 0;
                IsStart = true;
            }
        }
        private const double Epsilon = 1e-6;
        private const double Rad2Deg = 180.0 / Math.PI;

        /// <summary>
        /// 寻找最优圆心位置
        /// </summary>
        public Vector3 GetCrowdedPos(List<Vector3> pointList, float radius)
        {
            if (pointList == null || pointList.Count == 0)
                return Vector3.zero;

            Vector3 bestCenter = Vector3.zero;
            int maxCount = 0;

            foreach (var basePoint in pointList)
            {
                var intervals = ListPool<AngleInterval>.Get();

                // 为当前基准点生成所有极角区间
                GenerateAngleIntervals(basePoint, pointList, radius, intervals);

                // 扫描获取最大覆盖数
                int currentMax = ScanAngles(intervals, radius,out Vector3 candidate);

                if (currentMax > maxCount)
                {
                    maxCount = currentMax;
                    bestCenter = candidate + basePoint;
                }

                // 归还对象到池
                foreach (var interval in intervals)
                {
                    interval.Clear();
                }
                ListPool<AngleInterval>.Release(intervals);
            }

            return bestCenter;
        }

        /// <summary>
        /// 生成极角区间数据
        /// </summary>
        private void GenerateAngleIntervals(Vector3 basePoint, List<Vector3> points,
            float radius, List<AngleInterval> intervals)
        {
            foreach (var p in points)
            {
                if (p == basePoint) continue;

                double dx = p.x - basePoint.x;
                double dy = p.z - basePoint.z; // 假设使用XZ平面
                double distanceSq = dx * dx + dy * dy;

                // 距离超过直径无法相交
                if (distanceSq > 4 * radius * radius) continue;

                double distance = Math.Sqrt(distanceSq);
                double angle = Math.Atan2(dy, dx) * Rad2Deg;
                angle = (angle + 360) % 360; // 规范化到[0,360)

                double halfAngle = Math.Acos(distance / (2 * radius)) * Rad2Deg;

                // 创建区间对象
                var interval = ReferencePool.Acquire<AngleInterval>();
                interval.StartAngle = (angle - halfAngle + 360) % 360;
                interval.EndAngle = (angle + halfAngle) % 360;
                interval.IsSplit = interval.StartAngle > interval.EndAngle;

                intervals.Add(interval);
            }
        }

        /// <summary>
        /// 角度扫描核心算法
        /// </summary>
        private int ScanAngles(List<AngleInterval> intervals,float radius, out Vector3 bestPoint)
        {
            List<AngleEvent> events = ListPool<AngleEvent>.Get();

            // 构建事件列表
            foreach (var interval in intervals)
            {
                if (interval.IsSplit)
                {
                    events.Add(ReferencePool.Acquire<AngleEvent>().Init(interval.StartAngle, true));
                    events.Add(ReferencePool.Acquire<AngleEvent>().Init(360.0, false));
                    events.Add(ReferencePool.Acquire<AngleEvent>().Init(0.0, true));
                    events.Add(ReferencePool.Acquire<AngleEvent>().Init(interval.EndAngle, false));
                }
                else
                {
                    events.Add(ReferencePool.Acquire<AngleEvent>().Init(interval.StartAngle, true));
                    events.Add(ReferencePool.Acquire<AngleEvent>().Init(interval.EndAngle, false));
                }
            }

            // 按角度排序事件点
            events.Sort((a, b) => a.Angle.CompareTo(b.Angle));

            int maxCount = 1; // 至少包含基准点
            int currentCount = 0;
            double bestAngle = 0;

            foreach (var e in events)
            {
                currentCount += e.IsStart ? 1 : -1;
                if (currentCount > maxCount)
                {
                    maxCount = currentCount;
                    bestAngle = e.Angle;
                }
            }

            foreach (var oneAngle in events)
            {
                ReferencePool.Release(oneAngle);
            }
            ListPool<AngleEvent>.Release(events);
            // 计算最佳点坐标（需要根据实际基准点和半径转换）
            bestPoint = CalculatePointFromAngle(bestAngle,radius);
            return maxCount;
        }

        private Vector3 CalculatePointFromAngle(double angle,float radius)
        {
            double radians = angle * Math.PI / 180.0;
            // 计算相对于基准点的偏移量（XZ平面）
            double dx = Math.Cos(radians) * radius;
            double dz = Math.Sin(radians) * radius;
            return new Vector3((float)dx, 0, (float)dz);
        }

        #endregion
    }
}