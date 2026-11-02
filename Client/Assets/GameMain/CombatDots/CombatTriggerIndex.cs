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
        public Entity Trigger;
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

        public static bool TryAdd(int ownerId, int triggerType, out int registrationId,
            Entity trigger = default)
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
                RegistrationId = registrationId,
                Trigger = trigger
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

        // Copy the entity links before executing commands. A command may change
        // the listener buffer or destroy a skill while the event is dispatched.
        public static bool TryCollectEntries(int ownerId, int triggerType,
            List<CombatTriggerListener> destination)
        {
            destination.Clear();
            if (!TryGetOwner(ownerId, out EntityManager manager, out Entity entity))
                return false;
            DynamicBuffer<CombatTriggerListener> listeners =
                manager.GetBuffer<CombatTriggerListener>(entity);
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i].TriggerType == triggerType)
                    destination.Add(listeners[i]);
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

    // Complete data-only attribute rules execute in their original call order.
    // Other rules retain the OneTrigger callback path.
    public static class CombatAttributeTriggerExecutor
    {
        public static bool TryExecute(Entity trigger, int ownerHandle,
            int triggerType, int buffHandle = 0, float deltaMs = 0f) =>
            TryExecuteCore(trigger, ownerHandle, triggerType,
                default, false, buffHandle, deltaMs);

        public static bool TryExecute(Entity trigger, int ownerHandle,
            int triggerType, CombatDamageEvent damage, int buffHandle = 0) =>
            TryExecuteCore(trigger, ownerHandle, triggerType, damage,
                true, buffHandle, 0f);

        private static bool TryExecuteCore(Entity trigger, int ownerHandle,
            int triggerType, CombatDamageEvent damage, bool hasDamage,
            int buffHandle, float deltaMs)
        {
            if (trigger == Entity.Null || ownerHandle == 0 ||
                (hasDamage && triggerType != (int)damage.Stage) ||
                !CombatAttributeBridge.TryGetEntity(ownerHandle, out Entity owner))
                return false;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;
            EntityManager manager = world.EntityManager;
            if (!manager.Exists(trigger) ||
                !manager.HasComponent<CombatTriggerDefinition>(trigger) ||
                !manager.HasBuffer<CombatConditionNode>(trigger) ||
                !manager.HasBuffer<CombatTargetPickerNode>(trigger) ||
                !manager.HasBuffer<CombatCommandNode>(trigger))
                return false;
            CombatTriggerDefinition definition =
                manager.GetComponentData<CombatTriggerDefinition>(trigger);
            if (!definition.Enabled || definition.Owner != owner ||
                definition.TriggerType != triggerType)
                return false;
            DynamicBuffer<CombatConditionNode> conditions =
                manager.GetBuffer<CombatConditionNode>(trigger);
            DynamicBuffer<CombatTargetPickerNode> pickers =
                manager.GetBuffer<CombatTargetPickerNode>(trigger);
            DynamicBuffer<CombatCommandNode> commands =
                manager.GetBuffer<CombatCommandNode>(trigger);
            if (conditions.Length != 1 || pickers.Length != 1 ||
                commands.Length == 0)
                return false;
            CombatConditionNode condition = conditions[0];
            bool timed = condition.Kind == CombatConditionKind.Timed &&
                         buffHandle != 0 && deltaMs > 0f && !hasDamage &&
                         condition.Param1 > 0 &&
                         pickers[0].Kind == CombatTargetPickerKind.TriggerOwner;
            bool sourceSkillType = condition.Kind ==
                                       CombatConditionKind.RelateSkill &&
                                   condition.Source == 0 &&
                                   condition.CheckType == 0 &&
                                   definition.Skill != Entity.Null &&
                                   manager.Exists(definition.Skill) &&
                                   manager.HasComponent<CombatSkillDefinition>(
                                       definition.Skill);
            bool conditionGroup = condition.Kind == CombatConditionKind.Group &&
                                  TryValidateConditionGroup(conditions, 0,
                                      definition.Skill, manager,
                                      buffHandle != 0 && deltaMs > 0f &&
                                      !hasDamage && pickers[0].Kind ==
                                      CombatTargetPickerKind.TriggerOwner,
                                      0);
            if (!timed && !sourceSkillType && !conditionGroup &&
                (condition.Kind != CombatConditionKind.Always ||
                 condition.Reverse)) return false;
            if (!TryCountAttributeOperations(commands, 0, commands.Length,
                    0, buffHandle != 0, out int operationCount) ||
                operationCount == 0)
                return false;
            if (buffHandle != 0 &&
                (!CombatBuffTimerBridge.TryGetEntity(buffHandle,
                    out Entity buffEntity) ||
                 !manager.HasComponent<CombatBuffTimer>(buffEntity) ||
                 manager.GetComponentData<CombatBuffTimer>(buffEntity)
                     .OwnerHandle != ownerHandle))
                return false;
            if (sourceSkillType)
            {
                bool sameType = manager.GetComponentData<CombatSkillDefinition>(
                    definition.Skill).SkillType == condition.Param1;
                if (!(condition.Reverse ? !sameType : sameType)) return true;
            }

            int targetHandle;
            Entity targetEntity;
            switch (pickers[0].Kind)
            {
                case CombatTargetPickerKind.SkillCaster:
                    if (!CombatSkillEntityBridge.TryGetCasterHandle(
                            definition.Skill, out targetHandle) ||
                        !CombatAttributeBridge.TryGetEntity(targetHandle,
                            out targetEntity)) return false;
                    break;
                case CombatTargetPickerKind.TriggerOwner:
                    targetHandle = ownerHandle;
                    targetEntity = owner;
                    break;
                case CombatTargetPickerKind.RelatedDamageTarget:
                    if (!hasDamage) return false;
                    bool source = pickers[0].DamageTargetType == 0;
                    targetHandle = source ? damage.CasterHandle : damage.TargetHandle;
                    targetEntity = source ? damage.Caster : damage.Target;
                    break;
                case CombatTargetPickerKind.SkillCasterCurTarget:
                    if (!CombatSkillEntityBridge.TryGetCasterHandle(
                            definition.Skill, out int casterHandle)) return false;
                    int targetUid = CombatAttributeBridge.GetAttackTargetUid(casterHandle);
                    if (targetUid == 0 ||
                        !CombatAttributeBridge.TryGetHandleByUid(targetUid,
                            out targetHandle)) return false;
                    if (!CombatAttributeBridge.TryGetEntity(targetHandle,
                            out targetEntity)) return false;
                    break;
                default:
                    return false;
            }
            if (targetHandle == 0 || targetEntity == Entity.Null ||
                !CombatAttributeBridge.TryGetEntity(targetHandle,
                    out Entity currentTarget) || currentTarget != targetEntity)
                return false;
            if (timed)
            {
                bool passed = false;
                if (condition.Param2 == 0 ||
                    condition.Param2 > condition.PassCount)
                {
                    condition.AccumulatedMs += deltaMs;
                    passed = condition.AccumulatedMs >= condition.Param1;
                    if (passed)
                    {
                        condition.AccumulatedMs -= condition.Param1;
                        condition.PassCount++;
                    }
                    conditions[0] = condition;
                }
                if (!(condition.Reverse ? !passed : passed)) return true;
            }
            else if (conditionGroup)
            {
                int skillType = definition.Skill != Entity.Null &&
                                manager.Exists(definition.Skill) &&
                                manager.HasComponent<CombatSkillDefinition>(
                                    definition.Skill)
                    ? manager.GetComponentData<CombatSkillDefinition>(
                        definition.Skill).SkillType : 0;
                if (!EvaluateConditionGroup(conditions, 0, skillType,
                        deltaMs)) return true;
            }
            ExecuteAttributeOperations(commands, 0, commands.Length,
                targetHandle, buffHandle);
            return true;
        }

        private static bool TryValidateConditionGroup(
            DynamicBuffer<CombatConditionNode> nodes, int index,
            Entity sourceSkill, EntityManager manager, bool allowTimed,
            int depth)
        {
            if (depth > 8 || index < 0 || index >= nodes.Length)
                return false;
            CombatConditionNode node = nodes[index];
            if (node.EndIndex <= index || node.EndIndex > nodes.Length)
                return false;
            switch (node.Kind)
            {
                case CombatConditionKind.Group:
                    if (node.Logic > 1) return false;
                    for (int child = index + 1; child < node.EndIndex;)
                    {
                        if (!TryValidateConditionGroup(nodes, child,
                                sourceSkill, manager, allowTimed, depth + 1))
                            return false;
                        if (nodes[child].EndIndex > node.EndIndex)
                            return false;
                        child = nodes[child].EndIndex;
                    }
                    return index != 0 || node.EndIndex == nodes.Length;
                case CombatConditionKind.Always:
                    return node.EndIndex == index + 1;
                case CombatConditionKind.RelateSkill:
                    return node.EndIndex == index + 1 &&
                           node.Source == 0 && node.CheckType == 0 &&
                           sourceSkill != Entity.Null &&
                           manager.Exists(sourceSkill) &&
                           manager.HasComponent<CombatSkillDefinition>(
                               sourceSkill);
                case CombatConditionKind.Timed:
                    return node.EndIndex == index + 1 && allowTimed &&
                           node.Param1 > 0;
                default:
                    return false;
            }
        }

        private static bool EvaluateConditionGroup(
            DynamicBuffer<CombatConditionNode> nodes, int index,
            int sourceSkillType, float deltaMs)
        {
            CombatConditionNode node = nodes[index];
            bool result;
            switch (node.Kind)
            {
                case CombatConditionKind.Group:
                    result = index + 1 == node.EndIndex || node.Logic == 0;
                    for (int child = index + 1; child < node.EndIndex;)
                    {
                        bool childResult = EvaluateConditionGroup(nodes, child,
                            sourceSkillType, deltaMs);
                        result = node.Logic == 0
                            ? result && childResult : result || childResult;
                        if ((node.Logic == 0 && !result) ||
                            (node.Logic == 1 && result)) break;
                        child = nodes[child].EndIndex;
                    }
                    break;
                case CombatConditionKind.RelateSkill:
                    result = sourceSkillType == node.Param1;
                    break;
                case CombatConditionKind.Timed:
                    result = false;
                    if (node.Param2 == 0 || node.Param2 > node.PassCount)
                    {
                        node.AccumulatedMs += deltaMs;
                        result = node.AccumulatedMs >= node.Param1;
                        if (result)
                        {
                            node.AccumulatedMs -= node.Param1;
                            node.PassCount++;
                        }
                        nodes[index] = node;
                    }
                    break;
                default:
                    result = true;
                    break;
            }
            return node.Reverse ? !result : result;
        }

        private static bool TryCountAttributeOperations(
            DynamicBuffer<CombatCommandNode> commands, int start, int end,
            int depth, bool allowShield, out int count)
        {
            count = 0;
            if (depth > 8) return false;
            for (int index = start; index < end;)
            {
                CombatCommandNode node = commands[index];
                if (node.EndIndex <= index || node.EndIndex > end)
                    return false;
                switch (node.Kind)
                {
                    case CombatCommandKind.ChangeAttribute:
                        if (node.EndIndex != index + 1) return false;
                        count++;
                        break;
                    case CombatCommandKind.Shield:
                        if (!allowShield || node.EndIndex != index + 1)
                            return false;
                        count++;
                        break;
                    case CombatCommandKind.Repeat:
                        if (node.RepeatNumberType != 0 ||
                            !TryCountAttributeOperations(commands, index + 1,
                                node.EndIndex, depth + 1, allowShield,
                                out int childCount)) return false;
                        count += (int)System.Math.Min(513L,
                            (long)System.Math.Max(0, node.RepeatParameter) *
                            childCount);
                        break;
                    case CombatCommandKind.NoOp:
                        break;
                    default:
                        return false;
                }
                if (count > 512) return false;
                index = node.EndIndex;
            }
            return true;
        }

        private static void ExecuteAttributeOperations(
            DynamicBuffer<CombatCommandNode> commands, int start, int end,
            int targetHandle, int buffHandle)
        {
            for (int index = start; index < end;)
            {
                CombatCommandNode node = commands[index];
                if (node.Kind == CombatCommandKind.ChangeAttribute)
                    CombatAttributeBridge.AddNum(targetHandle,
                        node.AttributeType, node.Delta);
                else if (node.Kind == CombatCommandKind.Shield)
                    CombatBuffTimerBridge.ApplyShield(buffHandle,
                        node.RemoveShield, node.ShieldMode,
                        node.ShieldFixedValue, node.ShieldAttributeType,
                        node.ShieldAttributePercent);
                else if (node.Kind == CombatCommandKind.Repeat)
                    for (int repetition = 0; repetition < node.RepeatParameter;
                         repetition++)
                        ExecuteAttributeOperations(commands, index + 1,
                            node.EndIndex, targetHandle, buffHandle);
                index = node.EndIndex;
            }
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
