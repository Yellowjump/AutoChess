using System.Collections.Generic;
using DataTable;
using UnityGameFramework.Runtime;
using GameFramework;
using UnityEngine.Pool;

namespace SkillSystem
{
    public class SkillFactory
    {
        public static CommandBase CreateCommand(CommandType type)
        {
            switch (type)
            {
                case CommandType.DoNothing:
                    return ReferencePool.Acquire<CommandBase>();
                case CommandType.CauseDamage:
                    var commandCauseDamage = ReferencePool.Acquire<CommandCauseDamage>();
                    commandCauseDamage.ParamInt1 = CreateTableParamInt();
                    commandCauseDamage.ParamInt2 = CreateTableParamInt();
                    commandCauseDamage.ParamInt3 = CreateTableParamInt();
                    return commandCauseDamage;
                case CommandType.CreateBuff:
                    var commandCauseBuff = ReferencePool.Acquire<CommandCreateBuff>();
                    commandCauseBuff.BuffID = CreateTableParamInt();
                    commandCauseBuff.BuffList = CreateTableParamString();
                    return commandCauseBuff;
                case CommandType.PlayAnim:
                    var commandPlayAnim = ReferencePool.Acquire<CommandPlayAnim>();
                    commandPlayAnim.AnimAssetID = CreateTableParamInt();
                    return commandPlayAnim;
                case CommandType.CreateBullet:
                    var commandCauseBullet = ReferencePool.Acquire<CommandCreateBullet>();
                    commandCauseBullet.ParamInt1 = CreateTableParamInt();
                    commandCauseBullet.CurBulletID = CreateTableParamInt();
                    commandCauseBullet.BulletTrigger = CreateNewEmptyTriggerList();
                    commandCauseBullet.PosOffset = CreateTableParamVector3();
                    return commandCauseBullet;
                case CommandType.CreateHuDun:
                    var commandHuDun = ReferencePool.Acquire<CommandHuDun>();
                    commandHuDun.ParamInt1 = CreateTableParamInt();
                    return commandHuDun;
                case CommandType.CreateSfx:
                    var commandCreateSfx = ReferencePool.Acquire<CommandCreateSfx>();
                    commandCreateSfx.SfxID = CreateTableParamInt();
                    return commandCreateSfx;
                case CommandType.ShowWeapon:
                    var commandShowWeapon = ReferencePool.Acquire<CommandShowWeapon>();
                    commandShowWeapon.ShowWeaponItemID = CreateTableParamInt();
                    return commandShowWeapon;
                case CommandType.RepeatExecute:
                    var commandRepeatExecuteCmd = ReferencePool.Acquire<CommandRepeatExecuteCmd>();
                    commandRepeatExecuteCmd.ParamInt1 = CreateTableParamInt();
                    commandRepeatExecuteCmd.CurCommandList = ListPool<CommandBase>.Get();
                    return commandRepeatExecuteCmd;
                case CommandType.RemoveBuff:
                    var commandRemoveBuff = ReferencePool.Acquire<CommandRemoveBuff>();
                    commandRemoveBuff.BuffID = CreateTableParamInt();
                    return commandRemoveBuff;
                case CommandType.RemoveBullet:
                    return ReferencePool.Acquire<CommandRemoveBullet>();
                case CommandType.ChangeAttribute:
                    var commandChangeAttribute = ReferencePool.Acquire<CommandChangeAttribute>();
                    commandChangeAttribute.TargetAttrType = CreateTableParamInt();
                    commandChangeAttribute.ParamInt1 = CreateTableParamInt();
                    return commandChangeAttribute;
                default:
                    return ReferencePool.Acquire<CommandBase>();
            }
        }
        public static ConditionBase CreateCondition(ConditionType type)
        {
            switch (type)
            {
                case ConditionType.NoCondition:
                    return ReferencePool.Acquire<ConditionBase>();
                case ConditionType.ConditionGroup:
                    var conditionGroup = ReferencePool.Acquire<ConditionGroup>();
                    conditionGroup.ConditionList = ListPool<ConditionBase>.Get();
                    return conditionGroup;
                case ConditionType.Percentage:
                    var conditionPercent = ReferencePool.Acquire<ConditionPercent>();
                    conditionPercent.PercentTarget = CreateTableParamInt();
                    return conditionPercent;
                case ConditionType.Timed:
                    var conditionTimed = ReferencePool.Acquire<ConditionTimed>();
                    conditionTimed.TimeIntervalMs = CreateTableParamInt();
                    return conditionTimed;
                case ConditionType.RelateItem:
                    var conditionRelateItem = ReferencePool.Acquire<ConditionRelateItem>();
                    conditionRelateItem.ParamInt1 = CreateTableParamInt();
                    conditionRelateItem.ParamInt2 = CreateTableParamInt();
                    return conditionRelateItem;
                case ConditionType.RelateSkill:
                    var conditionRelateSkill = ReferencePool.Acquire<ConditionRelateSkill>();
                    conditionRelateSkill.ParamInt1 = CreateTableParamInt();
                    conditionRelateSkill.ParamInt2 = CreateTableParamInt();
                    return conditionRelateSkill;
                default:
                    return ReferencePool.Acquire<ConditionBase>();
            }
        }
        public static TargetPickerBase CreateTargetPicker(TargetPickerType type)
        {
            switch (type)
            {
                case TargetPickerType.NoTarget:
                    return ReferencePool.Acquire<TargetPickerBase>();
                case TargetPickerType.SkillCasterCurTarget:
                    return ReferencePool.Acquire<TargetPickerSkillCasterCurTarget>();
                case TargetPickerType.SkillCaster:
                    return ReferencePool.Acquire<TargetPickerSkillCaster>();
                case TargetPickerType.Arg:
                    return ReferencePool.Acquire<TargetPickerArg>();
                case TargetPickerType.TriggerOwner:
                    return ReferencePool.Acquire<TargetPickerTriggerOwner>();
                case TargetPickerType.RelatedDamageTarget:
                    return ReferencePool.Acquire<TargetPickerRelatedDamageTarget>();
                case TargetPickerType.OwnerDirection:
                    var targetPickerOwnerDirection = ReferencePool.Acquire<TargetPickerOwnerDirection>();
                    targetPickerOwnerDirection.WeaponLength = CreateTableParamInt();
                    targetPickerOwnerDirection.ValidAngle = CreateTableParamInt();
                    return targetPickerOwnerDirection;
                case TargetPickerType.RandomFromList:
                    var targetPickerRandomFromList = ReferencePool.Acquire<TargetPickerRandomFromList>();
                    targetPickerRandomFromList.ParamInt1 = CreateTableParamInt();
                    targetPickerRandomFromList.WorkTargetPicker = CreateTargetPicker(TargetPickerType.NoTarget);
                    return targetPickerRandomFromList;
                case TargetPickerType.Bullet:
                    var targetPickerBullet = ReferencePool.Acquire<TargetPickerBullet>();
                    targetPickerBullet.BulletID = CreateTableParamInt();
                    return targetPickerBullet;
                default:
                    return ReferencePool.Acquire<TargetPickerBase>();
            }
        }

        public static TableParamInt CreateTableParamInt()
        {
            return ReferencePool.Acquire<TableParamInt>();
        }
        public static TableParamString CreateTableParamString()
        {
            return ReferencePool.Acquire<TableParamString>();
        }
        public static TableParamVector3 CreateTableParamVector3()
        {
            return ReferencePool.Acquire<TableParamVector3>();
        }
        public static TriggerList CreateNewEmptyTriggerList(Skill skill = null)
        {
            var emptyTriggerList = ReferencePool.Acquire<TriggerList>();
            emptyTriggerList.CurTriggerList = ListPool<OneTrigger>.Get();
            emptyTriggerList.ParentSkill = skill;
            return emptyTriggerList;
        }
        public static OneTrigger CreateNewDefaultTrigger()
        {
            var emptyTrigger =  ReferencePool.Acquire<OneTrigger>();
            emptyTrigger.CurCommandList = ListPool<CommandBase>.Get();
            emptyTrigger.CurCondition = CreateCondition(ConditionType.NoCondition);
            emptyTrigger.CurTargetPicker = CreateTargetPicker(TargetPickerType.NoTarget);
            return emptyTrigger;
        }
        public static OneTrigger CreateNewEmptyTrigger()
        {
            var emptyTrigger = ReferencePool.Acquire<OneTrigger>();
            emptyTrigger.CurCommandList = ListPool<CommandBase>.Get();
            return emptyTrigger;
        }

        public static Buff CreateNewBuff()
        {
            var newBuff = ReferencePool.Acquire<Buff>();
            newBuff.CurTriggerList = ListPool<OneTrigger>.Get();
            return newBuff;
        }

        public static Skill CreateNewSkill()
        {
            var newSkill = ReferencePool.Acquire<Skill>();
            newSkill.CurTriggerList = ListPool<OneTrigger>.Get();
            newSkill.ParentSkill = newSkill;
            return newSkill;
        }

        public static Skill CreateDefaultSkill()
        {
            var newSkill = ReferencePool.Acquire<Skill>();;
            newSkill.CurTriggerList = ListPool<OneTrigger>.Get();
            newSkill.ParentSkill = newSkill;
            var playAniTrigger = CreateNewEmptyTrigger();
            playAniTrigger.CurTriggerType = TriggerType.OnActive;
            playAniTrigger.CurCondition = CreateCondition(ConditionType.NoCondition);;
            playAniTrigger.CurTargetPicker = CreateTargetPicker(TargetPickerType.SkillCaster);
            CommandPlayAnim playAniCommand = CreateCommand(CommandType.PlayAnim) as CommandPlayAnim;
            if (playAniCommand != null)
            {
                playAniCommand.AnimAssetID.CurMatchTable = GenerateEnumDataTables.Skill;
                playAniCommand.AnimAssetID.CurMatchPropertyIndex = (int)DRSkillField.SkillAnim;
                playAniTrigger.CurCommandList.Add(playAniCommand);
            }
            newSkill.CurTriggerList.Add(playAniTrigger);

            var beforeShakeEndTrigger = CreateNewEmptyTrigger();
            beforeShakeEndTrigger.CurTriggerType = TriggerType.SkillBeforeShakeEnd;
            beforeShakeEndTrigger.CurCondition = CreateCondition(ConditionType.NoCondition);;
            beforeShakeEndTrigger.CurTargetPicker = CreateTargetPicker(TargetPickerType.SkillCasterCurTarget);
            newSkill.CurTriggerList.Add(beforeShakeEndTrigger);
            
            return newSkill;
        }
    }
}