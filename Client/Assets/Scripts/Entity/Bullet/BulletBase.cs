using GameFramework;
using SkillSystem;
using Unity.VisualScripting;

namespace Entity.Bullet
{
    public class BulletBase:EntityBase,IReference
    {
        public int BulletID;
        public EntityQizi Caster;//创建者
        public EntityQizi Target;
        public TriggerList OwnerTriggerList;
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
            QiziGuanLi.instance.DestoryBullet(this);
        }

        public void Clear()
        {
            Caster = null;
            Target = null;
            OwnerTriggerList = null;
        }
    }
}