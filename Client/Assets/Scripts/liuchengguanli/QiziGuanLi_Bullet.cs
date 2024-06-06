using System.Collections.Generic;
using DataTable;
using Entity.Bullet;
using GameFramework;
using SkillSystem;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

public partial class QiziGuanLi
{
    public List<BulletBase> BulletList = new List<BulletBase>();
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
                default:
                    ret = ReferencePool.Acquire<BulletTracking>(); 
                    break;
            }
            BulletList.Add(ret);
            ret.BulletID = bulletID;
            ret.GObj = Pool.instance.GetNewBulletGameObject(bulletData.AssetPathID);
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
        var bulletTable = GameEntry.DataTable.GetDataTable<DRBullet>("Bullet");
        if (bulletTable != null && bulletTable.HasDataRow(bullet.BulletID))
        {
            var bulletData = bulletTable[bullet.BulletID];
            Pool.instance.ReleaseBulletGameObject(bullet.GObj,bulletData.AssetPathID);
        }
        ReferencePool.Release(bullet);
    }
    public void OnLogicUpdateBullet(float elapseSeconds, float realElapseSeconds)
    {
        List<BulletBase> tempBulletList = ListPool<BulletBase>.Get();
        //先轮询己方棋子，后续联机的话需要判断 玩家uid来确定先后
        tempBulletList.AddRange(BulletList);
        foreach (var oneBullet in tempBulletList)
        {
            oneBullet.LogicUpdate(elapseSeconds,realElapseSeconds);
        }
    }
}