using GameFramework;
using SkillSystem;
using UnityEngine;
using Zizouqi.CombatDots;

namespace Entity.Bullet
{
    public class BulletTracking:BulletBase
    {
        public override void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            LogicUpdate(elapseSeconds, realElapseSeconds, null);
        }

        public void LogicUpdate(float elapseSeconds, float realElapseSeconds,
            CombatProjectileMotionBatch motion)
        {
            if (m_ProjectileHandle == 0)
            {
                return;
            }
            int executingHandle = m_ProjectileHandle;
            base.LogicUpdate(elapseSeconds,realElapseSeconds);
            if (m_ProjectileHandle != executingHandle)
            {
                return;
            }
            if (Target == null)
            {
                OnDead();
                return;
            }
            CombatProjectileStep step;
            if (motion == null || !motion.TryApplyTracking(m_ProjectileHandle,
                    out step))
                step = CombatProjectileBridge.StepTracking(m_ProjectileHandle,
                    elapseSeconds, ConstValue.EntityQiziHeight);
            if (step == CombatProjectileStep.TargetLost)
            {
                OnDead();
                return;
            }
            if (step == CombatProjectileStep.Hit)
            {
                OnHitTarget(Target);//命中目标
                return;
            }
        }

        public override void UpdateVisual()
        {
            base.UpdateVisual();
            if (GObj != null && Target != null)
                GObj.transform.LookAt(Target.LogicHitPosition);
        }
    }
}
