using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst.Intrinsics;

namespace Zizouqi.CombatDots
{
    public enum CombatSkillPhaseKind : byte
    {
        ShakeFinished,
        AnimationFinished,
        CastFinished
    }

    [InternalBufferCapacity(3)]
    public struct CombatSkillPhaseEvent : IBufferElementData
    {
        public Entity Skill;
        public CombatSkillPhaseKind Kind;
    }

    [InternalBufferCapacity(4)]
    public struct CombatSkillTriggerInvocation : IBufferElementData
    {
        public Entity Trigger;
        public int Order;
    }

    // The attack phase belongs to the unit entity. Skill callbacks are committed
    // later in unit order; jobs only publish the phase transitions.
    public struct CombatAttackCast : IComponentData
    {
        public Entity Skill;
        public int ShakeTriggerType;
        public int NormalSkillIndex;
        public int ShakeBeforeMs;
        public int AnimationDurationMs;
        public int CastCooldownMs;
        public float ElapsedSeconds;
        public bool Active;
        public bool ShakeTriggered;
        public bool AnimationFinished;
        public bool CastFinished;
    }

    [DisableAutoCreation]
    public partial class CombatAttackCastSystem : SystemBase
    {
        public float DeltaSeconds;
        private EntityQuery m_Query;

        [BurstCompile]
        private struct AdvanceJob : IJobChunk
        {
            public ComponentTypeHandle<CombatAttackCast> CastType;
            public BufferTypeHandle<CombatSkillPhaseEvent> EventType;
            public BufferTypeHandle<CombatSkillTriggerInvocation> InvocationType;
            [ReadOnly] public BufferLookup<CombatSkillTriggerLink> SkillLinks;
            public float DeltaSeconds;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<CombatAttackCast> casts = chunk.GetNativeArray(ref CastType);
                BufferAccessor<CombatSkillPhaseEvent> buffers = chunk.GetBufferAccessor(ref EventType);
                BufferAccessor<CombatSkillTriggerInvocation> invocationBuffers =
                    chunk.GetBufferAccessor(ref InvocationType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out int index))
                {
                    CombatAttackCast cast = casts[index];
                    if (!cast.Active) continue;
                    DynamicBuffer<CombatSkillPhaseEvent> events = buffers[index];
                    cast.ElapsedSeconds += DeltaSeconds;
                    float elapsedMs = cast.ElapsedSeconds * 1000f;
                    if (!cast.ShakeTriggered && elapsedMs >= cast.ShakeBeforeMs)
                    {
                        cast.ShakeTriggered = true;
                        events.Add(new CombatSkillPhaseEvent
                        {
                            Skill = cast.Skill,
                            Kind = CombatSkillPhaseKind.ShakeFinished
                        });
                        if (SkillLinks.HasBuffer(cast.Skill))
                        {
                            DynamicBuffer<CombatSkillTriggerLink> links = SkillLinks[cast.Skill];
                            DynamicBuffer<CombatSkillTriggerInvocation> invocations =
                                invocationBuffers[index];
                            for (int i = 0; i < links.Length; i++)
                            {
                                CombatSkillTriggerLink link = links[i];
                                if (link.TriggerType != cast.ShakeTriggerType) continue;
                                invocations.Add(new CombatSkillTriggerInvocation
                                {
                                    Trigger = link.Trigger,
                                    Order = link.Order
                                });
                            }
                        }
                    }
                    if (!cast.AnimationFinished && elapsedMs >= cast.AnimationDurationMs)
                    {
                        cast.AnimationFinished = true;
                        events.Add(new CombatSkillPhaseEvent
                        {
                            Skill = cast.Skill,
                            Kind = CombatSkillPhaseKind.AnimationFinished
                        });
                    }
                    if (!cast.CastFinished && elapsedMs >= cast.CastCooldownMs)
                    {
                        cast.CastFinished = true;
                        events.Add(new CombatSkillPhaseEvent
                        {
                            Skill = cast.Skill,
                            Kind = CombatSkillPhaseKind.CastFinished
                        });
                    }
                    casts[index] = cast;
                }
            }
        }

        protected override void OnCreate()
        {
            m_Query = GetEntityQuery(
                ComponentType.ReadWrite<CombatAttackCast>(),
                ComponentType.ReadWrite<CombatSkillPhaseEvent>(),
                ComponentType.ReadWrite<CombatSkillTriggerInvocation>());
        }

        protected override void OnUpdate()
        {
            Dependency = new AdvanceJob
            {
                CastType = GetComponentTypeHandle<CombatAttackCast>(false),
                EventType = GetBufferTypeHandle<CombatSkillPhaseEvent>(false),
                InvocationType = GetBufferTypeHandle<CombatSkillTriggerInvocation>(false),
                SkillLinks = GetBufferLookup<CombatSkillTriggerLink>(true),
                DeltaSeconds = DeltaSeconds
            }.ScheduleParallel(m_Query, Dependency);
            Dependency.Complete();
        }
    }

    public static class CombatAttackCastBridge
    {
        public static void AdvanceAll(float deltaSeconds)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (deltaSeconds <= 0f || world == null || !world.IsCreated) return;
            CombatAttackCastSystem system =
                world.GetOrCreateSystemManaged<CombatAttackCastSystem>();
            system.DeltaSeconds = deltaSeconds;
            system.Update();
        }

        public static void Begin(int unitHandle, Entity skill, int normalSkillIndex,
            int shakeTriggerType,
            int shakeBeforeMs, int animationDurationMs, int castCooldownMs)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            manager.GetBuffer<CombatSkillPhaseEvent>(entity).Clear();
            manager.GetBuffer<CombatSkillTriggerInvocation>(entity).Clear();
            manager.SetComponentData(entity, new CombatAttackCast
            {
                Active = true,
                Skill = skill,
                ShakeTriggerType = shakeTriggerType,
                NormalSkillIndex = normalSkillIndex,
                ShakeBeforeMs = shakeBeforeMs,
                AnimationDurationMs = animationDurationMs,
                CastCooldownMs = castCooldownMs
            });
        }

        public static bool IsActive(int unitHandle)
        {
            if (!CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
                return false;
            World world = World.DefaultGameObjectInjectionWorld;
            return world != null && world.IsCreated &&
                world.EntityManager.GetComponentData<CombatAttackCast>(entity).Active;
        }

        public static int GetNormalSkillIndex(int unitHandle)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            return manager.GetComponentData<CombatAttackCast>(entity).NormalSkillIndex;
        }

        public static bool TryConsumeShake(int unitHandle) =>
            Consume(unitHandle, CombatSkillPhaseKind.ShakeFinished);

        public static bool TryDequeueShakeTrigger(int unitHandle, out Entity trigger,
            out int order)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            DynamicBuffer<CombatSkillTriggerInvocation> invocations =
                manager.GetBuffer<CombatSkillTriggerInvocation>(entity);
            if (invocations.Length == 0)
            {
                trigger = Entity.Null;
                order = -1;
                return false;
            }
            CombatSkillTriggerInvocation invocation = invocations[0];
            invocations.RemoveAt(0);
            trigger = invocation.Trigger;
            order = invocation.Order;
            return true;
        }

        public static bool TryConsumeAnimationEnd(int unitHandle) =>
            Consume(unitHandle, CombatSkillPhaseKind.AnimationFinished);

        public static bool IsComplete(int unitHandle)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            if (!manager.GetComponentData<CombatAttackCast>(entity).Active)
                return false;
            DynamicBuffer<CombatSkillPhaseEvent> events =
                manager.GetBuffer<CombatSkillPhaseEvent>(entity);
            for (int i = 0; i < events.Length; i++)
                if (events[i].Kind == CombatSkillPhaseKind.CastFinished)
                    return true;
            return false;
        }

        public static void End(int unitHandle)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (unitHandle == 0 || world == null || !world.IsCreated ||
                !CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
                return;
            EntityManager manager = world.EntityManager;
            CombatAttackCast cast = manager.GetComponentData<CombatAttackCast>(entity);
            cast.Active = false;
            manager.SetComponentData(entity, cast);
            manager.GetBuffer<CombatSkillPhaseEvent>(entity).Clear();
            manager.GetBuffer<CombatSkillTriggerInvocation>(entity).Clear();
        }

        private static bool Consume(int unitHandle, CombatSkillPhaseKind phase)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            if (!manager.GetComponentData<CombatAttackCast>(entity).Active)
                return false;
            DynamicBuffer<CombatSkillPhaseEvent> events =
                manager.GetBuffer<CombatSkillPhaseEvent>(entity);
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind != phase) continue;
                events.RemoveAt(i);
                return true;
            }
            return false;
        }

        private static Entity GetEntity(int unitHandle, out EntityManager manager)
        {
            if (!CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
                throw new System.InvalidOperationException("Attack cast unit has been released.");
            manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            return entity;
        }
    }
}
