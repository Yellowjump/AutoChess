using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.Pool;

namespace Zizouqi.CombatDots
{
    // Spatial selection reads the authoritative CombatUnit components.
    public static class CombatTargetQueries
    {
        private static World s_World;
        private static EntityQuery s_Units;

        public static void VisitOpposingGroup(int sourceCamp, Func<int, bool> visit)
        {
            int targetGroup = sourceCamp == 1 ? 0 : 1;
            List<int> heroUids = ListPool<int>.Get();
            try
            {
                CombatAttributeBridge.CollectGroupUids(targetGroup, heroUids);
                foreach (int uid in heroUids)
                {
                    if (!visit(uid))
                    {
                        break;
                    }
                }
            }
            finally
            {
                ListPool<int>.Release(heroUids);
            }
        }

        public static void CollectOpposingGroup(int sourceCamp, List<int> heroUids)
        {
            CombatAttributeBridge.CollectGroupUids(sourceCamp == 1 ? 0 : 1, heroUids);
        }

        public static bool IsInNormalSkillRange(int sourceHandle, int targetHandle, int rangeMm)
        {
            CombatAttributeBridge.GetPosition(sourceHandle, out float sourceX, out _, out float sourceZ);
            CombatAttributeBridge.GetPosition(targetHandle, out float targetX, out _, out float targetZ);
            float dx = sourceX - targetX;
            float dz = sourceZ - targetZ;
            float distanceSquare = dx * dx + dz * dz;
            float truncated = (float)(Math.Floor(distanceSquare * 10000d) / 10000d);
            return truncated <= (rangeMm * rangeMm) / 1000000f;
        }

        public static bool FindNearest(float sourceX, float sourceZ, int sourceCamp,
            int targetCamp, int rangeMm, out int heroUid)
        {
            heroUid = 0;
            float bestDistance = float.MaxValue;
            int bestCampOrder = int.MaxValue;
            using (NativeArray<CombatUnit> units = GetUnits().ToComponentDataArray<CombatUnit>(Allocator.Temp))
            {
                foreach (CombatUnit unit in units)
                {
                    if (!unit.Alive || !unit.ActiveOnBoard ||
                        !MatchesCamp(sourceCamp, targetCamp, unit.Camp))
                    {
                        continue;
                    }
                    float dx = unit.Position.x - sourceX;
                    float dz = unit.Position.z - sourceZ;
                    float distance = dx * dx + dz * dz;
                    int campOrder = unit.Camp == 0 ? 0 : 1;
                    if (distance < bestDistance ||
                        (distance == bestDistance && (campOrder < bestCampOrder ||
                         (campOrder == bestCampOrder && unit.HeroUid < heroUid))))
                    {
                        bestDistance = distance;
                        bestCampOrder = campOrder;
                        heroUid = unit.HeroUid;
                    }
                }
            }
            float truncated = (float)(Math.Floor(bestDistance * 1000d) / 1000d);
            return heroUid != 0 && truncated < (rangeMm * rangeMm) / 1000000f;
        }

        public static bool FindNearest(int sourceUnitHandle, int targetCamp,
            int rangeMm, out int heroUid)
        {
            CombatAttributeBridge.GetPosition(sourceUnitHandle,
                out float x, out _, out float z);
            return FindNearest(x, z, CombatAttributeBridge.GetCamp(sourceUnitHandle),
                targetCamp, rangeMm, out heroUid);
        }

        public static void CollectWithinDistance(float x, float z, int sourceCamp,
            int targetCamp, float radius, List<int> destination)
        {
            destination.Clear();
            if (radius <= 0)
            {
                return;
            }
            using (NativeArray<CombatUnit> units = GetUnits().ToComponentDataArray<CombatUnit>(Allocator.Temp))
            {
                // The former HeroManager lists visited enemy units before friendly units.
                for (int camp = 0; camp <= 1; camp++)
                {
                    if (!MatchesCamp(sourceCamp, targetCamp, camp))
                    {
                        continue;
                    }
                    int start = destination.Count;
                    foreach (CombatUnit unit in units)
                    {
                        if (!unit.Alive || !unit.ActiveOnBoard || unit.Camp != camp)
                        {
                            continue;
                        }
                        float dx = unit.Position.x - x;
                        float dz = unit.Position.z - z;
                        if ((float)Math.Sqrt(dx * dx + dz * dz) < radius)
                        {
                            destination.Add(unit.HeroUid);
                        }
                    }
                    destination.Sort(start, destination.Count - start, Comparer<int>.Default);
                }
            }
        }

        public static void CollectSector(float x, float z, float facingX, float facingZ,
            int sourceCamp, float radius, float angleDegrees, List<int> destination)
        {
            destination.Clear();
            if (sourceCamp != 0 && sourceCamp != 1)
            {
                return;
            }
            float facingLength = (float)Math.Sqrt(facingX * facingX + facingZ * facingZ);
            float midX = facingLength == 0 ? 0 : facingX / facingLength * radius;
            float midZ = facingLength == 0 ? 0 : facingZ / facingLength * radius;
            float halfAngleRadians = angleDegrees / 2f * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(halfAngleRadians);
            float sin = (float)Math.Sin(halfAngleRadians);
            float edge1X = midX * cos - midZ * sin;
            float edge1Z = midX * sin + midZ * cos;
            float edge2X = midX * cos + midZ * sin;
            float edge2Z = -midX * sin + midZ * cos;

            using (NativeArray<CombatUnit> units = GetUnits().ToComponentDataArray<CombatUnit>(Allocator.Temp))
            {
                foreach (CombatUnit unit in units)
                {
                    if (!unit.Alive || !unit.ActiveOnBoard || unit.Camp == sourceCamp)
                    {
                        continue;
                    }
                    float dx = unit.Position.x - x;
                    float dz = unit.Position.z - z;
                    if (dx * dx + dz * dz < 0.25f ||
                        IsPointInSector(dx, dz, midX, midZ, radius, angleDegrees, 0.5f) ||
                        DistanceToSegment(dx, dz, edge1X, edge1Z) <= 0.5f ||
                        DistanceToSegment(dx, dz, edge2X, edge2Z) <= 0.5f)
                    {
                        destination.Add(unit.HeroUid);
                    }
                }
            }
            destination.Sort();
        }

        private static bool IsPointInSector(float dx, float dz, float midX, float midZ,
            float radius, float angleDegrees, float targetRadius)
        {
            if (dx * dx + dz * dz > (radius + targetRadius) * (radius + targetRadius))
            {
                return false;
            }
            float length = (float)Math.Sqrt(dx * dx + dz * dz);
            float midLength = (float)Math.Sqrt(midX * midX + midZ * midZ);
            float cosine = (dx * midX + dz * midZ) / (length * midLength);
            cosine = Math.Max(-1f, Math.Min(1f, cosine));
            return Math.Acos(cosine) <= angleDegrees / 2f * Math.PI / 180f;
        }

        private static float DistanceToSegment(float px, float pz, float bx, float bz)
        {
            float length = (float)Math.Sqrt(bx * bx + bz * bz);
            if (length == 0)
            {
                return (float)Math.Sqrt(px * px + pz * pz);
            }
            // Keep the original sector picker's projection formula.
            float t = Math.Max(0f, Math.Min(1f, (px * bx + pz * bz) / length));
            float dx = px - t * bx;
            float dz = pz - t * bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static bool MatchesCamp(int sourceCamp, int targetCamp, int candidateCamp)
        {
            if (candidateCamp != 0 && candidateCamp != 1)
            {
                return false;
            }
            if (targetCamp == 2)
            {
                return true;
            }
            if (sourceCamp != 0 && sourceCamp != 1)
            {
                return false;
            }
            return targetCamp == 0 ? candidateCamp != sourceCamp : candidateCamp == sourceCamp;
        }

        private static EntityQuery GetUnits()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                throw new InvalidOperationException("The DOTS world must exist before targeting.");
            }
            if (world != s_World)
            {
                s_World = world;
                s_Units = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CombatUnit>());
            }
            return s_Units;
        }
    }

    // One immutable unit snapshot per projectile phase. Burst builds the spatial
    // lookup in parallel; the hot-update layer still applies hits synchronously.
    public sealed class CombatProjectileBroadphase : IDisposable
    {
        private struct IndexedUnit
        {
            public int HeroUid;
            public int UpdateGroup;
            public float2 Position;
        }

        [BurstCompile]
        private struct BuildSpatialIndexJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CombatUnit> Units;
            public NativeParallelMultiHashMap<int, IndexedUnit>.ParallelWriter Index;

            public void Execute(int index)
            {
                CombatUnit unit = Units[index];
                if (!unit.Alive || (unit.UpdateGroup != 0 && unit.UpdateGroup != 1))
                {
                    return;
                }
                float2 position = new float2(unit.Position.x, unit.Position.z);
                int2 cell = (int2)math.floor(position);
                Index.Add(CellKey(cell.x, cell.y), new IndexedUnit
                {
                    HeroUid = unit.HeroUid,
                    UpdateGroup = unit.UpdateGroup,
                    Position = position
                });
            }
        }

        private NativeArray<CombatUnit> m_Units;
        private NativeParallelMultiHashMap<int, IndexedUnit> m_Index;
        private World m_World;
        private int m_SpatialVersion;

        public bool IsCurrent => m_World != null && m_World.IsCreated &&
            World.DefaultGameObjectInjectionWorld == m_World &&
            CombatAttributeBridge.SpatialVersion == m_SpatialVersion;

        public static CombatProjectileBroadphase Build()
        {
            var broadphase = new CombatProjectileBroadphase();
            try
            {
                broadphase.m_World = World.DefaultGameObjectInjectionWorld;
                broadphase.m_Units = CombatAttributeBridge.GetUnitSnapshot(Allocator.TempJob);
                broadphase.m_SpatialVersion = CombatAttributeBridge.SpatialVersion;
                broadphase.m_Index = new NativeParallelMultiHashMap<int, IndexedUnit>(
                    Math.Max(1, broadphase.m_Units.Length), Allocator.TempJob);
                var job = new BuildSpatialIndexJob
                {
                    Units = broadphase.m_Units,
                    Index = broadphase.m_Index.AsParallelWriter()
                };
                job.Schedule(broadphase.m_Units.Length, 32).Complete();
                return broadphase;
            }
            catch
            {
                broadphase.Dispose();
                throw;
            }
        }

        public void CollectNearby(int sourceCamp, float x, float z, List<int> heroUids)
        {
            CollectNearbySegment(sourceCamp, x, z, x, z, heroUids);
        }

        public void CollectNearbySegment(int sourceCamp, float startX,
            float startZ, float endX, float endZ, List<int> heroUids)
        {
            heroUids.Clear();
            int targetGroup = sourceCamp == 1 ? 0 : 1;
            int minCellX = (int)math.floor(math.min(startX, endX) - 0.5f);
            int maxCellX = (int)math.floor(math.max(startX, endX) + 0.5f);
            int minCellZ = (int)math.floor(math.min(startZ, endZ) - 0.5f);
            int maxCellZ = (int)math.floor(math.max(startZ, endZ) + 0.5f);
            float segmentX = endX - startX;
            float segmentZ = endZ - startZ;
            float segmentLengthSq = segmentX * segmentX + segmentZ * segmentZ;
            if ((long)maxCellX - minCellX > 16 ||
                (long)maxCellZ - minCellZ > 16)
            {
                // A very long step is cheaper to check against the unit snapshot
                // than to visit every empty grid cell along its bounding box.
                for (int i = 0; i < m_Units.Length; i++)
                {
                    CombatUnit unit = m_Units[i];
                    if (unit.Alive && unit.UpdateGroup == targetGroup &&
                        IsNearSegment(unit.Position.x, unit.Position.z, startX,
                            startZ, segmentX, segmentZ, segmentLengthSq))
                    {
                        heroUids.Add(unit.HeroUid);
                    }
                }
            }
            else
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
                    {
                        int key = CellKey(cellX, cellZ);
                        if (!m_Index.TryGetFirstValue(key, out IndexedUnit unit,
                                out NativeParallelMultiHashMapIterator<int> iterator))
                        {
                            continue;
                        }
                        do
                        {
                            if (unit.UpdateGroup == targetGroup &&
                                IsNearSegment(unit.Position.x, unit.Position.y,
                                    startX, startZ, segmentX, segmentZ,
                                    segmentLengthSq))
                            {
                                heroUids.Add(unit.HeroUid);
                            }
                        } while (m_Index.TryGetNextValue(out unit, ref iterator));
                    }
                }
            }
            heroUids.Sort();
            for (int i = heroUids.Count - 1; i > 0; i--)
            {
                if (heroUids[i] == heroUids[i - 1])
                {
                    heroUids.RemoveAt(i);
                }
            }
        }

        private static bool IsNearSegment(float x, float z, float startX,
            float startZ, float segmentX, float segmentZ, float segmentLengthSq)
        {
            float t = segmentLengthSq > 0f
                ? math.clamp(((x - startX) * segmentX +
                    (z - startZ) * segmentZ) / segmentLengthSq, 0f, 1f)
                : 0f;
            float dx = x - (startX + t * segmentX);
            float dz = z - (startZ + t * segmentZ);
            return dx * dx + dz * dz <= 0.25f;
        }

        public void Dispose()
        {
            if (m_Index.IsCreated)
            {
                m_Index.Dispose();
            }
            if (m_Units.IsCreated)
            {
                m_Units.Dispose();
            }
        }

        private static int CellKey(int x, int z) => (int)math.hash(new int2(x, z));
    }

    // One unit snapshot for the whole unit phase. The expensive nearest-target
    // search runs in parallel; managed skill effects are applied afterwards.
    public sealed partial class CombatUnitSkillPlanBatch : IDisposable
    {
        private struct SkillInput
        {
            public int UnitIndex;
            public CombatSkillSlot Slot;
        }

        private struct SkillResult
        {
            public int Result;
            public int TargetUid;
        }

        private static readonly ProfilerMarker SnapshotMarker =
            new ProfilerMarker("Combat.Unit.PlanSnapshot");
        private static readonly ProfilerMarker CompleteMarker =
            new ProfilerMarker("Combat.Unit.PlanComplete");
        private static readonly ProfilerMarker PublishMarker =
            new ProfilerMarker("Combat.Unit.PublishIntents");
        private struct NearestTarget
        {
            public int HeroUid;
            public float DistanceSquared;
        }

        [BurstCompile]
        private struct FindNearestJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CombatUnit> Units;
            public NativeArray<NearestTarget> Results;

            public void Execute(int index)
            {
                CombatUnit source = Units[index];
                if (!source.Alive || !source.ActiveOnBoard ||
                    (source.Camp != 0 && source.Camp != 1))
                {
                    Results[index] = default;
                    return;
                }
                int targetGroup = source.Camp == 0 ? 1 : 0;
                int bestUid = 0;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < Units.Length; i++)
                {
                    CombatUnit target = Units[i];
                    if (!target.Alive || !target.ActiveOnBoard ||
                        target.Camp != targetGroup)
                    {
                        continue;
                    }
                    float dx = target.Position.x - source.Position.x;
                    float dz = target.Position.z - source.Position.z;
                    float distance = dx * dx + dz * dz;
                    if (distance < bestDistance ||
                        (distance == bestDistance && target.HeroUid < bestUid))
                    {
                        bestDistance = distance;
                        bestUid = target.HeroUid;
                    }
                }
                Results[index] = new NearestTarget
                {
                    HeroUid = bestUid,
                    DistanceSquared = bestDistance
                };
            }
        }

        [BurstCompile]
        private struct EvaluateSkillsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CombatUnit> Units;
            [ReadOnly] public NativeArray<int> Power;
            [ReadOnly] public NativeArray<int> Hp;
            [ReadOnly] public NativeArray<NearestTarget> Nearest;
            [ReadOnly] public NativeParallelHashMap<int, int> IndexByUid;
            [ReadOnly] public NativeArray<SkillInput> Skills;
            public NativeArray<SkillResult> Results;

            public void Execute(int index)
            {
                SkillInput input = Skills[index];
                CombatUnit source = Units[input.UnitIndex];
                CombatSkillSlot slot = input.Slot;
                SkillResult result = default;
                if (Power[input.UnitIndex] < slot.CastPower)
                {
                    result.Result = 2; // NoPower
                }
                else if (slot.TargetType == 0)
                {
                    result.Result = 1; // CanCast, NoNeedTarget
                }
                else if (slot.TargetType != 1)
                {
                    result.Result = 5; // NoValidTarget
                }
                else
                {
                    int oldUid = source.AttackTargetUid;
                    if (oldUid != 0 &&
                        IndexByUid.TryGetValue(oldUid, out int oldIndex) &&
                        Units[oldIndex].Alive && Hp[oldIndex] >= 0)
                    {
                        float dx = source.Position.x - Units[oldIndex].Position.x;
                        float dz = source.Position.z - Units[oldIndex].Position.z;
                        float distance = dx * dx + dz * dz;
                        float truncated = (float)(
                            math.floor((double)distance * 10000d) / 10000d);
                        if (truncated <= slot.RangeMm * slot.RangeMm / 1000000f)
                        {
                            result.Result = 1;
                            result.TargetUid = oldUid;
                            Results[index] = result;
                            return;
                        }
                    }
                    NearestTarget nearest = Nearest[input.UnitIndex];
                    result.TargetUid = nearest.HeroUid;
                    if (nearest.HeroUid == 0)
                    {
                        result.Result = 5;
                    }
                    else
                    {
                        float truncated = (float)(math.floor(
                            (double)nearest.DistanceSquared * 1000d) / 1000d);
                        result.Result = truncated <
                            slot.RangeMm * slot.RangeMm / 1000000f ? 1 : 3;
                    }
                }
                Results[index] = result;
            }
        }

        // Target results stay in ECS buffers; the managed FSM only reads them.
        [BurstCompile]
        private struct PublishIntentsJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<CombatUnit> UnitType;
            public BufferTypeHandle<CombatUnitSkillIntent> IntentType;
            [ReadOnly] public NativeParallelHashMap<int, int> IndexByUid;
            [ReadOnly] public NativeArray<int> SkillStarts;
            [ReadOnly] public NativeArray<int> SkillCounts;
            [ReadOnly] public NativeArray<SkillInput> Skills;
            [ReadOnly] public NativeArray<SkillResult> Results;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<CombatUnit> units = chunk.GetNativeArray(ref UnitType);
                BufferAccessor<CombatUnitSkillIntent> buffers =
                    chunk.GetBufferAccessor(ref IntentType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask,
                    chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out int entityIndex))
                {
                    DynamicBuffer<CombatUnitSkillIntent> intents = buffers[entityIndex];
                    intents.Clear();
                    if (!IndexByUid.TryGetValue(units[entityIndex].HeroUid,
                            out int unitIndex)) continue;
                    int start = SkillStarts[unitIndex];
                    int end = start + SkillCounts[unitIndex];
                    for (int i = start; i < end; i++)
                    {
                        SkillResult result = Results[i];
                        intents.Add(new CombatUnitSkillIntent
                        {
                            SkillIndex = Skills[i].Slot.Index,
                            Result = result.Result,
                            TargetUid = result.TargetUid
                        });
                    }
                }
            }
        }

        private NativeArray<CombatUnit> m_Units;
        private NativeArray<int> m_Power;
        private NativeArray<int> m_Hp;
        private NativeArray<NearestTarget> m_Nearest;
        private NativeList<SkillInput> m_Skills;
        private NativeArray<SkillResult> m_SkillResults;
        private NativeArray<int> m_SkillStarts;
        private NativeArray<int> m_SkillCounts;
        private NativeParallelHashMap<int, int> m_IndexByUid;
        private EntityQuery m_IntentQuery;
        private bool m_HasIntentQuery;
        private JobHandle m_Job;
        private bool m_Completed;

        public static CombatUnitSkillPlanBatch Schedule()
        {
            using var sample = SnapshotMarker.Auto();
            var batch = new CombatUnitSkillPlanBatch();
            try
            {
                batch.m_Units = CombatAttributeBridge.GetUnitSnapshot(Allocator.TempJob);
                int count = batch.m_Units.Length;
                batch.m_Power = new NativeArray<int>(count, Allocator.TempJob);
                batch.m_Hp = new NativeArray<int>(count, Allocator.TempJob);
                batch.m_Nearest = new NativeArray<NearestTarget>(count, Allocator.TempJob);
                batch.m_IndexByUid = new NativeParallelHashMap<int, int>(
                    Math.Max(1, count), Allocator.TempJob);
                batch.m_Skills = new NativeList<SkillInput>(Math.Max(1, count),
                    Allocator.TempJob);
                batch.m_SkillStarts = new NativeArray<int>(count, Allocator.TempJob);
                batch.m_SkillCounts = new NativeArray<int>(count, Allocator.TempJob);
                EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
                for (int i = 0; i < count; i++)
                {
                    CombatUnit unit = batch.m_Units[i];
                    batch.m_SkillStarts[i] = batch.m_Skills.Length;
                    if (!unit.Alive || !unit.ActiveOnBoard)
                    {
                        continue;
                    }
                    if (!CombatAttributeBridge.TryGetHandleByUid(unit.HeroUid,
                            out int handle) ||
                        !CombatAttributeBridge.TryGetEntity(handle, out Entity entity))
                    {
                        continue;
                    }
                    batch.m_IndexByUid.TryAdd(unit.HeroUid, i);
                    batch.m_Power[i] = CombatAttributeBridge.GetValue(handle, 3);
                    batch.m_Hp[i] = CombatAttributeBridge.GetValue(handle, 1);
                    DynamicBuffer<CombatSkillSlot> slots =
                        manager.GetBuffer<CombatSkillSlot>(entity);
                    for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                    {
                        if (slots[slotIndex].Kind != CombatSkillSlotKind.Normal)
                        {
                            continue;
                        }
                        batch.m_Skills.Add(new SkillInput
                        {
                            UnitIndex = i,
                            Slot = slots[slotIndex]
                        });
                        batch.m_SkillCounts[i]++;
                    }
                }
                batch.m_SkillResults = new NativeArray<SkillResult>(
                    batch.m_Skills.Length, Allocator.TempJob);
                batch.m_IntentQuery = manager.CreateEntityQuery(
                    ComponentType.ReadOnly<CombatUnit>(),
                    ComponentType.ReadWrite<CombatUnitSkillIntent>());
                batch.m_HasIntentQuery = true;
                if (count > 0)
                {
                    var nearestJob = new FindNearestJob
                    {
                        Units = batch.m_Units,
                        Results = batch.m_Nearest
                    };
                    if (count >= 16)
                    {
                        JobHandle nearestHandle = nearestJob.Schedule(count, 32);
                        batch.m_Job = nearestHandle;
                        if (batch.m_Skills.Length > 0)
                        {
                            batch.m_Job = new EvaluateSkillsJob
                            {
                                Units = batch.m_Units,
                                Power = batch.m_Power,
                                Hp = batch.m_Hp,
                                Nearest = batch.m_Nearest,
                                IndexByUid = batch.m_IndexByUid,
                                Skills = batch.m_Skills.AsArray(),
                                Results = batch.m_SkillResults
                            }.Schedule(batch.m_Skills.Length, 32, nearestHandle);
                        }
                    }
                    else
                    {
                        nearestJob.Run(count);
                        if (batch.m_Skills.Length > 0)
                        {
                            new EvaluateSkillsJob
                            {
                                Units = batch.m_Units,
                                Power = batch.m_Power,
                                Hp = batch.m_Hp,
                                Nearest = batch.m_Nearest,
                                IndexByUid = batch.m_IndexByUid,
                                Skills = batch.m_Skills.AsArray(),
                                Results = batch.m_SkillResults
                            }.Run(batch.m_Skills.Length);
                        }
                        batch.m_Completed = true;
                    }
                }
                else
                {
                    batch.m_Completed = true;
                }
                var publishJob = new PublishIntentsJob
                {
                    UnitType = manager.GetComponentTypeHandle<CombatUnit>(true),
                    IntentType = manager.GetBufferTypeHandle<CombatUnitSkillIntent>(false),
                    IndexByUid = batch.m_IndexByUid,
                    SkillStarts = batch.m_SkillStarts,
                    SkillCounts = batch.m_SkillCounts,
                    Skills = batch.m_Skills.AsArray(),
                    Results = batch.m_SkillResults
                };
                if (batch.m_Completed)
                {
                    publishJob.Run(batch.m_IntentQuery);
                }
                else
                {
                    batch.m_Job = publishJob.ScheduleParallel(
                        batch.m_IntentQuery, batch.m_Job);
                }
                return batch;
            }
            catch
            {
                batch.Dispose();
                throw;
            }
        }

        public bool HasPower(int heroUid, int castPower)
        {
            Complete();
            return m_IndexByUid.TryGetValue(heroUid, out int index) &&
                   m_Power[index] >= castPower;
        }

        public void CompleteAndPublish()
        {
            using (PublishMarker.Auto()) Complete();
        }

        public bool TryGetSkillIntent(int heroUid, int skillIndex,
            out int result, out int targetUid)
        {
            Complete();
            if (m_IndexByUid.TryGetValue(heroUid, out int unitIndex))
            {
                int start = m_SkillStarts[unitIndex];
                int end = start + m_SkillCounts[unitIndex];
                for (int i = start; i < end; i++)
                {
                    if (m_Skills[i].Slot.Index == skillIndex)
                    {
                        SkillResult skillResult = m_SkillResults[i];
                        result = skillResult.Result;
                        targetUid = skillResult.TargetUid;
                        return true;
                    }
                }
            }
            result = 0;
            targetUid = 0;
            return false;
        }

        public int GetAttackTargetUid(int heroUid)
        {
            Complete();
            return m_IndexByUid.TryGetValue(heroUid, out int index)
                ? m_Units[index].AttackTargetUid : 0;
        }

        public bool IsOldTargetInRange(int ownerUid, int targetUid, int rangeMm)
        {
            Complete();
            if (!m_IndexByUid.TryGetValue(ownerUid, out int ownerIndex) ||
                !m_IndexByUid.TryGetValue(targetUid, out int targetIndex) ||
                !m_Units[targetIndex].Alive || m_Hp[targetIndex] < 0)
            {
                return false;
            }
            float dx = m_Units[ownerIndex].Position.x - m_Units[targetIndex].Position.x;
            float dz = m_Units[ownerIndex].Position.z - m_Units[targetIndex].Position.z;
            float distance = dx * dx + dz * dz;
            float truncated = (float)(Math.Floor(distance * 10000d) / 10000d);
            return truncated <= rangeMm * rangeMm / 1000000f;
        }

        public bool FindNearest(int ownerUid, int rangeMm, out int targetUid)
        {
            Complete();
            targetUid = 0;
            if (!m_IndexByUid.TryGetValue(ownerUid, out int index))
            {
                return false;
            }
            NearestTarget nearest = m_Nearest[index];
            targetUid = nearest.HeroUid;
            float truncated = (float)(Math.Floor(nearest.DistanceSquared * 1000d) / 1000d);
            return targetUid != 0 && truncated < rangeMm * rangeMm / 1000000f;
        }

        private void Complete()
        {
            if (!m_Completed)
            {
                using var sample = CompleteMarker.Auto();
                m_Job.Complete();
                m_Completed = true;
            }
        }

        public void Dispose()
        {
            Complete();
            if (m_HasIntentQuery) m_IntentQuery.Dispose();
            if (m_IndexByUid.IsCreated) m_IndexByUid.Dispose();
            if (m_SkillCounts.IsCreated) m_SkillCounts.Dispose();
            if (m_SkillStarts.IsCreated) m_SkillStarts.Dispose();
            if (m_SkillResults.IsCreated) m_SkillResults.Dispose();
            if (m_Skills.IsCreated) m_Skills.Dispose();
            if (m_Nearest.IsCreated) m_Nearest.Dispose();
            if (m_Hp.IsCreated) m_Hp.Dispose();
            if (m_Power.IsCreated) m_Power.Dispose();
            if (m_Units.IsCreated) m_Units.Dispose();
        }
    }
}
