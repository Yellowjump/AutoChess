using System;
using System.Collections.Generic;
using Unity.Entities;

namespace Zizouqi.CombatDots
{
    // Each registration is distinct, even when the same skill activates twice.
    public struct CombatTriggerListener : IBufferElementData
    {
        public int TriggerType;
        public int RegistrationId;
    }

    // The buffer lives on the same ECS entity as the unit's attributes.
    public static class CombatTriggerIndex
    {
        public static bool IsOwnerValid(int unitHandle)
        {
            return TryGetOwner(unitHandle, out _, out _);
        }

        public static bool TryCreateOwner(int unitHandle)
        {
            if (!CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
            {
                return false;
            }
            EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!manager.HasBuffer<CombatTriggerListener>(entity))
            {
                manager.AddBuffer<CombatTriggerListener>(entity);
            }
            return true;
        }

        public static bool TryAdd(int ownerId, int triggerType, out int registrationId)
        {
            registrationId = 0;
            if (!TryGetOwner(ownerId, out EntityManager manager, out Entity entity))
            {
                return false;
            }

            registrationId = CombatBoardBridge.AllocateTriggerRegistrationId();
            manager.GetBuffer<CombatTriggerListener>(entity).Add(new CombatTriggerListener
            {
                TriggerType = triggerType,
                RegistrationId = registrationId
            });
            return true;
        }

        public static void Remove(int ownerId, int registrationId)
        {
            if (!TryGetOwner(ownerId, out EntityManager manager, out Entity entity))
            {
                return;
            }

            DynamicBuffer<CombatTriggerListener> listeners = manager.GetBuffer<CombatTriggerListener>(entity);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].RegistrationId == registrationId)
                {
                    listeners.RemoveAt(i);
                    return;
                }
            }
        }

        // Copy before calling hot-update commands: they can add/remove listeners recursively.
        public static bool TryCollect(int ownerId, int triggerType, List<int> destination)
        {
            destination.Clear();
            if (!TryGetOwner(ownerId, out EntityManager manager, out Entity entity))
            {
                return false;
            }

            DynamicBuffer<CombatTriggerListener> listeners = manager.GetBuffer<CombatTriggerListener>(entity);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].TriggerType == triggerType)
                {
                    destination.Add(listeners[i].RegistrationId);
                }
            }
            return true;
        }

        // Conservative when the owner index is unavailable: keep managed
        // damage callbacks rather than silently skipping a registered rule.
        public static bool HasEither(int ownerId, int firstType, int secondType)
        {
            if (!TryGetOwner(ownerId, out EntityManager manager, out Entity entity))
                return true;
            DynamicBuffer<CombatTriggerListener> listeners =
                manager.GetBuffer<CombatTriggerListener>(entity);
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i].TriggerType == firstType ||
                    listeners[i].TriggerType == secondType)
                    return true;
            return false;
        }

        public static bool HasDamageListeners(int casterHandle, int targetHandle)
        {
            // TriggerType values are serialized and new values are appended.
            return HasEither(casterHandle, 6, 8) ||
                   HasEither(targetHandle, 7, 9);
        }

        public static void Release(int unitHandle)
        {
            if (TryGetOwner(unitHandle, out EntityManager manager, out Entity entity))
            {
                manager.RemoveComponent<CombatTriggerListener>(entity);
            }
        }

        private static bool TryGetOwner(int unitHandle, out EntityManager manager, out Entity entity)
        {
            manager = default;
            entity = Entity.Null;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || unitHandle == 0 ||
                !CombatAttributeBridge.TryGetEntity(unitHandle, out entity))
            {
                return false;
            }
            manager = world.EntityManager;
            return manager.HasBuffer<CombatTriggerListener>(entity);
        }
    }

    // A skill, buff, projectile, or position point owns one ordered trigger group.
    // Slots resolve authoring objects only when a matching event is dispatched.
    public static class CombatTriggerGroupBridge
    {
        private static readonly Dictionary<int, Entity> Groups = new Dictionary<int, Entity>();
        private static World s_World;
        private static int s_NextHandle;

        public static void Create(ref int handle, List<int> triggerTypes)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            Entity entity = manager.CreateEntity();
            DynamicBuffer<CombatTriggerListener> listeners =
                manager.AddBuffer<CombatTriggerListener>(entity);
            for (int i = 0; i < triggerTypes.Count; i++)
            {
                listeners.Add(new CombatTriggerListener
                {
                    TriggerType = triggerTypes[i],
                    RegistrationId = i
                });
            }
            handle = ++s_NextHandle;
            Groups.Add(handle, entity);
        }

        public static bool Matches(int handle, List<int> triggerTypes)
        {
            if (handle == 0 || !TryGetManager(out EntityManager manager) ||
                !Groups.TryGetValue(handle, out Entity entity) || !manager.Exists(entity))
            {
                return false;
            }
            DynamicBuffer<CombatTriggerListener> listeners =
                manager.GetBuffer<CombatTriggerListener>(entity);
            if (listeners.Length != triggerTypes.Count)
            {
                return false;
            }
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].TriggerType != triggerTypes[i])
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsValid(int handle)
        {
            return handle != 0 && TryGetManager(out EntityManager manager) &&
                Groups.TryGetValue(handle, out Entity entity) && manager.Exists(entity);
        }

        public static void Collect(int handle, int triggerType, List<int> slots)
        {
            slots.Clear();
            DynamicBuffer<CombatTriggerListener> listeners = GetListeners(handle);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].TriggerType == triggerType)
                {
                    slots.Add(listeners[i].RegistrationId);
                }
            }
        }

        public static void CollectRegistrations(int handle, int activeType,
            int destroyType, List<int> slots)
        {
            slots.Clear();
            DynamicBuffer<CombatTriggerListener> listeners = GetListeners(handle);
            for (int i = 0; i < listeners.Length; i++)
            {
                int type = listeners[i].TriggerType;
                if (type != activeType && type != destroyType)
                {
                    slots.Add(listeners[i].RegistrationId);
                }
            }
        }

        private static DynamicBuffer<CombatTriggerListener> GetListeners(int handle)
        {
            EntityManager manager = GetManager();
            if (!Groups.TryGetValue(handle, out Entity entity) || !manager.Exists(entity))
            {
                throw new InvalidOperationException("Combat trigger group was released before dispatch.");
            }
            return manager.GetBuffer<CombatTriggerListener>(entity);
        }

        public static void Release(ref int handle)
        {
            if (handle != 0 && TryGetManager(out EntityManager manager) &&
                Groups.TryGetValue(handle, out Entity entity) && manager.Exists(entity))
            {
                manager.DestroyEntity(entity);
            }
            Groups.Remove(handle);
            handle = 0;
        }

        private static EntityManager GetManager()
        {
            if (!TryGetManager(out EntityManager manager))
            {
                throw new InvalidOperationException("Combat triggers require an active ECS World.");
            }
            return manager;
        }

        private static bool TryGetManager(out EntityManager manager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                Groups.Clear();
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
