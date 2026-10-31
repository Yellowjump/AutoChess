using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Zizouqi.CombatDots
{
    [DisableAutoCreation]
    public partial class CombatProjectileMotionLookupSystem : SystemBase
    {
        protected override void OnUpdate() { }

        public ComponentLookup<CombatUnit> Units() =>
            GetComponentLookup<CombatUnit>(true);
        public ComponentLookup<CombatOrbitProjectile> Orbits() =>
            GetComponentLookup<CombatOrbitProjectile>(true);
        public ComponentLookup<CombatMarkPointProjectile> Marks() =>
            GetComponentLookup<CombatMarkPointProjectile>(true);
    }

    // Snapshot movement for the three projectile kinds whose paths do not need
    // the Bezier batch. PerTick and hit events are still committed in spawn order.
    public sealed class CombatProjectileMotionBatch : IDisposable
    {
        private const int MinParallelProjectiles = 32;
        private static readonly Stack<CombatProjectileMotionBatch> Pool =
            new Stack<CombatProjectileMotionBatch>();

        private struct MotionResult
        {
            public float3 Position;
            public float3 TargetPosition;
            public float Angle;
            public float TotalAngle;
            public float LifetimeProgress;
            public float BeforeAngle;
            public float ChangeAngle;
            public CombatOrbitProjectile OriginalOrbit;
            public byte Kind;
            public byte Step;
            public bool Completed;
        }

        [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High)]
        private struct MoveJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Entity> Entities;
            [ReadOnly] public NativeArray<CombatProjectile> Projectiles;
            [ReadOnly] public ComponentLookup<CombatUnit> Units;
            [ReadOnly] public ComponentLookup<CombatOrbitProjectile> Orbits;
            [ReadOnly] public ComponentLookup<CombatMarkPointProjectile> Marks;
            public NativeArray<MotionResult> Results;
            public float DeltaSeconds;
            public float TargetHeight;

            public void Execute(int index)
            {
                Entity entity = Entities[index];
                CombatProjectile projectile = Projectiles[index];
                if (!projectile.Alive) return;
                if (Marks.HasComponent(entity))
                {
                    float progress = projectile.LifetimeProgress;
                    if (projectile.DurationMs != 0)
                        progress += DeltaSeconds / 1000f;
                    Results[index] = new MotionResult
                    {
                        Kind = 3,
                        LifetimeProgress = progress,
                        Completed = projectile.DurationMs != 0 &&
                            progress >= projectile.DurationMs
                    };
                    return;
                }
                Entity targetEntity = projectile.Target;
                if (targetEntity == Entity.Null || !Units.HasComponent(targetEntity))
                {
                    Results[index] = new MotionResult { Kind = 1, Step = 2 };
                    return;
                }
                CombatUnit target = Units[targetEntity];
                if (!target.Alive)
                {
                    Results[index] = new MotionResult { Kind = 1, Step = 2 };
                    return;
                }
                float3 targetPosition = target.Position + new float3(0f,
                    TargetHeight, 0f);
                if (Orbits.HasComponent(entity))
                {
                    CombatOrbitProjectile orbit = Orbits[entity];
                    float change = orbit.AngleSpeed * DeltaSeconds;
                    float before = orbit.Angle;
                    float angle = math.fmod(before + change, 360f);
                    if (angle < 0f) angle += 360f;
                    float total = orbit.TotalAngle + change;
                    float radians = math.radians(angle);
                    Results[index] = new MotionResult
                    {
                        Kind = 2,
                        Position = targetPosition + new float3(
                            orbit.Distance * math.cos(radians), 0f,
                            -orbit.Distance * math.sin(radians)),
                        TargetPosition = targetPosition,
                        Angle = angle,
                        TotalAngle = total,
                        BeforeAngle = before,
                        ChangeAngle = change,
                        OriginalOrbit = orbit,
                        Completed = orbit.EndAngle != 0 &&
                            math.abs(total) >= orbit.EndAngle
                    };
                    return;
                }
                float3 direction = targetPosition - projectile.Position;
                float distance = projectile.Speed * DeltaSeconds;
                float magnitude = math.length(direction);
                Results[index] = new MotionResult
                {
                    Kind = 1,
                    TargetPosition = targetPosition,
                    Position = projectile.Position +
                        math.normalizesafe(direction) * distance,
                    Step = (byte)(magnitude < distance ? 1 : 0)
                };
            }
        }

        private readonly Dictionary<int, int> m_Indices = new Dictionary<int, int>();
        private NativeArray<Entity> m_Entities;
        private NativeArray<CombatProjectile> m_Projectiles;
        private NativeArray<MotionResult> m_Results;
        private JobHandle m_Job;
        private World m_World;
        private float m_TargetHeight;
        private bool m_Completed;

        public static CombatProjectileMotionBatch Schedule(float deltaSeconds,
            float targetHeight)
        {
            CombatProjectileMotionBatch batch = Pool.Count > 0
                ? Pool.Pop() : new CombatProjectileMotionBatch();
            try
            {
                batch.m_World = World.DefaultGameObjectInjectionWorld;
                batch.m_TargetHeight = targetHeight;
                if (batch.m_World == null || !batch.m_World.IsCreated)
                    throw new InvalidOperationException(
                        "Combat projectiles require an active ECS World.");
                EntityManager manager = batch.m_World.EntityManager;
                CombatProjectileMotionLookupSystem lookup = batch.m_World
                    .GetOrCreateSystemManaged<CombatProjectileMotionLookupSystem>();
                using (EntityQuery query = manager.CreateEntityQuery(
                           ComponentType.ReadOnly<CombatProjectile>(),
                           ComponentType.ReadOnly<CombatProjectileActive>(),
                           ComponentType.Exclude<CombatBezierProjectile>()))
                {
                    if (query.CalculateEntityCount() < MinParallelProjectiles)
                    {
                        batch.m_Completed = true;
                        return batch;
                    }
                    batch.m_Entities = query.ToEntityArray(Allocator.TempJob);
                    batch.m_Projectiles = query.ToComponentDataArray<CombatProjectile>(
                        Allocator.TempJob);
                }
                for (int i = 0; i < batch.m_Entities.Length; i++)
                {
                    if (batch.m_Projectiles[i].Alive)
                        batch.m_Indices.Add(batch.m_Projectiles[i].Handle, i);
                }
                if (batch.m_Indices.Count < MinParallelProjectiles)
                {
                    batch.m_Indices.Clear();
                    batch.m_Completed = true;
                    return batch;
                }
                batch.m_Results = new NativeArray<MotionResult>(batch.m_Entities.Length,
                    Allocator.TempJob);
                batch.m_Job = new MoveJob
                {
                    Entities = batch.m_Entities,
                    Projectiles = batch.m_Projectiles,
                    Units = lookup.Units(),
                    Orbits = lookup.Orbits(),
                    Marks = lookup.Marks(),
                    Results = batch.m_Results,
                    DeltaSeconds = deltaSeconds,
                    TargetHeight = targetHeight
                }.Schedule(batch.m_Entities.Length, 32);
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

        private bool TryGetResult(int handle, byte kind, out Entity entity,
            out CombatProjectile projectile, out MotionResult result)
        {
            entity = Entity.Null;
            projectile = default;
            result = default;
            if (!m_Completed || m_World == null || !m_World.IsCreated ||
                World.DefaultGameObjectInjectionWorld != m_World ||
                !m_Indices.TryGetValue(handle, out int index)) return false;
            EntityManager manager = m_World.EntityManager;
            entity = m_Entities[index];
            if (!manager.Exists(entity)) return false;
            projectile = manager.GetComponentData<CombatProjectile>(entity);
            CombatProjectile original = m_Projectiles[index];
            result = m_Results[index];
            if (result.Kind != kind || projectile.Handle != handle ||
                projectile.Alive != original.Alive ||
                projectile.Target != original.Target ||
                projectile.Speed != original.Speed ||
                projectile.DurationMs != original.DurationMs ||
                projectile.LifetimeProgress != original.LifetimeProgress ||
                !math.all(projectile.Position == original.Position)) return false;
            if (kind == 3) return true;
            if (projectile.Target == Entity.Null ||
                !manager.Exists(projectile.Target)) return result.Step == 2;
            if (!manager.HasComponent<CombatUnit>(projectile.Target))
                return result.Step == 2;
            CombatUnit target = manager.GetComponentData<CombatUnit>(projectile.Target);
            float3 expected = target.Position + new float3(0f, m_TargetHeight, 0f);
            return target.Alive == (result.Step != 2) &&
                (result.Step == 2 ||
                 math.all(expected == result.TargetPosition));
        }

        public bool TryApplyTracking(int handle, out CombatProjectileStep step)
        {
            step = CombatProjectileStep.Moving;
            if (!TryGetResult(handle, 1, out Entity entity,
                    out CombatProjectile projectile, out MotionResult result))
                return false;
            step = (CombatProjectileStep)result.Step;
            if (step == CombatProjectileStep.Moving)
            {
                projectile.Position = result.Position;
                m_World.EntityManager.SetComponentData(entity, projectile);
            }
            return true;
        }

        public bool TryApplyOrbit(int handle, out bool completed,
            out float beforeAngle, out float changeAngle)
        {
            completed = false;
            beforeAngle = changeAngle = 0f;
            if (!TryGetResult(handle, 2, out Entity entity,
                    out CombatProjectile projectile, out MotionResult result))
                return false;
            EntityManager manager = m_World.EntityManager;
            CombatOrbitProjectile orbit = manager.GetComponentData<CombatOrbitProjectile>(
                entity);
            CombatOrbitProjectile original = result.OriginalOrbit;
            if (orbit.Angle != original.Angle ||
                orbit.TotalAngle != original.TotalAngle ||
                orbit.Distance != original.Distance ||
                orbit.MinimumRange != original.MinimumRange ||
                orbit.MaximumRange != original.MaximumRange ||
                orbit.AngleSpeed != original.AngleSpeed ||
                orbit.EndAngle != original.EndAngle ||
                !math.all(orbit.Center == original.Center)) return false;
            orbit.Center = result.TargetPosition;
            orbit.Angle = result.Angle;
            orbit.TotalAngle = result.TotalAngle;
            projectile.Position = result.Position;
            manager.SetComponentData(entity, orbit);
            manager.SetComponentData(entity, projectile);
            completed = result.Completed;
            beforeAngle = result.BeforeAngle;
            changeAngle = result.ChangeAngle;
            return true;
        }

        public bool TryApplyMarkPoint(int handle, out bool completed)
        {
            completed = false;
            if (!TryGetResult(handle, 3, out Entity entity,
                    out CombatProjectile projectile, out MotionResult result))
                return false;
            projectile.LifetimeProgress = result.LifetimeProgress;
            m_World.EntityManager.SetComponentData(entity, projectile);
            completed = result.Completed;
            return true;
        }

        public void Dispose()
        {
            Complete();
            if (m_Results.IsCreated) m_Results.Dispose();
            if (m_Projectiles.IsCreated) m_Projectiles.Dispose();
            if (m_Entities.IsCreated) m_Entities.Dispose();
            m_Results = default;
            m_Projectiles = default;
            m_Entities = default;
            m_Job = default;
            m_World = null;
            m_Completed = false;
            m_Indices.Clear();
            Pool.Push(this);
        }
    }
}
