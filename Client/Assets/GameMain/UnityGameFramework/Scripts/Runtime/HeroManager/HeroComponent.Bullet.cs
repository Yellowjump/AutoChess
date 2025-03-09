//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System.Collections.Generic;
using DataTable;
using Entity;
using Entity.Bullet;
using SkillSystem;
using UnityEngine.Pool;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
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
            List<BulletBase> tempBulletList = ListPool<BulletBase>.Get();
            //先轮询己方棋子，后续联机的话需要判断 玩家uid来确定先后
            tempBulletList.AddRange(BulletList);
            foreach (var oneBullet in tempBulletList)
            {
                oneBullet.LogicUpdate(elapseSeconds, realElapseSeconds);
            }
            ListPool<BulletBase>.Release(tempBulletList);
        }
        private void ClearBullet()
        {
            for (int i = BulletList.Count - 1; i >= 0; i--)
            {
                DestoryBullet(BulletList[i]);
            }
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
        }
    }
}