using SkillSystem;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Entity.Bullet
{
    public class BulletRotateOwner : BulletBase
    {
        private static readonly ProfilerMarker HitScanMarker =
            new ProfilerMarker("Combat.Bullet.Orbit.HitScan");

        public int StartAngle;

        internal override void BindRuntime(int handle, int parameter)
        {
            base.BindRuntime(handle, parameter);
            StartAngle = parameter;
        }

        public override void InitGObj()
        {
            base.InitGObj();
        }

        protected override void OnGetHeroGObjCallback(GameObject obj, string path)
        {
            using var visualSample = VisualReadyMarker.Auto();
            GObj = obj;
            UpdateVisual();
        }

        public override void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            LogicUpdate(elapseSeconds, realElapseSeconds, null);
        }

        public void LogicUpdate(float elapseSeconds, float realElapseSeconds,
            CombatProjectileMotionBatch motion)
        {
            LogicUpdate(elapseSeconds, realElapseSeconds, motion, null);
        }

        public void LogicUpdate(float elapseSeconds, float realElapseSeconds,
            CombatProjectileMotionBatch motion,
            CombatOrbitHitCandidateBatch candidates)
        {
            if (m_ProjectileHandle == 0)
            {
                return;
            }
            if (Target == null || Caster.IsValid == false)
            {
                OnDead();
                return;
            }
            int executingHandle = m_ProjectileHandle;
            base.LogicUpdate(elapseSeconds, realElapseSeconds);
            if (m_ProjectileHandle != executingHandle)
            {
                return;
            }
            Vector3 center = Target.LogicHitPosition;
            bool completed;
            float beforeAngle;
            float changeAngle;
            if (motion == null || !motion.TryApplyOrbit(m_ProjectileHandle,
                    out completed, out beforeAngle, out changeAngle))
                completed = CombatProjectileBridge.StepOrbit(m_ProjectileHandle,
                    elapseSeconds, center.x, center.y, center.z,
                    out beforeAngle, out changeAngle);
            using (HitScanMarker.Auto())
            {
                var opposingUids = ListPool<int>.Get();
                try
                {
                    bool usedCandidates = false;
                    if (candidates != null && candidates.HasCandidates)
                    {
                        CombatProjectileBridge.GetOrbitScanState(m_ProjectileHandle,
                            out float centerX, out float centerZ,
                            out float minimumRange, out float maximumRange);
                        usedCandidates = candidates.TryCollect(
                            m_ProjectileHandle, (int)BelongCamp,
                            centerX, centerZ, minimumRange, maximumRange,
                            beforeAngle, changeAngle, opposingUids);
                    }
                    if (!usedCandidates)
                        CombatTargetQueries.CollectOpposingGroup(
                            (int)BelongCamp, opposingUids);
                    foreach (int uid in opposingUids)
                    {
                        if (m_ProjectileHandle != executingHandle)
                        {
                            break;
                        }
                        EntityQizi enemy = HotGameEntry.HeroManager.GetEntityByUID(uid);
                        if (enemy == null || !enemy.IsValid)
                        {
                            continue;
                        }
                        if (CombatProjectileBridge.IsOrbitHit(m_ProjectileHandle,
                                enemy.CombatUnitHandle, beforeAngle, changeAngle))
                        {
                            TriggerHitTarget(enemy);
                        }
                    }
                }
                finally
                {
                    ListPool<int>.Release(opposingUids);
                }
            }

            if (completed && m_ProjectileHandle == executingHandle)
            {
                OnDead();
            }
        }

        public override void UpdateVisual()
        {
            base.UpdateVisual();
            if (GObj != null)
            {
                GObj.transform.rotation = Quaternion.Euler(0,
                    CombatProjectileBridge.GetOrbitAngle(m_ProjectileHandle) + 90, 0);
            }
        }

        public override void Clear()
        {
            base.Clear();
            StartAngle = 0;
        }
    }
}
