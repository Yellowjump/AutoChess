using Unity.Entities;

namespace Zizouqi.CombatDots
{
    public struct CombatAttackCast : IComponentData
    {
        public int NormalSkillIndex;
        public float ElapsedSeconds;
        public bool ShakeTriggered;
        public bool AnimationFinished;
    }

    [DisableAutoCreation]
    public partial class CombatAttackCastSystem : SystemBase
    {
        public float DeltaSeconds;

        protected override void OnUpdate()
        {
            float deltaSeconds = DeltaSeconds;
            Entities.ForEach((ref CombatAttackCast cast) =>
            {
                cast.ElapsedSeconds += deltaSeconds;
            }).ScheduleParallel();
            Dependency.Complete();
        }
    }

    // The hot-update FSM still invokes callbacks in the original order. Their
    // elapsed time and one-shot flags belong to the unit's ECS entity.
    public static class CombatAttackCastBridge
    {
        public static void AdvanceAll(float deltaSeconds)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (deltaSeconds <= 0f || world == null || !world.IsCreated)
            {
                return;
            }
            CombatAttackCastSystem system =
                world.GetOrCreateSystemManaged<CombatAttackCastSystem>();
            system.DeltaSeconds = deltaSeconds;
            system.Update();
        }

        public static void Begin(int unitHandle, int normalSkillIndex)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            var cast = new CombatAttackCast { NormalSkillIndex = normalSkillIndex };
            if (manager.HasComponent<CombatAttackCast>(entity))
            {
                manager.SetComponentData(entity, cast);
            }
            else
            {
                manager.AddComponentData(entity, cast);
            }
        }

        public static int GetNormalSkillIndex(int unitHandle)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            return manager.GetComponentData<CombatAttackCast>(entity).NormalSkillIndex;
        }

        public static bool TryConsumeShake(int unitHandle, float shakeBeforeMs)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            CombatAttackCast cast = manager.GetComponentData<CombatAttackCast>(entity);
            if (cast.ShakeTriggered || cast.ElapsedSeconds * 1000f < shakeBeforeMs)
            {
                return false;
            }
            cast.ShakeTriggered = true;
            manager.SetComponentData(entity, cast);
            return true;
        }

        public static bool TryConsumeAnimationEnd(int unitHandle, float animationDurationMs)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            CombatAttackCast cast = manager.GetComponentData<CombatAttackCast>(entity);
            if (cast.AnimationFinished || cast.ElapsedSeconds * 1000f < animationDurationMs)
            {
                return false;
            }
            cast.AnimationFinished = true;
            manager.SetComponentData(entity, cast);
            return true;
        }

        public static bool IsComplete(int unitHandle, float castCooldownMs)
        {
            Entity entity = GetEntity(unitHandle, out EntityManager manager);
            return manager.GetComponentData<CombatAttackCast>(entity).ElapsedSeconds * 1000f >= castCooldownMs;
        }

        public static void End(int unitHandle)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (unitHandle == 0 || world == null || !world.IsCreated)
            {
                return;
            }
            if (CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
            {
                EntityManager manager = world.EntityManager;
                if (manager.HasComponent<CombatAttackCast>(entity))
                {
                    manager.RemoveComponent<CombatAttackCast>(entity);
                }
            }
        }

        private static Entity GetEntity(int unitHandle, out EntityManager manager)
        {
            if (!CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
            {
                throw new System.InvalidOperationException("Attack cast unit has been released.");
            }
            manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            return entity;
        }
    }
}
