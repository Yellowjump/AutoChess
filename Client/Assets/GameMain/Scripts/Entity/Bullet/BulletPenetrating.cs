using System.Collections.Generic;
using GameFramework;
using SkillSystem;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Entity.Bullet
{
    public class BulletPenetrating : BulletBase
    {
        private static readonly ProfilerMarker HitScanMarker =
            new ProfilerMarker("Combat.Bullet.Penetrating.HitScan");
        private static readonly ProfilerMarker IndexedScanMarker =
            new ProfilerMarker("Combat.Bullet.Penetrating.IndexedScan");
        private static readonly ProfilerMarker BatchCandidateMarker =
            new ProfilerMarker("Combat.Bullet.Penetrating.BatchCandidates");
        private static readonly ProfilerMarker LiveScanMarker =
            new ProfilerMarker("Combat.Bullet.Penetrating.LiveScan");
        private static readonly ProfilerMarker BezierFallbackMarker =
            new ProfilerMarker("Combat.Bullet.Penetrating.BezierFallback");


        public override void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            LogicUpdate(elapseSeconds, realElapseSeconds, null, null, null);
        }

        public void LogicUpdate(float elapseSeconds, float realElapseSeconds,
            CombatProjectileBroadphase broadphase, CombatBezierStepBatch bezierSteps,
            CombatProjectileHitCandidateBatch hitCandidates)
        {
            if (m_ProjectileHandle == 0)
            {
                return;
            }
            int executingHandle = m_ProjectileHandle;
            base.LogicUpdate(elapseSeconds, realElapseSeconds);
            if (m_ProjectileHandle != executingHandle)
            {
                return;
            }
            Vector3 segmentStart = LogicPosition;
            bool completed;
            Vector3 scanPosition;
            if (bezierSteps == null || !bezierSteps.TryApply(m_ProjectileHandle,
                    out completed, out scanPosition,
                    out _, out _, out _))
            {
                using (BezierFallbackMarker.Auto())
                {
                    Vector3 currentTarget =
                        CombatProjectileBridge.BezierTracksTarget(m_ProjectileHandle)
                            ? Target.LogicHitPosition : Vector3.zero;
                    completed = CombatProjectileBridge.StepBezier(m_ProjectileHandle,
                        elapseSeconds, currentTarget.x, currentTarget.y, currentTarget.z,
                        out _, out _, out _);
                }
                scanPosition = LogicPosition;
            }
            using (HitScanMarker.Auto())
            {
                int sourceCamp = (int)BelongCamp;
                List<int> nearbyUids = ListPool<int>.Get();
                bool useBatch;
                using (BatchCandidateMarker.Auto())
                {
                    useBatch = hitCandidates != null && hitCandidates.TryCollect(
                        executingHandle, sourceCamp, segmentStart.x, segmentStart.z,
                        scanPosition.x, scanPosition.z, nearbyUids);
                }
                bool useBroadphase = !useBatch && broadphase != null &&
                    broadphase.IsCurrent;
                List<int> opposingUids = useBatch || useBroadphase
                    ? null : ListPool<int>.Get();
                try
                {
                    if (useBatch || useBroadphase)
                    {
                        if (!useBatch)
                        {
                            broadphase.CollectNearbySegment(sourceCamp,
                                segmentStart.x, segmentStart.z,
                                scanPosition.x, scanPosition.z, nearbyUids);
                        }
                        int lastUid = int.MinValue;
                        using (IndexedScanMarker.Auto())
                        {
                            foreach (int uid in nearbyUids)
                            {
                                if (m_ProjectileHandle != executingHandle)
                                {
                                    break;
                                }
                                lastUid = uid;
                                float beforeX = scanPosition.x;
                                float beforeZ = scanPosition.z;
                                if (TryHitEnemy(uid, executingHandle,
                                        ref segmentStart, ref scanPosition) &&
                                    (scanPosition.x != beforeX || scanPosition.z != beforeZ ||
                                     (useBatch ? !hitCandidates.IsCurrent :
                                         !broadphase.IsCurrent)))
                                {
                                    useBroadphase = false;
                                    useBatch = false;
                                    break;
                                }
                            }
                        }
                        if (!useBatch && !useBroadphase &&
                            m_ProjectileHandle == executingHandle)
                        {
                            using (LiveScanMarker.Auto())
                            {
                                List<int> liveUids = ListPool<int>.Get();
                                try
                                {
                                    CombatTargetQueries.CollectOpposingGroup(sourceCamp, liveUids);
                                    foreach (int uid in liveUids)
                                    {
                                        if (m_ProjectileHandle != executingHandle)
                                        {
                                            break;
                                        }
                                        if (uid > lastUid)
                                        {
                                            TryHitEnemy(uid, executingHandle,
                                                ref segmentStart, ref scanPosition);
                                        }
                                    }
                                }
                                finally
                                {
                                    ListPool<int>.Release(liveUids);
                                }
                            }
                        }
                    }
                    else
                    {
                        CombatTargetQueries.CollectOpposingGroup(sourceCamp, opposingUids);
                        using (LiveScanMarker.Auto())
                        {
                            foreach (int uid in opposingUids)
                            {
                                if (m_ProjectileHandle != executingHandle)
                                {
                                    break;
                                }
                                TryHitEnemy(uid, executingHandle,
                                    ref segmentStart, ref scanPosition);
                            }
                        }
                    }
                }
                finally
                {
                    if (nearbyUids != null)
                    {
                        ListPool<int>.Release(nearbyUids);
                    }
                    if (opposingUids != null)
                    {
                        ListPool<int>.Release(opposingUids);
                    }
                }
            }

            if (completed && m_ProjectileHandle == executingHandle)
            {
                OnDead();
            }
        }

        private bool TryHitEnemy(int uid, int executingHandle,
            ref Vector3 segmentStart, ref Vector3 scanPosition)
        {
            EntityQizi enemy = HotGameEntry.HeroManager.GetEntityByUID(uid);
            if (enemy == null || !enemy.IsValid ||
                !CombatProjectileBridge.TryHitBezierAlongSegment(m_ProjectileHandle,
                    enemy.CombatUnitHandle, enemy.HeroUID,
                    segmentStart.x, segmentStart.z,
                    scanPosition.x, scanPosition.z))
            {
                return false;
            }
            OnHitTarget(enemy);
            if (m_ProjectileHandle == executingHandle)
            {
                Vector3 currentPosition = LogicPosition;
                if (currentPosition != scanPosition)
                {
                    scanPosition = currentPosition;
                    segmentStart = currentPosition;
                }
            }
            return true;
        }

        public override void OnHitTarget(EntityQizi target)
        {
            if (CombatTriggerProjectileSpawnQueue.TryEnqueueSimpleHit(this,
                    target)) return;
            if (!CombatTriggerProjectileSpawnQueue.TryEnqueue(OwnerTriggerList, target))
            {
                OwnerTriggerList?.OnTrigger(TriggerType.OnBulletHitTarget, target);
            }
        }

        public override void UpdateVisual()
        {
            base.UpdateVisual();
            if (GObj == null || m_ProjectileHandle == 0) return;
            Vector3 target = Target != null
                ? Target.LogicHitPosition : Vector3.zero;
            CombatProjectileBridge.GetBezierTangent(m_ProjectileHandle,
                target.x, target.y, target.z,
                out float x, out float y, out float z);
            Vector3 tangent = new Vector3(x, y, z);
            if (tangent != Vector3.zero)
                GObj.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }

        public override void Clear()
        {
            base.Clear();
        }
    }
}
