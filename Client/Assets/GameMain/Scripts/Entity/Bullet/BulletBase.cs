using DataTable;
using GameFramework;
using SkillSystem;
using Unity.VisualScripting;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity.Bullet
{
    public class BulletBase:EntityBase,IReference
    {
        public int BulletID;
        public EntityQizi Caster;//创建者
        public EntityQizi Target;
        public TriggerList OwnerTriggerList;
        public DRBullet CurBulletData;
        public virtual void SetParamValue(TableParamInt[] paramIntArray)
        {
            
        }
        public override void InitGObj()
        {
            OwnerTriggerList?.OnTrigger(TriggerType.OnActive);
            GameEntry.HeroManager.GetBulletObjByID(BulletID,OnGetHeroGObjCallback);
        }
        protected virtual void OnGetHeroGObjCallback(GameObject obj,string path)
        {
            GObj = obj;
            GObj.transform.position = LogicPosition;
        }
        public virtual void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            //各自的飞行逻辑
            OwnerTriggerList?.OnTrigger(TriggerType.PerTick);
        }

        public virtual void OnHitTarget()
        {
            OwnerTriggerList?.OnTrigger(TriggerType.OnBulletHitTarget,Target);
            OnDead();
        }

        public virtual void OnDead()
        {
            OwnerTriggerList?.OnTrigger(TriggerType.OnDestory);
            GameEntry.HeroManager.DestoryBullet(this);
        }

        public virtual void Clear()
        {
            GameEntry.HeroManager.ReleaseBulletGameObject(BulletID,GObj,OnGetHeroGObjCallback);
            BulletID = 0;
            GObj = null;
            Caster = null;
            Target = null;
            OwnerTriggerList = null;
        }
    }
}