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
        public GameObject GObj;
        public CampType BelongCamp;
        public bool IsValid = true;
        private Vector3 _logicPosition;
        public Vector3 LogicPosition
        {
            get =>_logicPosition;
            set
            {
                _logicPosition = value;
                if (GObj != null)
                {
                    GObj.transform.position = value;
                }
            }
        }

        public virtual Vector3 LogicHitPosition => _logicPosition;
        public virtual void Init(int index)
        {
            Log.Info("hfk:base");
        }

        public virtual void InitGObj()
        {
            
        }
        public virtual void AddBuff(Buff buff)
        {
            
        }
        public virtual void RemoveBuff(int buffID)
        {
            
        }

        public virtual void AddSfx(int sfxID)
        {
            
        }

        public virtual void RemoveSfx(int sfxID)
        {
            
        }

        public float GetDistanceSquare(EntityBase target)
        {
            float distanceSquare = (LogicPosition.x - target.LogicPosition.x) * (LogicPosition.x - target.LogicPosition.x) + (LogicPosition.z - target.LogicPosition.z) * (LogicPosition.z - target.LogicPosition.z);
            return distanceSquare;
        }
    }
}