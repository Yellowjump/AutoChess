using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Zizouqi.CombatDots
{
    // This assembly is AOT. Hot-update code accesses the Entity through the bridge only.
    public struct CombatCooldown : IComponentData
    {
        public float RemainingSeconds;
        public int CastDurationMs;
        public int ShakeBeforeMs;
        public int AnimationDurationMs;
        public int TriggerValue;
    }

    [DisableAutoCreation]
    public partial class CombatCooldownSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = CombatCooldownBridge.ConsumeDeltaTime();
            Entities.ForEach((ref CombatCooldown cooldown) =>
            {
                if (cooldown.RemainingSeconds > 0)
                {
                    cooldown.RemainingSeconds -= deltaTime;
                }
            }).ScheduleParallel();
            Dependency.Complete();
        }
    }

    public static class CombatCooldownBridge
    {
        private static readonly Dictionary<int, Entity> EntitiesByHandle = new Dictionary<int, Entity>();
        private static World s_World;
        private static int s_NextHandle;

        // Called once per battle logic step, before skills inspect their cooldowns.
        public static void Advance(float deltaTime)
        {
            if (deltaTime <= 0 || !TryGetManager(out _))
            {
                return;
            }

            CombatBoardBridge.SetCooldownDeltaSeconds(deltaTime);
            s_World.GetOrCreateSystemManaged<CombatCooldownSystem>().Update();
        }

        internal static float ConsumeDeltaTime()
        {
            return CombatBoardBridge.ConsumeCooldownDeltaSeconds();
        }

        // Returns false until a World exists; the old timer can advance during startup.
        public static bool SetRemaining(ref int handle, float seconds)
        {
            if (!TryGetManager(out EntityManager manager))
            {
                return false;
            }

            if (handle == 0 || !EntitiesByHandle.TryGetValue(handle, out Entity entity) || !manager.Exists(entity))
            {
                if (handle != 0)
                {
                    EntitiesByHandle.Remove(handle);
                }
                entity = manager.CreateEntity(typeof(CombatCooldown));
                handle = ++s_NextHandle;
                EntitiesByHandle.Add(handle, entity);
            }

            CombatCooldown cooldown = manager.GetComponentData<CombatCooldown>(entity);
            cooldown.RemainingSeconds = seconds;
            manager.SetComponentData(entity, cooldown);
            return true;
        }

        public static void Configure(int handle, int castDurationMs, int shakeBeforeMs,
            int animationDurationMs, int triggerValue)
        {
            if (TryGetCooldown(handle, out EntityManager manager, out Entity entity))
            {
                CombatCooldown cooldown = manager.GetComponentData<CombatCooldown>(entity);
                cooldown.CastDurationMs = castDurationMs;
                cooldown.ShakeBeforeMs = shakeBeforeMs;
                cooldown.AnimationDurationMs = animationDurationMs;
                cooldown.TriggerValue = triggerValue;
                manager.SetComponentData(entity, cooldown);
            }
        }

        public static void SetCast(int handle, int castDurationMs, int shakeBeforeMs,
            int animationDurationMs)
        {
            if (TryGetCooldown(handle, out EntityManager manager, out Entity entity))
            {
                CombatCooldown cooldown = manager.GetComponentData<CombatCooldown>(entity);
                cooldown.CastDurationMs = castDurationMs;
                cooldown.ShakeBeforeMs = shakeBeforeMs;
                cooldown.AnimationDurationMs = animationDurationMs;
                manager.SetComponentData(entity, cooldown);
            }
        }

        public static void ResetAfterDestroy(ref int handle, int castDurationMs,
            int shakeBeforeMs, int animationDurationMs)
        {
            if (handle == 0)
            {
                return;
            }
            if (TryGetCooldown(handle, out EntityManager manager, out Entity entity))
            {
                CombatCooldown cooldown = manager.GetComponentData<CombatCooldown>(entity);
                cooldown.RemainingSeconds = 0;
                cooldown.CastDurationMs = castDurationMs;
                cooldown.ShakeBeforeMs = shakeBeforeMs;
                cooldown.AnimationDurationMs = animationDurationMs;
                manager.SetComponentData(entity, cooldown);
            }
            else if (SetRemaining(ref handle, 0))
            {
                SetCast(handle, castDurationMs, shakeBeforeMs, animationDurationMs);
            }
        }

        public static void BeginCast(ref int handle, int casterHandle, int defaultCooldownMs,
            int defaultShakeMs, int defaultAnimationMs, int initialTriggerValue)
        {
            int cooldownReduction = CombatAttributeBridge.GetValue(casterHandle, 7);
            float reduction = cooldownReduction / (cooldownReduction + 100f);
            float durationMs = defaultCooldownMs * (1 - reduction);
            int castMs = Mathf.CeilToInt(durationMs);
            int shakeMs = defaultShakeMs;
            int animationMs = defaultAnimationMs;
            if (castMs < defaultAnimationMs)
            {
                shakeMs = Mathf.CeilToInt(defaultShakeMs * castMs / (float)defaultAnimationMs);
                animationMs = castMs;
            }
            int previousHandle = handle;
            if (!SetRemaining(ref handle, durationMs / 1000f))
            {
                throw new System.InvalidOperationException("The DOTS world must exist before skills are cast.");
            }
            if (previousHandle != handle)
            {
                SetValue(handle, initialTriggerValue);
            }
            SetCast(handle, castMs, shakeMs, animationMs);
        }

        public static bool TryGetCast(int handle, out int castDurationMs, out int shakeBeforeMs,
            out int animationDurationMs)
        {
            castDurationMs = 0;
            shakeBeforeMs = 0;
            animationDurationMs = 0;
            if (!TryGetCooldown(handle, out EntityManager manager, out Entity entity))
            {
                return false;
            }
            CombatCooldown cooldown = manager.GetComponentData<CombatCooldown>(entity);
            castDurationMs = cooldown.CastDurationMs;
            shakeBeforeMs = cooldown.ShakeBeforeMs;
            animationDurationMs = cooldown.AnimationDurationMs;
            return true;
        }

        public static bool TryGetValue(int handle, out int value)
        {
            value = 0;
            if (!TryGetCooldown(handle, out EntityManager manager, out Entity entity))
            {
                return false;
            }
            value = manager.GetComponentData<CombatCooldown>(entity).TriggerValue;
            return true;
        }

        public static void SetValue(int handle, int value)
        {
            if (TryGetCooldown(handle, out EntityManager manager, out Entity entity))
            {
                CombatCooldown cooldown = manager.GetComponentData<CombatCooldown>(entity);
                cooldown.TriggerValue = value;
                manager.SetComponentData(entity, cooldown);
            }
        }

        public static bool TryGetRemaining(int handle, out float seconds)
        {
            seconds = 0;
            if (!TryGetCooldown(handle, out EntityManager manager, out Entity entity))
            {
                return false;
            }

            seconds = manager.GetComponentData<CombatCooldown>(entity).RemainingSeconds;
            return true;
        }

        public static bool IsReady(int handle)
        {
            return !TryGetRemaining(handle, out float seconds) || seconds <= 0;
        }

        private static bool TryGetCooldown(int handle, out EntityManager manager, out Entity entity)
        {
            manager = default;
            entity = Entity.Null;
            return handle != 0 && TryGetManager(out manager) &&
                   EntitiesByHandle.TryGetValue(handle, out entity) && manager.Exists(entity);
        }

        public static void Release(ref int handle)
        {
            if (handle == 0)
            {
                return;
            }

            if (TryGetManager(out EntityManager manager) &&
                EntitiesByHandle.TryGetValue(handle, out Entity entity) && manager.Exists(entity))
            {
                manager.DestroyEntity(entity);
            }

            EntitiesByHandle.Remove(handle);
            handle = 0;
        }

        private static bool TryGetManager(out EntityManager manager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                EntitiesByHandle.Clear();
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
