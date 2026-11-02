using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine.Pool;
using Zizouqi.CombatDots;
using EcsEntity = Unity.Entities.Entity;

namespace SkillSystem
{
    // Converts a configured authoring Skill into unmanaged ECS definitions.
    // Callers may release the authoring graph after all runtime consumers use
    // the resulting skill and trigger entities.
    internal static class CombatSkillEntityCompiler
    {
        public static EcsEntity Compile(Skill source, EcsEntity unit, int slotIndex)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                throw new InvalidOperationException("Skill compilation requires an ECS World.");
            EntityManager manager = world.EntityManager;
            EcsEntity skill = manager.CreateEntity(typeof(CombatSkillDefinition),
                typeof(CombatSkillRuntime));
            try
            {
                DynamicBuffer<CombatSkillTriggerLink> links =
                    manager.AddBuffer<CombatSkillTriggerLink>(skill);
                manager.SetComponentData(skill, new CombatSkillDefinition
                {
                    Unit = unit,
                    SkillId = source.SkillID,
                    SkillType = (int)source.CurSkillType,
                    SlotIndex = slotIndex,
                    FromItemId = source.FromItemID,
                    FromItemUid = source.FromItemUID,
                    CastPower = source.CastPower,
                    RangeMm = source.SkillRange,
                    TargetType = (int)source.CurSkillCastTargetType,
                    CastDurationMs = source.DefaultSkillCDMs,
                    ShakeBeforeMs = source.DefaultShakeBeforeMs,
                    AnimationDurationMs = source.DefaultAnimationDurationMs
                });
                manager.SetComponentData(skill, new CombatSkillRuntime
                {
                    Parameter = source.paramInt,
                    CooldownRemainingSeconds = source.LeftSkillCD
                });
                if (source.CurTriggerList == null) return skill;
                for (int i = 0; i < source.CurTriggerList.Count; i++)
                {
                    OneTrigger sourceTrigger = source.CurTriggerList[i];
                    EcsEntity trigger = CompileTrigger(manager, sourceTrigger, skill, unit, i);
                    // Creating an entity is structural; reacquire the skill buffer.
                    links = manager.GetBuffer<CombatSkillTriggerLink>(skill);
                    links.Add(new CombatSkillTriggerLink
                    {
                        Trigger = trigger,
                        TriggerType = (int)sourceTrigger.CurTriggerType,
                        Order = i
                    });
                }
                return skill;
            }
            catch
            {
                CombatSkillEntityBridge.Destroy(manager, skill);
                throw;
            }
        }

        public static void CompileBuff(Buff source, int ownerHandle)
        {
            if (source == null || source.CurTriggerList == null ||
                source.TimerHandle == 0 || ownerHandle == 0 ||
                !CombatBuffTimerBridge.TryGetEntity(source.TimerHandle,
                    out EcsEntity buffEntity) ||
                !CombatAttributeBridge.TryGetEntity(ownerHandle,
                    out EcsEntity owner)) return;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            EntityManager manager = world.EntityManager;
            if (manager.HasBuffer<CombatBuffTriggerLink>(buffEntity)) return;
            manager.AddBuffer<CombatBuffTriggerLink>(buffEntity);
            EcsEntity sourceSkill = source.ParentSkill?.RuntimeDefinition ??
                EcsEntity.Null;
            try
            {
                for (int i = 0; i < source.CurTriggerList.Count; i++)
                {
                    OneTrigger authored = source.CurTriggerList[i];
                    EcsEntity trigger = CompileTrigger(manager, authored,
                        sourceSkill, owner, i);
                    manager.GetBuffer<CombatBuffTriggerLink>(buffEntity).Add(
                        new CombatBuffTriggerLink { Trigger = trigger, Order = i });
                }
            }
            catch
            {
                int count = manager.GetBuffer<CombatBuffTriggerLink>(buffEntity).Length;
                for (int i = 0; i < count; i++)
                {
                    EcsEntity trigger = manager.GetBuffer<CombatBuffTriggerLink>(
                        buffEntity)[i].Trigger;
                    if (manager.Exists(trigger)) manager.DestroyEntity(trigger);
                }
                manager.GetBuffer<CombatBuffTriggerLink>(buffEntity).Clear();
                manager.RemoveComponent<CombatBuffTriggerLink>(buffEntity);
                throw;
            }
        }

        private static EcsEntity CompileTrigger(EntityManager manager, OneTrigger source,
            EcsEntity skill, EcsEntity owner, int order)
        {
            if (source == null) throw new InvalidOperationException("Null skill trigger.");
            List<CombatConditionNode> conditions = ListPool<CombatConditionNode>.Get();
            List<CombatTargetPickerNode> pickers = ListPool<CombatTargetPickerNode>.Get();
            List<CombatCommandNode> commands = ListPool<CombatCommandNode>.Get();
            List<int> buffIds = ListPool<int>.Get();
            try
            {
                if (source.CurCondition != null)
                    CombatConditionCompiler.CompileNodes(source.CurCondition, conditions);
                if (source.CurTargetPicker != null)
                    CombatTargetPickerCompiler.CompileNodes(source.CurTargetPicker, pickers);
                CombatCommandCompiler.CompileNodes(source.CurCommandList, commands, buffIds);

                EcsEntity trigger = manager.CreateEntity(typeof(CombatTriggerDefinition));
                try
                {
                    manager.SetComponentData(trigger, new CombatTriggerDefinition
                    {
                        Skill = skill,
                        Owner = owner,
                        TriggerType = (int)source.CurTriggerType,
                        Order = order,
                        Enabled = source.CurCondition != null
                    });
                    Copy(manager.AddBuffer<CombatConditionNode>(trigger), conditions);
                    Copy(manager.AddBuffer<CombatTargetPickerNode>(trigger), pickers);
                    Copy(manager.AddBuffer<CombatCommandNode>(trigger), commands);
                    Copy(manager.AddBuffer<CombatCommandBuffId>(trigger), buffIds,
                        value => new CombatCommandBuffId { Value = value });
                    return trigger;
                }
                catch
                {
                    if (manager.Exists(trigger)) manager.DestroyEntity(trigger);
                    throw;
                }
            }
            finally
            {
                ListPool<CombatConditionNode>.Release(conditions);
                ListPool<CombatTargetPickerNode>.Release(pickers);
                ListPool<CombatCommandNode>.Release(commands);
                ListPool<int>.Release(buffIds);
            }
        }

        private static void Copy<T>(DynamicBuffer<T> destination, List<T> values)
            where T : unmanaged, IBufferElementData
        {
            destination.ResizeUninitialized(values.Count);
            for (int i = 0; i < values.Count; i++) destination[i] = values[i];
        }

        private static void Copy<TSource, TDestination>(
            DynamicBuffer<TDestination> destination, List<TSource> values,
            Func<TSource, TDestination> convert)
            where TDestination : unmanaged, IBufferElementData
        {
            destination.ResizeUninitialized(values.Count);
            for (int i = 0; i < values.Count; i++) destination[i] = convert(values[i]);
        }
    }
}
