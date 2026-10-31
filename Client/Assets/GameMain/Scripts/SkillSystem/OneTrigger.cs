using System;
using System.Collections.Generic;
using System.IO;
using Entity;
using GameFramework;
using Unity.Profiling;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    public class OneTrigger:IReference
    {
        private static readonly ProfilerMarker TriggerMarker =
            new ProfilerMarker("Combat.Trigger");
        private static readonly ProfilerMarker ConditionMarker =
            new ProfilerMarker("Combat.Trigger.Condition");
        private static readonly ProfilerMarker PickerMarker =
            new ProfilerMarker("Combat.Trigger.Picker");
        private static readonly ProfilerMarker DirectUnitPickerMarker =
            new ProfilerMarker("Combat.Trigger.Picker.DirectUnit");
        private static readonly ProfilerMarker SpatialPickerMarker =
            new ProfilerMarker("Combat.Trigger.Picker.DirectSpatial");
        private static readonly ProfilerMarker CommandsMarker =
            new ProfilerMarker("Combat.Trigger.Commands");
        private static readonly ProfilerMarker CommandProgramMarker =
            new ProfilerMarker("Combat.Trigger.CommandProgram");
        private static readonly ProfilerMarker CommandCompileMarker =
            new ProfilerMarker("Combat.Trigger.CommandCompile");
        private static readonly ProfilerMarker PickerCompileMarker =
            new ProfilerMarker("Combat.Trigger.PickerCompile");
        private static readonly Func<int> RollPercent =
            CombatConditionCompiler.RollPercent;
        private static readonly Func<int, int, bool> ItemContainsSubItem =
            ConditionRelateItem.CheckItemContainSubItemID;
        private static readonly Func<int, bool> HasManagedUnit =
            CombatTargetProjection.HasUnitByUid;
        private static readonly Func<int, int, int> ResolveWeaponLength =
            TargetPickerBase.LookupWeaponLength;

        public TriggerList ParentTriggerList;
        public TriggerType CurTriggerType;
        public ConditionBase CurCondition;
        public TargetPickerBase CurTargetPicker;
        public List<CommandBase> CurCommandList;
        private int m_ConditionProgramHandle;
        private int m_CommandProgramHandle;
        private int m_TargetPickerProgramHandle;
        private readonly List<CommandBase> m_CompiledCommands = new List<CommandBase>();
        internal bool TryGetBatchableHitCommands()
        {
            if (CurCondition == null ||
                CurCondition.CurConditionType != ConditionType.NoCondition ||
                CurCondition.ReverseResult ||
                CurTargetPicker == null ||
                CurTargetPicker.CurTargetPickerType != TargetPickerType.Arg ||
                CurCommandList == null || CurCommandList.Count == 0)
            {
                return false;
            }
            bool hasProjectile = false;
            foreach (CommandBase command in CurCommandList)
            {
                if (command is CommandCreateBullet createBullet)
                {
                    if (createBullet.CurBulletID == null ||
                        createBullet.ParamInt1 == null ||
                        createBullet.PosOffset == null) return false;
                    hasProjectile = true;
                }
                else if (command is CommandCauseDamage damage)
                {
                    if (damage.ParamInt1 == null || damage.ParamInt2 == null ||
                        damage.ParamInt3 == null) return false;
                }
                else
                {
                    return false;
                }
            }
            return hasProjectile;
        }
        internal bool HasScheduledPointTimer =>
            CombatConditionProgramBridge.IsScheduledPointTimer(m_ConditionProgramHandle);

        internal bool TryEnterDataBatchInvocation(out int programHandle)
        {
            programHandle = 0;
            if (!CombatConditionProgramBridge.IsValid(m_ConditionProgramHandle))
                CombatConditionCompiler.Compile(ref m_ConditionProgramHandle,
                    CurCondition);
            int handle = m_ConditionProgramHandle;
            var context = new CombatConditionContext
            {
                DeltaMs = GameEntry.CurrentLogicDeltaTime * 1000f
            };
            if (!CombatConditionProgramBridge.Check(handle, context,
                    RollPercent, null))
                return false;
            CombatConditionProgramBridge.EnterInvocation(handle);
            programHandle = handle;
            return true;
        }

        internal bool TrySchedulePointTimer(int pointHandle)
        {
            if (CurTriggerType != TriggerType.PerTick || CurCondition == null)
            {
                return false;
            }
            if (!CombatConditionProgramBridge.IsValid(m_ConditionProgramHandle))
            {
                CombatConditionCompiler.Compile(ref m_ConditionProgramHandle, CurCondition);
            }
            return CombatConditionProgramBridge.TrySchedulePointTimer(
                m_ConditionProgramHandle, pointHandle);
        }

        internal void OnScheduledPointTick(object arg)
        {
            using (TriggerMarker.Auto())
            {
                int handle = m_ConditionProgramHandle;
                CombatConditionProgramBridge.EnterInvocation(handle);
                try
                {
                    ExecuteInvocation(arg);
                }
                finally
                {
                    CombatConditionProgramBridge.ExitInvocation(handle);
                }
            }
        }

        public void OnActive()
        {
            if (CurTriggerType == TriggerType.OnActive)
            {
                OnTrigger();
            }
        }
        public void OnTrigger(object arg = null)
        {
            using (TriggerMarker.Auto())
            {
                if (CombatTriggerSimpleQueue.TryEnqueue(this, arg))
                    return;
                if (CurCondition != null &&
                    CurCondition.CurConditionType == ConditionType.NoCondition)
                {
                    if (!CurCondition.ReverseResult)
                    {
                        ExecuteInvocation(arg);
                    }
                    return;
                }
                int invocationProgramHandle = 0;
                try
                {
                    if (CurCondition != null)
                    {
                        if (!CombatConditionProgramBridge.IsValid(m_ConditionProgramHandle))
                        {
                            CombatConditionCompiler.Compile(ref m_ConditionProgramHandle,
                                CurCondition);
                        }
                        int programHandle = m_ConditionProgramHandle;
                        CombatConditionContext context =
                            CombatConditionCompiler.CreateContext(this, arg,
                                GameEntry.CurrentLogicDeltaTime * 1000f);
                        using (ConditionMarker.Auto())
                        {
                            if (!CombatConditionProgramBridge.Check(programHandle, context,
                                    RollPercent, ItemContainsSubItem))
                            {
                                return;
                            }
                        }
                        CombatConditionProgramBridge.EnterInvocation(programHandle);
                        invocationProgramHandle = programHandle;
                    }
                    if (invocationProgramHandle != 0)
                    {
                        ExecuteInvocation(arg);
                    }
                }
                finally
                {
                    if (invocationProgramHandle != 0)
                    {
                        CombatConditionProgramBridge.ExitInvocation(invocationProgramHandle);
                    }
                }
            }
        }

        internal bool TryEnterTimedBatchInvocation(out int programHandle)
        {
            programHandle = 0;
            if (CurCondition is not ConditionTimed &&
                CurCondition is not ConditionGroup) return false;
            if (!CombatConditionProgramBridge.IsValid(m_ConditionProgramHandle))
                CombatConditionCompiler.Compile(ref m_ConditionProgramHandle,
                    CurCondition);
            if (!CombatConditionProgramBridge.TryGetSingleTimedNode(
                    m_ConditionProgramHandle, out _, out _)) return false;
            CombatConditionProgramBridge.EnterInvocation(m_ConditionProgramHandle);
            programHandle = m_ConditionProgramHandle;
            return true;
        }

        internal bool TryEnterPureSkillGroupInvocation(out int programHandle,
            out Unity.Entities.Entity programEntity)
        {
            programHandle = 0;
            programEntity = Unity.Entities.Entity.Null;
            if (CurCondition is not ConditionGroup) return false;
            if (!CombatConditionProgramBridge.IsValid(m_ConditionProgramHandle))
                CombatConditionCompiler.Compile(ref m_ConditionProgramHandle,
                    CurCondition);
            if (!CombatConditionProgramBridge.TryGetPureSkillGroup(
                    m_ConditionProgramHandle, out programEntity)) return false;
            CombatConditionProgramBridge.EnterInvocation(m_ConditionProgramHandle);
            programHandle = m_ConditionProgramHandle;
            return true;
        }

        internal bool TryEnterTimedSkillGroupInvocation(out int programHandle,
            out Unity.Entities.Entity programEntity)
        {
            programHandle = 0;
            programEntity = Unity.Entities.Entity.Null;
            if (CurCondition is not ConditionGroup) return false;
            if (!CombatConditionProgramBridge.IsValid(m_ConditionProgramHandle))
                CombatConditionCompiler.Compile(ref m_ConditionProgramHandle,
                    CurCondition);
            if (!CombatConditionProgramBridge.TryGetTimedSkillGroup(
                    m_ConditionProgramHandle, out programEntity)) return false;
            CombatConditionProgramBridge.EnterInvocation(m_ConditionProgramHandle);
            programHandle = m_ConditionProgramHandle;
            return true;
        }

        private void ExecuteCommands(int targetSelectionHandle,
            CombatTargetProjection projection)
        {
            if (!CombatCommandProgramBridge.IsValid(m_CommandProgramHandle))
            {
                using (CommandCompileMarker.Auto())
                    CombatCommandCompiler.Compile(ref m_CommandProgramHandle,
                        CurCommandList, m_CompiledCommands);
            }
            using (CommandProgramMarker.Auto())
                if (CombatCommandProgramBridge.TryExecutePureAttributes(
                        m_CommandProgramHandle, targetSelectionHandle))
                    return;
            int currentBuffHandle = ParentTriggerList is Buff buff &&
                buff.Owner is EntityQizi ? buff.TimerHandle : 0;
            using (CommandProgramMarker.Auto())
                if (CombatCommandProgramBridge.TryExecutePureShields(
                        m_CommandProgramHandle, currentBuffHandle))
                    return;
            if (CurCommandList.Count == 1 &&
                CurCommandList[0] is CommandCauseDamage damageCommand &&
                damageCommand.ParentTrigger == this)
            {
                int casterHandle =
                    ParentTriggerList?.ParentSkill?.Caster?.CombatUnitHandle ?? 0;
                using (CommandProgramMarker.Auto())
                    if (CombatCommandProgramBridge.TryExecuteIndependentDamage(
                            m_CommandProgramHandle, targetSelectionHandle,
                            casterHandle))
                        return;
            }
            using (var callbacks = ReferencePool.Acquire<CombatCommandCallbacks>())
            {
                callbacks.Bind(this, projection, m_CompiledCommands);
                using (CommandProgramMarker.Auto())
                {
                    CombatCommandProgramBridge.Execute(m_CommandProgramHandle,
                        targetSelectionHandle, callbacks);
                }
            }
        }

        private void ExecuteInvocation(object arg)
        {
            if (TryExecuteDirectDataCommands(arg) ||
                TryExecuteSpatialPureAttributes()) return;
            List<EntityBase> executionTargets = ListPool<EntityBase>.Get();
            int targetSelectionHandle = 0;
            try
            {
                var projection = new CombatTargetProjection(executionTargets);
                CombatTargetSelectionBridge.Create(ref targetSelectionHandle);
                using (PickerMarker.Auto())
                {
                    if (CurTargetPicker != null)
                    {
                        if (!CombatTargetPickerProgramBridge.IsValid(
                                m_TargetPickerProgramHandle))
                        {
                            using (PickerCompileMarker.Auto())
                                CombatTargetPickerCompiler.Compile(
                                    ref m_TargetPickerProgramHandle, CurTargetPicker);
                        }
                        CombatTargetPickerCallbacks callbacks =
                            ReferencePool.Acquire<CombatTargetPickerCallbacks>();
                        try
                        {
                            callbacks.Bind(this, projection, arg);
                            CombatTargetPickerProgramBridge.Execute(
                                m_TargetPickerProgramHandle, 0,
                                targetSelectionHandle, callbacks);
                        }
                        finally
                        {
                            ReferencePool.Release(callbacks);
                        }
                    }
                }
                using (CommandsMarker.Auto())
                {
                    if (CurCommandList != null && CurCommandList.Count != 0)
                    {
                        ExecuteCommands(targetSelectionHandle, projection);
                    }
                }
            }
            finally
            {
                try
                {
                    if (targetSelectionHandle != 0)
                    {
                        CombatTargetSelectionBridge.Release(ref targetSelectionHandle);
                    }
                }
                finally
                {
                    ListPool<EntityBase>.Release(executionTargets);
                }
            }
        }

        private bool TryExecuteDirectDataCommands(object arg)
        {
            if (CurCommandList == null || CurCommandList.Count == 0 ||
                CurTargetPicker == null) return false;
            switch (CurTargetPicker.CurTargetPickerType)
            {
                case TargetPickerType.NoTarget:
                case TargetPickerType.SkillCaster:
                case TargetPickerType.SkillCasterCurTarget:
                case TargetPickerType.Arg:
                case TargetPickerType.TriggerOwner:
                case TargetPickerType.RelatedDamageTarget:
                    break;
                default:
                    return false;
            }

            if (!CombatCommandProgramBridge.IsValid(m_CommandProgramHandle))
            {
                using (CommandCompileMarker.Auto())
                    CombatCommandCompiler.Compile(ref m_CommandProgramHandle,
                        CurCommandList, m_CompiledCommands);
            }
            bool pureAttributes = CombatCommandProgramBridge.IsPureAttributeProgram(
                m_CommandProgramHandle);
            bool singleDamage = !pureAttributes && CurCommandList.Count == 1 &&
                CurCommandList[0] is CommandCauseDamage damageCommand &&
                damageCommand.ParentTrigger == this;
            if (!pureAttributes && !singleDamage) return false;

            int unitHandle = 0;
            using (DirectUnitPickerMarker.Auto())
            {
                EntityQizi caster = ParentTriggerList?.ParentSkill?.Caster;
                EntityQizi target;
                switch (CurTargetPicker.CurTargetPickerType)
                {
                    case TargetPickerType.NoTarget:
                        return true;
                    case TargetPickerType.SkillCaster:
                        if (caster == null) return true;
                        target = caster;
                        break;
                    case TargetPickerType.SkillCasterCurTarget:
                        if (caster == null) return true;
                        if (caster.CombatUnitHandle == 0)
                        {
                            target = caster.CurAttackTarget;
                        }
                        else
                        {
                            int targetUid = CombatAttributeBridge.GetAttackTargetUid(
                                caster.CombatUnitHandle);
                            target = targetUid == 0 ? null :
                                HotGameEntry.HeroManager.GetEntityByUID(targetUid);
                        }
                        if (target == null) return false;
                        break;
                    case TargetPickerType.Arg:
                        if (arg is not EntityQizi argUnit) return true;
                        target = argUnit;
                        break;
                    case TargetPickerType.TriggerOwner:
                        if (ParentTriggerList == null) return true;
                        if (ParentTriggerList.Owner is not EntityQizi ownerUnit)
                            return false;
                        target = ownerUnit;
                        break;
                    case TargetPickerType.RelatedDamageTarget:
                        if (arg is not CauseDamageData causeData) return true;
                        if (!CombatTargetPickerProgramBridge.IsValid(
                                m_TargetPickerProgramHandle))
                        {
                            using (PickerCompileMarker.Auto())
                                CombatTargetPickerCompiler.Compile(
                                    ref m_TargetPickerProgramHandle,
                                    CurTargetPicker);
                        }
                        if (!CombatTargetPickerProgramBridge.TryGetRelatedDamageNode(
                                m_TargetPickerProgramHandle,
                                out CombatTargetPickerNode damagePicker))
                            return false;
                        target = damagePicker.DamageTargetType == 0
                            ? causeData.Caster : causeData.Target;
                        if (target == null) return false;
                        if (!CombatAttributeBridge.TryGetHandleByUid(
                                target.HeroUID, out int registeredHandle) ||
                            registeredHandle != target.CombatUnitHandle)
                            return false;
                        break;
                    default:
                        return false;
                }
                unitHandle = target.CombatUnitHandle;
                if (!CombatAttributeBridge.TryGetEntity(unitHandle, out _))
                    return false;
            }

            int selectionHandle = 0;
            try
            {
                CombatTargetSelectionBridge.Create(ref selectionHandle);
                CombatTargetSelectionBridge.Append(selectionHandle,
                    new CombatSelectedTarget
                    {
                        Kind = CombatSelectedTargetKind.Unit,
                        Handle = unitHandle
                    });
                using (CommandProgramMarker.Auto())
                    return pureAttributes
                        ? CombatCommandProgramBridge.TryExecutePureAttributes(
                            m_CommandProgramHandle, selectionHandle)
                        : CombatCommandProgramBridge.TryExecuteIndependentDamage(
                            m_CommandProgramHandle, selectionHandle,
                            ParentTriggerList?.ParentSkill?.Caster?.CombatUnitHandle ?? 0);
            }
            finally
            {
                CombatTargetSelectionBridge.Release(ref selectionHandle);
            }
        }

        private bool TryExecuteSpatialPureAttributes()
        {
            if (CurCommandList == null || CurCommandList.Count == 0 ||
                CurTargetPicker == null) return false;
            switch (CurTargetPicker.CurTargetPickerType)
            {
                case TargetPickerType.Nearest:
                case TargetPickerType.OwnerDistance:
                case TargetPickerType.OwnerDirection:
                    break;
                default:
                    return false;
            }
            if (!CombatCommandProgramBridge.IsValid(m_CommandProgramHandle))
            {
                using (CommandCompileMarker.Auto())
                    CombatCommandCompiler.Compile(ref m_CommandProgramHandle,
                        CurCommandList, m_CompiledCommands);
            }
            if (!CombatCommandProgramBridge.IsPureAttributeProgram(
                    m_CommandProgramHandle)) return false;
            if (!CombatTargetPickerProgramBridge.IsValid(m_TargetPickerProgramHandle))
            {
                using (PickerCompileMarker.Auto())
                    CombatTargetPickerCompiler.Compile(
                        ref m_TargetPickerProgramHandle, CurTargetPicker);
            }
            if (!CombatTargetPickerProgramBridge.TryGetSpatialNode(
                    m_TargetPickerProgramHandle,
                    out CombatTargetPickerNode node)) return false;

            EntityBase owner = ParentTriggerList?.Owner;
            if (owner == null) return true;
            int selectionHandle = 0;
            try
            {
                CombatTargetSelectionBridge.Create(ref selectionHandle);
                using (SpatialPickerMarker.Auto())
                {
                    int itemId = ParentTriggerList.ParentSkill?.FromItemID ?? 0;
                    switch (node.Kind)
                    {
                        case CombatTargetPickerKind.Nearest:
                            if (owner is EntityQizi nearestUnit)
                            {
                                if (!CombatAttributeBridge.TryGetEntity(
                                        nearestUnit.CombatUnitHandle, out _))
                                    return false;
                                CombatSpatialSelection.SelectNearest(selectionHandle,
                                    nearestUnit.CombatUnitHandle, node.TargetCamp,
                                    node.LengthMm, HasManagedUnit);
                            }
                            else
                            {
                                UnityEngine.Vector3 position = owner.LogicPosition;
                                CombatSpatialSelection.SelectNearest(selectionHandle,
                                    position.x, position.z, (int)owner.BelongCamp,
                                    node.TargetCamp, node.LengthMm, HasManagedUnit);
                            }
                            break;
                        case CombatTargetPickerKind.OwnerDistance:
                            UnityEngine.Vector3 center = owner.LogicPosition;
                            int lengthMm = CombatSpatialSelection.ResolveLengthMm(
                                node.LengthMm, node.UseWeaponLength, itemId,
                                ResolveWeaponLength);
                            CombatSpatialSelection.SelectWithinDistance(selectionHandle,
                                center.x, center.z, (int)owner.BelongCamp,
                                node.TargetCamp, lengthMm / 1000f,
                                HasManagedUnit);
                            break;
                        case CombatTargetPickerKind.OwnerDirection:
                            if (owner is not EntityQizi directionUnit)
                                return true;
                            if (!CombatAttributeBridge.TryGetEntity(
                                    directionUnit.CombatUnitHandle, out _))
                                return false;
                            CombatSpatialSelection.SelectSectorFromAttackTarget(
                                selectionHandle, directionUnit.CombatUnitHandle,
                                node.LengthMm, node.UseWeaponLength, itemId,
                                node.AngleDegrees, HasManagedUnit,
                                ResolveWeaponLength, HasManagedUnit);
                            break;
                    }
                }
                using (CommandProgramMarker.Auto())
                    return CombatCommandProgramBridge.TryExecutePureAttributes(
                        m_CommandProgramHandle, selectionHandle);
            }
            finally
            {
                CombatTargetSelectionBridge.Release(ref selectionHandle);
            }
        }

        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)CurTriggerType);
            writer.Write((int)CurCondition.CurConditionType);
            CurCondition.WriteToFile(writer);
            writer.Write((int)CurTargetPicker.CurTargetPickerType);
            CurTargetPicker.WriteToFile(writer);
            writer.Write(CurCommandList.Count);
            foreach (var oneCommand in CurCommandList)
            {
                writer.Write((int)oneCommand.CurCommandType);
                oneCommand.WriteToFile(writer);
            }
        }
        public void ReadFromFile(BinaryReader reader)
        {
            CombatConditionProgramBridge.Release(ref m_ConditionProgramHandle);
            CombatCommandProgramBridge.Release(ref m_CommandProgramHandle);
            CombatTargetPickerProgramBridge.Release(ref m_TargetPickerProgramHandle);
            m_CompiledCommands.Clear();
            CurTriggerType = (TriggerType)reader.ReadInt32();
            
            var curConditionType = (ConditionType)reader.ReadInt32();
            CurCondition = SkillFactory.CreateCondition(curConditionType);
            CurCondition.ReadFromFile(reader);
            CurCondition.ParentTrigger = this;
            
            var curTargetPickerType = (TargetPickerType)reader.ReadInt32();
            CurTargetPicker = SkillFactory.CreateTargetPicker(curTargetPickerType);
            CurTargetPicker.ReadFromFile(reader);
            CurTargetPicker.BindTrigger(this);
            
            var commandCount = reader.ReadInt32();
            CurCommandList.Clear();
            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                var curCommandType = (CommandType)reader.ReadInt32();
                var curCommand = SkillFactory.CreateCommand(curCommandType);
                CurCommandList.Add(curCommand);
                curCommand.ReadFromFile(reader);
                curCommand.ParentTrigger = this;
            }
        }

        public void Clone(OneTrigger copy)
        {
            copy.ResetRuntimeState();
            copy.CurTriggerType = CurTriggerType;
            if (copy.CurCondition == null ||
                copy.CurCondition.CurConditionType != CurCondition.CurConditionType)
            {
                if (copy.CurCondition != null) ReferencePool.Release(copy.CurCondition);
                copy.CurCondition = SkillFactory.CreateCondition(CurCondition.CurConditionType);
            }
            var copyCondition = copy.CurCondition;
            copyCondition.ParentTrigger = copy;
            CurCondition.Clone(copyCondition);
            if (copy.CurTargetPicker == null ||
                copy.CurTargetPicker.CurTargetPickerType != CurTargetPicker.CurTargetPickerType)
            {
                if (copy.CurTargetPicker != null) ReferencePool.Release(copy.CurTargetPicker);
                copy.CurTargetPicker = SkillFactory.CreateTargetPicker(CurTargetPicker.CurTargetPickerType);
            }
            var copyTargetPicker = copy.CurTargetPicker;
            CurTargetPicker.Clone(copyTargetPicker);
            copyTargetPicker.BindTrigger(copy);

            copy.CurCommandList ??= ListPool<CommandBase>.Get();
            for (int i = 0; i < CurCommandList.Count; i++)
            {
                var oneCommand = CurCommandList[i];
                CommandBase copyCommand;
                if (i < copy.CurCommandList.Count &&
                    copy.CurCommandList[i].CurCommandType == oneCommand.CurCommandType)
                {
                    copyCommand = copy.CurCommandList[i];
                }
                else
                {
                    if (i < copy.CurCommandList.Count)
                    {
                        ReferencePool.Release(copy.CurCommandList[i]);
                        copyCommand = SkillFactory.CreateCommand(oneCommand.CurCommandType);
                        copy.CurCommandList[i] = copyCommand;
                    }
                    else
                    {
                        copyCommand = SkillFactory.CreateCommand(oneCommand.CurCommandType);
                        copy.CurCommandList.Add(copyCommand);
                    }
                }
                copyCommand.ParentTrigger = copy;
                oneCommand.Clone(copyCommand);
            }
            for (int i = copy.CurCommandList.Count - 1; i >= CurCommandList.Count; i--)
            {
                ReferencePool.Release(copy.CurCommandList[i]);
                copy.CurCommandList.RemoveAt(i);
            }

            // Picker and command nodes are immutable after compilation. The pooled
            // trigger keeps its own managed command instances, but shares the ECS
            // program entities and native command snapshot with the template.
            if (CurTargetPicker != null)
            {
                if (!CombatTargetPickerProgramBridge.IsValid(m_TargetPickerProgramHandle))
                {
                    using (PickerCompileMarker.Auto())
                        CombatTargetPickerCompiler.Compile(
                            ref m_TargetPickerProgramHandle, CurTargetPicker);
                }
                CombatTargetPickerProgramBridge.Retain(m_TargetPickerProgramHandle);
                copy.m_TargetPickerProgramHandle = m_TargetPickerProgramHandle;
            }
            if (CurCommandList != null && CurCommandList.Count != 0)
            {
                if (!CombatCommandProgramBridge.IsValid(m_CommandProgramHandle))
                {
                    using (CommandCompileMarker.Auto())
                        CombatCommandCompiler.Compile(ref m_CommandProgramHandle,
                            CurCommandList, m_CompiledCommands);
                }
                CombatCommandProgramBridge.Retain(m_CommandProgramHandle);
                copy.m_CommandProgramHandle = m_CommandProgramHandle;
                CombatCommandCompiler.CollectManagedCommands(copy.CurCommandList,
                    copy.m_CompiledCommands);
            }
        }

        internal void ResetRuntimeState()
        {
            CombatConditionProgramBridge.Release(ref m_ConditionProgramHandle);
            CombatCommandProgramBridge.Release(ref m_CommandProgramHandle);
            CombatTargetPickerProgramBridge.Release(ref m_TargetPickerProgramHandle);
            m_CompiledCommands.Clear();
        }
        public void SetSkillValue(DataRowBase dataTable)
        {
            CombatConditionProgramBridge.Release(ref m_ConditionProgramHandle);
            CombatCommandProgramBridge.Release(ref m_CommandProgramHandle);
            CombatTargetPickerProgramBridge.Release(ref m_TargetPickerProgramHandle);
            m_CompiledCommands.Clear();
            CurCondition.SetSkillValue(dataTable);
            CurTargetPicker.SetSkillValue(dataTable);
            foreach (var oneCommand in CurCommandList)
            {
                oneCommand.SetSkillValue(dataTable);
            }
        }

        public void Clear()
        {
            CombatConditionProgramBridge.Release(ref m_ConditionProgramHandle);
            CombatCommandProgramBridge.Release(ref m_CommandProgramHandle);
            CombatTargetPickerProgramBridge.Release(ref m_TargetPickerProgramHandle);
            m_CompiledCommands.Clear();
            ParentTriggerList = null;
            CurTriggerType = TriggerType.OnActive;
            if (CurCondition != null)
            {
                ReferencePool.Release(CurCondition);
                CurCondition = null;
            }
            if (CurTargetPicker != null)
            {
                ReferencePool.Release(CurTargetPicker);
                CurTargetPicker = null;
            }
            if (CurCommandList != null)
            {
                foreach (var oneCommand in CurCommandList)
                {
                    ReferencePool.Release(oneCommand);
                }
                CurCommandList.Clear();
                /*ListPool<CommandBase>.Release(CurCommandList);
                CurCommandList = null;*/
            }
        }
    }
}
