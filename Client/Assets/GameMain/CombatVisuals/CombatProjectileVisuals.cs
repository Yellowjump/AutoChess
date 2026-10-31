using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Unity.Profiling;
using Zizouqi.CombatDots;

namespace Zizouqi.CombatVisuals
{
    public struct CombatProjectileVisualOwner : IComponentData
    {
        public Entity Projectile;
        public int SpawnOrder;
        public quaternion MeshRotation;
        public float3 MeshPosition;
        public float3 MeshScale;
        public float3 InitialDirection;
    }

    // Runtime prototypes are built once from loaded assets. Individual mesh visuals
    // are then created through a parallel ECB rather than GameObject.Instantiate.
    public static class CombatProjectileVisualBridge
    {
        private static readonly ProfilerMarker SpawnMarker =
            new ProfilerMarker("Combat.Visual.EntitySpawn");
        private static readonly ProfilerMarker PlaybackMarker =
            new ProfilerMarker("Combat.Visual.EntityPlayback");
        private struct Template
        {
            public Entity Prototype;
            public quaternion MeshRotation;
            public float3 MeshPosition;
            public float3 MeshScale;
        }

        private struct SpawnRequest
        {
            public Entity Prototype;
            public CombatProjectileVisualOwner Owner;
            public float3 Position;
            public float3 Direction;
        }

        [BurstCompile]
        private struct SpawnJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SpawnRequest> Requests;
            public EntityCommandBuffer.ParallelWriter Commands;

            public void Execute(int index)
            {
                SpawnRequest request = Requests[index];
                Entity visual = Commands.Instantiate(index, request.Prototype);
                Commands.SetComponent(index, visual, request.Owner);
                quaternion facing = quaternion.LookRotationSafe(request.Direction,
                    new float3(0f, 1f, 0f));
                Commands.SetComponent(index, visual, new LocalToWorld
                {
                    Value = float4x4.TRS(request.Position +
                        math.rotate(facing, request.Owner.MeshPosition),
                        math.mul(facing, request.Owner.MeshRotation),
                        request.Owner.MeshScale)
                });
            }
        }

        private static readonly Dictionary<int, Template> Templates = new();
        private static readonly List<SpawnRequest> Pending = new();
        private static World s_World;

        public static bool RegisterSingleMesh(int assetPathId, GameObject source)
        {
            if (source == null || !TryGetManager(out EntityManager manager))
            {
                return false;
            }
            if (Templates.TryGetValue(assetPathId, out Template existing) &&
                manager.Exists(existing.Prototype))
            {
                return true;
            }
            MeshRenderer[] renderers = source.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 1 ||
                source.GetComponentsInChildren<Renderer>(true).Length != 1 ||
                source.GetComponentsInChildren<ParticleSystem>(true).Length != 0 ||
                source.GetComponentsInChildren<TrailRenderer>(true).Length != 0)
            {
                return false;
            }
            MeshRenderer renderer = renderers[0];
            bool rendererAtRoot = renderer.transform == source.transform;
            if ((!rendererAtRoot && renderer.transform.parent != source.transform) ||
                !renderer.TryGetComponent(out MeshFilter filter) ||
                filter.sharedMesh == null || renderer.sharedMaterials.Length != 1 ||
                renderer.sharedMaterial == null)
            {
                return false;
            }

            Entity prototype = manager.CreateEntity();
            try
            {
                var description = new RenderMeshDescription(renderer);
                var meshes = new RenderMeshArray(
                    new[] { renderer.sharedMaterial }, new[] { filter.sharedMesh });
                RenderMeshUtility.AddComponents(prototype, manager, description, meshes,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
                var template = new Template
                {
                    Prototype = prototype,
                    MeshRotation = rendererAtRoot ? Quaternion.identity :
                        renderer.transform.localRotation,
                    MeshPosition = rendererAtRoot ? Vector3.zero :
                        renderer.transform.localPosition,
                    MeshScale = rendererAtRoot ? source.transform.localScale :
                        Vector3.Scale(source.transform.localScale,
                            renderer.transform.localScale)
                };
                manager.AddComponentData(prototype, new CombatProjectileVisualOwner
                {
                    MeshRotation = template.MeshRotation,
                    MeshPosition = template.MeshPosition,
                    MeshScale = template.MeshScale
                });
                manager.AddComponentData(prototype, new LocalToWorld
                {
                    Value = float4x4.identity
                });
                manager.AddComponent<Prefab>(prototype);
                Templates[assetPathId] = template;
                return true;
            }
            catch
            {
                if (manager.Exists(prototype)) manager.DestroyEntity(prototype);
                throw;
            }
        }

        public static bool TryQueue(int assetPathId, int projectileHandle,
            int spawnOrder, Vector3 position, Vector3 target)
        {
            if (!TryGetManager(out EntityManager manager) ||
                !Templates.TryGetValue(assetPathId, out Template template) ||
                !manager.Exists(template.Prototype) ||
                !CombatProjectileBridge.TryGetVisualSource(projectileHandle,
                    out Entity projectile))
            {
                return false;
            }
            Vector3 direction = target - position;
            if (direction.sqrMagnitude < 0.000001f) direction = Vector3.forward;
            Pending.Add(new SpawnRequest
            {
                Prototype = template.Prototype,
                Owner = new CombatProjectileVisualOwner
                {
                    Projectile = projectile,
                    SpawnOrder = spawnOrder,
                    MeshRotation = template.MeshRotation,
                    MeshPosition = template.MeshPosition,
                    MeshScale = template.MeshScale,
                    InitialDirection = direction
                },
                Position = position,
                Direction = direction
            });
            return true;
        }

        public static void Flush()
        {
            if (Pending.Count == 0) return;
            using var spawnSample = SpawnMarker.Auto();
            if (!TryGetManager(out EntityManager manager))
            {
                Pending.Clear();
                return;
            }
            var requests = new NativeArray<SpawnRequest>(Pending.Count,
                Allocator.TempJob);
            try
            {
                for (int i = 0; i < Pending.Count; i++) requests[i] = Pending[i];
                Pending.Clear();
                using var commands = new EntityCommandBuffer(Allocator.TempJob);
                new SpawnJob
                {
                    Requests = requests,
                    Commands = commands.AsParallelWriter()
                }.Schedule(requests.Length, 32).Complete();
                using (PlaybackMarker.Auto())
                {
                    commands.Playback(manager);
                }
            }
            finally
            {
                requests.Dispose();
            }
        }

        private static bool TryGetManager(out EntityManager manager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                Templates.Clear();
                Pending.Clear();
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

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct CombatProjectileVisualSyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CombatProjectileVisualOwner>();
        }

        [BurstCompile]
        private partial struct SyncJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<CombatProjectile> Projectiles;
            [ReadOnly] public ComponentLookup<CombatBezierProjectile> Curves;
            [ReadOnly] public ComponentLookup<CombatUnit> Units;
            public EntityCommandBuffer.ParallelWriter Commands;

            private void Execute([EntityIndexInQuery] int index, Entity entity,
                ref LocalToWorld transform, in CombatProjectileVisualOwner visual)
            {
                if (!Projectiles.HasComponent(visual.Projectile) ||
                    !Projectiles[visual.Projectile].Alive ||
                    Projectiles[visual.Projectile].SpawnOrder != visual.SpawnOrder)
                {
                    Commands.DestroyEntity(index, entity);
                    return;
                }
                CombatProjectile projectile = Projectiles[visual.Projectile];
                float3 direction = visual.InitialDirection;
                if (Curves.HasComponent(visual.Projectile))
                {
                    CombatBezierProjectile curve = Curves[visual.Projectile];
                    float t = curve.TotalDistance > 0f
                        ? math.saturate(curve.TraveledDistance / curve.TotalDistance)
                        : 0f;
                    direction = 2f * (1f - t) * (curve.Control - curve.Start) +
                        2f * t * (curve.Target - curve.Control);
                }
                else if (projectile.Target != Entity.Null &&
                         Units.HasComponent(projectile.Target))
                {
                    direction = Units[projectile.Target].Position - projectile.Position;
                }
                quaternion facing = quaternion.LookRotationSafe(
                    math.normalizesafe(direction, new float3(0f, 0f, 1f)),
                    new float3(0f, 1f, 0f));
                transform.Value = float4x4.TRS(projectile.Position +
                    math.rotate(facing, visual.MeshPosition),
                    math.mul(facing, visual.MeshRotation), visual.MeshScale);
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            using var commands = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new SyncJob
            {
                Projectiles = state.GetComponentLookup<CombatProjectile>(true),
                Curves = state.GetComponentLookup<CombatBezierProjectile>(true),
                Units = state.GetComponentLookup<CombatUnit>(true),
                Commands = commands.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();
            commands.Playback(state.EntityManager);
        }
    }
}
