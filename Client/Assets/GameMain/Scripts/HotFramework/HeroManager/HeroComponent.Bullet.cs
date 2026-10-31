//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System.Collections.Generic;
using DataTable;
using Entity;
using Entity.Bullet;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using Unity.Profiling;
using Zizouqi.CombatDots;
using Zizouqi.CombatVisuals;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        private static readonly ProfilerMarker RegisterBulletMarker =
            new ProfilerMarker("Combat.Bullet.RegisterRuntime");
        private static readonly ProfilerMarker PresentBulletMarker =
            new ProfilerMarker("Combat.Presentation.BulletVisuals");
        private static readonly ProfilerMarker BulletVisualFallbackMarker =
            new ProfilerMarker("Combat.Visual.GameObjectFallback");
        private static ProfilerCounterValue<int> s_BulletVisualFallbacks =
            new ProfilerCounterValue<int>("Combat.Visual.GameObjectFallbacks");
        private static readonly ProfilerMarker PresentBulletAudioMarker =
            new ProfilerMarker("Combat.Presentation.BulletAudio");
        private readonly List<BulletBase> m_PendingBullets = new List<BulletBase>();
        private readonly Dictionary<int, BulletBase> m_BulletsByProjectileHandle =
            new Dictionary<int, BulletBase>();
        private readonly Dictionary<int, EntityPosPoint> m_PosPointsByHandle =
            new Dictionary<int, EntityPosPoint>();
        private readonly List<CombatPointPresentationPlayback> m_PointPlayback =
            new List<CombatPointPresentationPlayback>();
        private readonly List<CombatProjectileAudioPlayback> m_BulletAudioPlayback =
            new List<CombatProjectileAudioPlayback>();
        private static readonly ProfilerMarker PresentPointMarker =
            new ProfilerMarker("Combat.Presentation.PointEffects");
        private System.Action<int, int> m_ShowBulletVisual;
        private System.Func<int, int, bool> m_IsBulletVisualAlive;

        public void QueueBulletVisual(int projectileHandle, int spawnOrder)
        {
            if (CombatProjectileBridge.TryGetVisualSource(projectileHandle,
                    spawnOrder, out _))
                CombatBoardBridge.QueueProjectileVisual(projectileHandle,
                    spawnOrder);
        }

        private bool IsBulletVisualAlive(int projectileHandle, int spawnOrder)
        {
            return CombatProjectileBridge.TryGetVisualSource(projectileHandle,
                spawnOrder, out _);
        }

        private void ShowBulletVisual(int projectileHandle, int spawnOrder)
        {
            if (CombatProjectileBridge.TryGetVisualSource(projectileHandle,
                    spawnOrder, out CombatProjectileVisualSource source))
            {
                Vector3 position = new Vector3(source.Position.x,
                    source.Position.y, source.Position.z);
                Vector3 target = new Vector3(source.TargetPosition.x,
                    source.TargetPosition.y, source.TargetPosition.z);
                if (CombatProjectileVisualBridge.TryQueue(
                        source.AssetPathId, projectileHandle, spawnOrder,
                        position, target))
                {
                    return;
                }
                if (!m_BulletsByProjectileHandle.TryGetValue(projectileHandle,
                        out BulletBase bullet)) return;
                s_BulletVisualFallbacks.Value++;
                using (BulletVisualFallbackMarker.Auto())
                {
                    bullet.BeginVisualLoad();
                }
            }
        }

        private void UpdateBulletVisuals()
        {
            using var sample = PresentBulletMarker.Auto();
            m_ShowBulletVisual ??= ShowBulletVisual;
            m_IsBulletVisualAlive ??= IsBulletVisualAlive;
            CombatBoardBridge.DrainProjectileVisuals(m_IsBulletVisualAlive,
                m_ShowBulletVisual);
            foreach (BulletBase bullet in m_BulletsByProjectileHandle.Values)
                bullet.UpdateVisual();
            CombatProjectileVisualBridge.Flush();
        }

        private void PlayProjectileActiveAudio()
        {
            using (PresentBulletAudioMarker.Auto())
            {
                CombatProjectileBridge.AdvanceActiveAudio();
                m_BulletAudioPlayback.Clear();
                CombatProjectileBridge.CollectActiveAudio(m_BulletAudioPlayback);
                foreach (CombatProjectileAudioPlayback playback in m_BulletAudioPlayback)
                {
                    if (!CombatProjectileBridge.TryGetVisualSource(
                            playback.Handle, playback.SpawnOrder,
                            out CombatProjectileVisualSource source))
                        continue;
                    int serialId = HotGameEntry.Sound.PlaySfxSoundThisFrame(
                        playback.AudioId, new Vector3(source.Position.x,
                            source.Position.y, source.Position.z));
                    CombatProjectileBridge.SetSimpleAudioSerial(playback.Handle,
                        serialId);
                }
                m_BulletAudioPlayback.Clear();
            }
        }

        // The common tracking projectile already has its hit rule on the Entity.
        // Consume the parallel motion result directly; only presentation and
        // pooled-object release still need the legacy wrapper.
        private void UpdateSimpleTrackingProjectile(BulletTracking bullet,
            float deltaSeconds, float realDeltaSeconds,
            CombatProjectileMotionBatch motion)
        {
            int handle = bullet.ProjectileHandle;
            if (handle == 0 || bullet.OwnerTriggerList != null ||
                !CombatProjectileBridge.TryGetSimpleHit(handle, out _))
            {
                bullet.LogicUpdate(deltaSeconds, realDeltaSeconds, motion);
                return;
            }
            if (bullet.Target == null)
            {
                bullet.OnDead();
                return;
            }
            CombatProjectileStep step;
            if (motion == null || !motion.TryApplyTracking(handle, out step))
                step = CombatProjectileBridge.StepTracking(handle, deltaSeconds,
                    ConstValue.EntityQiziHeight);
            if (step == CombatProjectileStep.TargetLost)
            {
                bullet.OnDead();
            }
            else if (step == CombatProjectileStep.Hit)
            {
                if (CombatTriggerProjectileSpawnQueue.TryEnqueueSimpleHit(handle,
                        bullet.SpawnOrder, bullet.Caster?.HeroUID ?? 0,
                        bullet.RuntimeSourceSkill, bullet.Target))
                    bullet.OnDead();
                else
                    bullet.OnHitTarget(bullet.Target);
            }
        }
        public BulletBase CreateBullet(int bulletID)
        {
            var bullet = GameEntry.DataTable.GetDataTable<DRBullet>("Bullet");
            if (bullet != null && bullet.HasDataRow(bulletID))
                return CreateBullet(bullet[bulletID],
                    CombatBoardBridge.AllocateProjectileSpawnOrder());
            return null;
        }

        public BulletBase CreateBullet(DRBullet bulletData, int spawnOrder)
        {
            if (bulletData != null)
            {
                BulletBase ret;
                switch ((BulletType)bulletData.BulletType)
                {
                    case BulletType.TrackingBullet:
                        ret = ReferencePool.Acquire<BulletTracking>();
                        break;
                    case BulletType.RotateOwner:
                        ret = ReferencePool.Acquire<BulletRotateOwner>();
                        break;
                    case BulletType.PenetratingBullet:
                        ret = ReferencePool.Acquire<BulletPenetrating>();
                        break;
                    case BulletType.MarkPoint:
                        ret = ReferencePool.Acquire<BulletMarkPoint>();
                        break;
                    default:
                        ret = ReferencePool.Acquire<BulletTracking>();
                        break;
                }

                m_PendingBullets.Add(ret);
                ret.SpawnOrder = spawnOrder;
                ret.BulletID = bulletData.Id;
                ret.CurBulletData = bulletData;
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

            m_BulletsByProjectileHandle.Remove(bullet.ProjectileHandle);
            m_PendingBullets.Remove(bullet);
            ReferencePool.Release(bullet);
        }

        public void RegisterBulletRuntime(BulletBase bullet)
        {
            if (bullet == null || bullet.ProjectileHandle == 0)
            {
                return;
            }
            using var registerSample = RegisterBulletMarker.Auto();
            m_BulletsByProjectileHandle.Add(bullet.ProjectileHandle, bullet);
            m_PendingBullets.Remove(bullet);
        }

        public void CollectBullets(List<BulletBase> bullets)
        {
            List<int> handles = ListPool<int>.Get();
            bool hasWorld = CombatProjectileBridge.CollectUpdateHandles(handles);
            foreach (int handle in handles)
            {
                if (m_BulletsByProjectileHandle.TryGetValue(handle, out BulletBase bullet))
                {
                    bullets.Add(bullet);
                }
            }
            ListPool<int>.Release(handles);
            if (!hasWorld)
            {
                bullets.AddRange(m_BulletsByProjectileHandle.Values);
            }
            bullets.AddRange(m_PendingBullets);
            bullets.Sort((a, b) => a.SpawnOrder.CompareTo(b.SpawnOrder));
        }

        public void CollectPendingBulletTargets(int bulletId, bool sameCaster,
            EntityQizi caster,
            List<CombatProjectileSelection.PendingProjectile> pendingTargets)
        {
            foreach (BulletBase bullet in m_PendingBullets)
            {
                if (MatchesPendingBullet(bullet, bulletId, sameCaster, caster))
                {
                    pendingTargets.Add(new CombatProjectileSelection.PendingProjectile
                    {
                        Handle = bullet.ProjectileHandle,
                        SpawnOrder = bullet.SpawnOrder
                    });
                }
            }
        }

        public BulletBase GetBulletTarget(int projectileHandle, int spawnOrder,
            bool pendingTarget)
        {
            if (!pendingTarget)
            {
                return m_BulletsByProjectileHandle.TryGetValue(projectileHandle,
                    out BulletBase registered) ? registered : null;
            }
            foreach (BulletBase pending in m_PendingBullets)
            {
                if (pending.SpawnOrder == spawnOrder)
                {
                    return pending;
                }
            }
            return null;
        }

        private static bool MatchesPendingBullet(BulletBase bullet, int bulletId,
            bool sameCaster, EntityQizi caster)
        {
            return bullet.IsValid && bullet.BulletID == bulletId &&
                   (!sameCaster || bullet.Caster == caster);
        }

        private void ClearBullet()
        {
            m_BulletAudioPlayback.Clear();
            CombatBoardBridge.ClearProjectileVisuals();
            List<BulletBase> bullets = ListPool<BulletBase>.Get();
            CollectBullets(bullets);
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                DestoryBullet(bullets[i]);
            }
            ListPool<BulletBase>.Release(bullets);
            m_PendingBullets.Clear();
            m_BulletsByProjectileHandle.Clear();
            CombatBoardBridge.ResetProjectileSpawnOrder();
        }

        public EntityPosPoint CreatePosPoint()
        {
            var newPos = ReferencePool.Acquire<EntityPosPoint>();
            newPos.CreateCombatPoint(CombatBoardBridge.AllocatePositionPointSpawnOrder());
            m_PosPointsByHandle.Add(newPos.CombatHandle, newPos);
            return newPos;
        }
        private void UpdatePointPresentation()
        {
            using (PresentPointMarker.Auto())
            {
                m_PointPlayback.Clear();
                CombatPosPointBridge.CollectPresentationPlayback(m_PointPlayback);
                foreach (CombatPointPresentationPlayback playback in m_PointPlayback)
                {
                    if (!m_PosPointsByHandle.TryGetValue(playback.PointHandle,
                            out EntityPosPoint point) || !point.IsValid)
                    {
                        continue;
                    }
                    if (playback.Kind == CombatPointPresentationKind.Sfx)
                    {
                        if (playback.Create) point.AddSfx(playback.Id);
                        else point.RemoveSfx(playback.Id);
                    }
                    else if (playback.Create)
                    {
                        int serialId = HotGameEntry.Sound.PlaySfxSoundThisFrame(
                            playback.Id, point.LogicHitPosition);
                        CombatPosPointBridge.SetAudioSerial(playback.PointHandle,
                            serialId);
                    }
                    else
                    {
                        HotGameEntry.Sound.ReducePlayingSfxCount(
                            CombatPosPointBridge.GetAudioSerial(playback.PointHandle));
                    }
                }
                m_PointPlayback.Clear();
            }
        }
        public void DestoryPosPoint(EntityPosPoint posPoint)
        {
            if (posPoint == null)
            {
                return;
            }
            m_PosPointsByHandle.Remove(posPoint.CombatHandle);
            ReferencePool.Release(posPoint);
        }
        private void ClearPosPoint()
        {
            m_PointPlayback.Clear();
            List<int> handles = ListPool<int>.Get();
            if (CombatPosPointBridge.CollectHandles(handles))
            {
                for (int i = handles.Count - 1; i >= 0; i--)
                {
                    if (m_PosPointsByHandle.TryGetValue(handles[i], out EntityPosPoint point))
                    {
                        DestoryPosPoint(point);
                    }
                }
            }
            foreach (EntityPosPoint point in m_PosPointsByHandle.Values)
            {
                ReferencePool.Release(point);
            }
            ListPool<int>.Release(handles);
            m_PosPointsByHandle.Clear();
            CombatBoardBridge.ResetPositionPointSpawnOrder();
        }
    }
}
