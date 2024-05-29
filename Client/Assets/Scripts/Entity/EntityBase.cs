using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    /// <summary>
    /// 所有在棋盘上有的GameObject的管理实体 基类，包括英雄，子弹，炮台，召唤物
    /// </summary>
    public class EntityBase
    {
        public int Index;//保存Object的编号
        public GameObject GObj;
        public CampType BelongCamp;

        private Vector3 _logicPosition;
        public Vector3 LogicPosition
        {
            get =>_logicPosition;
            set
            {
                _logicPosition = value;
                GObj.transform.position = value;
            }
        }

        public virtual void Init(int index)
        {
            Log.Info("hfk:base");
        }
        public virtual void AddBuff(Buff buff)
        {
            
        }

        public float GetDistanceSquare(EntityBase target)
        {
            float distanceSquare = (LogicPosition.x - target.LogicPosition.x) * (LogicPosition.x - target.LogicPosition.x) + (LogicPosition.z - target.LogicPosition.z) * (LogicPosition.z - target.LogicPosition.z);
            return distanceSquare;
        }
    }
}