using System.Collections.Generic;
using Entity;
using Entity.Bullet;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    // One logic step owns this queue. The hit scan records value snapshots; the
    // Burst job prepares placements; managed effects commit in hit order.
    internal static class CombatTriggerProjectileSpawnQueue
    {
        private enum SimpleHitCommand : byte
        {
            None,
            HitSequence
        }

        private struct Pending
        {
            public OneTrigger Trigger;
            public CommandCreateBullet Command;
            public CommandCauseDamage DamageCommand;
            public EntityQizi DamageCaster;
            public EntityQizi Target;
            public BulletBase Source;
            public int SourceProjectileHandle;
            public int SourceSpawnOrder;
            public Skill SourceSkill;
            public int SimpleCasterUid;
            public int SimpleTargetUid;
            public float3 SimpleTargetPosition;
            public SimpleHitCommand SimpleCommand;
            public CombatProjectileSimpleHit SimpleRule;
            public CombatCommandNode Node;
            public CombatTriggerProjectileSpawnInput Input;
        }

        private static readonly ProfilerMarker PrepareMarker =
            new ProfilerMarker("Combat.Trigger.ProjectileSpawn.Prepare");
        private static readonly ProfilerMarker CommitMarker =
            new ProfilerMarker("Combat.Trigger.ProjectileSpawn.Commit");
        private static readonly ProfilerMarker SimpleHitCommitMarker =
            new ProfilerMarker("Combat.Trigger.ProjectileSpawn.SimpleHit");
        private static readonly ProfilerMarker SimpleHitDamageMarker =
            new ProfilerMarker("Combat.Trigger.ProjectileSpawn.SimpleHit.Damage");
        private static readonly ProfilerMarker SimpleHitSfxMarker =
            new ProfilerMarker("Combat.Trigger.ProjectileSpawn.SimpleHit.Sfx");
        private static readonly ProfilerMarker SimpleHitAudioMarker =
            new ProfilerMarker("Combat.Trigger.ProjectileSpawn.SimpleHit.Audio");
        private static ProfilerCounterValue<int> s_QueuedCounter =
            new ProfilerCounterValue<int>("Combat.Trigger.ProjectileSpawn.Queued");
        private static ProfilerCounterValue<int> s_FallbackCounter =
            new ProfilerCounterValue<int>("Combat.Trigger.ProjectileSpawn.Fallback");
        private static ProfilerCounterValue<int> s_SimpleHitCounter =
            new ProfilerCounterValue<int>("Combat.Bullet.EcsSimpleHits");
        private static ProfilerCounterValue<int> s_DamageEcsCounter =
            new ProfilerCounterValue<int>("Combat.Bullet.DamageEcsCount");
        private static readonly List<Pending> PendingEvents = new List<Pending>(64);
        private static readonly CombatTriggerProjectileSpawnResult[] SmallResults =
            new CombatTriggerProjectileSpawnResult[64];
        private static bool s_Recording;
        private static bool s_Flushing;

        public static void BeginStep()
        {
            // A prior step must have committed before its pooled sources can be reused.
            Flush();
            s_QueuedCounter.Value = 0;
            s_FallbackCounter.Value = 0;
            s_SimpleHitCounter.Value = 0;
            s_DamageEcsCounter.Value = 0;
            s_Recording = true;
        }

        public static void EndStep()
        {
            try
            {
                Flush();
            }
            finally
            {
                s_Recording = false;
            }
        }

        public static void CommitProjectiles()
        {
            Flush();
        }

        public static bool TryEnqueueSimpleHit(BulletBase source,
            EntityQizi target)
        {
            if (source == null) return false;
            return TryEnqueueSimpleHit(source.ProjectileHandle,
                source.SpawnOrder, source.Caster?.HeroUID ?? 0,
                source.RuntimeSourceSkill, target);
        }

        public static bool TryEnqueueSimpleHit(int projectileHandle,
            int spawnOrder, int casterUid, Skill sourceSkill,
            EntityQizi target)
        {
            if (target == null || !target.IsValid ||
                !CombatProjectileBridge.TryGetSimpleHit(projectileHandle,
                    out CombatProjectileSimpleHit rule)) return false;

            bool hasCaster = CombatAttributeBridge.TryGetHandleByUid(
                casterUid, out int casterHandle);
            int damageBase = 0;
            if (hasCaster &&
                (rule.DamageMode == 0 || rule.DamagePercent > 0))
            {
                int attributeType = rule.DamageMode == 0 ? 8 :
                    rule.DamageAttributeType;
                damageBase = CombatAttributeBridge.GetValue(
                    casterHandle, attributeType);
            }
            Vector3 hitPosition = target.LogicHitPosition;
            var pending = new Pending
            {
                SourceProjectileHandle = projectileHandle,
                SourceSpawnOrder = spawnOrder,
                SourceSkill = sourceSkill,
                SimpleCasterUid = casterUid,
                SimpleTargetUid = target.HeroUID,
                SimpleTargetPosition = new float3(hitPosition.x,
                    hitPosition.y, hitPosition.z),
                SimpleRule = rule,
                Input = new CombatTriggerProjectileSpawnInput
                {
                    IsDamage = true,
                    HasCaster = hasCaster,
                    DamageMode = rule.DamageMode,
                    DamageBaseValue = damageBase,
                    DamageFixedValue = rule.DamageFixedValue,
                    DamagePercent = rule.DamagePercent
                }
            };
            pending.SimpleCommand = SimpleHitCommand.HitSequence;
            EnqueueSimple(pending);
            s_SimpleHitCounter.Value++;
            if (!s_Recording && !s_Flushing) Flush();
            return true;
        }

        private static void EnqueueSimple(Pending pending)
        {
            if (s_Flushing)
                Commit(pending,
                    CombatTriggerProjectileSpawnBatch.PrepareSingle(pending.Input));
            else
                PendingEvents.Add(pending);
        }

        public static bool TryEnqueue(TriggerList list, EntityQizi target)
        {
            if (!s_Recording || s_Flushing || target == null || !target.IsValid ||
                !(list?.Owner is BulletPenetrating source) ||
                list.CurTriggerList == null)
            {
                return false;
            }

            // Never split a trigger list between delayed and immediate execution.
            int count = 0;
            foreach (OneTrigger trigger in list.CurTriggerList)
            {
                if (trigger.CurTriggerType != TriggerType.OnBulletHitTarget) continue;
                if (!trigger.TryGetBatchableHitCommands())
                {
                    s_FallbackCounter.Value++;
                    return false;
                }
                foreach (CommandBase command in trigger.CurCommandList)
                    if (command is CommandCreateBullet) count++;
            }
            if (count == 0) return false;

            Vector3 owner = source.LogicPosition;
            Vector3 targetPosition = target.LogicPosition;
            // Unity's LookRotation zero-vector behavior differs from LookRotationSafe.
            Vector3 forward = targetPosition - owner;
            if (forward.sqrMagnitude < 0.000001f ||
                forward.x * forward.x + forward.z * forward.z < 0.000001f)
            {
                s_FallbackCounter.Value++;
                return false;
            }
            foreach (OneTrigger trigger in list.CurTriggerList)
            {
                if (trigger.CurTriggerType != TriggerType.OnBulletHitTarget) continue;
                foreach (CommandBase commandBase in trigger.CurCommandList)
                {
                    if (commandBase is CommandCauseDamage damage)
                    {
                        EntityQizi caster = damage.GetDamageCaster();
                        int damageMode = (int)damage.CurDamageComputeType;
                        int percent = damage.ParamInt3.Value;
                        int attributeType = damageMode == 0 ? 8 :
                            damage.ParamInt2.Value;
                        int damageBaseValue = caster != null &&
                            (damageMode == 0 || percent > 0)
                            ? CombatAttributeBridge.GetValue(caster.CombatUnitHandle,
                                attributeType) : 0;
                        PendingEvents.Add(new Pending
                        {
                            Trigger = trigger,
                            DamageCommand = damage,
                            DamageCaster = caster,
                            Target = target,
                            Source = source,
                            SourceSpawnOrder = source.SpawnOrder,
                            Node = new CombatCommandNode
                            {
                                Kind = CombatCommandKind.Damage,
                                TriggerDamageRelate = damage.TriggerDamageRelate
                            },
                            Input = new CombatTriggerProjectileSpawnInput
                            {
                                IsDamage = true,
                                HasCaster = caster != null,
                                DamageMode = damageMode,
                                DamageBaseValue = damageBaseValue,
                                DamageFixedValue = damage.ParamInt1.Value,
                                DamagePercent = percent
                            }
                        });
                        continue;
                    }
                    var command = (CommandCreateBullet)commandBase;
                    Vector3 offset = command.PosOffset.Value;
                    PendingEvents.Add(new Pending
                    {
                        Trigger = trigger,
                        Command = command,
                        Target = target,
                        Source = source,
                        SourceSpawnOrder = source.SpawnOrder,
                        Node = new CombatCommandNode
                        {
                            Kind = CombatCommandKind.CreateProjectile,
                            ProjectileId = command.CurBulletID.Value,
                            ProjectileParameter = command.ParamInt1.Value,
                            ProjectileOffsetX = offset.x,
                            ProjectileOffsetY = offset.y,
                            ProjectileOffsetZ = offset.z
                        },
                        Input = new CombatTriggerProjectileSpawnInput
                        {
                            OwnerPosition = new float3(owner.x, owner.y, owner.z),
                            TargetPosition = new float3(targetPosition.x, targetPosition.y,
                                targetPosition.z),
                            Offset = new float3(offset.x, offset.y, offset.z)
                        }
                    });
                }
            }
            s_QueuedCounter.Value += count;
            return true;
        }

        public static void FlushBeforeRecycle(BulletBase bullet)
        {
            if (s_Flushing || PendingEvents.Count == 0) return;
            for (int i = 0; i < PendingEvents.Count; i++)
            {
                if (ReferenceEquals(PendingEvents[i].Source, bullet) ||
                    (PendingEvents[i].SimpleCommand != SimpleHitCommand.None &&
                     PendingEvents[i].SourceProjectileHandle == bullet.ProjectileHandle &&
                     PendingEvents[i].SourceSpawnOrder == bullet.SpawnOrder))
                {
                    Flush();
                    return;
                }
            }
        }

        public static void FlushSimpleBeforeRecycle(int projectileHandle,
            int spawnOrder)
        {
            if (s_Flushing || PendingEvents.Count == 0) return;
            for (int i = 0; i < PendingEvents.Count; i++)
            {
                Pending pending = PendingEvents[i];
                if (pending.SimpleCommand != SimpleHitCommand.None &&
                    pending.SourceProjectileHandle == projectileHandle &&
                    pending.SourceSpawnOrder == spawnOrder)
                {
                    Flush();
                    return;
                }
            }
        }

        private static void Flush()
        {
            if (s_Flushing || PendingEvents.Count == 0) return;
            s_Flushing = true;
            int count = PendingEvents.Count;
            try
            {
                if (count < 64)
                {
                    using (PrepareMarker.Auto())
                    {
                        for (int i = 0; i < count; i++)
                            SmallResults[i] = CombatTriggerProjectileSpawnBatch
                                .PrepareSingle(PendingEvents[i].Input);
                    }
                    using (CommitMarker.Auto())
                        for (int i = 0; i < count; i++)
                            Commit(PendingEvents[i], SmallResults[i]);
                    return;
                }
                var inputs = new NativeArray<CombatTriggerProjectileSpawnInput>(count,
                    Allocator.TempJob);
                var results = new NativeArray<CombatTriggerProjectileSpawnResult>(count,
                    Allocator.TempJob);
                try
                {
                    for (int i = 0; i < count; i++) inputs[i] = PendingEvents[i].Input;
                    using (PrepareMarker.Auto())
                        CombatTriggerProjectileSpawnBatch.Prepare(inputs, results);
                    using (CommitMarker.Auto())
                    {
                        for (int i = 0; i < count; i++)
                            Commit(PendingEvents[i], results[i]);
                    }
                }
                finally
                {
                    results.Dispose();
                    inputs.Dispose();
                }
            }
            finally
            {
                PendingEvents.RemoveRange(0, count);
                s_Flushing = false;
            }
        }

        private static void Commit(Pending pending,
            CombatTriggerProjectileSpawnResult result)
        {
            if (pending.SimpleCommand != SimpleHitCommand.None)
            {
                CommitSimple(pending, result);
                return;
            }
            if (pending.Source.SpawnOrder != pending.SourceSpawnOrder ||
                pending.Trigger.ParentTriggerList == null) return;
            if (pending.DamageCommand != null)
            {
                if (pending.Target.CombatUnitHandle != 0 &&
                    CombatAttributeBridge.IsAlive(pending.Target.CombatUnitHandle))
                {
                    Vector3 position = pending.Target.LogicPosition;
                    if (CombatAttributeBridge.TryApplyIndependentDamage(
                            pending.DamageCaster?.CombatUnitHandle ?? 0,
                            pending.Target.CombatUnitHandle, result.Damage,
                            pending.Node.TriggerDamageRelate,
                            position.x, position.y, position.z))
                        s_DamageEcsCounter.Value++;
                    else
                        pending.DamageCommand.ApplyDamage(pending.Trigger,
                            pending.Target, pending.DamageCaster,
                            result.Damage, pending.Node.TriggerDamageRelate);
                }
            }
            else
            {
                float3 position = result.Position;
                pending.Command.ExecutePreparedTarget(pending.Trigger,
                    pending.Target, pending.Node,
                    new Vector3(position.x, position.y, position.z));
            }
        }

        private static void CommitSimple(Pending pending,
            CombatTriggerProjectileSpawnResult result)
        {
            using var commitSample = SimpleHitCommitMarker.Auto();
            int projectileHandle = pending.SourceProjectileHandle;
            if (!CombatProjectileBridge.TryGetVisualSource(projectileHandle,
                    pending.SourceSpawnOrder, out _)) return;
            int previousAudioSerial =
                CombatProjectileBridge.GetSimpleAudioSerial(projectileHandle);
            EntityQizi target = HotGameEntry.HeroManager.GetEntityByUID(
                pending.SimpleTargetUid);
            EntityQizi caster = pending.SimpleCasterUid == 0 ? null :
                HotGameEntry.HeroManager.GetEntityByUID(pending.SimpleCasterUid) ??
                pending.SourceSkill?.Caster;
            if (target != null && target.IsValid && caster != null)
            {
                using (SimpleHitDamageMarker.Auto())
                {
                    Vector3 position = target.LogicPosition;
                    if (CombatAttributeBridge.TryApplyIndependentDamage(
                            caster.CombatUnitHandle,
                            target.CombatUnitHandle, result.Damage,
                            pending.SimpleRule.TriggerDamageRelate,
                            position.x, position.y, position.z))
                        s_DamageEcsCounter.Value++;
                    else
                        CommandCauseDamage.ApplyDamage(pending.SourceSkill,
                            target, caster, result.Damage,
                            pending.SimpleRule.TriggerDamageRelate);
                }
            }
            if (target != null && target.IsValid)
            {
                using (SimpleHitSfxMarker.Auto())
                {
                    if (pending.SimpleRule.CreateSfx)
                        target.AddSfx(pending.SimpleRule.SfxId);
                    else
                        target.RemoveSfx(pending.SimpleRule.SfxId);
                }
            }
            using (SimpleHitAudioMarker.Auto())
            {
                HotGameEntry.Sound.ReducePlayingSfxCount(previousAudioSerial);
                Vector3 audioPosition = target != null ? target.LogicHitPosition :
                    new Vector3(pending.SimpleTargetPosition.x,
                        pending.SimpleTargetPosition.y,
                        pending.SimpleTargetPosition.z);
                int serialId = HotGameEntry.Sound.PlaySfxSoundThisFrame(
                    pending.SimpleRule.AudioStartId,
                    audioPosition);
                if (CombatProjectileBridge.TryGetVisualSource(projectileHandle,
                        pending.SourceSpawnOrder, out _))
                    CombatProjectileBridge.SetSimpleAudioSerial(projectileHandle,
                        serialId);
            }
        }
    }
}
