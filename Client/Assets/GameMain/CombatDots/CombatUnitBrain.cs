using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Zizouqi.CombatDots
{
    public enum CombatUnitBrainPhase : byte
    {
        Idle,
        Moving,
        UnderControl,
        BattleWon,
        BattleLost
    }

    public struct CombatUnitBrainState : IComponentData
    {
        public CombatUnitBrainPhase Phase;
    }

    public struct CombatUnitBrainPlan : IComponentData
    {
        public bool EvaluateSkills;
    }

    [InternalBufferCapacity(4)]
    public struct CombatUnitBrainCandidate : IBufferElementData
    {
        public int SkillIndex;
        public int Result;
        public int TargetUid;
        public bool PathFound;
        public int StartRow;
        public int StartColumn;
        public int TargetRow;
        public int TargetColumn;
        public int NextRow;
        public int NextColumn;
    }

    // Skill eligibility is published before this system. Each unit independently
    // filters it in its current rotation order; path reservations and casts are
    // then committed in the established friendly/enemy UID order.
    [DisableAutoCreation]
    public partial class CombatUnitBrainSystem : SystemBase
    {
        private EntityQuery m_Query;

        [BurstCompile]
        private struct PlanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<CombatUnit> UnitType;
            [ReadOnly] public ComponentTypeHandle<CombatUnitBrainState> StateType;
            [ReadOnly] public ComponentTypeHandle<CombatAttackCast> CastType;
            public ComponentTypeHandle<CombatUnitBrainPlan> PlanType;
            [ReadOnly] public BufferTypeHandle<CombatUnitSkillIntent> SkillType;
            public BufferTypeHandle<CombatUnitBrainCandidate> CandidateType;
            [ReadOnly] public ComponentLookup<CombatMovement> MovementLookup;
            [ReadOnly] public BufferTypeHandle<CombatSkillSlot> SlotType;
            [ReadOnly] public NativeArray<CombatBoardCell> Cells;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Entity> entities = chunk.GetNativeArray(EntityType);
                NativeArray<CombatUnit> units = chunk.GetNativeArray(ref UnitType);
                NativeArray<CombatUnitBrainState> states =
                    chunk.GetNativeArray(ref StateType);
                NativeArray<CombatAttackCast> casts =
                    chunk.GetNativeArray(ref CastType);
                NativeArray<CombatUnitBrainPlan> plans =
                    chunk.GetNativeArray(ref PlanType);
                BufferAccessor<CombatUnitSkillIntent> skills =
                    chunk.GetBufferAccessor(ref SkillType);
                BufferAccessor<CombatUnitBrainCandidate> candidates =
                    chunk.GetBufferAccessor(ref CandidateType);
                BufferAccessor<CombatSkillSlot> slots =
                    chunk.GetBufferAccessor(ref SlotType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask,
                    chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out int index))
                {
                    DynamicBuffer<CombatUnitBrainCandidate> output =
                        candidates[index];
                    output.Clear();
                    CombatUnit unit = units[index];
                    CombatUnitBrainPhase phase = states[index].Phase;
                    bool evaluate = unit.Alive && unit.ActiveOnBoard &&
                                    !casts[index].Active &&
                                    (phase == CombatUnitBrainPhase.Idle ||
                                     phase == CombatUnitBrainPhase.Moving);
                    if (evaluate && phase == CombatUnitBrainPhase.Moving &&
                        MovementLookup.HasComponent(entities[index]))
                    {
                        CombatMovement movement = MovementLookup[entities[index]];
                        evaluate = !movement.StepReady || movement.Arrived;
                    }
                    plans[index] = new CombatUnitBrainPlan
                    {
                        EvaluateSkills = evaluate
                    };
                    if (!evaluate) continue;
                    DynamicBuffer<CombatUnitSkillIntent> input = skills[index];
                    if (input.Length == 0) continue;
                    int start = unit.NextNormalSkillIndex;
                    if (start < 0 || start >= input.Length) start = 0;
                    for (int offset = 0; offset < input.Length; offset++)
                    {
                        int skillIndex = start + offset;
                        if (skillIndex >= input.Length) skillIndex -= input.Length;
                        CombatUnitSkillIntent intent = default;
                        bool found = false;
                        for (int source = 0; source < input.Length; source++)
                        {
                            if (input[source].SkillIndex != skillIndex) continue;
                            intent = input[source];
                            found = true;
                            break;
                        }
                        if (!found) continue;
                        if (intent.Result != 1 && intent.Result != 3 &&
                            intent.Result != 4) continue;
                        var candidate = new CombatUnitBrainCandidate
                        {
                            SkillIndex = intent.SkillIndex,
                            Result = intent.Result,
                            TargetUid = intent.TargetUid
                        };
                        if (intent.Result == 3 && intent.TargetUid != 0)
                        {
                            int rangeMm = 0;
                            DynamicBuffer<CombatSkillSlot> unitSlots = slots[index];
                            for (int slot = 0; slot < unitSlots.Length; slot++)
                            {
                                if (unitSlots[slot].Kind !=
                                        CombatSkillSlotKind.Normal ||
                                    unitSlots[slot].Index != intent.SkillIndex)
                                    continue;
                                rangeMm = unitSlots[slot].RangeMm;
                                break;
                            }
                            candidate.PathFound = TryFindPath(unit.HeroUid,
                                intent.TargetUid, rangeMm, ref candidate);
                        }
                        output.Add(candidate);
                    }
                }
            }

            private bool TryFindPath(int sourceUid, int targetUid,
                int rangeMm, ref CombatUnitBrainCandidate result)
            {
                const int columns = CombatBoardBridge.Columns;
                const int rows = CombatBoardBridge.Rows;
                const int cellCount = columns * rows;
                if (Cells.Length != cellCount) return false;
                int start = -1;
                int end = -1;
                for (int i = 0; i < cellCount; i++)
                {
                    if (Cells[i].HeroUid == sourceUid) start = i;
                    if (Cells[i].HeroUid == targetUid) end = i;
                }
                if (start < 0 || end < 0) return false;
                result.StartRow = start / columns;
                result.StartColumn = start % columns;
                result.TargetRow = end / columns;
                result.TargetColumn = end % columns;
                FixedList4096Bytes<int> open = default;
                FixedList512Bytes<int> parents = default;
                FixedList512Bytes<float> costs = default;
                for (int i = 0; i < cellCount; i++)
                {
                    parents.Add(-1);
                    costs.Add(float.MaxValue);
                }
                open.Add(start);
                costs[start] = 0f;
                int last = start;
                bool found = false;
                while (open.Length > 0)
                {
                    int bestOpen = 0;
                    float bestScore = float.MaxValue;
                    for (int i = 0; i < open.Length; i++)
                    {
                        int cell = open[i];
                        float cost = costs[cell];
                        float score = cost * cost + DistanceSquared(
                            Cells[cell], Cells[end]);
                        if (score >= bestScore) continue;
                        bestOpen = i;
                        bestScore = score;
                    }
                    int current = open[bestOpen];
                    open.RemoveAt(bestOpen);
                    float distance = DistanceSquared(Cells[current], Cells[end]);
                    distance = (float)(math.floor((double)distance * 10000d) /
                        10000d);
                    if (distance <= rangeMm * rangeMm / 1000000f)
                    {
                        last = current;
                        found = true;
                        break;
                    }
                    int row = current / columns;
                    int column = current % columns;
                    for (int direction = 0; direction < 6; direction++)
                    {
                        int adjacentRow = row + (direction == 0 ||
                            direction == 4 ? 1 : direction == 1 ||
                            direction == 5 ? -1 : 0);
                        int columnOffset = direction == 2 ? -1 :
                            direction == 3 ? 1 : direction >= 4
                                ? (row & 1) == 0 ? 1 : -1 : 0;
                        int adjacentColumn = column + columnOffset;
                        if (adjacentRow < 0 || adjacentRow >= rows ||
                            adjacentColumn < 0 || adjacentColumn >= columns)
                            continue;
                        int adjacent = adjacentRow * columns + adjacentColumn;
                        float nextCost = costs[current] + 1f;
                        if (parents[adjacent] >= 0 &&
                            costs[adjacent] <= nextCost) continue;
                        if (Cells[adjacent].HeroUid > -1) continue;
                        if (open.Length >= 1000) return false;
                        open.Add(adjacent);
                        parents[adjacent] = current;
                        costs[adjacent] = nextCost;
                    }
                }
                if (!found) return false;
                int first = last;
                while (first != start)
                {
                    int parent = parents[first];
                    if (parent < 0) return false;
                    if (parent == start) break;
                    first = parent;
                }
                result.NextRow = first / columns;
                result.NextColumn = first % columns;
                return true;
            }

            private static float DistanceSquared(CombatBoardCell left,
                CombatBoardCell right)
            {
                float dx = left.X - right.X;
                float dz = left.Z - right.Z;
                return dx * dx + dz * dz;
            }
        }

        protected override void OnCreate()
        {
            m_Query = GetEntityQuery(
                ComponentType.ReadOnly<CombatUnit>(),
                ComponentType.ReadOnly<CombatUnitBrainState>(),
                ComponentType.ReadOnly<CombatAttackCast>(),
                ComponentType.ReadWrite<CombatUnitBrainPlan>(),
                ComponentType.ReadOnly<CombatUnitSkillIntent>(),
                ComponentType.ReadOnly<CombatSkillSlot>(),
                ComponentType.ReadWrite<CombatUnitBrainCandidate>());
        }

        protected override void OnUpdate()
        {
            if (m_Query.IsEmptyIgnoreFilter) return;
            using NativeArray<CombatBoardCell> cells =
                CombatBoardBridge.SnapshotCells(Allocator.TempJob);
            var job = new PlanJob
            {
                EntityType = GetEntityTypeHandle(),
                UnitType = GetComponentTypeHandle<CombatUnit>(true),
                StateType = GetComponentTypeHandle<CombatUnitBrainState>(true),
                CastType = GetComponentTypeHandle<CombatAttackCast>(true),
                PlanType = GetComponentTypeHandle<CombatUnitBrainPlan>(false),
                SkillType = GetBufferTypeHandle<CombatUnitSkillIntent>(true),
                CandidateType = GetBufferTypeHandle<CombatUnitBrainCandidate>(false),
                MovementLookup = GetComponentLookup<CombatMovement>(true),
                SlotType = GetBufferTypeHandle<CombatSkillSlot>(true),
                Cells = cells
            };
            if (m_Query.CalculateEntityCount() < 16)
            {
                Dependency.Complete();
                job.Run(m_Query);
            }
            else
            {
                Dependency = job.ScheduleParallel(m_Query, Dependency);
                Dependency.Complete();
            }
        }
    }

    public static class CombatUnitBrainBridge
    {
        public static void AdvanceAll()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            world.GetOrCreateSystemManaged<CombatUnitBrainSystem>().Update();
        }

        public static CombatUnitBrainPhase GetPhase(int handle)
        {
            Entity entity = GetEntity(handle, out EntityManager manager);
            return manager.GetComponentData<CombatUnitBrainState>(entity).Phase;
        }

        public static void SetPhase(int handle, CombatUnitBrainPhase phase)
        {
            Entity entity = GetEntity(handle, out EntityManager manager);
            manager.SetComponentData(entity, new CombatUnitBrainState
            {
                Phase = phase
            });
        }

        public static bool ShouldEvaluate(int handle)
        {
            Entity entity = GetEntity(handle, out EntityManager manager);
            return manager.GetComponentData<CombatUnitBrainPlan>(entity)
                .EvaluateSkills;
        }

        public static DynamicBuffer<CombatUnitBrainCandidate> GetCandidates(
            int handle)
        {
            Entity entity = GetEntity(handle, out EntityManager manager);
            return manager.GetBuffer<CombatUnitBrainCandidate>(entity);
        }

        private static Entity GetEntity(int handle, out EntityManager manager)
        {
            if (!CombatAttributeBridge.TryGetEntity(handle, out Entity entity))
                throw new InvalidOperationException("Combat brain unit was released.");
            manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            return entity;
        }
    }
}
