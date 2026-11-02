using System;
using System.Collections.Generic;
using Entity;
using Unity.Collections;
using Unity.Profiling;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    // Strictly matched trigger invocations use one value snapshot per phase.
    // The Burst batch chooses targets and prepares damage; effects commit in
    // invocation order. Unsupported rules keep the existing immediate path.
    internal static class CombatTriggerSimpleQueue
    {
        private struct Pending
        {
            public OneTrigger Trigger;
            public TriggerList SourceList;
            public Skill SourceSkill;
            public EntityQizi Caster;
            public CommandCreateBullet BulletCommand;
            public CombatCommandNode BulletNode;
            public CommandCreatePosPoint PointCommand;
            public CombatCommandNode PointNode;
            public bool IsBuff;
            public CommandCreateBuff TemporaryBuffCommand;
            public int BuffId;
            public bool CreateTemporaryBuff;
            public bool IsRemoveBuff;
            public int RemoveBuffId;
            public bool IsSfx;
            public bool CreateSfx;
            public int SfxId;
            public bool IsAudio;
            public bool CreateAudio;
            public int AudioId;
            public bool IsWeapon;
            public bool ShowWeapon;
            public bool UseItemWeapon;
            public WeaponHandleType WeaponHandleType;
            public int WeaponItemId;
            public bool IsAnimation;
            public int AnimationId;
            public bool IsDamage;
            public bool RecomputeDamage;
            public int DamageMode;
            public int DamageFixedValue;
            public int DamageAttributeType;
            public int DamagePercent;
            public bool IsAttribute;
            public bool CompiledAttribute;
            public Unity.Entities.Entity CompiledTrigger;
            public int CompiledOwnerHandle;
            public int AttributeType;
            public int AttributeDelta;
            public bool TriggerDamageRelate;
            public int SelectionIndex;
        }

        private static readonly ProfilerMarker PrepareMarker =
            new ProfilerMarker("Combat.Trigger.Simple.Prepare");
        private static readonly ProfilerMarker DataConditionMarker =
            new ProfilerMarker("Combat.Trigger.Simple.DataCondition");
        private static readonly ProfilerMarker TimedBatchMarker =
            new ProfilerMarker("Combat.Trigger.Simple.TimedBatch");
        private static readonly ProfilerMarker PureGroupMarker =
            new ProfilerMarker("Combat.Trigger.Simple.PureGroup");
        private static readonly ProfilerMarker TimedSkillGroupMarker =
            new ProfilerMarker("Combat.Trigger.Simple.TimedSkillGroup");
        private static readonly ProfilerMarker CommitMarker =
            new ProfilerMarker("Combat.Trigger.Simple.Commit");
        private static readonly ProfilerMarker AttributeMarker =
            new ProfilerMarker("Combat.Trigger.Simple.Attributes");
        private static readonly ProfilerMarker DamageMarker =
            new ProfilerMarker("Combat.Trigger.Simple.DamageEcs");
        private static ProfilerCounterValue<int> s_Queued =
            new ProfilerCounterValue<int>("Combat.Trigger.Simple.Queued");
        private static ProfilerCounterValue<int> s_AreaQueued =
            new ProfilerCounterValue<int>("Combat.Trigger.Simple.AreaQueued");
        private static ProfilerCounterValue<int> s_TimedQueued =
            new ProfilerCounterValue<int>("Combat.Trigger.Simple.TimedQueued");
        private static ProfilerCounterValue<int> s_PureGroupQueued =
            new ProfilerCounterValue<int>("Combat.Trigger.Simple.PureGroupQueued");
        private static ProfilerCounterValue<int> s_TimedSkillGroupQueued =
            new ProfilerCounterValue<int>("Combat.Trigger.Simple.TimedSkillGroupQueued");
        private static ProfilerCounterValue<int> s_CommandEvents =
            new ProfilerCounterValue<int>("Combat.Trigger.Simple.CommandEvents");
        private static ProfilerCounterValue<int> s_DamageEcsCount =
            new ProfilerCounterValue<int>("Combat.Trigger.Simple.DamageEcsCount");
        private static readonly List<Pending> Events = new List<Pending>(64);
        private static readonly List<int> SelectionConditionHandles =
            new List<int>(64);
        private static readonly List<CombatSimpleTargetCandidate> SelectedTargets =
            new List<CombatSimpleTargetCandidate>(32);
        private static readonly List<CombatAttributeIntent> AttributeIntents =
            new List<CombatAttributeIntent>(64);
        private static readonly HashSet<int> AttributeTargetUids =
            new HashSet<int>(64);
        private static readonly List<CombatAttributeIntent> DamageIntents =
            new List<CombatAttributeIntent>(64);
        private static readonly List<CombatDamageNumberEvent> DamageNumbers =
            new List<CombatDamageNumberEvent>(64);
        private static readonly HashSet<int> DamageTargetUids =
            new HashSet<int>(64);
        private static readonly Comparison<CombatSimpleTargetCandidate> TargetOrder =
            CompareTargets;
        private static readonly Comparison<CombatSimpleTargetCandidate> UidOrder =
            CompareTargetUids;
        private static bool s_Recording;
        private static bool s_Flushing;
        private static int s_PendingTimed;
        private static int s_PendingPureGroups;
        private static int s_PendingTimedSkillGroups;

        public static void BeginStep()
        {
            Flush();
            s_Queued.Value = 0;
            s_AreaQueued.Value = 0;
            s_TimedQueued.Value = 0;
            s_PureGroupQueued.Value = 0;
            s_TimedSkillGroupQueued.Value = 0;
            s_PendingTimed = 0;
            s_PendingPureGroups = 0;
            s_PendingTimedSkillGroups = 0;
            s_CommandEvents.Value = 0;
            s_DamageEcsCount.Value = 0;
            s_Recording = true;
        }

        public static void EndStep()
        {
            try { Flush(); }
            finally { s_Recording = false; }
        }

        // The authored graph is not consulted for this complete data-only rule.
        // It joins the existing selection batch to preserve frame-end commit order.
        public static bool TryEnqueueCompiledAttributes(
            Unity.Entities.Entity triggerEntity, int ownerHandle, int triggerType)
        {
            if (!s_Recording || s_Flushing || ownerHandle == 0 ||
                (triggerType != (int)TriggerType.OnActive &&
                 triggerType != (int)TriggerType.PerTick &&
                 triggerType != (int)TriggerType.SkillBeforeShakeEnd) ||
                !CombatAttributeBridge.TryGetEntity(ownerHandle,
                    out Unity.Entities.Entity ownerEntity))
                return false;
            Unity.Entities.World world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;
            Unity.Entities.EntityManager manager = world.EntityManager;
            if (!manager.Exists(triggerEntity) ||
                !manager.HasComponent<CombatTriggerDefinition>(triggerEntity) ||
                !manager.HasBuffer<CombatConditionNode>(triggerEntity) ||
                !manager.HasBuffer<CombatTargetPickerNode>(triggerEntity) ||
                !manager.HasBuffer<CombatCommandNode>(triggerEntity))
                return false;
            CombatTriggerDefinition definition =
                manager.GetComponentData<CombatTriggerDefinition>(triggerEntity);
            if (!definition.Enabled || definition.Owner != ownerEntity ||
                definition.TriggerType != triggerType)
                return false;
            var conditions = manager.GetBuffer<CombatConditionNode>(triggerEntity);
            var pickers = manager.GetBuffer<CombatTargetPickerNode>(triggerEntity);
            var commands = manager.GetBuffer<CombatCommandNode>(triggerEntity);
            if (conditions.Length != 1 ||
                conditions[0].Kind != CombatConditionKind.Always ||
                conditions[0].Reverse || pickers.Length != 1 ||
                commands.Length == 0)
                return false;
            var input = new CombatSimpleTriggerInput();
            switch (pickers[0].Kind)
            {
                case CombatTargetPickerKind.SkillCaster:
                    if (!CombatSkillEntityBridge.TryGetCasterHandle(
                            definition.Skill, out int skillCasterHandle)) return false;
                    input.DirectUid = CombatAttributeBridge.GetHeroUid(skillCasterHandle);
                    break;
                case CombatTargetPickerKind.TriggerOwner:
                    input.DirectUid = CombatAttributeBridge.GetHeroUid(ownerHandle);
                    break;
                case CombatTargetPickerKind.SkillCasterCurTarget:
                    if (!CombatSkillEntityBridge.TryGetCasterHandle(
                            definition.Skill, out int currentCasterHandle)) return false;
                    input.DirectUid = CombatAttributeBridge.GetAttackTargetUid(
                        currentCasterHandle);
                    break;
                case CombatTargetPickerKind.Nearest:
                    input.Picker = CombatSimplePicker.Nearest;
                    input.SourceCamp = CombatAttributeBridge.GetCamp(ownerHandle);
                    input.TargetCamp = pickers[0].TargetCamp;
                    CombatAttributeBridge.GetPosition(ownerHandle,
                        out input.SourceX, out _, out input.SourceZ);
                    break;
                case CombatTargetPickerKind.OwnerDistance when
                    !pickers[0].UseWeaponLength:
                    input.Picker = CombatSimplePicker.OwnerDistance;
                    input.SourceCamp = CombatAttributeBridge.GetCamp(ownerHandle);
                    input.TargetCamp = pickers[0].TargetCamp;
                    input.Radius = pickers[0].LengthMm / 1000f;
                    CombatAttributeBridge.GetPosition(ownerHandle,
                        out input.SourceX, out _, out input.SourceZ);
                    break;
                default:
                    return false;
            }
            for (int i = 0; i < commands.Length; i++)
            {
                CombatCommandNode node = commands[i];
                if (node.Kind != CombatCommandKind.ChangeAttribute ||
                    node.EndIndex != i + 1)
                    return false;
            }
            var selections = CombatBoardBridge.GetSimpleTriggerInputs();
            int selectionIndex = selections.Length;
            selections.Add(input);
            SelectionConditionHandles.Add(0);
            for (int i = 0; i < commands.Length; i++)
            {
                CombatCommandNode node = commands[i];
                Events.Add(new Pending
                {
                    CompiledAttribute = true,
                    CompiledTrigger = triggerEntity,
                    CompiledOwnerHandle = ownerHandle,
                    IsAttribute = true,
                    AttributeType = node.AttributeType,
                    AttributeDelta = node.Delta,
                    SelectionIndex = selectionIndex
                });
            }
            s_Queued.Value++;
            s_CommandEvents.Value += commands.Length;
            if (input.Picker == CombatSimplePicker.OwnerDistance)
                s_AreaQueued.Value++;
            return true;
        }

        public static bool TryEnqueue(OneTrigger trigger, object argument)
        {
            if (!s_Recording || s_Flushing || trigger?.CurCondition == null ||
                trigger.CurCommandList == null ||
                trigger.CurCommandList.Count == 0 ||
                trigger.ParentTriggerList is not Skill skill ||
                skill.Caster == null || skill.Caster.CombatUnitHandle == 0 ||
                trigger.CurTargetPicker == null)
                return false;

            if (trigger.CurTriggerType != TriggerType.OnActive &&
                trigger.CurTriggerType != TriggerType.SkillBeforeShakeEnd &&
                trigger.CurTriggerType != TriggerType.PerTick)
                return false;

            if (!TryGetSimpleCondition(trigger.CurCondition,
                    out ConditionPercent percentCondition,
                    out ConditionRelateSkill relateSkillCondition,
                    out bool programCondition,
                    out bool reverseCondition,
                    out bool passArg)) return false;

            // Program conditions may advance timers or consume random rolls even
            // when the picker finds no target. These two pickers always reach
            // the batch; a missing current target is represented by UID 0.
            // Other pickers can fail during setup, so keep their original
            // condition-before-picker execution path.
            if (programCondition &&
                trigger.CurTargetPicker.CurTargetPickerType !=
                    TargetPickerType.SkillCaster &&
                trigger.CurTargetPicker.CurTargetPickerType !=
                    TargetPickerType.SkillCasterCurTarget)
                return false;

            EntityBase owner = trigger.ParentTriggerList.Owner;
            var input = new CombatSimpleTriggerInput();
            switch (trigger.CurTargetPicker.CurTargetPickerType)
            {
                case TargetPickerType.Arg when argument is EntityQizi argUnit:
                    input.DirectUid = argUnit.HeroUID;
                    break;
                case TargetPickerType.SkillCaster:
                    input.DirectUid = skill.Caster.HeroUID;
                    break;
                case TargetPickerType.SkillCasterCurTarget:
                    input.DirectUid = skill.Caster.CurAttackTarget?.HeroUID ?? 0;
                    break;
                case TargetPickerType.TriggerOwner when owner is EntityQizi unit:
                    input.DirectUid = unit.HeroUID;
                    break;
                case TargetPickerType.Nearest when owner != null &&
                    trigger.CurTargetPicker is TargetPickerNearest nearest:
                    input.Picker = CombatSimplePicker.Nearest;
                    input.SourceCamp = (int)owner.BelongCamp;
                    input.TargetCamp = (int)nearest.TargetCamp;
                    if (owner is EntityQizi sourceUnit &&
                        sourceUnit.CombatUnitHandle != 0)
                    {
                        CombatAttributeBridge.GetPosition(sourceUnit.CombatUnitHandle,
                            out input.SourceX, out _, out input.SourceZ);
                        input.SourceCamp = CombatAttributeBridge.GetCamp(
                            sourceUnit.CombatUnitHandle);
                    }
                    else
                    {
                        input.SourceX = owner.LogicPosition.x;
                        input.SourceZ = owner.LogicPosition.z;
                    }
                    break;
                case TargetPickerType.OwnerDistance when owner != null &&
                    trigger.CurTargetPicker is TargetPickerOwnerDistance area &&
                    area.WeaponLength != null:
                    input.Picker = CombatSimplePicker.OwnerDistance;
                    input.SourceCamp = (int)owner.BelongCamp;
                    input.TargetCamp = (int)area.TargetCamp;
                    int lengthMm = area.WeaponLength.Value;
                    if (area.LengthUseWeapon && skill.FromItemID != 0)
                        lengthMm = TargetPickerBase.LookupWeaponLength(
                            skill.FromItemID, lengthMm);
                    input.Radius = lengthMm / 1000f;
                    input.SourceX = owner.LogicPosition.x;
                    input.SourceZ = owner.LogicPosition.z;
                    break;
                case TargetPickerType.OwnerDirection when owner is EntityQizi
                    directionSource && directionSource.CombatUnitHandle != 0 &&
                    trigger.CurTargetPicker is TargetPickerOwnerDirection direction &&
                    direction.WeaponLength != null && direction.ValidAngle != null:
                    int sourceHandle = directionSource.CombatUnitHandle;
                    int attackUid = CombatAttributeBridge.GetAttackTargetUid(
                        sourceHandle);
                    if (attackUid == 0 ||
                        !CombatTargetProjection.HasUnitByUid(attackUid) ||
                        !CombatAttributeBridge.TryGetHandleByUid(attackUid,
                            out int attackHandle)) return false;
                    input.Picker = CombatSimplePicker.OwnerDirection;
                    input.SourceCamp = CombatAttributeBridge.GetCamp(sourceHandle);
                    int directionLengthMm = direction.WeaponLength.Value;
                    if (direction.LengthUseWeapon && skill.FromItemID != 0)
                        directionLengthMm = TargetPickerBase.LookupWeaponLength(
                            skill.FromItemID, directionLengthMm);
                    input.Radius = directionLengthMm / 1000f;
                    input.AngleDegrees = direction.ValidAngle.Value;
                    CombatAttributeBridge.GetPosition(sourceHandle,
                        out input.SourceX, out _, out input.SourceZ);
                    CombatAttributeBridge.GetPosition(attackHandle,
                        out float attackX, out _, out float attackZ);
                    input.FacingX = attackX - input.SourceX;
                    input.FacingZ = attackZ - input.SourceZ;
                    PrepareSectorGeometry(ref input);
                    break;
                default:
                    return false;
            }

            if (!TryCountCommands(trigger.CurCommandList, owner,
                    input.Picker, skill, 0, out int commandCount)) return false;

            if (percentCondition != null)
            {
                input.CheckPercent = true;
                input.PercentRoll = CombatConditionCompiler.RollPercent();
                input.PercentThreshold = percentCondition.PercentTarget.Value;
                input.ReverseCondition = reverseCondition;
            }
            if (relateSkillCondition != null)
            {
                input.CheckRelateSkill = true;
                input.RelateSkillReverse = reverseCondition;
                input.RelateSkillPassArg = passArg;
                Skill sourceSkill = trigger.ParentTriggerList.ParentSkill;
                input.HasSourceSkill = sourceSkill != null;
                input.SourceSkillType = sourceSkill != null
                    ? (int)sourceSkill.CurSkillType : 0;
                input.HasArgSkill = argument is Skill;
                input.ArgSkillType = argument is Skill argSkill
                    ? (int)argSkill.CurSkillType : 0;
                input.RelateSkillSource =
                    (int)relateSkillCondition.SkillFrom;
                input.RelateSkillCheckType =
                    (int)relateSkillCondition.CheckType;
                input.RelateSkillExpectedType =
                    relateSkillCondition.ParamInt1.Value;
            }

            int conditionHandle = 0;
            if (programCondition)
            {
                if (commandCount != 0 &&
                    trigger.TryEnterTimedBatchInvocation(out conditionHandle))
                {
                    input.CheckTimed = true;
                    input.TimedDeltaMs = GameEntry.CurrentLogicDeltaTime * 1000f;
                }
                else if (commandCount != 0 &&
                         trigger.TryEnterPureSkillGroupInvocation(
                             out conditionHandle,
                             out Unity.Entities.Entity conditionEntity))
                {
                    input.CheckPureSkillGroup = true;
                    input.PureSkillGroupEntity = conditionEntity;
                    Skill sourceSkill = trigger.ParentTriggerList.ParentSkill;
                    input.HasSourceSkill = sourceSkill != null;
                    input.SourceSkillType = sourceSkill != null
                        ? (int)sourceSkill.CurSkillType : 0;
                }
                else if (commandCount != 0 &&
                         trigger.TryEnterTimedSkillGroupInvocation(
                             out conditionHandle,
                             out Unity.Entities.Entity timedSkillEntity))
                {
                    input.CheckTimedSkillGroup = true;
                    input.TimedSkillGroupEntity = timedSkillEntity;
                    input.TimedDeltaMs = GameEntry.CurrentLogicDeltaTime * 1000f;
                    Skill sourceSkill = trigger.ParentTriggerList.ParentSkill;
                    input.HasSourceSkill = sourceSkill != null;
                    input.SourceSkillType = sourceSkill != null
                        ? (int)sourceSkill.CurSkillType : 0;
                }
                else
                {
                    using (DataConditionMarker.Auto())
                        if (!trigger.TryEnterDataBatchInvocation(
                                out conditionHandle))
                            return true;
                }
            }

            if (commandCount == 0)
            {
                if (conditionHandle != 0)
                    CombatConditionProgramBridge.ExitInvocation(conditionHandle);
                return true;
            }
            var selections = CombatBoardBridge.GetSimpleTriggerInputs();
            int selectionIndex = selections.Length;
            int eventStart = Events.Count;
            try
            {
                selections.Add(input);
                SelectionConditionHandles.Add(conditionHandle);
                RecordCommands(trigger.CurCommandList, trigger, skill,
                    selectionIndex, commandCount, ref input);
            }
            catch
            {
                Events.RemoveRange(eventStart, Events.Count - eventStart);
                selections.RemoveAt(selectionIndex);
                SelectionConditionHandles.RemoveAt(selectionIndex);
                if (conditionHandle != 0)
                    CombatConditionProgramBridge.ExitInvocation(conditionHandle);
                throw;
            }
            s_Queued.Value++;
            if (input.CheckTimed)
            {
                s_TimedQueued.Value++;
                s_PendingTimed++;
            }
            if (input.CheckPureSkillGroup)
            {
                s_PureGroupQueued.Value++;
                s_PendingPureGroups++;
            }
            if (input.CheckTimedSkillGroup)
            {
                s_TimedSkillGroupQueued.Value++;
                s_PendingTimedSkillGroups++;
            }
            s_CommandEvents.Value += commandCount;
            if (input.Picker == CombatSimplePicker.OwnerDistance ||
                input.Picker == CombatSimplePicker.OwnerDirection)
                s_AreaQueued.Value++;
            return true;
        }

        private static void RecordCommands(IReadOnlyList<CommandBase> commands,
            OneTrigger trigger, Skill skill,
            int selectionIndex, int commandCount,
            ref CombatSimpleTriggerInput input)
        {
            foreach (CommandBase command in commands)
            {
                if (command is CommandRepeatExecuteCmd repeat)
                {
                    int times = RepeatCount(repeat, skill);
                    for (int iteration = 0; iteration < times; iteration++)
                        RecordCommands(repeat.CurCommandList, trigger, skill,
                            selectionIndex, commandCount, ref input);
                    continue;
                }
                var pending = new Pending
                {
                    Trigger = trigger,
                    SourceList = trigger.ParentTriggerList,
                    SourceSkill = skill,
                    Caster = skill.Caster,
                    SelectionIndex = selectionIndex
                };
                if (command is CommandCauseDamage damage)
                {
                    pending.IsDamage = true;
                    pending.RecomputeDamage = commandCount > 1;
                    pending.DamageMode = (int)damage.CurDamageComputeType;
                    pending.DamageFixedValue = damage.ParamInt1.Value;
                    pending.DamageAttributeType = damage.ParamInt2.Value;
                    pending.DamagePercent = damage.ParamInt3.Value;
                    pending.TriggerDamageRelate = damage.TriggerDamageRelate;
                    if (!pending.RecomputeDamage &&
                        (pending.DamageMode == 0 || pending.DamagePercent > 0))
                    {
                        int attributeType = pending.DamageMode == 0 ? 8 :
                            pending.DamageAttributeType;
                        input.DamageBaseValue = CombatAttributeBridge.GetValue(
                            skill.Caster.CombatUnitHandle, attributeType);
                    }
                    if (!pending.RecomputeDamage)
                    {
                        input.DamageMode = pending.DamageMode;
                        input.DamageFixedValue = pending.DamageFixedValue;
                        input.DamagePercent = pending.DamagePercent;
                        var selectionBuffer =
                            CombatBoardBridge.GetSimpleTriggerInputs();
                        selectionBuffer[selectionIndex] = input;
                    }
                }
                else if (command is CommandCreateBullet bullet)
                {
                    pending.BulletCommand = bullet;
                    var offset = bullet.PosOffset.Value;
                    pending.BulletNode = new CombatCommandNode
                    {
                        Kind = CombatCommandKind.CreateProjectile,
                        ProjectileId = bullet.CurBulletID.Value,
                        ProjectileParameter = bullet.ParamInt1.Value,
                        ProjectileOffsetX = offset.x,
                        ProjectileOffsetY = offset.y,
                        ProjectileOffsetZ = offset.z
                    };
                }
                else if (command is CommandCreatePosPoint point)
                {
                    pending.PointCommand = point;
                    var offset = point.PosOffset.Value;
                    var maximum = point.PosOffsetMax.Value;
                    pending.PointNode = new CombatCommandNode
                    {
                        Kind = CombatCommandKind.CreatePositionPoint,
                        PositionPointMode = (int)point.CurPositionType,
                        PositionPointDurationMs = point.DurationMS.Value,
                        PositionPointLookAtTarget = point.LookAtTarget.Value == 1,
                        PositionPointOffsetX = offset.x,
                        PositionPointOffsetY = offset.y,
                        PositionPointOffsetZ = offset.z,
                        PositionPointOffsetMaxX = maximum.x,
                        PositionPointOffsetMaxY = maximum.y,
                        PositionPointOffsetMaxZ = maximum.z
                    };
                }
                else if (command is CommandCreateBuff createBuff)
                {
                    pending.IsBuff = true;
                    if (!createBuff.UseTemplateBuff)
                        pending.TemporaryBuffCommand = createBuff;
                    pending.CreateTemporaryBuff = !createBuff.UseTemplateBuff;
                    pending.BuffId = createBuff.UseTemplateBuff
                        ? createBuff.BuffID.Value : 0;
                }
                else if (command is CommandRemoveBuff removeBuff)
                {
                    pending.IsRemoveBuff = true;
                    pending.RemoveBuffId = removeBuff.BuffID.Value;
                }
                else if (command is CommandCreateSfx sfx)
                {
                    pending.IsSfx = true;
                    pending.CreateSfx = sfx.CreateOrRemove;
                    pending.SfxId = sfx.SfxID.Value;
                }
                else if (command is CommandPlayAudio audio)
                {
                    pending.IsAudio = true;
                    pending.CreateAudio = audio.CreateOrRemove;
                    pending.AudioId = audio.AudioID.Value;
                }
                else if (command is CommandShowWeapon weapon)
                {
                    pending.IsWeapon = true;
                    pending.ShowWeapon = weapon.ShowOrHidden;
                    pending.UseItemWeapon = weapon.ShowItemWeapon;
                    pending.WeaponHandleType = weapon.ShowHandleType;
                    pending.WeaponItemId = weapon.ShowWeaponItemID.Value;
                }
                else if (command is CommandPlayAnim animation)
                {
                    pending.IsAnimation = true;
                    pending.AnimationId = animation.AnimAssetID.Value;
                }
                else if (command is CommandChangeAttribute attribute)
                {
                    pending.IsAttribute = true;
                    pending.AttributeType = attribute.TargetAttrType.Value;
                    pending.AttributeDelta = attribute.AddOrReduce
                        ? attribute.ParamInt1.Value : -attribute.ParamInt1.Value;
                }
                Events.Add(pending);
            }
        }

        private static bool TryCountCommands(IReadOnlyList<CommandBase> commands,
            EntityBase owner, CombatSimplePicker picker, Skill skill, int depth,
            out int count)
        {
            count = 0;
            if (commands == null || depth > 8) return false;
            foreach (CommandBase command in commands)
            {
                if (command is CommandRepeatExecuteCmd repeat)
                {
                    if (repeat.ParamInt1 == null ||
                        repeat.CurCommandList == null ||
                        !TryCountCommands(repeat.CurCommandList, owner, picker,
                            skill, depth + 1, out int childCount)) return false;
                    int times = RepeatCount(repeat, skill);
                    if ((long)count + (long)Math.Max(0, times) * childCount > 512)
                        return false;
                    count += Math.Max(0, times) * childCount;
                }
                else
                {
                    if (!CanBatch(command, owner) ||
                        (command is CommandPlayAnim &&
                         (picker == CombatSimplePicker.OwnerDistance ||
                          picker == CombatSimplePicker.OwnerDirection)))
                        return false;
                    if (++count > 512) return false;
                }
            }
            return true;
        }

        private static bool TryGetSimpleCondition(ConditionBase condition,
            out ConditionPercent percent,
            out ConditionRelateSkill relateSkill,
            out bool program, out bool reverse, out bool passArg)
        {
            percent = null;
            relateSkill = null;
            program = false;
            reverse = false;
            passArg = true;
            for (int depth = 0; depth <= 8; depth++)
            {
                if (condition == null) return false;
                reverse ^= condition.ReverseResult;
                if (condition is ConditionGroup group)
                {
                    if (group.ConditionList == null || group.ConditionList.Count == 0 ||
                        (group.CurLogicOperator != LogicOperator.And &&
                         group.CurLogicOperator != LogicOperator.Or))
                        return false;
                    if (group.ConditionList.Count != 1)
                    {
                        if (!IsDataConditionTree(group, depth) &&
                            !IsPureSkillConditionTree(group, depth) &&
                            !IsTimedSkillConditionTree(group, depth))
                            return false;
                        program = true;
                        return true;
                    }
                    passArg = false;
                    condition = group.ConditionList[0];
                    continue;
                }
                if (condition.CurConditionType == ConditionType.NoCondition)
                    return !reverse;
                if (condition is ConditionTimed timedRule &&
                    timedRule.TimeIntervalMs != null &&
                    timedRule.PassNumber != null &&
                    timedRule.TimeIntervalMs.Value > 0)
                {
                    program = true;
                    return true;
                }
                if (condition is ConditionRelateSkill related &&
                    related.ParamInt1 != null &&
                    related.CheckType == ConditionRelateSkillCheckType.SkillType)
                {
                    relateSkill = related;
                    return true;
                }
                percent = condition as ConditionPercent;
                return percent?.PercentTarget != null;
            }
            return false;
        }

        private static bool IsDataConditionTree(ConditionBase condition, int depth)
        {
            if (condition == null || depth > 8) return false;
            if (condition is ConditionGroup group)
            {
                if (group.ConditionList == null || group.ConditionList.Count == 0 ||
                    (group.CurLogicOperator != LogicOperator.And &&
                     group.CurLogicOperator != LogicOperator.Or)) return false;
                foreach (ConditionBase child in group.ConditionList)
                    if (!IsDataConditionTree(child, depth + 1)) return false;
                return true;
            }
            if (condition.CurConditionType == ConditionType.NoCondition)
                return true;
            if (condition is ConditionPercent percent)
                return percent.PercentTarget != null;
            if (condition is ConditionTimed timed)
                return timed.TimeIntervalMs != null &&
                       timed.PassNumber != null &&
                       timed.TimeIntervalMs.Value > 0;
            return false;
        }

        private static bool IsPureSkillConditionTree(ConditionBase condition,
            int depth)
        {
            if (condition == null || depth > 8) return false;
            if (condition is ConditionGroup group)
            {
                if (group.ConditionList == null ||
                    group.ConditionList.Count == 0 ||
                    (group.CurLogicOperator != LogicOperator.And &&
                     group.CurLogicOperator != LogicOperator.Or)) return false;
                foreach (ConditionBase child in group.ConditionList)
                    if (!IsPureSkillConditionTree(child, depth + 1))
                        return false;
                return true;
            }
            if (condition.CurConditionType == ConditionType.NoCondition)
                return true;
            return condition is ConditionRelateSkill related &&
                   related.ParamInt1 != null &&
                   related.CheckType == ConditionRelateSkillCheckType.SkillType;
        }

        private static bool IsTimedSkillConditionTree(ConditionBase condition,
            int depth)
        {
            if (condition == null || depth > 8) return false;
            if (condition is ConditionGroup group)
            {
                if (group.ConditionList == null ||
                    group.ConditionList.Count == 0 ||
                    (group.CurLogicOperator != LogicOperator.And &&
                     group.CurLogicOperator != LogicOperator.Or)) return false;
                foreach (ConditionBase child in group.ConditionList)
                    if (!IsTimedSkillConditionTree(child, depth + 1))
                        return false;
                return true;
            }
            if (condition.CurConditionType == ConditionType.NoCondition)
                return true;
            if (condition is ConditionTimed timed)
                return timed.TimeIntervalMs != null &&
                       timed.PassNumber != null &&
                       timed.TimeIntervalMs.Value > 0;
            return condition is ConditionRelateSkill related &&
                   related.ParamInt1 != null &&
                   related.CheckType == ConditionRelateSkillCheckType.SkillType;
        }

        private static int RepeatCount(CommandRepeatExecuteCmd repeat,
            Skill skill)
        {
            return repeat.CurType switch
            {
                NumberCheckType.FixedNumber => repeat.ParamInt1.Value,
                NumberCheckType.ParentSkillContainSubItemNumber =>
                    CommandRepeatExecuteCmd.CountSubItem(skill.FromItemID,
                        repeat.ParamInt1.Value),
                _ => 0
            };
        }

        private static void PrepareSectorGeometry(
            ref CombatSimpleTriggerInput input)
        {
            float facingLength = (float)Math.Sqrt(
                input.FacingX * input.FacingX +
                input.FacingZ * input.FacingZ);
            input.MidX = facingLength == 0f ? 0f :
                input.FacingX / facingLength * input.Radius;
            input.MidZ = facingLength == 0f ? 0f :
                input.FacingZ / facingLength * input.Radius;
            input.MidLength = (float)Math.Sqrt(
                input.MidX * input.MidX + input.MidZ * input.MidZ);
            float halfRadians = input.AngleDegrees / 2f * (float)Math.PI / 180f;
            float cosine = (float)Math.Cos(halfRadians);
            float sine = (float)Math.Sin(halfRadians);
            input.Edge1X = input.MidX * cosine - input.MidZ * sine;
            input.Edge1Z = input.MidX * sine + input.MidZ * cosine;
            input.Edge2X = input.MidX * cosine + input.MidZ * sine;
            input.Edge2Z = -input.MidX * sine + input.MidZ * cosine;
            input.Edge1Length = (float)Math.Sqrt(
                input.Edge1X * input.Edge1X +
                input.Edge1Z * input.Edge1Z);
            input.Edge2Length = (float)Math.Sqrt(
                input.Edge2X * input.Edge2X +
                input.Edge2Z * input.Edge2Z);
        }

        private static bool CanBatch(CommandBase command, EntityBase owner)
        {
            if (command is CommandCauseDamage damage &&
                damage.ParamInt1 != null && damage.ParamInt2 != null &&
                damage.ParamInt3 != null &&
                (damage.CurDamageComputeType == DamageComputeType.NormalDamage ||
                 damage.CurDamageComputeType ==
                     DamageComputeType.FixNumAddAttrPercent))
            {
                return true;
            }
            if (owner is EntityQizi && command is CommandCreateBullet bullet &&
                     bullet.CurBulletID != null && bullet.ParamInt1 != null &&
                     bullet.PosOffset != null && bullet.BulletTrigger != null)
            {
                return true;
            }
            if (command is CommandChangeAttribute attribute &&
                     attribute.TargetAttrType != null && attribute.ParamInt1 != null)
            {
                return true;
            }
            if (command is CommandCreatePosPoint point &&
                point.DurationMS != null && point.LookAtTarget != null &&
                point.PosOffset != null && point.PosOffsetMax != null &&
                point.PosPointTrigger != null)
            {
                return true;
            }
            if (command is CommandCreateBuff createBuff)
            {
                if (!createBuff.UseTemplateBuff)
                    return createBuff.TemporaryBuff != null;
                return createBuff.TemporaryBuff == null &&
                    createBuff.BuffID != null && createBuff.BuffID.Value != 0 &&
                    createBuff.BuffList != null &&
                    string.IsNullOrEmpty(createBuff.BuffList.Value);
            }
            if (command is CommandRemoveBuff removeBuff &&
                removeBuff.BuffID != null)
                return true;
            if (command is CommandCreateSfx sfx && sfx.SfxID != null)
                return true;
            if (command is CommandPlayAudio audio && audio.AudioID != null)
                return true;
            if (command is CommandShowWeapon weapon &&
                weapon.ShowWeaponItemID != null)
                return true;
            if (command is CommandPlayAnim animation &&
                animation.AnimAssetID != null)
                return true;
            return false;
        }

        public static void Flush()
        {
            if (s_Flushing || Events.Count == 0) return;
            if (s_PendingPureGroups != 0 || s_PendingTimedSkillGroups != 0)
                CombatTriggerSimpleBatch.EnsurePureGroupLookup();
            s_Flushing = true;
            int selectionCount = CombatBoardBridge.GetSimpleTriggerInputs().Length;
            int eventCount = Events.Count;
            NativeArray<CombatSimpleTriggerInput> inputs = default;
            NativeArray<CombatSimpleTriggerResult> results = default;
            NativeArray<int> areaRows = default;
            int unitCount = 0;
            try
            {
                var resultBuffer = CombatBoardBridge.GetSimpleTriggerResults();
                resultBuffer.ResizeUninitialized(selectionCount);
                inputs = CombatBoardBridge.GetSimpleTriggerInputs().AsNativeArray();
                results = resultBuffer.AsNativeArray();
                using (PrepareMarker.Auto())
                {
                    if (s_PendingTimed != 0)
                        using (TimedBatchMarker.Auto())
                            CombatTriggerSimpleBatch.PrepareTimed(inputs,
                                SelectionConditionHandles);
                    if (s_PendingPureGroups != 0)
                        using (PureGroupMarker.Auto())
                            CombatTriggerSimpleBatch.PreparePureSkillGroups(inputs);
                    if (s_PendingTimedSkillGroups != 0)
                        using (TimedSkillGroupMarker.Auto())
                            CombatTriggerSimpleBatch.PrepareTimedSkillGroups(inputs,
                                SelectionConditionHandles);
                    CombatTriggerSimpleBatch.Prepare(inputs, results,
                        out areaRows, out unitCount);
                }
                // Commit may create or recycle Entities. Never retain a buffer
                // NativeArray alias across those structural changes.
                inputs = default;
                results = default;
                using (CommitMarker.Auto())
                {
                    int currentSelection = -1;
                    CombatSimpleTriggerResult currentResult = default;
                    for (int i = 0; i < eventCount; i++)
                    {
                        Pending pending = Events[i];
                        int selectionIndex = pending.SelectionIndex;
                        if (selectionIndex != currentSelection)
                        {
                            currentSelection = selectionIndex;
                            CombatBoardBridge.ReadSimpleTriggerSelection(
                                selectionIndex, out CombatSimpleTriggerInput input,
                                out currentResult);
                            CollectTargets(input, currentResult, areaRows,
                                selectionIndex, unitCount);
                        }
                        CombatSimpleTriggerResult result = currentResult;
                        if (pending.IsAttribute)
                        {
                            FlushDamages();
                            foreach (CombatSimpleTargetCandidate target in SelectedTargets)
                                QueueAttribute(pending, target.HeroUid);
                        }
                        else if (pending.IsDamage)
                        {
                            FlushAttributes();
                            foreach (CombatSimpleTargetCandidate target in SelectedTargets)
                            {
                                if (TryQueueDamage(pending, target.HeroUid, result))
                                    continue;
                                FlushDamages();
                                Commit(pending, target.HeroUid, result);
                            }
                        }
                        else
                        {
                            FlushAttributes();
                            FlushDamages();
                            foreach (CombatSimpleTargetCandidate target in SelectedTargets)
                                Commit(pending, target.HeroUid, result);
                        }
                    }
                    FlushAttributes();
                    FlushDamages();
                }
            }
            finally
            {
                for (int i = 0; i < selectionCount; i++)
                {
                    int conditionHandle = SelectionConditionHandles[i];
                    if (conditionHandle != 0)
                        CombatConditionProgramBridge.ExitInvocation(conditionHandle);
                }
                SelectionConditionHandles.RemoveRange(0, selectionCount);
                Events.RemoveRange(0, eventCount);
                CombatBoardBridge.ClearSimpleTriggerBatch(selectionCount);
                SelectedTargets.Clear();
                AttributeIntents.Clear();
                AttributeTargetUids.Clear();
                DamageIntents.Clear();
                DamageNumbers.Clear();
                DamageTargetUids.Clear();
                s_PendingTimed = 0;
                s_PendingPureGroups = 0;
                s_PendingTimedSkillGroups = 0;
                if (areaRows.IsCreated) areaRows.Dispose();
                s_Flushing = false;
            }
        }

        private static void CollectTargets(CombatSimpleTriggerInput input,
            CombatSimpleTriggerResult result,
            NativeArray<int> areaRows,
            int selectionIndex, int unitCount)
        {
            SelectedTargets.Clear();
            if (input.Picker != CombatSimplePicker.OwnerDistance &&
                input.Picker != CombatSimplePicker.OwnerDirection)
            {
                if (result.TargetUid != 0)
                    SelectedTargets.Add(new CombatSimpleTargetCandidate
                    {
                        HeroUid = result.TargetUid
                    });
                return;
            }
            if (!areaRows.IsCreated) return;
            int row = areaRows[selectionIndex];
            if (row < 0) return;
            int start = row * unitCount;
            var areaCandidates = CombatBoardBridge.GetSimpleTargetCandidates();
            for (int i = 0; i < unitCount; i++)
            {
                CombatSimpleTargetCandidate candidate = areaCandidates[start + i];
                if (candidate.HeroUid != 0) SelectedTargets.Add(candidate);
            }
            SelectedTargets.Sort(input.Picker == CombatSimplePicker.OwnerDirection
                ? UidOrder : TargetOrder);
        }

        private static int CompareTargets(CombatSimpleTargetCandidate left,
            CombatSimpleTargetCandidate right)
        {
            int order = left.Camp.CompareTo(right.Camp);
            return order != 0 ? order : left.HeroUid.CompareTo(right.HeroUid);
        }

        private static int CompareTargetUids(CombatSimpleTargetCandidate left,
            CombatSimpleTargetCandidate right) =>
            left.HeroUid.CompareTo(right.HeroUid);

        private static void QueueAttribute(Pending pending, int targetUid)
        {
            if (pending.CompiledAttribute)
            {
                Unity.Entities.World world =
                    Unity.Entities.World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated ||
                    !world.EntityManager.Exists(pending.CompiledTrigger)) return;
                if (!CombatAttributeBridge.TryGetEntity(
                        pending.CompiledOwnerHandle, out _) ||
                    !CombatAttributeBridge.TryGetHandleByUid(targetUid,
                        out int targetHandle) ||
                    !CombatAttributeBridge.IsAlive(targetHandle)) return;
                AttributeIntents.Add(new CombatAttributeIntent
                {
                    HeroUid = targetUid,
                    UnitHandle = targetHandle,
                    AttributeType = pending.AttributeType,
                    Delta = pending.AttributeDelta
                });
                return;
            }
            if (pending.Trigger.ParentTriggerList != pending.SourceList ||
                pending.SourceSkill.Caster != pending.Caster)
                return;
            EntityQizi target = HotGameEntry.HeroManager.GetEntityByUID(targetUid);
            if (target == null || !target.IsValid || target.CombatUnitHandle == 0)
                return;
            AttributeIntents.Add(new CombatAttributeIntent
            {
                HeroUid = targetUid,
                UnitHandle = target.CombatUnitHandle,
                AttributeType = pending.AttributeType,
                Delta = pending.AttributeDelta
            });
        }

        private static void FlushAttributes()
        {
            if (AttributeIntents.Count == 0) return;
            using (AttributeMarker.Auto())
            {
                if (AttributeIntents.Count >= 32)
                    foreach (CombatAttributeIntent intent in AttributeIntents)
                        AttributeTargetUids.Add(intent.HeroUid);
                if (AttributeTargetUids.Count >= 4)
                    CombatAttributeBridge.ApplyAttributeIntents(AttributeIntents);
                else
                    foreach (CombatAttributeIntent intent in AttributeIntents)
                        CombatAttributeBridge.AddNum(intent.UnitHandle,
                            intent.AttributeType, intent.Delta);
            }
            AttributeIntents.Clear();
            AttributeTargetUids.Clear();
        }

        private static bool TryQueueDamage(Pending pending, int targetUid,
            CombatSimpleTriggerResult result)
        {
            if (pending.RecomputeDamage ||
                pending.Trigger.ParentTriggerList != pending.SourceList ||
                pending.SourceSkill.Caster != pending.Caster ||
                DamageTargetUids.Contains(targetUid)) return false;
            EntityQizi target = HotGameEntry.HeroManager.GetEntityByUID(targetUid);
            if (target == null || !target.IsValid || target.CombatUnitHandle == 0)
                return false;
            int targetHandle = target.CombatUnitHandle;
            int casterHandle = pending.Caster.CombatUnitHandle;
            if (!CombatAttributeBridge.CanApplyIndependentDamage(casterHandle,
                    targetHandle, result.Damage,
                    pending.TriggerDamageRelate)) return false;
            int damage = (int)result.Damage;
            DamageTargetUids.Add(targetUid);
            DamageIntents.Add(new CombatAttributeIntent
            {
                HeroUid = targetUid,
                UnitHandle = targetHandle,
                AttributeType = 1,
                Delta = -damage
            });
            UnityEngine.Vector3 position = target.LogicPosition;
            DamageNumbers.Add(new CombatDamageNumberEvent
            {
                Value = damage,
                Position = new Unity.Mathematics.float3(position.x,
                    position.y + 0.5f, position.z)
            });
            s_DamageEcsCount.Value++;
            return true;
        }

        private static void FlushDamages()
        {
            if (DamageIntents.Count == 0) return;
            using (DamageMarker.Auto())
            {
                if (DamageIntents.Count >= 32 && DamageTargetUids.Count >= 4)
                    CombatAttributeBridge.ApplyAttributeIntents(DamageIntents);
                else
                    foreach (CombatAttributeIntent intent in DamageIntents)
                        CombatAttributeBridge.AddNum(intent.UnitHandle,
                            intent.AttributeType, intent.Delta);
                foreach (CombatDamageNumberEvent number in DamageNumbers)
                    CombatBoardBridge.QueueDamageNumber(number.Value,
                        number.Position.x, number.Position.y, number.Position.z);
            }
            DamageIntents.Clear();
            DamageNumbers.Clear();
            DamageTargetUids.Clear();
        }

        private static void Commit(Pending pending, int targetUid,
            CombatSimpleTriggerResult result)
        {
            if (pending.Trigger.ParentTriggerList != pending.SourceList ||
                pending.SourceSkill.Caster != pending.Caster)
                return;
            EntityQizi target = HotGameEntry.HeroManager.GetEntityByUID(
                targetUid);
            if (target == null || !target.IsValid) return;
            if (pending.IsDamage)
            {
                if (target.CombatUnitHandle == 0 ||
                    !CombatAttributeBridge.IsAlive(target.CombatUnitHandle))
                    return;
                int casterHandle = pending.Caster.CombatUnitHandle;
                float damage = pending.RecomputeDamage
                    ? casterHandle == 0 ? 0 : CombatAttributeBridge.ComputeDamage(
                        casterHandle, pending.DamageMode,
                        pending.DamageFixedValue, pending.DamageAttributeType,
                        pending.DamagePercent)
                    : result.Damage;
                UnityEngine.Vector3 position = target.LogicPosition;
                if (CombatAttributeBridge.TryApplyIndependentDamage(casterHandle,
                        target.CombatUnitHandle, damage,
                        pending.TriggerDamageRelate,
                        position.x, position.y, position.z))
                    s_DamageEcsCount.Value++;
                else
                    CommandCauseDamage.ApplyDamage(pending.SourceSkill, target,
                        pending.Caster, damage,
                        pending.TriggerDamageRelate);
            }
            else if (pending.BulletCommand != null)
            {
                pending.BulletCommand.ExecuteTarget(pending.Trigger, target,
                    pending.BulletNode);
            }
            else if (pending.PointCommand != null)
            {
                pending.PointCommand.ExecuteTarget(pending.Trigger, target,
                    pending.PointNode);
            }
            else if (pending.IsBuff)
            {
                if (pending.CreateTemporaryBuff)
                    pending.TemporaryBuffCommand.ExecuteTarget(pending.Trigger,
                        target, pending.BuffId, true);
                else
                    CommandCreateBuff.ExecuteTarget(pending.SourceSkill, target,
                        null, pending.BuffId, false);
            }
            else if (pending.IsRemoveBuff)
            {
                if (pending.RemoveBuffId != 0)
                    target.RemoveBuff(pending.RemoveBuffId);
            }
            else if (pending.IsSfx)
            {
                if (pending.CreateSfx) target.AddSfx(pending.SfxId);
                else target.RemoveSfx(pending.SfxId);
            }
            else if (pending.IsAudio)
            {
                if (pending.CreateAudio)
                    pending.SourceList.paramInt =
                        HotGameEntry.Sound.PlaySfxSoundThisFrame(
                            pending.AudioId, target.LogicHitPosition);
                else
                    HotGameEntry.Sound.ReducePlayingSfxCount(
                        pending.SourceList.paramInt);
            }
            else if (pending.IsWeapon)
            {
                Skill skill = pending.SourceSkill;
                if (skill != null)
                {
                    if (pending.ShowWeapon)
                    {
                        int itemId = pending.UseItemWeapon
                            ? skill.FromItemID : pending.WeaponItemId;
                        if (itemId != 0)
                            target.AddOneWeapon(itemId,
                                pending.WeaponHandleType, skill);
                    }
                    else target.RemoveOneSkillAllWeapon(skill);
                }
            }
            else if (pending.IsAnimation)
            {
                Skill skill = pending.SourceSkill;
                float speed = 1f;
                if (skill != null && skill.CurCastCDMs <
                    skill.DefaultAnimationDurationMs)
                    speed = skill.DefaultAnimationDurationMs /
                        (float)skill.CurCastCDMs;
                target.AddAnimCommand(pending.AnimationId, speed);
            }
        }
    }
}
