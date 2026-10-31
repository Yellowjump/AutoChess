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
    // Conservative radial candidates. Angle and final range are checked against
    // live state on the main thread, in the original projectile/UID order.
    public sealed class CombatOrbitHitCandidateBatch : IDisposable
    {
        private const int MinParallelOrbits = 8;
        private const int MaxCandidateCells = 2_000_000;
        private static readonly Stack<CombatOrbitHitCandidateBatch> Pool =
            new Stack<CombatOrbitHitCandidateBatch>();

        [BurstCompile]
        private struct FindCandidatesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CombatProjectile> Projectiles;
            [ReadOnly] public NativeArray<CombatOrbitProjectile> Orbits;
            [ReadOnly] public NativeArray<CombatUnit> Units;
            [ReadOnly] public ComponentLookup<CombatUnit> UnitLookup;
            public NativeArray<float3> Centers;
            public NativeArray<byte> Valid;
            [NativeDisableParallelForRestriction] public NativeArray<byte> Candidates;
            public float TargetHeight;
            public float DeltaSeconds;

            public void Execute(int index)
            {
                CombatProjectile projectile = Projectiles[index];
                CombatOrbitProjectile orbit = Orbits[index];
                if (!projectile.Alive || projectile.Target == Entity.Null ||
                    !UnitLookup.HasComponent(projectile.Target) ||
                    !math.isfinite(orbit.MinimumRange) ||
                    !math.isfinite(orbit.MaximumRange)) return;
                CombatUnit target = UnitLookup[projectile.Target];
                if (!target.Alive) return;
                float3 center = target.Position + new float3(0f, TargetHeight, 0f);
                if (!math.all(math.isfinite(center))) return;
                Centers[index] = center;
                Valid[index] = 1;
                float minimum = math.max(0f, orbit.MinimumRange - 0.002f);
                float maximum = orbit.MaximumRange + 0.002f;
                float minimumSquared = minimum * minimum;
                float maximumSquared = maximum * maximum;
                float changeAngle = orbit.AngleSpeed * DeltaSeconds;
                if (!math.isfinite(orbit.Angle) || !math.isfinite(changeAngle))
                {
                    Valid[index] = 0;
                    return;
                }
                float radians = math.radians(orbit.Angle);
                float beforeX = math.cos(radians);
                float beforeZ = math.sin(radians);
                int targetGroup = projectile.Camp == 1 ? 0 : 1;
                int row = index * Units.Length;
                for (int unitIndex = 0; unitIndex < Units.Length; unitIndex++)
                {
                    CombatUnit unit = Units[unitIndex];
                    if (!unit.Alive || unit.UpdateGroup != targetGroup) continue;
                    float dx = unit.Position.x - center.x;
                    float dz = unit.Position.z - center.z;
                    float distanceSquared = dx * dx + dz * dz;
                    if (distanceSquared < minimumSquared ||
                        distanceSquared > maximumSquared) continue;
                    // Include a small angular margin; the main thread performs
                    // the exact SignedAngle check against current ECS values.
                    if (math.abs(changeAngle) < 360f)
                    {
                        float angle = -math.degrees(math.atan2(
                            beforeX * dz - beforeZ * dx,
                            beforeX * dx + beforeZ * dz));
                        if (angle < 0f) angle += 360f;
                        const float margin = 0.05f;
                        bool possible = changeAngle > 0f
                            ? angle < changeAngle + margin ||
                              angle > 360f - margin
                            : changeAngle < 0f &&
                              angle > changeAngle + 360f - margin;
                        if (!possible) continue;
                    }
                    Candidates[row + unitIndex] = 1;
                }
            }
        }

        private NativeArray<CombatProjectile> m_Projectiles;
        private NativeArray<CombatOrbitProjectile> m_Orbits;
        private NativeArray<CombatUnit> m_Units;
        private NativeArray<float3> m_Centers;
        private NativeArray<byte> m_Valid;
        private NativeArray<byte> m_Candidates;
        private NativeParallelHashMap<int, int> m_Indices;
        private JobHandle m_Job;
        private World m_World;
        private int m_SpatialVersion;
        private float m_DeltaSeconds;
        private bool m_Completed;

        public bool HasCandidates => m_Completed && m_Candidates.IsCreated;

        public static CombatOrbitHitCandidateBatch Schedule(float deltaSeconds,
            float targetHeight)
        {
            CombatOrbitHitCandidateBatch batch = Pool.Count > 0
                ? Pool.Pop() : new CombatOrbitHitCandidateBatch();
            try
            {
                batch.m_World = World.DefaultGameObjectInjectionWorld;
                batch.m_DeltaSeconds = deltaSeconds;
                if (batch.m_World == null || !batch.m_World.IsCreated)
                {
                    batch.m_Completed = true;
                    return batch;
                }
                EntityManager manager = batch.m_World.EntityManager;
                using (EntityQuery query = manager.CreateEntityQuery(
                           ComponentType.ReadOnly<CombatProjectile>(),
                           ComponentType.ReadOnly<CombatProjectileActive>(),
                           ComponentType.ReadOnly<CombatOrbitProjectile>()))
                {
                    if (query.CalculateEntityCount() < MinParallelOrbits)
                    {
                        batch.m_Completed = true;
                        return batch;
                    }
                    batch.m_Projectiles = query.ToComponentDataArray<CombatProjectile>(
                        Allocator.TempJob);
                    batch.m_Orbits = query.ToComponentDataArray<CombatOrbitProjectile>(
                        Allocator.TempJob);
                }
                batch.m_Units = CombatAttributeBridge.GetUnitSnapshot(Allocator.TempJob);
                int projectileCount = batch.m_Projectiles.Length;
                int unitCount = batch.m_Units.Length;
                if (unitCount == 0 ||
                    (long)projectileCount * unitCount > MaxCandidateCells)
                {
                    batch.m_Completed = true;
                    return batch;
                }
                for (int i = 1; i < unitCount; i++)
                {
                    CombatUnit unit = batch.m_Units[i];
                    int j = i - 1;
                    while (j >= 0 && batch.m_Units[j].HeroUid > unit.HeroUid)
                    {
                        batch.m_Units[j + 1] = batch.m_Units[j];
                        j--;
                    }
                    batch.m_Units[j + 1] = unit;
                }
                batch.m_SpatialVersion = CombatAttributeBridge.SpatialVersion;
                batch.m_Indices = new NativeParallelHashMap<int, int>(
                    projectileCount, Allocator.TempJob);
                for (int i = 0; i < projectileCount; i++)
                    batch.m_Indices.TryAdd(batch.m_Projectiles[i].Handle, i);
                batch.m_Centers = new NativeArray<float3>(projectileCount,
                    Allocator.TempJob, NativeArrayOptions.ClearMemory);
                batch.m_Valid = new NativeArray<byte>(projectileCount,
                    Allocator.TempJob, NativeArrayOptions.ClearMemory);
                batch.m_Candidates = new NativeArray<byte>(
                    projectileCount * unitCount, Allocator.TempJob,
                    NativeArrayOptions.ClearMemory);
                batch.m_Job = new FindCandidatesJob
                {
                    Projectiles = batch.m_Projectiles,
                    Orbits = batch.m_Orbits,
                    Units = batch.m_Units,
                    UnitLookup = batch.m_World.GetOrCreateSystemManaged<
                        CombatProjectileMotionLookupSystem>().Units(),
                    Centers = batch.m_Centers,
                    Valid = batch.m_Valid,
                    Candidates = batch.m_Candidates,
                    TargetHeight = targetHeight,
                    DeltaSeconds = deltaSeconds
                }.Schedule(projectileCount, 16);
                return batch;
            }
            catch
            {
                batch.Dispose();
                throw;
            }
        }

        public void Complete()
        {
            if (m_Completed) return;
            m_Job.Complete();
            m_Completed = true;
        }

        public bool TryCollect(int handle, int camp, float centerX, float centerZ,
            float minimumRange, float maximumRange, float beforeAngle,
            float changeAngle, List<int> heroUids)
        {
            if (!m_Completed || !m_Candidates.IsCreated ||
                m_World == null || !m_World.IsCreated ||
                World.DefaultGameObjectInjectionWorld != m_World ||
                CombatAttributeBridge.SpatialVersion != m_SpatialVersion ||
                !m_Indices.TryGetValue(handle, out int index) ||
                m_Valid[index] == 0 || m_Projectiles[index].Camp != camp ||
                math.abs(m_Centers[index].x - centerX) > 0.001f ||
                math.abs(m_Centers[index].z - centerZ) > 0.001f ||
                m_Orbits[index].MinimumRange != minimumRange ||
                m_Orbits[index].MaximumRange != maximumRange ||
                m_Orbits[index].Angle != beforeAngle ||
                m_Orbits[index].AngleSpeed * m_DeltaSeconds != changeAngle)
                return false;
            heroUids.Clear();
            int row = index * m_Units.Length;
            for (int i = 0; i < m_Units.Length; i++)
                if (m_Candidates[row + i] != 0)
                    heroUids.Add(m_Units[i].HeroUid);
            return true;
        }

        public void Dispose()
        {
            Complete();
            if (m_Indices.IsCreated) m_Indices.Dispose();
            if (m_Candidates.IsCreated) m_Candidates.Dispose();
            if (m_Valid.IsCreated) m_Valid.Dispose();
            if (m_Centers.IsCreated) m_Centers.Dispose();
            if (m_Units.IsCreated) m_Units.Dispose();
            if (m_Orbits.IsCreated) m_Orbits.Dispose();
            if (m_Projectiles.IsCreated) m_Projectiles.Dispose();
            m_Indices = default;
            m_Candidates = default;
            m_Valid = default;
            m_Centers = default;
            m_Units = default;
            m_Orbits = default;
            m_Projectiles = default;
            m_World = null;
            m_DeltaSeconds = 0f;
            m_Completed = false;
            Pool.Push(this);
        }
    }
}
