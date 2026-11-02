using System.Collections.Generic;
using Unity.Entities;

namespace Zizouqi.CombatDots
{
    // One entity per equipped skill. Authoring Skill objects are not referenced.
    public struct CombatSkillDefinition : IComponentData
    {
        public Entity Unit;
        public int SkillId;
        public int SkillType;
        public int SlotIndex;
        public int FromItemId;
        public int FromItemUid;
        public int CastPower;
        public int RangeMm;
        public int TargetType;
        public int CastDurationMs;
        public int ShakeBeforeMs;
        public int AnimationDurationMs;
    }

    public struct CombatSkillRuntime : IComponentData
    {
        public int Parameter;
        public float CooldownRemainingSeconds;
        public bool Active;
    }

    // Preserve the trigger order authored within each skill.
    public struct CombatSkillTriggerLink : IBufferElementData
    {
        public Entity Trigger;
        public int TriggerType;
        public int Order;
    }

    public struct CombatTriggerDefinition : IComponentData
    {
        public Entity Skill;
        public Entity Owner;
        public int TriggerType;
        public int Order;
        public bool Enabled;
    }

    public static class CombatSkillEntityBridge
    {
        public static bool TryGetCasterHandle(Entity skill, out int handle)
        {
            handle = 0;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;
            EntityManager manager = world.EntityManager;
            if (skill == Entity.Null || !manager.Exists(skill) ||
                !manager.HasComponent<CombatSkillDefinition>(skill)) return false;
            Entity unit = manager.GetComponentData<CombatSkillDefinition>(skill).Unit;
            if (!manager.Exists(unit) || !manager.HasComponent<CombatUnit>(unit))
                return false;
            int uid = manager.GetComponentData<CombatUnit>(unit).HeroUid;
            return CombatAttributeBridge.TryGetHandleByUid(uid, out handle) &&
                CombatAttributeBridge.TryGetEntity(handle, out Entity current) &&
                current == unit;
        }

        public static void Destroy(EntityManager manager, Entity skill)
        {
            if (!manager.Exists(skill)) return;
            var triggers = new List<Entity>();
            if (manager.HasBuffer<CombatSkillTriggerLink>(skill))
            {
                DynamicBuffer<CombatSkillTriggerLink> links =
                    manager.GetBuffer<CombatSkillTriggerLink>(skill);
                for (int i = 0; i < links.Length; i++)
                    triggers.Add(links[i].Trigger);
            }
            foreach (Entity trigger in triggers)
            {
                if (manager.Exists(trigger)) manager.DestroyEntity(trigger);
            }
            manager.DestroyEntity(skill);
        }

        public static void DestroyForUnit(EntityManager manager, Entity unit)
        {
            if (!manager.Exists(unit) || !manager.HasBuffer<CombatSkillSlot>(unit))
                return;
            DynamicBuffer<CombatSkillSlot> slots =
                manager.GetBuffer<CombatSkillSlot>(unit);
            var definitions = new List<Entity>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                Entity definition = slots[i].Definition;
                if (definition != Entity.Null) definitions.Add(definition);
            }
            foreach (Entity definition in definitions)
                Destroy(manager, definition);
        }
    }
}
