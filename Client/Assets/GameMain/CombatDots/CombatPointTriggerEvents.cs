using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Zizouqi.CombatDots
{
    // Runtime rules live on the point entity. Activation only queues an event;
    // the job evaluates the rules and emits ordered presentation intents.
    [InternalBufferCapacity(2)]
    public struct CombatPointTriggerEvent : IBufferElementData
    {
        public byte TriggerType;
    }

    [InternalBufferCapacity(2)]
    public struct CombatPointPresentationRule : IBufferElementData
    {
        public byte TriggerType;
        public CombatPointPresentationKind Kind;
        public int Id;
        public bool Create;
    }

    public enum CombatPointPresentationKind : byte
    {
        Sfx,
        Audio
    }

    [InternalBufferCapacity(2)]
    public struct CombatPointPresentationIntent : IBufferElementData
    {
        public CombatPointPresentationKind Kind;
        public int Id;
        public bool Create;
    }

    public struct CombatPointPresentationPlayback
    {
        public int PointHandle;
        public int SpawnOrder;
        public int Sequence;
        public CombatPointPresentationKind Kind;
        public int Id;
        public bool Create;
    }

    public struct CombatPointTimedTrigger : IComponentData
    {
        public int IntervalMs;
        public int MaxPasses;
        public int PassCount;
        public int CommandCount;
        public float AccumulatedMs;
        public bool PresentationOnly;
    }

    [InternalBufferCapacity(2)]
    public struct CombatPointProjectileIntent : IBufferElementData
    {
        public int CommandIndex;
    }

    [InternalBufferCapacity(2)]
    public struct CombatPointTimedPresentationRule : IBufferElementData
    {
        public CombatPointPresentationKind Kind;
        public int Id;
        public bool Create;
    }

    [DisableAutoCreation]
    public partial class CombatPointTriggerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<CombatPosPointActive>()
                .ForEach((DynamicBuffer<CombatPointTriggerEvent> events,
                    DynamicBuffer<CombatPointPresentationRule> rules,
                    DynamicBuffer<CombatPointPresentationIntent> intents) =>
                {
                    for (int i = 0; i < events.Length; i++)
                    {
                        CombatPointTriggerEvent triggerEvent = events[i];
                        for (int j = 0; j < rules.Length; j++)
                        {
                            CombatPointPresentationRule rule = rules[j];
                            if (rule.TriggerType != triggerEvent.TriggerType) continue;
                            intents.Add(new CombatPointPresentationIntent
                            {
                                Kind = rule.Kind,
                                Id = rule.Id,
                                Create = rule.Create
                            });
                        }
                    }
                    events.Clear();
                }).WithBurst().ScheduleParallel();
            Dependency.Complete();
        }
    }

    [DisableAutoCreation]
    public partial class CombatPointTimedTriggerSystem : SystemBase
    {
        public float DeltaMs;
        private EntityQuery m_Query;

        [BurstCompile]
        private struct AdvanceJob : IJobChunk
        {
            public ComponentTypeHandle<CombatPointTimedTrigger> TimerType;
            public BufferTypeHandle<CombatPointProjectileIntent> IntentType;
            [ReadOnly] public BufferTypeHandle<CombatPointTimedPresentationRule> RuleType;
            public BufferTypeHandle<CombatPointPresentationIntent> PresentationType;
            public float DeltaMs;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<CombatPointTimedTrigger> timers =
                    chunk.GetNativeArray(ref TimerType);
                BufferAccessor<CombatPointProjectileIntent> intents =
                    chunk.GetBufferAccessor(ref IntentType);
                BufferAccessor<CombatPointTimedPresentationRule> rules =
                    chunk.GetBufferAccessor(ref RuleType);
                BufferAccessor<CombatPointPresentationIntent> presentations =
                    chunk.GetBufferAccessor(ref PresentationType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask,
                    chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out int index))
                {
                    CombatPointTimedTrigger timer = timers[index];
                    if (timer.IntervalMs <= 0 ||
                        (timer.MaxPasses != 0 && timer.PassCount >= timer.MaxPasses))
                        continue;
                    timer.AccumulatedMs += DeltaMs;
                    if (timer.AccumulatedMs >= timer.IntervalMs)
                    {
                        timer.AccumulatedMs -= timer.IntervalMs;
                        timer.PassCount++;
                        if (timer.PresentationOnly)
                        {
                            DynamicBuffer<CombatPointTimedPresentationRule> pointRules =
                                rules[index];
                            DynamicBuffer<CombatPointPresentationIntent> pointIntents =
                                presentations[index];
                            for (int i = 0; i < pointRules.Length; i++)
                            {
                                CombatPointTimedPresentationRule rule = pointRules[i];
                                pointIntents.Add(new CombatPointPresentationIntent
                                {
                                    Kind = rule.Kind,
                                    Id = rule.Id,
                                    Create = rule.Create
                                });
                            }
                        }
                        else
                        {
                            for (int i = 0; i < timer.CommandCount; i++)
                                intents[index].Add(new CombatPointProjectileIntent
                                {
                                    CommandIndex = i
                                });
                        }
                    }
                    timers[index] = timer;
                }
            }
        }

        protected override void OnCreate()
        {
            m_Query = GetEntityQuery(
                ComponentType.ReadWrite<CombatPointTimedTrigger>(),
                ComponentType.ReadWrite<CombatPointProjectileIntent>(),
                ComponentType.ReadOnly<CombatPointTimedPresentationRule>(),
                ComponentType.ReadWrite<CombatPointPresentationIntent>(),
                ComponentType.ReadOnly<CombatPosPointActive>());
        }

        protected override void OnUpdate()
        {
            Dependency = new AdvanceJob
            {
                TimerType = GetComponentTypeHandle<CombatPointTimedTrigger>(false),
                IntentType = GetBufferTypeHandle<CombatPointProjectileIntent>(false),
                RuleType = GetBufferTypeHandle<CombatPointTimedPresentationRule>(true),
                PresentationType = GetBufferTypeHandle<CombatPointPresentationIntent>(false),
                DeltaMs = DeltaMs
            }.ScheduleParallel(m_Query, Dependency);
            Dependency.Complete();
        }
    }
}
