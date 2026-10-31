using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.Pool;

namespace Zizouqi.CombatDots
{
    // Preserve friendly-then-enemy unit order and projectile spawn order.
    // The hot-update callback handles FSM and presentation; eligible hit intents
    // commit after the projectile phase.
    public static class CombatFrameScheduler
    {
        private static readonly ProfilerMarker TimerMarker =
            new ProfilerMarker("Combat.Step.Timers");
        private static readonly ProfilerMarker UnitUpdateMarker =
            new ProfilerMarker("Combat.Step.Units");
        private static readonly ProfilerMarker UnitIntentMarker =
            new ProfilerMarker("Combat.Step.UnitIntents");
        private static readonly ProfilerMarker UnitMovementMarker =
            new ProfilerMarker("Combat.Step.UnitMovement");
        private static readonly ProfilerMarker ProjectileRosterMarker =
            new ProfilerMarker("Combat.Step.ProjectileRoster");
        private static readonly ProfilerMarker ProjectileBroadphaseMarker =
            new ProfilerMarker("Combat.Step.ProjectileBroadphase");
        private static readonly ProfilerMarker BezierScheduleMarker =
            new ProfilerMarker("Combat.Step.BezierSchedule");
        private static readonly ProfilerMarker MotionScheduleMarker =
            new ProfilerMarker("Combat.Step.ProjectileMotionSchedule");
        private static readonly ProfilerMarker MotionCompleteMarker =
            new ProfilerMarker("Combat.Step.ProjectileMotionComplete");
        private static readonly ProfilerMarker BezierCompleteMarker =
            new ProfilerMarker("Combat.Step.BezierComplete");
        private static readonly ProfilerMarker HitCandidateScheduleMarker =
            new ProfilerMarker("Combat.Step.HitCandidateSchedule");
        private static readonly ProfilerMarker HitCandidateCompleteMarker =
            new ProfilerMarker("Combat.Step.HitCandidateComplete");
        private static readonly ProfilerMarker OrbitCandidateScheduleMarker =
            new ProfilerMarker("Combat.Step.OrbitCandidateSchedule");
        private static readonly ProfilerMarker OrbitCandidateCompleteMarker =
            new ProfilerMarker("Combat.Step.OrbitCandidateComplete");
        private static readonly ProfilerMarker ProjectileUpdateMarker =
            new ProfilerMarker("Combat.Step.Projectiles");
        private static readonly ProfilerMarker PointRosterMarker =
            new ProfilerMarker("Combat.Step.PointRoster");
        private static readonly ProfilerMarker PointTimerMarker =
            new ProfilerMarker("Combat.Step.PointTimers");
        private static readonly ProfilerMarker PointUpdateMarker =
            new ProfilerMarker("Combat.Step.Points");
        private static readonly HashSet<int> DuePointHandles = new HashSet<int>();

        public static void AdvanceBattle(float deltaSeconds,
            Action<int> prepareUnit,
            Action<int, CombatUnitSkillPlanBatch> updateUnit,
            Action<int, CombatProjectileBroadphase, CombatBezierStepBatch,
                CombatProjectileHitCandidateBatch, CombatProjectileMotionBatch,
                CombatOrbitHitCandidateBatch>
                updateProjectile,
            Action<int, bool> updatePositionPoint,
            Action commitProjectileTriggers,
            Action playProjectileActiveAudio,
            Action commitSimpleTriggers)
        {
            using (CombatUnitSkillPlanBatch skillPlans = CombatUnitSkillPlanBatch.Schedule())
            {
                // Timer bridges may create their systems (and system entities) on
                // first use. Finish the unit-buffer writer before any structural
                // change, while retaining the pre-timer skill snapshot.
                skillPlans.CompleteAndPublish();
                using (TimerMarker.Auto())
                {
                    CombatCooldownBridge.Advance(deltaSeconds);
                    CombatBuffTimerBridge.Advance(deltaSeconds);
                    CombatAttackCastBridge.AdvanceAll(deltaSeconds);
                }
                using (UnitIntentMarker.Auto())
                {
                    VisitUnits(prepareUnit);
                }
                using (UnitMovementMarker.Auto())
                {
                    CombatMovementBridge.AdvanceAll(deltaSeconds);
                }
                VisitUnits(uid => updateUnit(uid, skillPlans));
            }
            commitSimpleTriggers?.Invoke();
            playProjectileActiveAudio?.Invoke();
            List<int> handles = ListPool<int>.Get();
            try
            {
                using (CombatProjectileMotionBatch motion =
                       ScheduleProjectileMotion(deltaSeconds))
                using (CombatBezierStepBatch bezierSteps = ScheduleBezierSteps(deltaSeconds))
                using (CombatProjectileHitCandidateBatch hitCandidates =
                       ScheduleHitCandidates(deltaSeconds))
                using (CombatOrbitHitCandidateBatch orbitCandidates =
                       ScheduleOrbitCandidates(deltaSeconds))
                using (CombatProjectileBroadphase broadphase = BuildProjectileBroadphase())
                {
                    using (ProjectileRosterMarker.Auto())
                    {
                        CombatProjectileBridge.CollectUpdateHandles(handles);
                    }
                    using (BezierCompleteMarker.Auto())
                    {
                        bezierSteps.Complete();
                    }
                    using (HitCandidateCompleteMarker.Auto())
                    {
                        hitCandidates.Complete();
                    }
                    using (OrbitCandidateCompleteMarker.Auto())
                    {
                        orbitCandidates.Complete();
                    }
                    using (MotionCompleteMarker.Auto())
                    {
                        motion.Complete();
                    }
                    using (ProjectileUpdateMarker.Auto())
                    {
                        foreach (int handle in handles)
                        {
                            updateProjectile(handle, broadphase, bezierSteps,
                                hitCandidates, motion, orbitCandidates);
                        }
                    }
                    commitProjectileTriggers?.Invoke();
                    commitSimpleTriggers?.Invoke();
                }
                List<CombatPointUpdate> pointUpdates = ListPool<CombatPointUpdate>.Get();
                List<int> duePointList = ListPool<int>.Get();
                try
                {
                    // Activations produced by units precede this step's timed
                    // point commands, including intervals shorter than one step.
                    CombatPosPointBridge.AdvanceTriggerEvents();
                    using (PointTimerMarker.Auto())
                    {
                        CombatPosPointBridge.AdvanceTimedTriggers(
                            deltaSeconds * 1000f);
                        CombatConditionProgramBridge.AdvancePointTimers(
                            deltaSeconds * 1000f, duePointList);
                        DuePointHandles.Clear();
                        foreach (int handle in duePointList)
                        {
                            DuePointHandles.Add(handle);
                        }
                    }
                    using (PointRosterMarker.Auto())
                    {
                        CombatPosPointBridge.CollectUpdates(pointUpdates, DuePointHandles);
                    }
                    using (PointUpdateMarker.Auto())
                    {
                        foreach (CombatPointUpdate update in pointUpdates)
                        {
                            updatePositionPoint(update.Handle, update.TickDue);
                        }
                    }
                }
                finally
                {
                    DuePointHandles.Clear();
                    ListPool<int>.Release(duePointList);
                    ListPool<CombatPointUpdate>.Release(pointUpdates);
                }
                CombatPosPointBridge.AdvanceTriggerEvents();
                commitSimpleTriggers?.Invoke();
                playProjectileActiveAudio?.Invoke();
            }
            finally
            {
                ListPool<int>.Release(handles);
            }
        }

        private static CombatProjectileBroadphase BuildProjectileBroadphase()
        {
            using (ProjectileBroadphaseMarker.Auto())
            {
                return CombatProjectileBroadphase.Build();
            }
        }

        private static CombatBezierStepBatch ScheduleBezierSteps(float deltaSeconds)
        {
            using (BezierScheduleMarker.Auto())
            {
                return CombatBezierStepBatch.Schedule(deltaSeconds);
            }
        }

        private static CombatProjectileMotionBatch ScheduleProjectileMotion(
            float deltaSeconds)
        {
            using (MotionScheduleMarker.Auto())
            {
                return CombatProjectileMotionBatch.Schedule(deltaSeconds, 1f);
            }
        }

        private static CombatProjectileHitCandidateBatch ScheduleHitCandidates(
            float deltaSeconds)
        {
            using (HitCandidateScheduleMarker.Auto())
            {
                return CombatProjectileHitCandidateBatch.Schedule(deltaSeconds);
            }
        }

        private static CombatOrbitHitCandidateBatch ScheduleOrbitCandidates(
            float deltaSeconds)
        {
            using (OrbitCandidateScheduleMarker.Auto())
                return CombatOrbitHitCandidateBatch.Schedule(deltaSeconds, 1f);
        }

        public static void VisitUnits(Action<int> updateUnit)
        {
            List<int> heroUids = ListPool<int>.Get();
            try
            {
                using (UnitUpdateMarker.Auto())
                {
                    for (int group = 1; group >= 0; group--)
                    {
                        heroUids.Clear();
                        CombatAttributeBridge.CollectGroupUids(group, heroUids);
                        foreach (int uid in heroUids)
                        {
                            updateUnit(uid);
                        }
                    }
                }
            }
            finally
            {
                ListPool<int>.Release(heroUids);
            }
        }
    }
}
