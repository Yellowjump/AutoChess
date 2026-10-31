using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Zizouqi.CombatDots
{
    public enum CombatConditionKind : byte
    {
        Always,
        Group,
        Percent,
        Timed,
        RelateItem,
        RelateSkill
    }

    public struct CombatConditionNode : IBufferElementData
    {
        public CombatConditionKind Kind;
        public bool Reverse;
        public byte Logic;
        public int EndIndex;
        public int Source;
        public int CheckType;
        public int Param1;
        public int Param2;
        public float AccumulatedMs;
        public int PassCount;
    }

    public struct CombatConditionContext
    {
        public bool HasSourceSkill;
        public bool HasArgSkill;
        public int SourceSkillType;
        public int ArgSkillType;
        public int SourceItemId;
        public int ArgItemId;
        public float DeltaMs;
    }

    public struct CombatTriggerExecutionState : IComponentData
    {
        public int Depth;
        public bool ReleaseRequested;
    }

    public struct CombatScheduledPointTimer : IComponentData
    {
        public int PointHandle;
        public int TimedNodeIndex;
        public bool Due;
    }

    public static class CombatConditionProgramBridge
    {
        [BurstCompile]
        private struct AdvancePointTimersJob : IJobChunk
        {
            public BufferTypeHandle<CombatConditionNode> NodesType;
            public ComponentTypeHandle<CombatScheduledPointTimer> TimerType;
            public float DeltaMs;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                bool useEnabledMask, in v128 chunkEnabledMask)
            {
                BufferAccessor<CombatConditionNode> buffers =
                    chunk.GetBufferAccessor(ref NodesType);
                NativeArray<CombatScheduledPointTimer> timers =
                    chunk.GetNativeArray(ref TimerType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask,
                    chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out int index))
                {
                    DynamicBuffer<CombatConditionNode> nodes = buffers[index];
                    CombatScheduledPointTimer timer = timers[index];
                    CombatConditionNode node = nodes[timer.TimedNodeIndex];
                    timer.Due = false;
                    if (node.Param2 == 0 || node.PassCount < node.Param2)
                    {
                        node.AccumulatedMs += DeltaMs;
                        if (node.AccumulatedMs >= node.Param1)
                        {
                            node.AccumulatedMs -= node.Param1;
                            node.PassCount++;
                            timer.Due = true;
                        }
                        nodes[timer.TimedNodeIndex] = node;
                    }
                    timers[index] = timer;
                }
            }
        }

        private static readonly Dictionary<int, Entity> Programs = new Dictionary<int, Entity>();
        private static World s_World;
        private static EntityQuery? s_PointTimerQuery;
        private static int s_NextHandle;

        public static bool IsValid(int handle)
        {
            return handle != 0 && TryGetManager(out EntityManager manager) &&
                Programs.TryGetValue(handle, out Entity entity) && manager.Exists(entity);
        }

        public static bool IsScheduledPointTimer(int handle)
        {
            return handle != 0 && TryGetManager(out EntityManager manager) &&
                Programs.TryGetValue(handle, out Entity entity) && manager.Exists(entity) &&
                manager.HasComponent<CombatScheduledPointTimer>(entity);
        }

        public static bool TrySchedulePointTimer(int handle, int pointHandle)
        {
            EntityManager manager = GetManager();
            if (pointHandle == 0 || !Programs.TryGetValue(handle, out Entity entity) ||
                !manager.Exists(entity))
            {
                return false;
            }
            DynamicBuffer<CombatConditionNode> nodes =
                manager.GetBuffer<CombatConditionNode>(entity);
            if (nodes.Length == 0 ||
                manager.HasComponent<CombatScheduledPointTimer>(entity))
            {
                return false;
            }
            int timedIndex = 0;
            while (nodes[timedIndex].Kind == CombatConditionKind.Group)
            {
                CombatConditionNode group = nodes[timedIndex];
                if (group.Reverse || timedIndex + 1 >= group.EndIndex ||
                    nodes[timedIndex + 1].EndIndex != group.EndIndex)
                {
                    return false;
                }
                timedIndex++;
            }
            CombatConditionNode timed = nodes[timedIndex];
            if (timed.Kind != CombatConditionKind.Timed || timed.Reverse ||
                timed.Param1 <= 0 || timed.EndIndex != nodes.Length)
            {
                return false;
            }
            manager.AddComponentData(entity, new CombatScheduledPointTimer
            {
                PointHandle = pointHandle,
                TimedNodeIndex = timedIndex
            });
            return true;
        }

        public static void AdvancePointTimers(float deltaMs, List<int> duePointHandles)
        {
            EntityManager manager = GetManager();
            if (!s_PointTimerQuery.HasValue)
            {
                s_PointTimerQuery = manager.CreateEntityQuery(
                    ComponentType.ReadWrite<CombatConditionNode>(),
                    ComponentType.ReadWrite<CombatScheduledPointTimer>());
            }
            EntityQuery query = s_PointTimerQuery.Value;
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }
            var job = new AdvancePointTimersJob
            {
                NodesType = manager.GetBufferTypeHandle<CombatConditionNode>(false),
                TimerType = manager.GetComponentTypeHandle<CombatScheduledPointTimer>(false),
                DeltaMs = deltaMs
            };
            job.ScheduleParallel(query, default(JobHandle)).Complete();
            using (NativeArray<CombatScheduledPointTimer> timers =
                   query.ToComponentDataArray<CombatScheduledPointTimer>(Allocator.Temp))
            {
                for (int i = 0; i < timers.Length; i++)
                {
                    if (timers[i].Due)
                    {
                        duePointHandles.Add(timers[i].PointHandle);
                    }
                }
            }
        }

        public static void Create(ref int handle, List<CombatConditionNode> nodes)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            Entity entity = manager.CreateEntity(typeof(CombatTriggerExecutionState));
            DynamicBuffer<CombatConditionNode> buffer = manager.AddBuffer<CombatConditionNode>(entity);
            for (int i = 0; i < nodes.Count; i++)
            {
                buffer.Add(nodes[i]);
            }
            handle = ++s_NextHandle;
            Programs.Add(handle, entity);
        }

        // A condition is evaluated at the exact point where OneTrigger checked it.
        // Callbacks run only for leaves reached after group short-circuiting. Do not
        // retain a DynamicBuffer view across callbacks that can change ECS structure.
        public static bool Check(int handle, CombatConditionContext context,
            Func<int> rollPercent, Func<int, int, bool> itemContains)
        {
            EntityManager manager = GetManager();
            if (!Programs.TryGetValue(handle, out Entity entity) || !manager.Exists(entity))
            {
                throw new InvalidOperationException("Combat condition program was released before evaluation.");
            }
            DynamicBuffer<CombatConditionNode> nodes = manager.GetBuffer<CombatConditionNode>(entity);
            return nodes.Length != 0 && Evaluate(manager, entity, 0, context, true,
                rollPercent, itemContains);
        }

        // Only a single timed leaf can be advanced by the simple-trigger batch.
        // More complex trees retain the ordered interpreter path.
        public static bool TryGetSingleTimedNode(int handle,
            out CombatConditionNode node, out bool reverse)
        {
            node = default;
            reverse = false;
            if (handle == 0 || !TryGetManager(out EntityManager manager) ||
                !Programs.TryGetValue(handle, out Entity entity) ||
                !manager.Exists(entity)) return false;
            DynamicBuffer<CombatConditionNode> nodes =
                manager.GetBuffer<CombatConditionNode>(entity);
            int index = 0;
            while (index < nodes.Length &&
                   nodes[index].Kind == CombatConditionKind.Group)
            {
                CombatConditionNode group = nodes[index];
                if (index + 1 >= nodes.Length ||
                    group.EndIndex != nodes.Length ||
                    nodes[index + 1].EndIndex != group.EndIndex)
                    return false;
                reverse ^= group.Reverse;
                index++;
            }
            if (index >= nodes.Length ||
                nodes[index].Kind != CombatConditionKind.Timed ||
                nodes[index].EndIndex != nodes.Length ||
                nodes[index].Param1 <= 0) return false;
            node = nodes[index];
            reverse ^= node.Reverse;
            return true;
        }

        public static bool TryGetPureSkillGroup(int handle, out Entity entity)
        {
            entity = Entity.Null;
            if (handle == 0 || !TryGetManager(out EntityManager manager) ||
                !Programs.TryGetValue(handle, out Entity candidate) ||
                !manager.Exists(candidate)) return false;
            DynamicBuffer<CombatConditionNode> nodes =
                manager.GetBuffer<CombatConditionNode>(candidate);
            if (nodes.Length == 0 || nodes.Length > 128 ||
                nodes[0].Kind != CombatConditionKind.Group ||
                nodes[0].EndIndex != nodes.Length) return false;
            for (int i = 0; i < nodes.Length; i++)
            {
                CombatConditionNode node = nodes[i];
                if (node.EndIndex <= i || node.EndIndex > nodes.Length)
                    return false;
                if (node.Kind == CombatConditionKind.Group)
                {
                    if (node.Logic > 1) return false;
                }
                else if (node.Kind != CombatConditionKind.Always &&
                         node.Kind != CombatConditionKind.RelateSkill)
                    return false;
            }
            entity = candidate;
            return true;
        }

        // Timed leaves mutate their own ECS nodes. The batch processes all
        // invocations of one program in order and different programs in parallel.
        public static bool TryGetTimedSkillGroup(int handle, out Entity entity)
        {
            entity = Entity.Null;
            if (handle == 0 || !TryGetManager(out EntityManager manager) ||
                !Programs.TryGetValue(handle, out Entity candidate) ||
                !manager.Exists(candidate)) return false;
            DynamicBuffer<CombatConditionNode> nodes =
                manager.GetBuffer<CombatConditionNode>(candidate);
            if (nodes.Length == 0 || nodes.Length > 128 ||
                nodes[0].Kind != CombatConditionKind.Group ||
                nodes[0].EndIndex != nodes.Length) return false;
            bool hasTimed = false;
            for (int i = 0; i < nodes.Length; i++)
            {
                CombatConditionNode node = nodes[i];
                if (node.EndIndex <= i || node.EndIndex > nodes.Length)
                    return false;
                switch (node.Kind)
                {
                    case CombatConditionKind.Group:
                        if (node.Logic > 1) return false;
                        break;
                    case CombatConditionKind.Timed:
                        if (node.Param1 <= 0) return false;
                        hasTimed = true;
                        break;
                    case CombatConditionKind.Always:
                    case CombatConditionKind.RelateSkill:
                        break;
                    default:
                        return false;
                }
            }
            if (!hasTimed) return false;
            entity = candidate;
            return true;
        }

        public static void SetSingleTimedNode(int handle,
            in CombatConditionNode node)
        {
            EntityManager manager = GetManager();
            if (!Programs.TryGetValue(handle, out Entity entity) ||
                !manager.Exists(entity))
                throw new InvalidOperationException(
                    "Combat timed condition was released before batch completion.");
            DynamicBuffer<CombatConditionNode> nodes =
                manager.GetBuffer<CombatConditionNode>(entity);
            int index = nodes.Length - 1;
            if (index < 0 || nodes[index].Kind != CombatConditionKind.Timed)
                throw new InvalidOperationException(
                    "Combat timed condition changed during batch completion.");
            nodes[index] = node;
        }

        public static void Release(ref int handle)
        {
            int releasedHandle = handle;
            if (handle != 0 && TryGetManager(out EntityManager manager) &&
                Programs.TryGetValue(handle, out Entity entity) && manager.Exists(entity))
            {
                if (manager.HasComponent<CombatScheduledPointTimer>(entity))
                {
                    CombatPosPointBridge.ClearScheduledTick(
                        manager.GetComponentData<CombatScheduledPointTimer>(entity).PointHandle);
                }
                CombatTriggerExecutionState state =
                    manager.GetComponentData<CombatTriggerExecutionState>(entity);
                if (state.Depth > 0)
                {
                    state.ReleaseRequested = true;
                    manager.SetComponentData(entity, state);
                    handle = 0;
                    return;
                }
                manager.DestroyEntity(entity);
            }
            Programs.Remove(releasedHandle);
            handle = 0;
        }

        public static void EnterInvocation(int handle)
        {
            EntityManager manager = GetManager();
            if (!Programs.TryGetValue(handle, out Entity entity) || !manager.Exists(entity))
            {
                throw new InvalidOperationException("Combat condition program was released before invocation.");
            }
            CombatTriggerExecutionState state =
                manager.GetComponentData<CombatTriggerExecutionState>(entity);
            state.Depth++;
            manager.SetComponentData(entity, state);
        }

        public static bool ExitInvocation(int handle)
        {
            if (!TryGetManager(out EntityManager manager) ||
                !Programs.TryGetValue(handle, out Entity entity) || !manager.Exists(entity))
            {
                return true;
            }
            CombatTriggerExecutionState state =
                manager.GetComponentData<CombatTriggerExecutionState>(entity);
            if (state.Depth <= 0)
            {
                throw new InvalidOperationException("Combat trigger invocation depth underflow.");
            }
            state.Depth--;
            bool outermost = state.Depth == 0;
            if (outermost && state.ReleaseRequested)
            {
                manager.DestroyEntity(entity);
                Programs.Remove(handle);
            }
            else
            {
                manager.SetComponentData(entity, state);
            }
            return outermost;
        }

        private static bool Evaluate(EntityManager manager, Entity entity, int index,
            CombatConditionContext context, bool passArg, Func<int> rollPercent,
            Func<int, int, bool> itemContains)
        {
            CombatConditionNode node = manager.GetBuffer<CombatConditionNode>(entity)[index];
            bool result;
            switch (node.Kind)
            {
                case CombatConditionKind.Group:
                    result = EvaluateGroup(manager, entity, index, node, context,
                        rollPercent, itemContains);
                    break;
                case CombatConditionKind.Percent:
                    result = rollPercent() < node.Param1;
                    break;
                case CombatConditionKind.Timed:
                    if (node.Param2 != 0 && node.Param2 <= node.PassCount)
                    {
                        result = false;
                        break;
                    }
                    node.AccumulatedMs += context.DeltaMs;
                    result = node.AccumulatedMs >= node.Param1;
                    if (result)
                    {
                        node.AccumulatedMs -= node.Param1;
                        node.PassCount++;
                    }
                    DynamicBuffer<CombatConditionNode> timedNodes =
                        manager.GetBuffer<CombatConditionNode>(entity);
                    timedNodes[index] = node;
                    break;
                case CombatConditionKind.RelateSkill:
                    bool hasSkill = node.Source == 0 ? context.HasSourceSkill :
                        node.Source == 1 && passArg && context.HasArgSkill;
                    int skillType = node.Source == 0 ? context.SourceSkillType : context.ArgSkillType;
                    result = hasSkill && node.CheckType == 0 && skillType == node.Param1;
                    break;
                case CombatConditionKind.RelateItem:
                    // Both serialized ItemFrom values use the arg skill in the current rules.
                    int itemId = (node.Source == 0 || node.Source == 1)
                        ? (passArg && context.HasArgSkill ? context.ArgItemId : 0)
                        : context.SourceItemId;
                    result = itemId != 0 && node.CheckType == 0 && itemContains(itemId, node.Param1);
                    break;
                default:
                    result = true;
                    break;
            }
            return node.Reverse ? !result : result;
        }

        private static bool EvaluateGroup(EntityManager manager, Entity entity, int index,
            CombatConditionNode group, CombatConditionContext context, Func<int> rollPercent,
            Func<int, int, bool> itemContains)
        {
            if (index + 1 == group.EndIndex)
            {
                return true;
            }
            bool result = group.Logic == 0;
            int child = index + 1;
            while (child < group.EndIndex)
            {
                int next = manager.GetBuffer<CombatConditionNode>(entity)[child].EndIndex;
                // ConditionGroup.OnCheck did not forward arg to its children.
                bool childResult = Evaluate(manager, entity, child, context, false,
                    rollPercent, itemContains);
                if (group.Logic == 0)
                {
                    result = result && childResult;
                    if (!result) return false;
                }
                else if (group.Logic == 1)
                {
                    result = result || childResult;
                    if (result) return true;
                }
                child = next;
            }
            return result;
        }

        private static EntityManager GetManager()
        {
            if (!TryGetManager(out EntityManager manager))
            {
                throw new InvalidOperationException("Combat conditions require an active ECS World.");
            }
            return manager;
        }

        private static bool TryGetManager(out EntityManager manager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                if (s_PointTimerQuery.HasValue && s_World != null && s_World.IsCreated)
                {
                    s_PointTimerQuery.Value.Dispose();
                }
                s_PointTimerQuery = null;
                Programs.Clear();
                s_World = world;
            }
            if (world == null || !world.IsCreated)
            {
                manager = default;
                return false;
            }
            manager = world.EntityManager;
            return true;
        }
    }
}
