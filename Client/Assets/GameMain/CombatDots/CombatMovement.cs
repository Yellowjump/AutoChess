using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Zizouqi.CombatDots
{
    public struct CombatMovement : IComponentData
    {
        public float3 Start;
        public float3 End;
        public float ElapsedSeconds;
        public bool StepReady;
        public bool Arrived;
    }

    [DisableAutoCreation]
    public partial class CombatMovementSystem : SystemBase
    {
        private EntityQuery m_MovingUnits;
        public float StepDeltaSeconds { get; set; }

        protected override void OnCreate()
        {
            m_MovingUnits = GetEntityQuery(typeof(CombatUnit),
                typeof(CombatMovement));
        }

        protected override void OnUpdate()
        {
            if (m_MovingUnits.IsEmptyIgnoreFilter) return;
            float deltaSeconds = StepDeltaSeconds;
            Dependency = Entities.WithName("AdvanceCombatMovement")
                .ForEach((ref CombatUnit unit, ref CombatMovement movement) =>
                {
                    if (!unit.Alive || movement.StepReady) return;
                    movement.ElapsedSeconds += deltaSeconds;
                    movement.Arrived = movement.ElapsedSeconds >= 0.5f;
                    unit.Position = movement.Arrived ? movement.End :
                        math.lerp(movement.Start, movement.End,
                            math.saturate(movement.ElapsedSeconds / 0.5f));
                    unit.ActiveOnBoard = true;
                    movement.StepReady = true;
                }).WithBurst().ScheduleParallel(Dependency);
            Dependency.Complete();
            CombatAttributeBridge.MarkSpatialMutation();
        }
    }

    public static class CombatMovementBridge
    {
        private const float CellDurationSeconds = 0.5f;

        public static void AdvanceAll(float deltaSeconds)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            CombatMovementSystem system =
                world.GetOrCreateSystemManaged<CombatMovementSystem>();
            system.StepDeltaSeconds = deltaSeconds;
            system.Update();
        }

        public static void Begin(int unitHandle, float endX, float endY, float endZ)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            var movement = new CombatMovement
            {
                Start = manager.GetComponentData<CombatUnit>(entity).Position,
                End = new float3(endX, endY, endZ)
            };
            if (manager.HasComponent<CombatMovement>(entity))
            {
                manager.SetComponentData(entity, movement);
            }
            else
            {
                manager.AddComponentData(entity, movement);
            }
        }

        // Returns false when no cell movement is active. A completed step is
        // removed before the FSM chooses its next action in the same tick.
        public static bool Advance(int unitHandle, float deltaSeconds, out bool arrived,
            out float x, out float y, out float z)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            CombatUnit unit = manager.GetComponentData<CombatUnit>(entity);
            if (!manager.HasComponent<CombatMovement>(entity))
            {
                arrived = false;
                x = unit.Position.x;
                y = unit.Position.y;
                z = unit.Position.z;
                return false;
            }
            CombatMovement movement = manager.GetComponentData<CombatMovement>(entity);
            movement.ElapsedSeconds += deltaSeconds;
            arrived = movement.ElapsedSeconds >= CellDurationSeconds;
            unit.Position = arrived ? movement.End :
                math.lerp(movement.Start, movement.End,
                    math.saturate(movement.ElapsedSeconds / CellDurationSeconds));
            unit.ActiveOnBoard = true;
            manager.SetComponentData(entity, unit);
            CombatAttributeBridge.MarkSpatialMutation();
            if (arrived)
            {
                manager.RemoveComponent<CombatMovement>(entity);
            }
            else
            {
                manager.SetComponentData(entity, movement);
            }
            x = unit.Position.x;
            y = unit.Position.y;
            z = unit.Position.z;
            return true;
        }

        // The parallel step writes the position and arrival flag. The FSM still
        // consumes arrival in unit order so its transitions and commands remain ordered.
        public static bool ConsumeAdvanced(int unitHandle, float deltaSeconds,
            out bool arrived, out float x, out float y, out float z)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            if (!manager.HasComponent<CombatMovement>(entity))
                return Advance(unitHandle, deltaSeconds, out arrived, out x,
                    out y, out z);
            CombatMovement movement = manager.GetComponentData<CombatMovement>(entity);
            if (!movement.StepReady)
                return Advance(unitHandle, deltaSeconds, out arrived, out x,
                    out y, out z);
            arrived = movement.Arrived;
            float3 position = manager.GetComponentData<CombatUnit>(entity).Position;
            x = position.x;
            y = position.y;
            z = position.z;
            if (arrived)
                manager.RemoveComponent<CombatMovement>(entity);
            else
            {
                movement.StepReady = false;
                manager.SetComponentData(entity, movement);
            }
            return true;
        }

        public static void End(int unitHandle)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (unitHandle == 0 || world == null || !world.IsCreated ||
                !CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
            {
                return;
            }
            EntityManager manager = world.EntityManager;
            if (manager.HasComponent<CombatMovement>(entity))
            {
                manager.RemoveComponent<CombatMovement>(entity);
            }
        }

        private static Entity GetEntity(int unitHandle, out EntityManager manager)
        {
            if (!CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
            {
                throw new InvalidOperationException("Movement unit has been released.");
            }
            manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            return entity;
        }
    }
}
