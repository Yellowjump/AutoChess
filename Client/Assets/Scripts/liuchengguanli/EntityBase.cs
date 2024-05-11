using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace liuchengguanli
{
    /// <summary>
    /// 所有在棋盘上有的GameObject的管理实体 基类，包括英雄，子弹，炮台，召唤物
    /// </summary>
    public class EntityBase
    {
        public int Index;//保存Object的编号
        public GameObject GObj;
        public CampType BelongCamp;
        public virtual void Init(int index)
        {
            Log.Info("hfk:base");
        }
        public virtual void AddBuff(Buff buff)
        {
            
        }

        public float GetDistanceSquare(EntityBase target)
        {
            float distanceSquare = (GObj.transform.position.x - target.GObj.transform.position.x) * (GObj.transform.position.x - target.GObj.transform.position.x) + (GObj.transform.position.z - target.GObj.transform.position.z) * (GObj.transform.position.z - target.GObj.transform.position.z);
            return distanceSquare;
        }
    }
}