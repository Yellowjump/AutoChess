using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Zizouqi.CombatDots
{
    [DisableAutoCreation]
    public partial class CombatPureSkillGroupLookupSystem : SystemBase
    {
        protected override void OnUpdate() { }

        public BufferLookup<CombatConditionNode> Nodes() =>
            GetBufferLookup<CombatConditionNode>(true);

        public BufferLookup<CombatConditionNode> MutableNodes() =>
            GetBufferLookup<CombatConditionNode>(false);
    }

    public enum CombatSimplePicker : byte
    {
        Direct,
        Nearest,
        OwnerDistance,
        OwnerDirection
    }

    [InternalBufferCapacity(32)]
    public struct CombatSimpleTriggerInput : IBufferElementData
    {
        public CombatSimplePicker Picker;
        public int DirectUid;
        public int SourceCamp;
        public int TargetCamp;
        public float SourceX;
        public float SourceZ;
        public float Radius;
        public float FacingX;
        public float FacingZ;
        public float AngleDegrees;
        public float MidX;
        public float MidZ;
        public float MidLength;
        public float Edge1X;
        public float Edge1Z;
        public float Edge1Length;
        public float Edge2X;
        public float Edge2Z;
        public float Edge2Length;
        public int DamageMode;
        public int DamageFixedValue;
        public int DamageBaseValue;
        public int DamagePercent;
        public bool CheckPercent;
        public int PercentRoll;
        public int PercentThreshold;
        public bool ReverseCondition;
        public bool CheckTimed;
        public bool TimedPassed;
        public float TimedDeltaMs;
        public bool CheckRelateSkill;
        public bool RelateSkillReverse;
        public bool RelateSkillPassArg;
        public bool HasSourceSkill;
        public bool HasArgSkill;
        public int SourceSkillType;
        public int ArgSkillType;
        public int RelateSkillSource;
        public int RelateSkillCheckType;
        public int RelateSkillExpectedType;
        public bool CheckPureSkillGroup;
        public bool PureSkillGroupPassed;
        public Entity PureSkillGroupEntity;
        public bool CheckTimedSkillGroup;
        public bool TimedSkillGroupPassed;
        public Entity TimedSkillGroupEntity;
    }

    [InternalBufferCapacity(32)]
    public struct CombatSimpleTriggerResult : IBufferElementData
    {
        public int TargetUid;
        public float Damage;
    }

    [InternalBufferCapacity(0)]
    public struct CombatSimpleTargetCandidate : IBufferElementData
    {
        public int HeroUid;
        public int Camp;
    }

    // One snapshot for all eligible invocations in a logic phase. Results keep
    // input order, so effects can be committed deterministically on the main thread.
    public static class CombatTriggerSimpleBatch
    {
        private struct PureGroupFrame
        {
            public int EndIndex;
            public byte Logic;
            public bool Reverse;
            public bool Value;
        }

        [BurstCompile]
        private struct EvaluatePureSkillGroupsJob : IJobParallelFor
        {
            [ReadOnly] public BufferLookup<CombatConditionNode> Nodes;
            public NativeArray<CombatSimpleTriggerInput> Inputs;

            public void Execute(int index)
            {
                CombatSimpleTriggerInput input = Inputs[index];
                if (!input.CheckPureSkillGroup) return;
                input.PureSkillGroupPassed =
                    Nodes.HasBuffer(input.PureSkillGroupEntity) &&
                    Evaluate(Nodes[input.PureSkillGroupEntity], input);
                Inputs[index] = input;
            }

            private static bool Evaluate(DynamicBuffer<CombatConditionNode> nodes,
                CombatSimpleTriggerInput input)
            {
                FixedList512Bytes<PureGroupFrame> stack = default;
                int index = 0;
                while (index < nodes.Length)
                {
                    CombatConditionNode node = nodes[index];
                    if (node.Kind == CombatConditionKind.Group &&
                        index + 1 < node.EndIndex)
                    {
                        stack.Add(new PureGroupFrame
                        {
                            EndIndex = node.EndIndex,
                            Logic = node.Logic,
                            Reverse = node.Reverse,
                            Value = node.Logic == 0
                        });
                        index++;
                        continue;
                    }
                    bool value;
                    if (node.Kind == CombatConditionKind.Group ||
                        node.Kind == CombatConditionKind.Always)
                        value = true;
                    else
                        value = node.Source == 0 && input.HasSourceSkill &&
                            node.CheckType == 0 &&
                            input.SourceSkillType == node.Param1;
                    if (node.Reverse) value = !value;
                    index = node.EndIndex;
                    while (stack.Length != 0)
                    {
                        int top = stack.Length - 1;
                        PureGroupFrame frame = stack[top];
                        frame.Value = frame.Logic == 0
                            ? frame.Value && value : frame.Value || value;
                        stack[top] = frame;
                        if (index < frame.EndIndex) break;
                        value = frame.Reverse ? !frame.Value : frame.Value;
                        index = frame.EndIndex;
                        stack.RemoveAt(top);
                    }
                    if (stack.Length == 0 && index == nodes.Length)
                        return value;
                }
                return false;
            }
        }

        public static void EnsurePureGroupLookup()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                throw new InvalidOperationException(
                    "Combat trigger batch requires an active ECS World.");
            world.GetOrCreateSystemManaged<CombatPureSkillGroupLookupSystem>();
        }

        public static void PreparePureSkillGroups(
            NativeArray<CombatSimpleTriggerInput> inputs)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            CombatPureSkillGroupLookupSystem lookup =
                world.GetOrCreateSystemManaged<CombatPureSkillGroupLookupSystem>();
            var job = new EvaluatePureSkillGroupsJob
            {
                Nodes = lookup.Nodes(),
                Inputs = inputs
            };
            if (inputs.Length >= 16)
                job.Schedule(inputs.Length, 8).Complete();
            else
                job.Run(inputs.Length);
        }

        private struct TimedSkillGroupState
        {
            public Entity Entity;
            public int First;
            public int Last;
        }

        [BurstCompile]
        private struct EvaluateTimedSkillGroupsJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public BufferLookup<CombatConditionNode> Nodes;
            [ReadOnly] public NativeArray<CombatSimpleTriggerInput> Inputs;
            [ReadOnly] public NativeArray<int> Next;
            [ReadOnly] public NativeArray<TimedSkillGroupState> Groups;
            [NativeDisableParallelForRestriction] public NativeArray<byte> Passed;

            public void Execute(int index)
            {
                TimedSkillGroupState group = Groups[index];
                DynamicBuffer<CombatConditionNode> nodes = Nodes[group.Entity];
                for (int selection = group.First; selection >= 0;
                     selection = Next[selection])
                    Passed[selection] = Evaluate(nodes, Inputs[selection]) ?
                        (byte)1 : (byte)0;
            }

            private static bool Evaluate(DynamicBuffer<CombatConditionNode> nodes,
                CombatSimpleTriggerInput input)
            {
                FixedList512Bytes<PureGroupFrame> stack = default;
                int index = 0;
                while (index < nodes.Length)
                {
                    CombatConditionNode node = nodes[index];
                    if (node.Kind == CombatConditionKind.Group &&
                        index + 1 < node.EndIndex)
                    {
                        stack.Add(new PureGroupFrame
                        {
                            EndIndex = node.EndIndex,
                            Logic = node.Logic,
                            Reverse = node.Reverse,
                            Value = node.Logic == 0
                        });
                        index++;
                        continue;
                    }
                    bool value;
                    switch (node.Kind)
                    {
                        case CombatConditionKind.Timed:
                            value = false;
                            if (node.Param2 == 0 || node.PassCount < node.Param2)
                            {
                                node.AccumulatedMs += input.TimedDeltaMs;
                                value = node.AccumulatedMs >= node.Param1;
                                if (value)
                                {
                                    node.AccumulatedMs -= node.Param1;
                                    node.PassCount++;
                                }
                                nodes[index] = node;
                            }
                            break;
                        case CombatConditionKind.RelateSkill:
                            value = node.Source == 0 && input.HasSourceSkill &&
                                node.CheckType == 0 &&
                                input.SourceSkillType == node.Param1;
                            break;
                        default:
                            value = true;
                            break;
                    }
                    if (node.Reverse) value = !value;
                    index = node.EndIndex;
                    while (stack.Length != 0)
                    {
                        int top = stack.Length - 1;
                        PureGroupFrame frame = stack[top];
                        frame.Value = frame.Logic == 0
                            ? frame.Value && value : frame.Value || value;
                        stack[top] = frame;
                        if (index < frame.EndIndex &&
                            (frame.Logic == 0 ? frame.Value : !frame.Value))
                            break;
                        value = frame.Reverse ? !frame.Value : frame.Value;
                        index = frame.EndIndex;
                        stack.RemoveAt(top);
                    }
                    if (stack.Length == 0 && index == nodes.Length)
                        return value;
                }
                return false;
            }
        }

        private static readonly Dictionary<int, int> TimedSkillGroupIndices =
            new Dictionary<int, int>(32);
        private static readonly List<TimedSkillGroupState> TimedSkillGroups =
            new List<TimedSkillGroupState>(32);

        public static void PrepareTimedSkillGroups(
            NativeArray<CombatSimpleTriggerInput> inputs,
            IReadOnlyList<int> conditionHandles)
        {
            if (inputs.Length != conditionHandles.Count)
                throw new InvalidOperationException(
                    "Simple trigger condition count does not match inputs.");
            NativeArray<int> next = default;
            NativeArray<byte> passed = default;
            NativeArray<TimedSkillGroupState> groups = default;
            try
            {
                next = new NativeArray<int>(inputs.Length, Allocator.TempJob);
                for (int i = 0; i < inputs.Length; i++)
                {
                    CombatSimpleTriggerInput input = inputs[i];
                    if (!input.CheckTimedSkillGroup) continue;
                    next[i] = -1;
                    int handle = conditionHandles[i];
                    if (!TimedSkillGroupIndices.TryGetValue(handle,
                            out int groupIndex))
                    {
                        groupIndex = TimedSkillGroups.Count;
                        TimedSkillGroupIndices.Add(handle, groupIndex);
                        TimedSkillGroups.Add(new TimedSkillGroupState
                        {
                            Entity = input.TimedSkillGroupEntity,
                            First = i,
                            Last = i
                        });
                    }
                    else
                    {
                        TimedSkillGroupState group = TimedSkillGroups[groupIndex];
                        next[group.Last] = i;
                        group.Last = i;
                        TimedSkillGroups[groupIndex] = group;
                    }
                }
                if (TimedSkillGroups.Count == 0) return;
                groups = new NativeArray<TimedSkillGroupState>(
                    TimedSkillGroups.Count, Allocator.TempJob);
                passed = new NativeArray<byte>(inputs.Length, Allocator.TempJob);
                for (int i = 0; i < groups.Length; i++)
                    groups[i] = TimedSkillGroups[i];
                World world = World.DefaultGameObjectInjectionWorld;
                CombatPureSkillGroupLookupSystem lookup =
                    world.GetOrCreateSystemManaged<CombatPureSkillGroupLookupSystem>();
                var job = new EvaluateTimedSkillGroupsJob
                {
                    Nodes = lookup.MutableNodes(),
                    Inputs = inputs,
                    Next = next,
                    Groups = groups,
                    Passed = passed
                };
                if (groups.Length >= 8)
                    job.Schedule(groups.Length, 1).Complete();
                else
                    job.Run(groups.Length);
                for (int i = 0; i < inputs.Length; i++)
                {
                    CombatSimpleTriggerInput input = inputs[i];
                    if (!input.CheckTimedSkillGroup) continue;
                    input.TimedSkillGroupPassed = passed[i] != 0;
                    inputs[i] = input;
                }
            }
            finally
            {
                if (groups.IsCreated) groups.Dispose();
                if (passed.IsCreated) passed.Dispose();
                if (next.IsCreated) next.Dispose();
                TimedSkillGroups.Clear();
                TimedSkillGroupIndices.Clear();
            }
        }

        private struct TimedGroupState
        {
            public int Handle;
            public int First;
            public int Last;
            public CombatConditionNode Node;
            public bool Reverse;
        }

        [BurstCompile]
        private struct EvaluateTimedGroupsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CombatSimpleTriggerInput> Inputs;
            [ReadOnly] public NativeArray<int> Next;
            public NativeArray<TimedGroupState> Groups;
            [NativeDisableParallelForRestriction]
            public NativeArray<byte> Passed;

            public void Execute(int index)
            {
                TimedGroupState group = Groups[index];
                CombatConditionNode node = group.Node;
                for (int selection = group.First; selection >= 0;
                     selection = Next[selection])
                {
                    bool result = false;
                    if (node.Param2 == 0 || node.PassCount < node.Param2)
                    {
                        node.AccumulatedMs += Inputs[selection].TimedDeltaMs;
                        if (node.AccumulatedMs >= node.Param1)
                        {
                            node.AccumulatedMs -= node.Param1;
                            node.PassCount++;
                            result = true;
                        }
                    }
                    Passed[selection] = (byte)((group.Reverse ? !result : result)
                        ? 1 : 0);
                }
                group.Node = node;
                Groups[index] = group;
            }
        }

        private static readonly Dictionary<int, int> TimedGroupIndices =
            new Dictionary<int, int>(32);
        private static readonly List<TimedGroupState> TimedGroups =
            new List<TimedGroupState>(32);

        // Each condition program owns one group. Invocations of the same
        // trigger advance in order; different triggers can run in parallel.
        public static void PrepareTimed(
            NativeArray<CombatSimpleTriggerInput> inputs,
            IReadOnlyList<int> conditionHandles)
        {
            if (inputs.Length != conditionHandles.Count)
                throw new InvalidOperationException(
                    "Simple trigger condition count does not match inputs.");
            NativeArray<int> next = default;
            NativeArray<byte> passed = default;
            NativeArray<TimedGroupState> groups = default;
            try
            {
                next = new NativeArray<int>(inputs.Length, Allocator.TempJob);
                for (int i = 0; i < inputs.Length; i++)
                {
                    CombatSimpleTriggerInput input = inputs[i];
                    if (!input.CheckTimed) continue;
                    next[i] = -1;
                    int handle = conditionHandles[i];
                    if (!TimedGroupIndices.TryGetValue(handle, out int groupIndex))
                    {
                        if (!CombatConditionProgramBridge.TryGetSingleTimedNode(
                                handle, out CombatConditionNode node,
                                out bool reverse))
                            throw new InvalidOperationException(
                                "Timed trigger was released before evaluation.");
                        groupIndex = TimedGroups.Count;
                        TimedGroupIndices.Add(handle, groupIndex);
                        TimedGroups.Add(new TimedGroupState
                        {
                            Handle = handle,
                            First = i,
                            Last = i,
                            Node = node,
                            Reverse = reverse
                        });
                    }
                    else
                    {
                        TimedGroupState group = TimedGroups[groupIndex];
                        next[group.Last] = i;
                        group.Last = i;
                        TimedGroups[groupIndex] = group;
                    }
                }
                if (TimedGroups.Count == 0) return;
                groups = new NativeArray<TimedGroupState>(TimedGroups.Count,
                    Allocator.TempJob);
                passed = new NativeArray<byte>(inputs.Length, Allocator.TempJob);
                for (int i = 0; i < TimedGroups.Count; i++)
                    groups[i] = TimedGroups[i];
                var job = new EvaluateTimedGroupsJob
                {
                    Inputs = inputs,
                    Next = next,
                    Groups = groups,
                    Passed = passed
                };
                if (groups.Length >= 8)
                    job.Schedule(groups.Length, 1).Complete();
                else
                    job.Run(groups.Length);
                for (int i = 0; i < groups.Length; i++)
                    CombatConditionProgramBridge.SetSingleTimedNode(
                        groups[i].Handle, groups[i].Node);
                for (int i = 0; i < inputs.Length; i++)
                {
                    CombatSimpleTriggerInput input = inputs[i];
                    if (!input.CheckTimed) continue;
                    input.TimedPassed = passed[i] != 0;
                    inputs[i] = input;
                }
            }
            finally
            {
                if (groups.IsCreated) groups.Dispose();
                if (passed.IsCreated) passed.Dispose();
                if (next.IsCreated) next.Dispose();
                TimedGroups.Clear();
                TimedGroupIndices.Clear();
            }
        }

        [BurstCompile]
        private struct SelectAndPrepareJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CombatUnit> Units;
            [ReadOnly] public NativeArray<CombatSimpleTriggerInput> Inputs;
            [WriteOnly] public NativeArray<CombatSimpleTriggerResult> Results;

            public void Execute(int index)
            {
                CombatSimpleTriggerInput input = Inputs[index];
                if (!PassesCondition(input))
                {
                    Results[index] = default;
                    return;
                }
                int targetUid = input.DirectUid;
                if (input.Picker == CombatSimplePicker.Nearest)
                {
                    targetUid = 0;
                    float bestDistance = float.MaxValue;
                    int bestCampOrder = int.MaxValue;
                    for (int i = 0; i < Units.Length; i++)
                    {
                        CombatUnit unit = Units[i];
                        if (!unit.Alive || !unit.ActiveOnBoard ||
                            !MatchesCamp(input.SourceCamp, input.TargetCamp,
                                unit.Camp)) continue;
                        float dx = unit.Position.x - input.SourceX;
                        float dz = unit.Position.z - input.SourceZ;
                        float distance = dx * dx + dz * dz;
                        int campOrder = unit.Camp == 0 ? 0 : 1;
                        if (distance < bestDistance ||
                            (distance == bestDistance &&
                             (campOrder < bestCampOrder ||
                              (campOrder == bestCampOrder &&
                               unit.HeroUid < targetUid))))
                        {
                            bestDistance = distance;
                            bestCampOrder = campOrder;
                            targetUid = unit.HeroUid;
                        }
                    }
                }
                float damage = input.DamageMode == 0
                    ? input.DamageBaseValue
                    : input.DamageMode == 1
                        ? input.DamageFixedValue +
                          (input.DamagePercent > 0
                              ? input.DamageBaseValue * input.DamagePercent / 100
                              : 0f)
                        : 0f;
                Results[index] = new CombatSimpleTriggerResult
                {
                    TargetUid = targetUid,
                    Damage = damage
                };
            }

        }

        [BurstCompile]
        private struct FindAreaTargetsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CombatUnit> Units;
            [ReadOnly] public NativeArray<CombatSimpleTriggerInput> Inputs;
            [ReadOnly] public NativeArray<int> SelectionIndices;
            [WriteOnly] public NativeArray<CombatSimpleTargetCandidate> Candidates;

            public void Execute(int index)
            {
                int unitCount = Units.Length;
                CombatSimpleTriggerInput input =
                    Inputs[SelectionIndices[index / unitCount]];
                CombatUnit unit = Units[index % unitCount];
                bool isDistance = input.Picker == CombatSimplePicker.OwnerDistance;
                bool isDirection = input.Picker == CombatSimplePicker.OwnerDirection;
                if ((!isDistance && !isDirection) ||
                    (isDistance && input.Radius <= 0f) ||
                    !unit.Alive || !unit.ActiveOnBoard ||
                    (isDistance && !MatchesCamp(input.SourceCamp,
                        input.TargetCamp, unit.Camp)) ||
                    (isDirection && (unit.Camp == input.SourceCamp ||
                                     (input.SourceCamp != 0 &&
                                      input.SourceCamp != 1))) ||
                    !PassesCondition(input))
                {
                    Candidates[index] = default;
                    return;
                }
                float dx = unit.Position.x - input.SourceX;
                float dz = unit.Position.z - input.SourceZ;
                bool inArea = isDistance
                    ? math.sqrt(dx * dx + dz * dz) < input.Radius
                    : IsSectorHit(input, dx, dz);
                Candidates[index] = inArea
                    ? new CombatSimpleTargetCandidate
                    {
                        HeroUid = unit.HeroUid,
                        Camp = unit.Camp
                    }
                    : default;
            }

            private static bool IsSectorHit(CombatSimpleTriggerInput input,
                float dx, float dz)
            {
                return dx * dx + dz * dz < 0.25f ||
                    IsPointInSector(dx, dz, input) ||
                    DistanceToSegment(dx, dz, input.Edge1X,
                        input.Edge1Z, input.Edge1Length) <= 0.5f ||
                    DistanceToSegment(dx, dz, input.Edge2X,
                        input.Edge2Z, input.Edge2Length) <= 0.5f;
            }

            private static bool IsPointInSector(float dx, float dz,
                CombatSimpleTriggerInput input)
            {
                if (dx * dx + dz * dz >
                    (input.Radius + 0.5f) * (input.Radius + 0.5f))
                    return false;
                float length = math.sqrt(dx * dx + dz * dz);
                float cosine = (dx * input.MidX + dz * input.MidZ) /
                    (length * input.MidLength);
                cosine = math.max(-1f, math.min(1f, cosine));
                return math.acos(cosine) <=
                    input.AngleDegrees / 2f * math.PI / 180f;
            }

            private static float DistanceToSegment(float px, float pz,
                float bx, float bz, float length)
            {
                if (length == 0f) return math.sqrt(px * px + pz * pz);
                float t = math.max(0f, math.min(1f,
                    (px * bx + pz * bz) / length));
                float dx = px - t * bx;
                float dz = pz - t * bz;
                return math.sqrt(dx * dx + dz * dz);
            }
        }

        private static bool MatchesCamp(int sourceCamp, int targetCamp,
            int candidateCamp)
        {
            if (candidateCamp != 0 && candidateCamp != 1) return false;
            if (targetCamp == 2) return true;
            if (sourceCamp != 0 && sourceCamp != 1) return false;
            return targetCamp == 0 ? candidateCamp != sourceCamp :
                candidateCamp == sourceCamp;
        }

        private static bool PassesCondition(CombatSimpleTriggerInput input)
        {
            if (input.CheckTimed && !input.TimedPassed) return false;
            if (input.CheckPureSkillGroup &&
                !input.PureSkillGroupPassed) return false;
            if (input.CheckTimedSkillGroup &&
                !input.TimedSkillGroupPassed) return false;
            if (input.CheckPercent &&
                ((input.PercentRoll < input.PercentThreshold) ==
                 input.ReverseCondition)) return false;
            if (input.CheckRelateSkill)
            {
                bool hasSkill = (input.RelateSkillSource == 0 &&
                                 input.HasSourceSkill) ||
                    (input.RelateSkillSource == 1 &&
                     input.RelateSkillPassArg && input.HasArgSkill);
                int skillType = input.RelateSkillSource == 0
                    ? input.SourceSkillType : input.ArgSkillType;
                bool passed = hasSkill && input.RelateSkillCheckType == 0 &&
                    skillType == input.RelateSkillExpectedType;
                if (input.RelateSkillReverse) passed = !passed;
                if (!passed) return false;
            }
            return true;
        }

        public static void Prepare(NativeArray<CombatSimpleTriggerInput> inputs,
            NativeArray<CombatSimpleTriggerResult> results,
            out NativeArray<int> areaRows,
            out int unitCount)
        {
            using NativeArray<CombatUnit> units =
                CombatAttributeBridge.GetUnitSnapshot(Allocator.TempJob);
            unitCount = units.Length;
            int areaCount = 0;
            for (int i = 0; i < inputs.Length; i++)
                if (inputs[i].Picker == CombatSimplePicker.OwnerDistance ||
                    inputs[i].Picker == CombatSimplePicker.OwnerDirection)
                    areaCount++;
            areaRows = default;
            var job = new SelectAndPrepareJob
            {
                Units = units,
                Inputs = inputs,
                Results = results
            };
            if (inputs.Length >= 16)
                job.Schedule(inputs.Length, 8).Complete();
            else
                job.Run(inputs.Length);
            if (areaCount == 0 || unitCount == 0) return;
            areaRows = new NativeArray<int>(inputs.Length, Allocator.TempJob);
            NativeArray<int> selectionIndices =
                new NativeArray<int>(areaCount, Allocator.TempJob);
            try
            {
                int row = 0;
                for (int i = 0; i < inputs.Length; i++)
                {
                    bool isArea =
                        inputs[i].Picker == CombatSimplePicker.OwnerDistance ||
                        inputs[i].Picker == CombatSimplePicker.OwnerDirection;
                    areaRows[i] = isArea ? row : -1;
                    if (isArea) selectionIndices[row++] = i;
                }
                var candidateBuffer =
                    CombatBoardBridge.GetSimpleTargetCandidates();
                candidateBuffer.ResizeUninitialized(areaCount * unitCount);
                NativeArray<CombatSimpleTargetCandidate> areaCandidates =
                    candidateBuffer.AsNativeArray();
                var areaJob = new FindAreaTargetsJob
                {
                    Units = units,
                    Inputs = inputs,
                    SelectionIndices = selectionIndices,
                    Candidates = areaCandidates
                };
                if (areaCandidates.Length >= 64)
                    areaJob.Schedule(areaCandidates.Length, 64).Complete();
                else
                    areaJob.Run(areaCandidates.Length);
            }
            finally
            {
                selectionIndices.Dispose();
            }
        }
    }
}
