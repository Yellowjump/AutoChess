using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.Pool;

namespace Zizouqi.CombatDots
{
    public struct CombatPosPoint : IComponentData
    {
        public int Handle;
        public int SpawnOrder;
        public float3 Position;
        public quaternion Rotation;
        public int Camp;
        public bool Alive;
        public int DurationMs;
        public float RemainingMs;
        public bool HasScheduledTick;
        public int AudioSerialId;
    }

    public struct CombatPosPointActive : IComponentData, IEnableableComponent
    {
    }

    public struct CombatPointUpdate
    {
        public int Handle;
        public bool TickDue;
    }

    public static class CombatPosPointBridge
    {
        private static readonly Dictionary<int, Entity> Points = new Dictionary<int, Entity>();
        private static readonly Stack<Entity> PointPool = new Stack<Entity>();
        private static World s_World;
        private static int s_NextHandle;
        private static int s_PrewarmTarget;
        private static int s_PendingTriggerEvents;
        private static bool s_HasPresentationPlayback;
        private static int s_TimedProjectilePoints;
        private static int s_TimedPresentationPoints;
        private static ProfilerCounterValue<int> s_PoolMisses =
            new ProfilerCounterValue<int>("Combat.Pool.Point.Misses");
        private static readonly ProfilerMarker PoolMissMarker =
            new ProfilerMarker("Combat.Point.Pool.Miss");
        private static ProfilerCounterValue<int> s_ActiveCount =
            new ProfilerCounterValue<int>("Combat.Pool.Point.Active");
        private static ProfilerCounterValue<int> s_TriggerEventCount =
            new ProfilerCounterValue<int>("Combat.Point.EcsTriggerEvents");
        private static ProfilerCounterValue<int> s_TimedProjectileCount =
            new ProfilerCounterValue<int>("Combat.Point.EcsTimedBulletPoints");
        private static ProfilerCounterValue<int> s_TimedPresentationCount =
            new ProfilerCounterValue<int>("Combat.Point.EcsTimedPresentationPoints");

        public static void PreparePoolPrewarm()
        {
            if (TryGetManager(out _)) s_PrewarmTarget = 320;
        }

        public static void AdvancePoolPrewarm(int budget)
        {
            if (!TryGetManager(out EntityManager manager)) return;
            while (PointPool.Count < s_PrewarmTarget && budget-- > 0)
            {
                Entity entity = manager.CreateEntity(typeof(CombatPosPoint),
                    typeof(CombatPosPointActive), typeof(CombatPointTriggerEvent),
                    typeof(CombatPointPresentationRule),
                    typeof(CombatPointPresentationIntent),
                    typeof(CombatPointTimedTrigger),
                    typeof(CombatPointProjectileIntent),
                    typeof(CombatPointTimedPresentationRule));
                manager.SetComponentEnabled<CombatPosPointActive>(entity, false);
                PointPool.Push(entity);
            }
        }

        public static bool IsPoolPrewarmReady()
        {
            return !TryGetManager(out _) || PointPool.Count >= s_PrewarmTarget;
        }

        public static void Create(ref int handle, int spawnOrder)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            Entity entity = Entity.Null;
            while (PointPool.Count != 0 && entity == Entity.Null)
            {
                Entity candidate = PointPool.Pop();
                if (manager.Exists(candidate)) entity = candidate;
            }
            if (entity == Entity.Null)
            {
                using (PoolMissMarker.Auto())
                {
                    s_PoolMisses.Value++;
                    entity = manager.CreateEntity(typeof(CombatPosPoint),
                        typeof(CombatPosPointActive), typeof(CombatPointTriggerEvent),
                        typeof(CombatPointPresentationRule),
                        typeof(CombatPointPresentationIntent),
                        typeof(CombatPointTimedTrigger),
                        typeof(CombatPointProjectileIntent),
                        typeof(CombatPointTimedPresentationRule));
                }
            }
            // Both the prewarmed and fallback entities use this fixed archetype.
            // Avoid repeated component lookups on every point spawn.
            manager.GetBuffer<CombatPointTriggerEvent>(entity).Clear();
            manager.GetBuffer<CombatPointPresentationRule>(entity).Clear();
            manager.GetBuffer<CombatPointPresentationIntent>(entity).Clear();
            manager.GetBuffer<CombatPointProjectileIntent>(entity).Clear();
            manager.GetBuffer<CombatPointTimedPresentationRule>(entity).Clear();
            manager.SetComponentData(entity, default(CombatPointTimedTrigger));
            handle = ++s_NextHandle;
            manager.SetComponentData(entity, new CombatPosPoint
            {
                Handle = handle,
                SpawnOrder = spawnOrder,
                Rotation = quaternion.identity,
                Alive = true
            });
            manager.SetComponentEnabled<CombatPosPointActive>(entity, true);
            Points.Add(handle, entity);
            s_ActiveCount.Value = Points.Count;
        }

        public static void AddPresentationRule(int handle, int triggerType,
            CombatPointPresentationKind kind, int id, bool create)
        {
            EntityManager manager = GetManager();
            GetPoint(handle, manager, out Entity entity);
            manager.GetBuffer<CombatPointPresentationRule>(entity).Add(
                new CombatPointPresentationRule
                {
                    TriggerType = (byte)triggerType,
                    Kind = kind,
                    Id = id,
                    Create = create
                });
        }

        public static void Initialize(int handle, int camp, int durationMs,
            float3 position, quaternion rotation)
        {
            EntityManager manager = GetManager();
            CombatPosPoint point = GetPoint(handle, manager, out Entity entity);
            point.Camp = camp;
            point.DurationMs = durationMs;
            point.RemainingMs = durationMs;
            point.Position = position;
            point.Rotation = rotation;
            manager.SetComponentData(entity, point);
        }

        public static void QueueTrigger(int handle, int triggerType)
        {
            EntityManager manager = GetManager();
            GetPoint(handle, manager, out Entity entity);
            manager.GetBuffer<CombatPointTriggerEvent>(entity).Add(
                new CombatPointTriggerEvent { TriggerType = (byte)triggerType });
            s_PendingTriggerEvents++;
        }

        public static void ConfigureTimedProjectiles(int handle, int intervalMs,
            int maxPasses, int commandCount)
        {
            ConfigureTimed(handle, intervalMs, maxPasses, commandCount, false);
        }

        public static void ConfigureTimedPresentation(int handle, int intervalMs,
            int maxPasses, int commandCount)
        {
            ConfigureTimed(handle, intervalMs, maxPasses, commandCount, true);
        }

        public static void AddTimedPresentationRule(int handle,
            CombatPointPresentationKind kind, int id, bool create)
        {
            EntityManager manager = GetManager();
            GetPoint(handle, manager, out Entity entity);
            manager.GetBuffer<CombatPointTimedPresentationRule>(entity).Add(
                new CombatPointTimedPresentationRule
                {
                    Kind = kind,
                    Id = id,
                    Create = create
                });
        }

        private static void ConfigureTimed(int handle, int intervalMs,
            int maxPasses, int commandCount, bool presentationOnly)
        {
            EntityManager manager = GetManager();
            GetPoint(handle, manager, out Entity entity);
            if (manager.GetComponentData<CombatPointTimedTrigger>(entity)
                    .IntervalMs <= 0)
            {
                if (presentationOnly) s_TimedPresentationPoints++;
                else s_TimedProjectilePoints++;
            }
            manager.SetComponentData(entity, new CombatPointTimedTrigger
            {
                IntervalMs = intervalMs,
                MaxPasses = maxPasses,
                CommandCount = commandCount,
                PresentationOnly = presentationOnly
            });
            s_TimedProjectileCount.Value = s_TimedProjectilePoints;
            s_TimedPresentationCount.Value = s_TimedPresentationPoints;
        }

        public static void AdvanceTimedTriggers(float deltaMs)
        {
            if (s_TimedProjectilePoints + s_TimedPresentationPoints == 0) return;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var system = world.GetOrCreateSystemManaged<CombatPointTimedTriggerSystem>();
            system.DeltaMs = deltaMs;
            system.Update();
            if (s_TimedPresentationPoints > 0)
                s_HasPresentationPlayback = true;
        }

        public static void CollectTimedProjectileIntents(int handle,
            List<int> commandIndices)
        {
            if (!TryGetManager(out EntityManager manager) ||
                !Points.TryGetValue(handle, out Entity entity) ||
                !manager.Exists(entity)) return;
            DynamicBuffer<CombatPointProjectileIntent> intents =
                manager.GetBuffer<CombatPointProjectileIntent>(entity);
            for (int i = 0; i < intents.Length; i++)
                commandIndices.Add(intents[i].CommandIndex);
            intents.Clear();
        }

        public static void AdvanceTriggerEvents()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            s_TriggerEventCount.Value = s_PendingTriggerEvents;
            if (world == null || !world.IsCreated ||
                s_PendingTriggerEvents == 0) return;
            world.GetOrCreateSystemManaged<CombatPointTriggerSystem>().Update();
            s_PendingTriggerEvents = 0;
            s_HasPresentationPlayback = true;
        }

        public static void CollectPresentationPlayback(
            List<CombatPointPresentationPlayback> destination)
        {
            if (!s_HasPresentationPlayback ||
                !TryGetManager(out EntityManager manager))
                return;
            s_HasPresentationPlayback = false;
            foreach (KeyValuePair<int, Entity> entry in Points)
            {
                Entity entity = entry.Value;
                if (!manager.Exists(entity)) continue;
                DynamicBuffer<CombatPointPresentationIntent> intents =
                    manager.GetBuffer<CombatPointPresentationIntent>(entity);
                if (intents.Length == 0) continue;
                int spawnOrder = manager.GetComponentData<CombatPosPoint>(entity).SpawnOrder;
                for (int i = 0; i < intents.Length; i++)
                {
                    CombatPointPresentationIntent intent = intents[i];
                    destination.Add(new CombatPointPresentationPlayback
                    {
                        PointHandle = entry.Key,
                        SpawnOrder = spawnOrder,
                        Sequence = i,
                        Kind = intent.Kind,
                        Id = intent.Id,
                        Create = intent.Create
                    });
                }
                intents.Clear();
            }
            destination.Sort((a, b) =>
            {
                int order = a.SpawnOrder.CompareTo(b.SpawnOrder);
                return order != 0 ? order : a.Sequence.CompareTo(b.Sequence);
            });
        }

        public static bool CollectHandles(List<int> handles)
        {
            if (!TryGetManager(out EntityManager manager))
            {
                return false;
            }
            using (EntityQuery query = manager.CreateEntityQuery(
                       ComponentType.ReadOnly<CombatPosPoint>(),
                       ComponentType.ReadOnly<CombatPosPointActive>()))
            using (NativeArray<CombatPosPoint> points =
                   query.ToComponentDataArray<CombatPosPoint>(Allocator.Temp))
            {
                List<CombatPosPoint> ordered = ListPool<CombatPosPoint>.Get();
                for (int i = 0; i < points.Length; i++)
                {
                    ordered.Add(points[i]);
                }
                ordered.Sort((a, b) => a.SpawnOrder.CompareTo(b.SpawnOrder));
                for (int i = 0; i < ordered.Count; i++)
                {
                    handles.Add(ordered[i].Handle);
                }
                ListPool<CombatPosPoint>.Release(ordered);
            }
            return true;
        }

        public static bool CollectUpdates(List<CombatPointUpdate> updates,
            HashSet<int> duePointHandles)
        {
            if (!TryGetManager(out EntityManager manager))
            {
                return false;
            }
            using (EntityQuery query = manager.CreateEntityQuery(
                       ComponentType.ReadOnly<CombatPosPoint>(),
                       ComponentType.ReadOnly<CombatPosPointActive>()))
            using (NativeArray<CombatPosPoint> points =
                   query.ToComponentDataArray<CombatPosPoint>(Allocator.Temp))
            {
                List<CombatPosPoint> ordered = ListPool<CombatPosPoint>.Get();
                try
                {
                    for (int i = 0; i < points.Length; i++)
                    {
                        ordered.Add(points[i]);
                    }
                    ordered.Sort((a, b) => a.SpawnOrder.CompareTo(b.SpawnOrder));
                    foreach (CombatPosPoint point in ordered)
                    {
                        updates.Add(new CombatPointUpdate
                        {
                            Handle = point.Handle,
                            TickDue = !point.HasScheduledTick ||
                                duePointHandles.Contains(point.Handle)
                        });
                    }
                }
                finally
                {
                    ListPool<CombatPosPoint>.Release(ordered);
                }
            }
            return true;
        }

        public static void SetScheduledTick(int handle)
        {
            EntityManager manager = GetManager();
            CombatPosPoint point = GetPoint(handle, manager, out Entity entity);
            point.HasScheduledTick = true;
            manager.SetComponentData(entity, point);
        }

        public static void ClearScheduledTick(int handle)
        {
            if (!TryGetManager(out EntityManager manager) ||
                !Points.TryGetValue(handle, out Entity entity) ||
                !manager.Exists(entity))
            {
                return;
            }
            CombatPosPoint point = manager.GetComponentData<CombatPosPoint>(entity);
            point.HasScheduledTick = false;
            manager.SetComponentData(entity, point);
        }

        public static void Advance(int handle, float deltaSeconds)
        {
            EntityManager manager = GetManager();
            CombatPosPoint point = GetPoint(handle, manager, out Entity entity);
            if (point.DurationMs != 0)
            {
                point.RemainingMs -= deltaSeconds * 1000f;
                if (point.RemainingMs <= 0)
                {
                    point.Alive = false;
                }
                manager.SetComponentData(entity, point);
            }
        }

        public static void GetPosition(int handle, out float x, out float y, out float z)
        {
            CombatPosPoint point = GetPoint(handle, GetManager(), out _);
            x = point.Position.x;
            y = point.Position.y;
            z = point.Position.z;
        }

        public static void SetPosition(int handle, float x, float y, float z)
        {
            EntityManager manager = GetManager();
            CombatPosPoint point = GetPoint(handle, manager, out Entity entity);
            point.Position = new float3(x, y, z);
            manager.SetComponentData(entity, point);
        }

        public static quaternion GetRotation(int handle)
        {
            return GetPoint(handle, GetManager(), out _).Rotation;
        }

        public static void SetRotation(int handle, quaternion rotation)
        {
            EntityManager manager = GetManager();
            CombatPosPoint point = GetPoint(handle, manager, out Entity entity);
            point.Rotation = rotation;
            manager.SetComponentData(entity, point);
        }

        public static int GetCamp(int handle)
        {
            return GetPoint(handle, GetManager(), out _).Camp;
        }

        public static void SetCamp(int handle, int camp)
        {
            EntityManager manager = GetManager();
            CombatPosPoint point = GetPoint(handle, manager, out Entity entity);
            point.Camp = camp;
            manager.SetComponentData(entity, point);
        }

        public static int GetAudioSerial(int handle)
        {
            return GetPoint(handle, GetManager(), out _).AudioSerialId;
        }

        public static void SetAudioSerial(int handle, int serialId)
        {
            EntityManager manager = GetManager();
            CombatPosPoint point = GetPoint(handle, manager, out Entity entity);
            point.AudioSerialId = serialId;
            manager.SetComponentData(entity, point);
        }

        public static bool IsAlive(int handle)
        {
            return GetPoint(handle, GetManager(), out _).Alive;
        }

        public static void SetAlive(int handle, bool alive)
        {
            EntityManager manager = GetManager();
            CombatPosPoint point = GetPoint(handle, manager, out Entity entity);
            point.Alive = alive;
            manager.SetComponentData(entity, point);
        }

        public static int GetDuration(int handle)
        {
            return GetPoint(handle, GetManager(), out _).DurationMs;
        }

        public static void SetDuration(int handle, int durationMs)
        {
            EntityManager manager = GetManager();
            CombatPosPoint point = GetPoint(handle, manager, out Entity entity);
            point.DurationMs = durationMs;
            manager.SetComponentData(entity, point);
        }

        public static float GetRemaining(int handle)
        {
            return GetPoint(handle, GetManager(), out _).RemainingMs;
        }

        public static void SetRemaining(int handle, float remainingMs)
        {
            EntityManager manager = GetManager();
            CombatPosPoint point = GetPoint(handle, manager, out Entity entity);
            point.RemainingMs = remainingMs;
            manager.SetComponentData(entity, point);
        }

        public static void Release(ref int handle)
        {
            if (handle != 0 && TryGetManager(out EntityManager manager) &&
                Points.TryGetValue(handle, out Entity entity) && manager.Exists(entity))
            {
                if (manager.HasComponent<CombatPointTimedTrigger>(entity))
                {
                    CombatPointTimedTrigger timer =
                        manager.GetComponentData<CombatPointTimedTrigger>(entity);
                    if (timer.IntervalMs > 0)
                    {
                        if (timer.PresentationOnly) s_TimedPresentationPoints--;
                        else s_TimedProjectilePoints--;
                        s_TimedProjectileCount.Value = s_TimedProjectilePoints;
                        s_TimedPresentationCount.Value = s_TimedPresentationPoints;
                    }
                }
                manager.SetComponentEnabled<CombatPosPointActive>(entity, false);
                manager.SetComponentData(entity, default(CombatPosPoint));
                PointPool.Push(entity);
            }
            Points.Remove(handle);
            s_ActiveCount.Value = Points.Count;
            handle = 0;
        }

        private static CombatPosPoint GetPoint(int handle, EntityManager manager,
            out Entity entity)
        {
            if (handle == 0 || !Points.TryGetValue(handle, out entity) || !manager.Exists(entity))
            {
                throw new InvalidOperationException("Combat position point has been released.");
            }
            return manager.GetComponentData<CombatPosPoint>(entity);
        }

        private static EntityManager GetManager()
        {
            if (!TryGetManager(out EntityManager manager))
            {
                throw new InvalidOperationException("Combat position points require an active ECS World.");
            }
            return manager;
        }

        private static bool TryGetManager(out EntityManager manager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                Points.Clear();
                s_ActiveCount.Value = 0;
                s_PoolMisses.Value = 0;
                s_TriggerEventCount.Value = 0;
                s_TimedProjectileCount.Value = 0;
                PointPool.Clear();
                s_PrewarmTarget = 0;
                s_PendingTriggerEvents = 0;
                s_HasPresentationPlayback = false;
                s_TimedProjectilePoints = 0;
                s_TimedPresentationPoints = 0;
                s_TimedPresentationCount.Value = 0;
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
