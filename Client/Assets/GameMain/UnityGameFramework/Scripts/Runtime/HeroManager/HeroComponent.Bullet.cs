//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System.Collections.Generic;
using System.Linq;
using DataTable;
using Entity;
using Entity.Bullet;
using GameMain.Scripts.JobSystem;
using SkillSystem;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        public struct BulletData
        {
            public float3 LogicPosition;
            public float3 TargetPosition;
            public float MoveSpeed;
            public int BulletIndex;
            public byte HasHit; // 0 = 未命中, 1 = 命中
        }
        public List<BulletBase> BulletList = new List<BulletBase>();
        public List<EntityPosPoint> PosPointList = new List<EntityPosPoint>();
        public BulletBase CreateBullet(int bulletID)
        {
            var bullet = GameEntry.DataTable.GetDataTable<DRBullet>("Bullet");
            if (bullet != null && bullet.HasDataRow(bulletID))
            {
                var bulletData = bullet[bulletID];
                BulletBase ret;
                switch ((BulletType)bulletData.BulletType)
                {
                    case BulletType.TrackingBullet:
                        ret = ReferencePool.Acquire<BulletTracking>();
                        break;
                    case BulletType.RotateOwner:
                        ret = ReferencePool.Acquire<BulletRotateOwner>();
                        break;
                    case BulletType.PenetratingBullet:
                        ret = ReferencePool.Acquire<BulletPenetrating>();
                        break;
                    case BulletType.MarkPoint:
                        ret = ReferencePool.Acquire<BulletMarkPoint>();
                        break;
                    default:
                        ret = ReferencePool.Acquire<BulletTracking>();
                        break;
                }

                BulletList.Add(ret);
                ret.BulletID = bulletID;
                ret.CurBulletData = bulletData;
                return ret;
            }

            return null;
        }

        public void DestoryBullet(BulletBase bullet)
        {
            if (bullet == null)
            {
                return;
            }

            BulletList?.Remove(bullet);
            ReferencePool.Release(bullet);
        }

        public void OnLogicUpdateBullet(float elapseSeconds, float realElapseSeconds)
        {
            UpdateTrackingBulletFly();
            List<BulletBase> tempBulletList = ListPool<BulletBase>.Get();
            //先轮询己方棋子，后续联机的话需要判断 玩家uid来确定先后
            tempBulletList.AddRange(BulletList);
            foreach (var oneBullet in tempBulletList)
            {
                if (oneBullet is BulletTracking)
                {
                    continue;
                }
                oneBullet.LogicUpdate(elapseSeconds, realElapseSeconds);
            }
            ListPool<BulletBase>.Release(tempBulletList);
        }
        private void UpdateTrackingBulletFly()
        {
            float dt = Time.deltaTime;
            List<BulletTracking> trackingBullets = BulletList
                .OfType<BulletTracking>()
                .ToList();
            // Step 1: 准备 Job 输入数据
            NativeArray<BulletData> bulletArray = new NativeArray<BulletData>(trackingBullets.Count, Allocator.TempJob);

            for (int i = 0; i < trackingBullets.Count; i++)
            {
                var bullet = trackingBullets[i];

                if (bullet.Target == null || bullet.Target.IsValid == false)
                {
                    bullet.OnDead();
                    continue;
                }

                bullet.TargetPosition = bullet.Target.LogicHitPosition;

                bulletArray[i] = new BulletData
                {
                    LogicPosition = bullet.LogicPosition,
                    TargetPosition = bullet.TargetPosition,
                    MoveSpeed = bullet.MoveSpeed,
                    BulletIndex = i,
                    HasHit = 0
                };
            }

            // Step 2: 调度 Job
            var job = new TrackingBulletFlyJob
            {
                ElapsedTime = dt,
                Bullets = bulletArray
            };

            JobHandle handle = job.Schedule(trackingBullets.Count, 32);
            handle.Complete();

            // Step 3: 应用计算结果
            for (int i = 0; i < trackingBullets.Count; i++)
            {
                var bullet = trackingBullets[i];
                var result = bulletArray[i];

                bullet.ApplyLogicResult(result.LogicPosition, result.HasHit == 1);
            }

            bulletArray.Dispose();
        }
        private void ClearBullet()
        {
            for (int i = BulletList.Count - 1; i >= 0; i--)
            {
                DestoryBullet(BulletList[i]);
            }
            BulletList.Clear();
        }

        public EntityPosPoint CreatePosPoint()
        {
            var newPos = ReferencePool.Acquire<EntityPosPoint>();
            PosPointList.Add(newPos);
            return newPos;
        }
        public void OnLogicUpdatePosUpdate(float elapseSeconds, float realElapseSeconds)
        {
            List<EntityPosPoint> tempPosEntity = ListPool<EntityPosPoint>.Get();
            tempPosEntity.AddRange(PosPointList);
            foreach (var onePosPoint in tempPosEntity)
            {
                onePosPoint.LogicUpdate(elapseSeconds, realElapseSeconds);
                if (onePosPoint.IsValid == false)
                {
                    DestoryPosPoint(onePosPoint);
                }
            }
            ListPool<EntityPosPoint>.Release(tempPosEntity);
        }
        public void DestoryPosPoint(EntityPosPoint posPoint)
        {
            if (posPoint == null)
            {
                return;
            }
            PosPointList?.Remove(posPoint);
            ReferencePool.Release(posPoint);
        }
        private void ClearPosPoint()
        {
            for (int i = PosPointList.Count - 1; i >= 0; i--)
            {
                DestoryPosPoint(PosPointList[i]);
            }
            PosPointList.Clear();
        }
    }
}