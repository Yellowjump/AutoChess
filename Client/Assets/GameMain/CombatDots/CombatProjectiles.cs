using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;

namespace Zizouqi.CombatDots
{
    public enum CombatProjectileStep : byte
    {
        Moving,
        Hit,
        TargetLost
    }

    public enum CombatProjectileKind : byte
    {
        Tracking,
        Orbit,
        Bezier,
        MarkPoint
    }

    // Normalized spawn data. Authoring objects are resolved before this request
    // reaches the projectile runtime; it contains no UnityEngine object reference.
    public struct CombatProjectileSpawnRequest
    {
        public CombatProjectileKind Kind;
        public int SpawnOrder;
        public int BulletId;
        public int AssetPathId;
        public int CasterUid;
        public int TargetUnitHandle;
        public int Camp;
        public float3 Position;
        public float3 TargetPosition;
        public float Speed;
        public int DurationMs;
        public float OrbitDistance;
        public float OrbitMinimumRange;
        public float OrbitMaximumRange;
        public int OrbitAngleSpeed;
        public int OrbitEndAngle;
        public float OrbitStartAngle;
        public float BezierControlLerp;
        public float BezierControlOffset;
        public bool BezierReverseOffset;
        public bool BezierTrackTarget;
        public bool HasSimpleHit;
        public CombatProjectileSimpleHit SimpleHit;
    }

    public struct CombatProjectile : IComponentData
    {
        public int Handle;
        public int SpawnOrder;
        public int BulletId;
        public int AssetPathId;
        public int CasterUid;
        public float3 Position;
        public float Speed;
        public Entity Target;
        public int Camp;
        public bool Alive;
        public int DurationMs;
        public float LifetimeProgress;
    }

    public struct CombatProjectileActive : IComponentData, IEnableableComponent
    {
    }

    public struct CombatMarkPointProjectile : IComponentData
    {
    }

    public struct CombatOrbitProjectile : IComponentData
    {
        public float3 Center;
        public float Angle;
        public float TotalAngle;
        public float Distance;
        public float MinimumRange;
        public float MaximumRange;
        public int AngleSpeed;
        public int EndAngle;
    }

    public struct CombatBezierProjectile : IComponentData
    {
        public float3 Start;
        public float3 Target;
        public float3 Control;
        public float TraveledDistance;
        public float TotalDistance;
        public bool TrackTarget;
    }

    public struct CombatProjectileHit : IBufferElementData
    {
        public int HeroUid;
    }

    public struct CombatProjectileBuff : IBufferElementData
    {
        public int TimerHandle;
    }

    // Immutable per-projectile hit rule for the common damage/effects sequence.
    // The hit queue snapshots this value before a pooled projectile can be reused.
    public struct CombatProjectileSimpleHit : IComponentData
    {
        public bool Enabled;
        public int DamageMode;
        public int DamageFixedValue;
        public int DamageAttributeType;
        public int DamagePercent;
        public bool TriggerDamageRelate;
        public int SfxId;
        public bool CreateSfx;
        public int AudioStopId;
        public int AudioStartId;
        public int ActiveAudioId;
        public int AudioSerialId;
        public bool PendingActiveAudio;
        public bool ReadyActiveAudio;
    }

    public struct CombatProjectileAudioPlayback
    {
        public int Handle;
        public int SpawnOrder;
        public int AudioId;
    }

    public struct CombatProjectileVisualSource
    {
        public int Handle;
        public int SpawnOrder;
        public int BulletId;
        public int AssetPathId;
        public float3 Position;
        public float3 TargetPosition;
    }

    [DisableAutoCreation]
    public partial class CombatProjectileActiveAudioSystem : SystemBase
    {
        private EntityQuery m_Query;

        [BurstCompile]
        private struct PrepareJob : IJobChunk
        {
            public ComponentTypeHandle<CombatProjectileSimpleHit> RuleType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<CombatProjectileSimpleHit> rules =
                    chunk.GetNativeArray(ref RuleType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask,
                    chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out int index))
                {
                    CombatProjectileSimpleHit rule = rules[index];
                    if (!rule.Enabled || !rule.PendingActiveAudio) continue;
                    rule.PendingActiveAudio = false;
                    rule.ReadyActiveAudio = true;
                    rules[index] = rule;
                }
            }
        }

        protected override void OnCreate()
        {
            m_Query = GetEntityQuery(
                ComponentType.ReadWrite<CombatProjectileSimpleHit>(),
                ComponentType.ReadOnly<CombatProjectileActive>());
        }

        protected override void OnUpdate()
        {
            Dependency = new PrepareJob
            {
                RuleType = GetComponentTypeHandle<CombatProjectileSimpleHit>(false)
            }.ScheduleParallel(m_Query, Dependency);
            Dependency.Complete();
        }
    }

    public static class CombatProjectileBridge
    {
        private static readonly Dictionary<int, Entity> Projectiles = new Dictionary<int, Entity>();
        private static readonly Dictionary<int, int> RosterPositions = new Dictionary<int, int>();
        private static int s_RosterHoles;
        private static readonly Stack<Entity> TrackingPool = new Stack<Entity>();
        private static readonly Stack<Entity> MarkPointPool = new Stack<Entity>();
        private static readonly Stack<Entity> OrbitPool = new Stack<Entity>();
        private static readonly Stack<Entity> BezierPool = new Stack<Entity>();
        private static readonly int[] PrewarmTargets = new int[4];
        private static ProfilerCounterValue<int> s_PoolMisses =
            new ProfilerCounterValue<int>("Combat.Pool.Projectile.Misses");
        private static ProfilerCounterValue<int> s_ActiveCount =
            new ProfilerCounterValue<int>("Combat.Pool.Projectile.Active");
        private static World s_World;
        private static int s_NextHandle;
        private static int s_PendingActiveAudio;
        private static bool s_HasReadyActiveAudio;

        public static void Spawn(ref int handle, in CombatProjectileSpawnRequest request)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            int poolKind = request.Kind switch
            {
                CombatProjectileKind.MarkPoint => 1,
                CombatProjectileKind.Orbit => 2,
                CombatProjectileKind.Bezier => 3,
                CombatProjectileKind.Tracking => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(request.Kind))
            };
            Entity entity = AcquireProjectileEntity(manager, poolKind);
            handle = ++s_NextHandle;
            Entity target = CombatAttributeBridge.TryGetEntity(
                request.TargetUnitHandle, out Entity targetEntity)
                ? targetEntity : Entity.Null;
            var projectile = new CombatProjectile
            {
                Handle = handle,
                SpawnOrder = request.SpawnOrder,
                BulletId = request.BulletId,
                AssetPathId = request.AssetPathId,
                CasterUid = request.CasterUid,
                Position = request.Position,
                Speed = request.Speed,
                Target = target,
                Camp = request.Camp,
                Alive = true,
                DurationMs = request.DurationMs
            };
            switch (request.Kind)
            {
                case CombatProjectileKind.Orbit:
                    var orbit = new CombatOrbitProjectile
                    {
                        Center = request.TargetPosition,
                        Angle = request.OrbitStartAngle,
                        Distance = request.OrbitDistance,
                        MinimumRange = request.OrbitMinimumRange,
                        MaximumRange = request.OrbitMaximumRange,
                        AngleSpeed = request.OrbitAngleSpeed,
                        EndAngle = request.OrbitEndAngle
                    };
                    projectile.Position = OrbitPosition(orbit);
                    manager.SetComponentData(entity, orbit);
                    break;
                case CombatProjectileKind.Bezier:
                    CombatBezierProjectile curve = CreateCurve(request);
                    manager.SetComponentData(entity, curve);
                    manager.GetBuffer<CombatProjectileHit>(entity).Clear();
                    break;
                case CombatProjectileKind.MarkPoint:
                    manager.GetBuffer<CombatProjectileBuff>(entity).Clear();
                    break;
                case CombatProjectileKind.Tracking:
                    break;
            }
            manager.SetComponentData(entity, projectile);
            if (request.HasSimpleHit)
            {
                CombatProjectileSimpleHit rule = request.SimpleHit;
                rule.PendingActiveAudio = true;
                manager.SetComponentData(entity, rule);
                s_PendingActiveAudio++;
            }
            manager.SetComponentEnabled<CombatProjectileActive>(entity, true);
            Projectiles.Add(handle, entity);
            DynamicBuffer<CombatProjectileRoster> roster =
                CombatBoardBridge.GetProjectileRoster();
            if (roster.Length == 0)
            {
                RosterPositions.Clear();
                s_RosterHoles = 0;
            }
            int insert = roster.Length;
            roster.Add(new CombatProjectileRoster
            {
                Handle = handle,
                SpawnOrder = request.SpawnOrder
            });
            while (insert > 0 && roster[insert - 1].SpawnOrder > request.SpawnOrder)
            {
                roster[insert] = roster[insert - 1];
                if (roster[insert].Handle != 0)
                    RosterPositions[roster[insert].Handle] = insert;
                insert--;
            }
            roster[insert] = new CombatProjectileRoster
            {
                Handle = handle,
                SpawnOrder = request.SpawnOrder
            };
            RosterPositions[handle] = insert;
            s_ActiveCount.Value = Projectiles.Count;
        }

        private static CombatBezierProjectile CreateCurve(
            in CombatProjectileSpawnRequest request)
        {
            float3 start = request.Position;
            float3 target = request.TargetPosition;
            float3 delta = target - start;
            float distance = math.length(delta);
            float3 direction = math.normalizesafe(delta);
            float3 normal = math.normalizesafe(math.cross(direction,
                new float3(0f, 1f, 0f))) * distance;
            if (request.BezierReverseOffset) normal = -normal;
            var curve = new CombatBezierProjectile
            {
                Start = start,
                Target = target,
                Control = start + delta * request.BezierControlLerp / 1000f +
                    normal * request.BezierControlOffset / 1000f,
                TrackTarget = request.BezierTrackTarget
            };
            float3 previous = start;
            for (int sample = 1; sample <= 10; sample++)
            {
                float t = sample / 10f;
                float oneMinusT = 1f - t;
                float3 position = oneMinusT * oneMinusT * start +
                    2f * oneMinusT * t * curve.Control + t * t * target;
                curve.TotalDistance += math.distance(previous, position);
                previous = position;
            }
            return curve;
        }

        public static void SetTarget(int handle, int targetUnitHandle)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager,
                out Entity entity);
            projectile.Target = CombatAttributeBridge.TryGetEntity(targetUnitHandle,
                out Entity target) ? target : Entity.Null;
            manager.SetComponentData(entity, projectile);
        }

        public static void PreparePoolPrewarm()
        {
            if (!TryGetManager(out _)) return;
            PrewarmTargets[0] = 64;
            PrewarmTargets[1] = 32;
            PrewarmTargets[2] = 32;
            PrewarmTargets[3] = 512;
        }

        public static void AdvancePoolPrewarm(int budget)
        {
            if (!TryGetManager(out EntityManager manager)) return;
            for (int kind = 3; kind >= 0 && budget > 0; kind--)
            {
                Stack<Entity> pool = kind switch
                {
                    1 => MarkPointPool,
                    2 => OrbitPool,
                    3 => BezierPool,
                    _ => TrackingPool
                };
                while (pool.Count < PrewarmTargets[kind] && budget > 0)
                {
                    budget--;
                    Entity entity = CreateProjectileEntity(manager, kind);
                    manager.SetComponentEnabled<CombatProjectileActive>(entity, false);
                    pool.Push(entity);
                }
            }
        }

        public static bool IsPoolPrewarmReady()
        {
            if (!TryGetManager(out _)) return true;
            return TrackingPool.Count >= PrewarmTargets[0] &&
                MarkPointPool.Count >= PrewarmTargets[1] &&
                OrbitPool.Count >= PrewarmTargets[2] &&
                BezierPool.Count >= PrewarmTargets[3];
        }

        private static Entity CreateProjectileEntity(EntityManager manager, int kind)
        {
            Entity entity;
            switch (kind)
            {
                case 1:
                    entity = manager.CreateEntity(typeof(CombatProjectile),
                        typeof(CombatProjectileActive),
                        typeof(CombatMarkPointProjectile),
                        typeof(CombatProjectileBuff),
                        typeof(CombatProjectileSimpleHit));
                    break;
                case 2:
                    entity = manager.CreateEntity(typeof(CombatProjectile),
                        typeof(CombatProjectileActive), typeof(CombatOrbitProjectile),
                        typeof(CombatProjectileSimpleHit));
                    break;
                case 3:
                    entity = manager.CreateEntity(typeof(CombatProjectile),
                        typeof(CombatProjectileActive), typeof(CombatBezierProjectile),
                        typeof(CombatProjectileHit), typeof(CombatProjectileSimpleHit));
                    break;
                default:
                    entity = manager.CreateEntity(typeof(CombatProjectile),
                        typeof(CombatProjectileActive),
                        typeof(CombatProjectileSimpleHit));
                    break;
            }
            return entity;
        }

        private static Entity AcquireProjectileEntity(EntityManager manager, int kind)
        {
            Stack<Entity> pool = kind switch
            {
                1 => MarkPointPool,
                2 => OrbitPool,
                3 => BezierPool,
                _ => TrackingPool
            };
            while (pool.Count != 0)
            {
                Entity pooled = pool.Pop();
                if (manager.Exists(pooled))
                {
                    if (!manager.HasComponent<CombatProjectileSimpleHit>(pooled))
                        manager.AddComponent<CombatProjectileSimpleHit>(pooled);
                    manager.SetComponentData(pooled, default(CombatProjectileSimpleHit));
                    return pooled;
                }
            }
            s_PoolMisses.Value++;
            return CreateProjectileEntity(manager, kind);
        }

        public static void AdvanceActiveAudio()
        {
            if (s_PendingActiveAudio == 0) return;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            world.GetOrCreateSystemManaged<CombatProjectileActiveAudioSystem>()
                .Update();
            s_PendingActiveAudio = 0;
            s_HasReadyActiveAudio = true;
        }

        public static void CollectActiveAudio(
            List<CombatProjectileAudioPlayback> destination)
        {
            if (!s_HasReadyActiveAudio ||
                !TryGetManager(out EntityManager manager)) return;
            s_HasReadyActiveAudio = false;
            foreach (KeyValuePair<int, Entity> entry in Projectiles)
            {
                Entity entity = entry.Value;
                if (!manager.Exists(entity) ||
                    !manager.HasComponent<CombatProjectileSimpleHit>(entity))
                    continue;
                CombatProjectileSimpleHit rule =
                    manager.GetComponentData<CombatProjectileSimpleHit>(entity);
                if (!rule.ReadyActiveAudio) continue;
                rule.ReadyActiveAudio = false;
                manager.SetComponentData(entity, rule);
                destination.Add(new CombatProjectileAudioPlayback
                {
                    Handle = entry.Key,
                    SpawnOrder = manager.GetComponentData<CombatProjectile>(entity)
                        .SpawnOrder,
                    AudioId = rule.ActiveAudioId
                });
            }
            destination.Sort((a, b) => a.SpawnOrder.CompareTo(b.SpawnOrder));
        }

        public static int GetSimpleAudioSerial(int handle)
        {
            EntityManager manager = GetManager();
            GetProjectile(handle, manager, out Entity entity);
            return manager.GetComponentData<CombatProjectileSimpleHit>(entity)
                .AudioSerialId;
        }

        public static void SetSimpleAudioSerial(int handle, int serialId)
        {
            EntityManager manager = GetManager();
            GetProjectile(handle, manager, out Entity entity);
            CombatProjectileSimpleHit rule =
                manager.GetComponentData<CombatProjectileSimpleHit>(entity);
            rule.AudioSerialId = serialId;
            manager.SetComponentData(entity, rule);
        }

        public static bool TryGetSimpleHit(int handle,
            out CombatProjectileSimpleHit rule)
        {
            rule = default;
            if (handle == 0 || !TryGetManager(out EntityManager manager) ||
                !Projectiles.TryGetValue(handle, out Entity entity) ||
                !manager.Exists(entity) ||
                !manager.HasComponent<CombatProjectileSimpleHit>(entity))
                return false;
            rule = manager.GetComponentData<CombatProjectileSimpleHit>(entity);
            return rule.Enabled;
        }

        public static void CreateTracking(ref int handle, float x, float y, float z,
            float speed, int camp, int targetUnitHandle)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            Entity target = CombatAttributeBridge.TryGetEntity(targetUnitHandle, out Entity targetEntity)
                ? targetEntity : Entity.Null;
            Entity entity = AcquireProjectileEntity(manager, 0);
            handle = ++s_NextHandle;
            manager.SetComponentData(entity, new CombatProjectile
            {
                Handle = handle,
                Position = new float3(x, y, z),
                Speed = speed,
                Target = target,
                Camp = camp,
                Alive = true
            });
            manager.SetComponentEnabled<CombatProjectileActive>(entity, true);
            Projectiles.Add(handle, entity);
            s_ActiveCount.Value = Projectiles.Count;
        }

        public static void CreateMarkPoint(ref int handle, float x, float y, float z,
            int camp, int durationMs)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            Entity entity = AcquireProjectileEntity(manager, 1);
            handle = ++s_NextHandle;
            manager.SetComponentData(entity, new CombatProjectile
            {
                Handle = handle,
                Position = new float3(x, y, z),
                Camp = camp,
                Alive = true,
                DurationMs = durationMs
            });
            manager.SetComponentEnabled<CombatProjectileActive>(entity, true);
            Projectiles.Add(handle, entity);
            s_ActiveCount.Value = Projectiles.Count;
        }

        public static void CreateOrbit(ref int handle, float x, float y, float z, int camp,
            float distance, float minimumRange, float maximumRange, int angleSpeed, int endAngle)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            Entity entity = AcquireProjectileEntity(manager, 2);
            handle = ++s_NextHandle;
            manager.SetComponentData(entity, new CombatProjectile
            {
                Handle = handle,
                Position = new float3(x, y, z),
                Camp = camp,
                Alive = true
            });
            manager.SetComponentData(entity, new CombatOrbitProjectile
            {
                Distance = distance,
                MinimumRange = minimumRange,
                MaximumRange = maximumRange,
                AngleSpeed = angleSpeed,
                EndAngle = endAngle
            });
            manager.SetComponentEnabled<CombatProjectileActive>(entity, true);
            Projectiles.Add(handle, entity);
            s_ActiveCount.Value = Projectiles.Count;
        }

        public static void CreateBezier(ref int handle, float startX, float startY, float startZ,
            float targetX, float targetY, float targetZ, float speed, int camp,
            float controlLerp, float controlOffset, bool reverseOffset, bool trackTarget)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            Vector3 start = new Vector3(startX, startY, startZ);
            Vector3 target = new Vector3(targetX, targetY, targetZ);
            Vector3 midPoint = start + (target - start) * controlLerp / 1000f;
            float distance = (target - start).magnitude;
            Vector3 direction = (target - start).normalized;
            Vector3 normal = Vector3.Cross(direction, Vector3.up).normalized * distance;
            if (reverseOffset)
            {
                normal = -normal;
            }
            Vector3 control = midPoint + normal * controlOffset / 1000;
            var curve = new CombatBezierProjectile
            {
                Start = new float3(start.x, start.y, start.z),
                Target = new float3(target.x, target.y, target.z),
                Control = new float3(control.x, control.y, control.z),
                TrackTarget = trackTarget
            };
            Vector3 previous = BezierPosition(curve, 0, target);
            for (int sample = 1; sample <= 10; sample++)
            {
                Vector3 position = BezierPosition(curve, sample / 10f, target);
                curve.TotalDistance += Vector3.Distance(previous, position);
                previous = position;
            }
            Entity entity = AcquireProjectileEntity(manager, 3);
            handle = ++s_NextHandle;
            manager.SetComponentData(entity, new CombatProjectile
            {
                Handle = handle,
                Position = curve.Start,
                Speed = speed,
                Camp = camp,
                Alive = true
            });
            manager.SetComponentData(entity, curve);
            manager.GetBuffer<CombatProjectileHit>(entity).Clear();
            manager.SetComponentEnabled<CombatProjectileActive>(entity, true);
            Projectiles.Add(handle, entity);
            s_ActiveCount.Value = Projectiles.Count;
        }

        public static void SetIdentity(int handle, int spawnOrder, int bulletId, int casterUid)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            projectile.SpawnOrder = spawnOrder;
            projectile.BulletId = bulletId;
            projectile.CasterUid = casterUid;
            manager.SetComponentData(entity, projectile);
        }

        public static bool TryGetVisualSource(int handle, out Entity projectile)
        {
            projectile = Entity.Null;
            return handle != 0 && TryGetManager(out EntityManager manager) &&
                Projectiles.TryGetValue(handle, out projectile) &&
                manager.Exists(projectile);
        }

        public static bool TryGetVisualSource(int handle, int spawnOrder,
            out CombatProjectileVisualSource source)
        {
            source = default;
            if (handle == 0 || !TryGetManager(out EntityManager manager) ||
                !Projectiles.TryGetValue(handle, out Entity entity) ||
                !manager.Exists(entity) ||
                !manager.IsComponentEnabled<CombatProjectileActive>(entity))
                return false;
            CombatProjectile projectile = manager.GetComponentData<CombatProjectile>(
                entity);
            if (!projectile.Alive || projectile.SpawnOrder != spawnOrder)
                return false;
            float3 target = projectile.Position + new float3(0f, 0f, 1f);
            if (projectile.Target != Entity.Null &&
                manager.Exists(projectile.Target) &&
                manager.HasComponent<CombatUnit>(projectile.Target))
                target = manager.GetComponentData<CombatUnit>(projectile.Target)
                    .Position + new float3(0f, 1f, 0f);
            source = new CombatProjectileVisualSource
            {
                Handle = handle,
                SpawnOrder = projectile.SpawnOrder,
                BulletId = projectile.BulletId,
                AssetPathId = projectile.AssetPathId,
                Position = projectile.Position,
                TargetPosition = target
            };
            return true;
        }

        public static bool CollectUpdateHandles(List<int> handles)
        {
            CompactRoster();
            return CollectHandles(handles, false, 0, false, 0);
        }

        private static void CompactRoster()
        {
            if (s_RosterHoles == 0 ||
                !CombatBoardBridge.TryGetProjectileRoster(
                    out DynamicBuffer<CombatProjectileRoster> roster)) return;
            int write = 0;
            for (int read = 0; read < roster.Length; read++)
            {
                CombatProjectileRoster entry = roster[read];
                if (entry.Handle == 0) continue;
                if (write != read) roster[write] = entry;
                RosterPositions[entry.Handle] = write++;
            }
            while (roster.Length > write) roster.RemoveAt(roster.Length - 1);
            s_RosterHoles = 0;
        }

        public static bool CollectMatchingHandles(int bulletId, bool sameCaster,
            int casterUid, List<int> handles)
        {
            return CollectHandles(handles, true, bulletId, sameCaster, casterUid);
        }

        public static int GetSpawnOrder(int handle)
        {
            EntityManager manager = GetManager();
            return GetProjectile(handle, manager, out _).SpawnOrder;
        }

        private static bool CollectHandles(List<int> handles, bool matchBullet,
            int bulletId, bool sameCaster, int casterUid)
        {
            if (!TryGetManager(out EntityManager manager))
            {
                return false;
            }
            if (!CombatBoardBridge.TryGetProjectileRoster(
                    out DynamicBuffer<CombatProjectileRoster> roster))
                return true;
            for (int i = 0; i < roster.Length; i++)
            {
                int handle = roster[i].Handle;
                if (handle == 0) continue;
                if (!Projectiles.TryGetValue(handle, out Entity entity) ||
                    !manager.Exists(entity)) continue;
                if (!matchBullet)
                {
                    handles.Add(handle);
                    continue;
                }
                CombatProjectile projectile = manager.GetComponentData<CombatProjectile>(entity);
                if (projectile.Alive && projectile.BulletId == bulletId &&
                    (!sameCaster || projectile.CasterUid == casterUid))
                    handles.Add(handle);
            }
            return true;
        }

        public static bool StepBezier(int handle, float deltaSeconds,
            float targetX, float targetY, float targetZ,
            out float tangentX, out float tangentY, out float tangentZ)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            CombatBezierProjectile curve = manager.GetComponentData<CombatBezierProjectile>(entity);
            curve.TraveledDistance += projectile.Speed * deltaSeconds;
            float t = Mathf.Clamp01(curve.TraveledDistance / curve.TotalDistance);
            Vector3 currentTarget = new Vector3(targetX, targetY, targetZ);
            Vector3 position = BezierPosition(curve, t, currentTarget);
            projectile.Position = new float3(position.x, position.y, position.z);
            Vector3 end = curve.TrackTarget ? currentTarget : ToVector3(curve.Target);
            Vector3 tangent = (2 * (1 - t) * (ToVector3(curve.Control) - ToVector3(curve.Start)) +
                               2 * t * (end - ToVector3(curve.Control))).normalized;
            tangentX = tangent.x;
            tangentY = tangent.y;
            tangentZ = tangent.z;
            manager.SetComponentData(entity, curve);
            manager.SetComponentData(entity, projectile);
            return t >= 1.0f;
        }

        public static bool BezierTracksTarget(int handle)
        {
            EntityManager manager = GetManager();
            GetProjectile(handle, manager, out Entity entity);
            return manager.GetComponentData<CombatBezierProjectile>(entity).TrackTarget;
        }

        public static void GetBezierTangent(int handle, float targetX,
            float targetY, float targetZ, out float x, out float y, out float z)
        {
            EntityManager manager = GetManager();
            GetProjectile(handle, manager, out Entity entity);
            CombatBezierProjectile curve =
                manager.GetComponentData<CombatBezierProjectile>(entity);
            if (curve.TotalDistance <= 0f ||
                !math.isfinite(curve.TotalDistance))
            {
                x = y = z = 0f;
                return;
            }
            float t = math.clamp(curve.TraveledDistance / curve.TotalDistance, 0f,
                1f);
            float3 end = curve.TrackTarget
                ? new float3(targetX, targetY, targetZ) : curve.Target;
            float3 tangent = math.normalizesafe(
                2f * (1f - t) * (curve.Control - curve.Start) +
                2f * t * (end - curve.Control));
            x = tangent.x;
            y = tangent.y;
            z = tangent.z;
        }

        public static bool TryHitBezier(int handle, int targetUnitHandle, int heroUid)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out _);
            return TryHitBezierAtPosition(handle, targetUnitHandle, heroUid,
                projectile.Position.x, projectile.Position.z);
        }

        public static bool TryHitBezierAtPosition(int handle, int targetUnitHandle,
            int heroUid, float projectileX, float projectileZ)
        {
            return TryHitBezierAlongSegment(handle, targetUnitHandle, heroUid,
                projectileX, projectileZ, projectileX, projectileZ);
        }

        public static bool TryHitBezierAlongSegment(int handle, int targetUnitHandle,
            int heroUid, float startX, float startZ, float endX, float endZ)
        {
            EntityManager manager = GetManager();
            if (!CombatAttributeBridge.TryGetEntity(targetUnitHandle, out Entity targetEntity) ||
                !manager.Exists(targetEntity))
            {
                return false;
            }
            float3 target = manager.GetComponentData<CombatUnit>(targetEntity).Position;
            float segmentX = endX - startX;
            float segmentZ = endZ - startZ;
            float segmentLengthSq = segmentX * segmentX + segmentZ * segmentZ;
            float t = segmentLengthSq > 0f
                ? math.clamp(((target.x - startX) * segmentX +
                    (target.z - startZ) * segmentZ) / segmentLengthSq, 0f, 1f)
                : 0f;
            float dx = target.x - (startX + t * segmentX);
            float dz = target.z - (startZ + t * segmentZ);
            if (!(dx * dx + dz * dz <= 0.25f))
            {
                return false;
            }
            GetProjectile(handle, manager, out Entity entity);
            DynamicBuffer<CombatProjectileHit> hits = manager.GetBuffer<CombatProjectileHit>(entity);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].HeroUid == heroUid)
                {
                    return false;
                }
            }
            hits.Add(new CombatProjectileHit { HeroUid = heroUid });
            return true;
        }

        private static Vector3 BezierPosition(CombatBezierProjectile curve, float t,
            Vector3 currentTarget)
        {
            Vector3 end = curve.TrackTarget ? currentTarget : ToVector3(curve.Target);
            return Mathf.Pow(1 - t, 2) * ToVector3(curve.Start) +
                   2 * (1 - t) * t * ToVector3(curve.Control) + Mathf.Pow(t, 2) * end;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        public static void SetOrbitCenter(int handle, float x, float y, float z)
        {
            EntityManager manager = GetManager();
            GetProjectile(handle, manager, out Entity entity);
            CombatOrbitProjectile orbit = manager.GetComponentData<CombatOrbitProjectile>(entity);
            orbit.Center = new float3(x, y, z);
            manager.SetComponentData(entity, orbit);
        }

        public static void SetOrbitAngle(int handle, float angle)
        {
            EntityManager manager = GetManager();
            GetProjectile(handle, manager, out Entity entity);
            CombatOrbitProjectile orbit = manager.GetComponentData<CombatOrbitProjectile>(entity);
            orbit.Angle = angle;
            manager.SetComponentData(entity, orbit);
        }

        public static float GetOrbitAngle(int handle)
        {
            EntityManager manager = GetManager();
            GetProjectile(handle, manager, out Entity entity);
            return manager.GetComponentData<CombatOrbitProjectile>(entity).Angle;
        }

        public static void UpdateOrbitPosition(int handle)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            CombatOrbitProjectile orbit = manager.GetComponentData<CombatOrbitProjectile>(entity);
            projectile.Position = OrbitPosition(orbit);
            manager.SetComponentData(entity, projectile);
        }

        public static bool StepOrbit(int handle, float deltaSeconds, float centerX,
            float centerY, float centerZ, out float beforeAngle, out float changeAngle)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            CombatOrbitProjectile orbit = manager.GetComponentData<CombatOrbitProjectile>(entity);
            orbit.Center = new float3(centerX, centerY, centerZ);
            changeAngle = orbit.AngleSpeed * deltaSeconds;
            beforeAngle = orbit.Angle;
            orbit.Angle = Mathf.Repeat(orbit.Angle + changeAngle, 360);
            orbit.TotalAngle += changeAngle;
            projectile.Position = OrbitPosition(orbit);
            manager.SetComponentData(entity, orbit);
            manager.SetComponentData(entity, projectile);
            return orbit.EndAngle != 0 && Math.Abs(orbit.TotalAngle) >= orbit.EndAngle;
        }

        public static bool IsOrbitHit(int handle, int targetUnitHandle, float beforeAngle,
            float changeAngle)
        {
            EntityManager manager = GetManager();
            GetProjectile(handle, manager, out Entity entity);
            if (!CombatAttributeBridge.TryGetEntity(targetUnitHandle, out Entity targetEntity) ||
                !manager.Exists(targetEntity))
            {
                return false;
            }
            CombatOrbitProjectile orbit = manager.GetComponentData<CombatOrbitProjectile>(entity);
            float3 targetPosition = manager.GetComponentData<CombatUnit>(targetEntity).Position;
            Vector2 direction = new Vector2(targetPosition.x - orbit.Center.x,
                targetPosition.z - orbit.Center.z);
            if (direction.magnitude > orbit.MaximumRange ||
                direction.magnitude < orbit.MinimumRange)
            {
                return false;
            }
            if (Math.Abs(changeAngle) >= 360)
            {
                return true;
            }
            Vector2 beforeDirection = new Vector2(Mathf.Cos(Mathf.Deg2Rad * beforeAngle),
                Mathf.Sin(Mathf.Deg2Rad * beforeAngle));
            float angleToTarget = -Vector2.SignedAngle(beforeDirection, direction);
            if (angleToTarget < 0)
            {
                angleToTarget += 360;
            }
            return changeAngle > 0 && angleToTarget < changeAngle ||
                   changeAngle < 0 && changeAngle + 360 < angleToTarget;
        }

        public static void GetOrbitScanState(int handle, out float centerX,
            out float centerZ, out float minimumRange, out float maximumRange)
        {
            EntityManager manager = GetManager();
            GetProjectile(handle, manager, out Entity entity);
            CombatOrbitProjectile orbit = manager.GetComponentData<CombatOrbitProjectile>(entity);
            centerX = orbit.Center.x;
            centerZ = orbit.Center.z;
            minimumRange = orbit.MinimumRange;
            maximumRange = orbit.MaximumRange;
        }

        private static float3 OrbitPosition(CombatOrbitProjectile orbit)
        {
            return orbit.Center + new float3(
                orbit.Distance * Mathf.Cos(Mathf.Deg2Rad * orbit.Angle), 0,
                -orbit.Distance * Mathf.Sin(Mathf.Deg2Rad * orbit.Angle));
        }

        public static bool AdvanceMarkPoint(int handle, float deltaSeconds)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            if (projectile.DurationMs == 0)
            {
                return false;
            }
            // Preserve the original mark-point lifetime calculation.
            projectile.LifetimeProgress += deltaSeconds / 1000f;
            manager.SetComponentData(entity, projectile);
            return projectile.LifetimeProgress >= projectile.DurationMs;
        }

        public static void AddBuffTimer(int projectileHandle, int timerHandle)
        {
            EntityManager manager = GetManager();
            GetProjectile(projectileHandle, manager, out Entity entity);
            manager.GetBuffer<CombatProjectileBuff>(entity).Add(
                new CombatProjectileBuff { TimerHandle = timerHandle });
        }

        public static bool TryCollectBuffTimers(int projectileHandle,
            List<int> destination)
        {
            if (projectileHandle == 0 ||
                !TryGetManager(out EntityManager manager) ||
                !Projectiles.TryGetValue(projectileHandle, out Entity entity) ||
                !manager.Exists(entity) ||
                !manager.HasComponent<CombatProjectileBuff>(entity))
                return false;
            DynamicBuffer<CombatProjectileBuff> buffs =
                manager.GetBuffer<CombatProjectileBuff>(entity);
            for (int i = 0; i < buffs.Length; i++)
                destination.Add(buffs[i].TimerHandle);
            return true;
        }

        public static void RemoveBuffTimer(int projectileHandle, int timerHandle)
        {
            EntityManager manager = GetManager();
            GetProjectile(projectileHandle, manager, out Entity entity);
            DynamicBuffer<CombatProjectileBuff> buffs =
                manager.GetBuffer<CombatProjectileBuff>(entity);
            for (int i = buffs.Length - 1; i >= 0; i--)
                if (buffs[i].TimerHandle == timerHandle)
                    buffs.RemoveAt(i);
        }

        public static void ClearBuffTimers(int projectileHandle)
        {
            EntityManager manager = GetManager();
            GetProjectile(projectileHandle, manager, out Entity entity);
            manager.GetBuffer<CombatProjectileBuff>(entity).Clear();
        }

        public static int GetDuration(int handle)
        {
            return GetProjectile(handle, GetManager(), out _).DurationMs;
        }

        public static void SetDuration(int handle, int durationMs)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            projectile.DurationMs = durationMs;
            manager.SetComponentData(entity, projectile);
        }

        public static CombatProjectileStep StepTracking(int handle, float deltaSeconds,
            float targetHeight)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            if (projectile.Target == Entity.Null || !manager.Exists(projectile.Target) ||
                !manager.GetComponentData<CombatUnit>(projectile.Target).Alive)
            {
                return CombatProjectileStep.TargetLost;
            }
            CombatUnit target = manager.GetComponentData<CombatUnit>(projectile.Target);
            Vector3 direction = new Vector3(target.Position.x - projectile.Position.x,
                target.Position.y + targetHeight - projectile.Position.y,
                target.Position.z - projectile.Position.z);
            float distance = projectile.Speed * deltaSeconds;
            if (direction.magnitude < distance)
            {
                return CombatProjectileStep.Hit;
            }
            Vector3 movement = direction.normalized * distance;
            projectile.Position += new float3(movement.x, movement.y, movement.z);
            manager.SetComponentData(entity, projectile);
            return CombatProjectileStep.Moving;
        }

        public static void GetPosition(int handle, out float x, out float y, out float z)
        {
            CombatProjectile projectile = GetProjectile(handle, GetManager(), out _);
            x = projectile.Position.x;
            y = projectile.Position.y;
            z = projectile.Position.z;
        }

        public static void SetPosition(int handle, float x, float y, float z)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            projectile.Position = new float3(x, y, z);
            manager.SetComponentData(entity, projectile);
        }

        public static float GetSpeed(int handle)
        {
            return GetProjectile(handle, GetManager(), out _).Speed;
        }

        public static void SetSpeed(int handle, float speed)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            projectile.Speed = speed;
            manager.SetComponentData(entity, projectile);
        }

        public static int GetCamp(int handle)
        {
            return GetProjectile(handle, GetManager(), out _).Camp;
        }

        public static void SetCamp(int handle, int camp)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            projectile.Camp = camp;
            manager.SetComponentData(entity, projectile);
        }

        public static bool IsAlive(int handle)
        {
            return GetProjectile(handle, GetManager(), out _).Alive;
        }

        public static void SetAlive(int handle, bool alive)
        {
            EntityManager manager = GetManager();
            CombatProjectile projectile = GetProjectile(handle, manager, out Entity entity);
            projectile.Alive = alive;
            manager.SetComponentData(entity, projectile);
        }

        public static void Release(ref int handle)
        {
            if (handle != 0 && RosterPositions.TryGetValue(handle,
                    out int rosterIndex) &&
                CombatBoardBridge.TryGetProjectileRoster(
                    out DynamicBuffer<CombatProjectileRoster> roster))
            {
                if ((uint)rosterIndex < (uint)roster.Length &&
                    roster[rosterIndex].Handle == handle)
                {
                    roster[rosterIndex] = default;
                    s_RosterHoles++;
                }
            }
            RosterPositions.Remove(handle);
            if (handle != 0 && TryGetManager(out EntityManager manager) &&
                Projectiles.TryGetValue(handle, out Entity entity) && manager.Exists(entity))
            {
                manager.SetComponentEnabled<CombatProjectileActive>(entity, false);
                Stack<Entity> pool;
                if (manager.HasComponent<CombatBezierProjectile>(entity))
                {
                    manager.GetBuffer<CombatProjectileHit>(entity).Clear();
                    manager.SetComponentData(entity, default(CombatBezierProjectile));
                    pool = BezierPool;
                }
                else if (manager.HasComponent<CombatOrbitProjectile>(entity))
                {
                    manager.SetComponentData(entity, default(CombatOrbitProjectile));
                    pool = OrbitPool;
                }
                else if (manager.HasComponent<CombatMarkPointProjectile>(entity))
                {
                    manager.GetBuffer<CombatProjectileBuff>(entity).Clear();
                    pool = MarkPointPool;
                }
                else
                {
                    pool = TrackingPool;
                }
                manager.SetComponentData(entity, default(CombatProjectile));
                pool.Push(entity);
            }
            Projectiles.Remove(handle);
            s_ActiveCount.Value = Projectiles.Count;
            handle = 0;
        }

        private static CombatProjectile GetProjectile(int handle, EntityManager manager,
            out Entity entity)
        {
            if (handle == 0 || !Projectiles.TryGetValue(handle, out entity) || !manager.Exists(entity))
            {
                throw new InvalidOperationException("Combat projectile has been released.");
            }
            return manager.GetComponentData<CombatProjectile>(entity);
        }

        private static EntityManager GetManager()
        {
            if (!TryGetManager(out EntityManager manager))
            {
                throw new InvalidOperationException("Combat projectiles require an active ECS World.");
            }
            return manager;
        }

        private static bool TryGetManager(out EntityManager manager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                Projectiles.Clear();
                RosterPositions.Clear();
                s_RosterHoles = 0;
                s_ActiveCount.Value = 0;
                s_PoolMisses.Value = 0;
                TrackingPool.Clear();
                MarkPointPool.Clear();
                OrbitPool.Clear();
                BezierPool.Clear();
                Array.Clear(PrewarmTargets, 0, PrewarmTargets.Length);
                s_PendingActiveAudio = 0;
                s_HasReadyActiveAudio = false;
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

    // Calculate Bezier steps from an ECS unit snapshot. PerTick and hit callbacks
    // still run in projectile order; changed projectiles use StepBezier.
    public sealed class CombatBezierStepBatch : IDisposable
    {
        private const int MinParallelProjectiles = 32;
        private static readonly Stack<CombatBezierStepBatch> Pool =
            new Stack<CombatBezierStepBatch>();
        private struct StepResult
        {
            public float TraveledDistance;
            public float3 Position;
            public float3 Tangent;
            public float3 TargetPosition;
            public bool Completed;
            public bool Valid;
        }

        [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High)]
        private struct CalculateStepsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CombatProjectile> Projectiles;
            [ReadOnly] public NativeArray<CombatBezierProjectile> Curves;
            [ReadOnly] public ComponentLookup<CombatUnit> Units;
            public NativeArray<StepResult> Results;
            public float DeltaSeconds;
            public float TargetHeight;

            public void Execute(int index)
            {
                CombatProjectile projectile = Projectiles[index];
                CombatBezierProjectile curve = Curves[index];
                float3 target = curve.Target;
                if (curve.TrackTarget)
                {
                    Entity targetEntity = projectile.Target;
                    if (targetEntity == Entity.Null ||
                        !Units.HasComponent(targetEntity)) return;
                    CombatUnit targetUnit = Units[targetEntity];
                    if (!targetUnit.Alive) return;
                    target = targetUnit.Position + new float3(0f, TargetHeight, 0f);
                }
                float traveled = curve.TraveledDistance + projectile.Speed * DeltaSeconds;
                float t = math.clamp(traveled / curve.TotalDistance, 0f, 1f);
                float oneMinusT = 1f - t;
                float3 position = math.pow(oneMinusT, 2f) * curve.Start +
                    2f * oneMinusT * t * curve.Control +
                    math.pow(t, 2f) * target;
                float3 tangent = 2f * oneMinusT * (curve.Control - curve.Start) +
                    2f * t * (target - curve.Control);
                float length = math.length(tangent);
                Results[index] = new StepResult
                {
                    TraveledDistance = traveled,
                    Position = position,
                    Tangent = length > 0.00001f ? tangent / length : float3.zero,
                    TargetPosition = target,
                    Completed = t >= 1f,
                    Valid = true
                };
            }
        }

        private readonly Dictionary<int, int> m_Indices = new Dictionary<int, int>();
        private NativeArray<Entity> m_Entities;
        private NativeArray<CombatProjectile> m_Projectiles;
        private NativeArray<CombatBezierProjectile> m_Curves;
        private NativeArray<StepResult> m_Results;
        private JobHandle m_Job;
        private World m_World;
        private float m_TargetHeight;
        private bool m_Completed;

        public static CombatBezierStepBatch Schedule(float deltaSeconds)
        {
            CombatBezierStepBatch batch = Pool.Count > 0
                ? Pool.Pop() : new CombatBezierStepBatch();
            try
            {
                batch.m_World = World.DefaultGameObjectInjectionWorld;
                batch.m_TargetHeight = 1f;
                if (batch.m_World == null || !batch.m_World.IsCreated)
                {
                    throw new InvalidOperationException("Combat projectiles require an active ECS World.");
                }
                using (EntityQuery query = batch.m_World.EntityManager.CreateEntityQuery(
                           ComponentType.ReadOnly<CombatProjectile>(),
                           ComponentType.ReadOnly<CombatProjectileActive>(),
                           ComponentType.ReadOnly<CombatBezierProjectile>()))
                {
                    if (query.CalculateEntityCount() < MinParallelProjectiles)
                    {
                        batch.m_Completed = true;
                        return batch;
                    }
                    batch.m_Entities = query.ToEntityArray(Allocator.TempJob);
                    batch.m_Projectiles =
                        query.ToComponentDataArray<CombatProjectile>(Allocator.TempJob);
                    batch.m_Curves =
                        query.ToComponentDataArray<CombatBezierProjectile>(Allocator.TempJob);
                }
                for (int i = 0; i < batch.m_Projectiles.Length; i++)
                {
                    if (batch.m_Curves[i].TotalDistance > 0f &&
                        math.isfinite(batch.m_Curves[i].TotalDistance) &&
                        math.isfinite(batch.m_Curves[i].TraveledDistance) &&
                        math.isfinite(batch.m_Projectiles[i].Speed))
                    {
                        batch.m_Indices.Add(batch.m_Projectiles[i].Handle, i);
                    }
                }
                if (batch.m_Indices.Count >= MinParallelProjectiles)
                {
                    batch.m_Results = new NativeArray<StepResult>(batch.m_Entities.Length,
                        Allocator.TempJob);
                    batch.m_Job = new CalculateStepsJob
                    {
                        Projectiles = batch.m_Projectiles,
                        Curves = batch.m_Curves,
                        Units = batch.m_World
                            .GetExistingSystemManaged<CombatProjectileMotionLookupSystem>()
                            .Units(),
                        Results = batch.m_Results,
                        DeltaSeconds = deltaSeconds,
                        TargetHeight = batch.m_TargetHeight
                    }.Schedule(batch.m_Entities.Length, 32);
                }
                else
                {
                    batch.m_Indices.Clear();
                    batch.m_Completed = true;
                }
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
            if (!m_Completed)
            {
                m_Job.Complete();
                m_Completed = true;
            }
        }

        public bool TryApply(int handle, out bool completed, out Vector3 position,
            out float tangentX,
            out float tangentY, out float tangentZ)
        {
            completed = false;
            position = Vector3.zero;
            tangentX = tangentY = tangentZ = 0f;
            if (!m_Completed || m_World == null || !m_World.IsCreated ||
                World.DefaultGameObjectInjectionWorld != m_World ||
                !m_Indices.TryGetValue(handle, out int index))
            {
                return false;
            }
            EntityManager manager = m_World.EntityManager;
            Entity entity = m_Entities[index];
            if (!manager.Exists(entity) ||
                !manager.HasComponent<CombatBezierProjectile>(entity))
            {
                return false;
            }
            CombatProjectile projectile = manager.GetComponentData<CombatProjectile>(entity);
            CombatBezierProjectile curve =
                manager.GetComponentData<CombatBezierProjectile>(entity);
            CombatProjectile originalProjectile = m_Projectiles[index];
            CombatBezierProjectile originalCurve = m_Curves[index];
            if (projectile.Handle != handle ||
                projectile.Alive != originalProjectile.Alive ||
                projectile.Target != originalProjectile.Target ||
                projectile.Speed != originalProjectile.Speed ||
                !math.all(projectile.Position == originalProjectile.Position) ||
                curve.TrackTarget != originalCurve.TrackTarget ||
                curve.TraveledDistance != originalCurve.TraveledDistance ||
                curve.TotalDistance != originalCurve.TotalDistance ||
                !math.all(curve.Start == originalCurve.Start) ||
                !math.all(curve.Control == originalCurve.Control) ||
                !math.all(curve.Target == originalCurve.Target))
            {
                return false;
            }
            StepResult result = m_Results[index];
            if (!result.Valid) return false;
            if (curve.TrackTarget)
            {
                if (projectile.Target == Entity.Null ||
                    !manager.Exists(projectile.Target) ||
                    !manager.HasComponent<CombatUnit>(projectile.Target))
                    return false;
                CombatUnit target = manager.GetComponentData<CombatUnit>(
                    projectile.Target);
                if (!target.Alive || !math.all(target.Position +
                        new float3(0f, m_TargetHeight, 0f) ==
                        result.TargetPosition)) return false;
            }
            curve.TraveledDistance = result.TraveledDistance;
            projectile.Position = result.Position;
            manager.SetComponentData(entity, curve);
            manager.SetComponentData(entity, projectile);
            completed = result.Completed;
            position = new Vector3(result.Position.x, result.Position.y,
                result.Position.z);
            tangentX = result.Tangent.x;
            tangentY = result.Tangent.y;
            tangentZ = result.Tangent.z;
            return true;
        }

        public void Dispose()
        {
            Complete();
            if (m_Results.IsCreated) m_Results.Dispose();
            if (m_Curves.IsCreated) m_Curves.Dispose();
            if (m_Projectiles.IsCreated) m_Projectiles.Dispose();
            if (m_Entities.IsCreated) m_Entities.Dispose();
            m_Results = default;
            m_Curves = default;
            m_Projectiles = default;
            m_Entities = default;
            m_Job = default;
            m_World = null;
            m_Completed = false;
            m_Indices.Clear();
            Pool.Push(this);
        }
    }

    // Broad hit candidates are computed from a read-only snapshot. Applying a hit
    // still runs in projectile/UID order and rechecks the live ECS state.
    public sealed class CombatProjectileHitCandidateBatch : IDisposable
    {
        private const int MinParallelProjectiles = 32;
        private const int MaxCandidateCells = 2_000_000;
        private static readonly Stack<CombatProjectileHitCandidateBatch> Pool =
            new Stack<CombatProjectileHitCandidateBatch>();

        [BurstCompile]
        private struct FindHitsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CombatProjectile> Projectiles;
            [ReadOnly] public NativeArray<CombatBezierProjectile> Curves;
            [ReadOnly] public NativeArray<CombatUnit> Units;
            public NativeArray<float4> Segments;
            public NativeArray<byte> Valid;
            // Each Execute owns [index * Units.Length, (index + 1) * Units.Length).
            // Unity's default IJobParallelFor restriction permits only [index],
            // even though these rows are disjoint and within the allocated array.
            [NativeDisableParallelForRestriction]
            public NativeArray<byte> Candidates;
            public float DeltaSeconds;

            public void Execute(int index)
            {
                CombatProjectile projectile = Projectiles[index];
                CombatBezierProjectile curve = Curves[index];
                if (!projectile.Alive || curve.TrackTarget ||
                    curve.TotalDistance <= 0f ||
                    !math.isfinite(curve.TotalDistance) ||
                    !math.isfinite(curve.TraveledDistance) ||
                    !math.isfinite(projectile.Speed))
                {
                    return;
                }

                float traveled = curve.TraveledDistance +
                    projectile.Speed * DeltaSeconds;
                float t = math.clamp(traveled / curve.TotalDistance, 0f, 1f);
                float oneMinusT = 1f - t;
                float3 end = math.pow(oneMinusT, 2f) * curve.Start +
                    2f * oneMinusT * t * curve.Control +
                    math.pow(t, 2f) * curve.Target;
                float startX = projectile.Position.x;
                float startZ = projectile.Position.z;
                float endX = end.x;
                float endZ = end.z;
                if (!math.isfinite(startX) || !math.isfinite(startZ) ||
                    !math.isfinite(endX) || !math.isfinite(endZ))
                {
                    return;
                }
                Valid[index] = 1;
                Segments[index] = new float4(startX, startZ, endX, endZ);
                float dx = endX - startX;
                float dz = endZ - startZ;
                float lengthSquared = dx * dx + dz * dz;
                int targetGroup = projectile.Camp == 1 ? 0 : 1;
                int row = index * Units.Length;
                for (int unitIndex = 0; unitIndex < Units.Length; unitIndex++)
                {
                    CombatUnit unit = Units[unitIndex];
                    if (!unit.Alive || unit.UpdateGroup != targetGroup)
                    {
                        continue;
                    }
                    float along = lengthSquared > 0f
                        ? math.clamp(((unit.Position.x - startX) * dx +
                            (unit.Position.z - startZ) * dz) / lengthSquared,
                            0f, 1f) : 0f;
                    float distanceX = unit.Position.x - (startX + along * dx);
                    float distanceZ = unit.Position.z - (startZ + along * dz);
                    // The main thread accepts up to 0.001 position difference
                    // before using this snapshot. Widen only the candidate test;
                    // TryHitBezierAlongSegment applies the exact 0.5 radius.
                    if (distanceX * distanceX + distanceZ * distanceZ <=
                        0.502f * 0.502f)
                    {
                        Candidates[row + unitIndex] = 1;
                    }
                }
            }
        }

        private NativeArray<CombatProjectile> m_Projectiles;
        private NativeArray<CombatBezierProjectile> m_Curves;
        private NativeArray<CombatUnit> m_Units;
        private NativeArray<float4> m_Segments;
        private NativeArray<byte> m_Valid;
        private NativeArray<byte> m_Candidates;
        private NativeParallelHashMap<int, int> m_ProjectileIndex;
        private JobHandle m_Job;
        private World m_World;
        private int m_SpatialVersion;
        private bool m_Completed;

        public bool IsCurrent => m_Completed && m_World != null && m_World.IsCreated &&
            World.DefaultGameObjectInjectionWorld == m_World &&
            CombatAttributeBridge.SpatialVersion == m_SpatialVersion;

        public static CombatProjectileHitCandidateBatch Schedule(float deltaSeconds)
        {
            CombatProjectileHitCandidateBatch batch = Pool.Count > 0
                ? Pool.Pop() : new CombatProjectileHitCandidateBatch();
            try
            {
                batch.m_World = World.DefaultGameObjectInjectionWorld;
                if (batch.m_World == null || !batch.m_World.IsCreated)
                {
                    batch.m_Completed = true;
                    return batch;
                }
                EntityManager manager = batch.m_World.EntityManager;
                using (EntityQuery query = manager.CreateEntityQuery(
                           ComponentType.ReadOnly<CombatProjectile>(),
                           ComponentType.ReadOnly<CombatProjectileActive>(),
                           ComponentType.ReadOnly<CombatBezierProjectile>()))
                {
                    if (query.CalculateEntityCount() < MinParallelProjectiles)
                    {
                        batch.m_Completed = true;
                        return batch;
                    }
                    batch.m_Projectiles = query.ToComponentDataArray<CombatProjectile>(
                        Allocator.TempJob);
                    batch.m_Curves = query.ToComponentDataArray<CombatBezierProjectile>(
                        Allocator.TempJob);
                }
                batch.m_Units = CombatAttributeBridge.GetUnitSnapshot(Allocator.TempJob);
                int projectileCount = batch.m_Projectiles.Length;
                int unitCount = batch.m_Units.Length;
                int eligibleCount = 0;
                for (int i = 0; i < projectileCount; i++)
                {
                    if (batch.m_Projectiles[i].Alive &&
                        !batch.m_Curves[i].TrackTarget &&
                        batch.m_Curves[i].TotalDistance > 0f)
                    {
                        eligibleCount++;
                    }
                }
                if (unitCount == 0 || eligibleCount < MinParallelProjectiles ||
                    (long)projectileCount * unitCount > MaxCandidateCells)
                {
                    batch.m_Completed = true;
                    return batch;
                }
                // Query order is unspecified; callbacks must retain the old UID order.
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
                batch.m_ProjectileIndex = new NativeParallelHashMap<int, int>(
                    projectileCount, Allocator.TempJob);
                for (int i = 0; i < projectileCount; i++)
                {
                    batch.m_ProjectileIndex.TryAdd(batch.m_Projectiles[i].Handle, i);
                }
                batch.m_Segments = new NativeArray<float4>(projectileCount,
                    Allocator.TempJob, NativeArrayOptions.ClearMemory);
                batch.m_Valid = new NativeArray<byte>(projectileCount,
                    Allocator.TempJob, NativeArrayOptions.ClearMemory);
                batch.m_Candidates = new NativeArray<byte>(
                    projectileCount * unitCount, Allocator.TempJob,
                    NativeArrayOptions.ClearMemory);
                batch.m_Job = new FindHitsJob
                {
                    Projectiles = batch.m_Projectiles,
                    Curves = batch.m_Curves,
                    Units = batch.m_Units,
                    Segments = batch.m_Segments,
                    Valid = batch.m_Valid,
                    Candidates = batch.m_Candidates,
                    DeltaSeconds = deltaSeconds
                }.Schedule(projectileCount, 32);
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
            if (!m_Completed)
            {
                m_Job.Complete();
                m_Completed = true;
            }
        }

        public bool TryCollect(int handle, int camp, float startX, float startZ,
            float endX, float endZ, List<int> heroUids)
        {
            if (!IsCurrent || !m_Candidates.IsCreated ||
                !m_ProjectileIndex.TryGetValue(handle, out int index))
            {
                return false;
            }
            if (m_Valid[index] == 0 || m_Projectiles[index].Camp != camp)
            {
                return false;
            }
            float4 segment = m_Segments[index];
            if (math.abs(segment.x - startX) > 0.001f ||
                math.abs(segment.y - startZ) > 0.001f ||
                math.abs(segment.z - endX) > 0.001f ||
                math.abs(segment.w - endZ) > 0.001f)
            {
                return false;
            }
            heroUids.Clear();
            int row = index * m_Units.Length;
            for (int i = 0; i < m_Units.Length; i++)
            {
                if (m_Candidates[row + i] != 0)
                {
                    heroUids.Add(m_Units[i].HeroUid);
                }
            }
            return true;
        }

        public void Dispose()
        {
            Complete();
            if (m_ProjectileIndex.IsCreated) m_ProjectileIndex.Dispose();
            if (m_Candidates.IsCreated) m_Candidates.Dispose();
            if (m_Valid.IsCreated) m_Valid.Dispose();
            if (m_Segments.IsCreated) m_Segments.Dispose();
            if (m_Units.IsCreated) m_Units.Dispose();
            if (m_Curves.IsCreated) m_Curves.Dispose();
            if (m_Projectiles.IsCreated) m_Projectiles.Dispose();
            m_ProjectileIndex = default;
            m_Candidates = default;
            m_Valid = default;
            m_Segments = default;
            m_Units = default;
            m_Curves = default;
            m_Projectiles = default;
            m_Job = default;
            m_World = null;
            m_SpatialVersion = 0;
            m_Completed = false;
            Pool.Push(this);
        }
    }
}
