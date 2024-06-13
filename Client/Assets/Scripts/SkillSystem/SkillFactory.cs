using System.Collections.Generic;
using DataTable;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class SkillFactory
    {
        public static CommandBase CreateCommand(CommandType type)
        {
            switch (type)
            {
                case CommandType.DoNothing:
                    return new CommandBase();
                case CommandType.CauseDamage:
                    return new CommandCauseDamage();
                case CommandType.CreateBuff:
                    return new CommandCreateBuff();
                case CommandType.PlayAnim:
                    return new CommandPlayAnim();
                case CommandType.CreateBullet:
                    return new CommandCreateBullet();
                default:
                    return new CommandBase();
            }
        }
        public static ConditionBase CreateCondition(ConditionType type)
        {
            switch (type)
            {
                case ConditionType.NoCondition:
                    return new ConditionBase();
                case ConditionType.ConditionGroup:
                    return new ConditionGroup();
                case ConditionType.Percentage:
                    return new ConditionPercent();
                case ConditionType.Timed:
                    return new ConditionTimed();
                default:
                    return new ConditionBase();
            }
        }
        public static TargetPickerBase CreateTargetPicker(TargetPickerType type)
        {
            switch (type)
            {
                case TargetPickerType.NoTarget:
                    return new TargetPickerBase();
                case TargetPickerType.SkillCasterCurTarget:
                    return new TargetPickerSkillCasterCurTarget();
                case TargetPickerType.SkillCaster:
                    return new TargetPickerSkillCaster();
                case TargetPickerType.Arg:
                    return new TargetPickerArg();
                case TargetPickerType.TriggerOwner:
                    return new TargetPickerTriggerOwner();
                default:
                    return new TargetPickerBase();
            }
        }

        public static TriggerList CreateNewEmptyTriggerList(Skill skill = null)
        {
            var emptyTriggerList = new TriggerList();
            emptyTriggerList.ParentSkill = skill;
            return emptyTriggerList;
        }
        public static OneTrigger CreateNewDefaultTrigger()
        {
            var emptyTrigger = new OneTrigger
            {
                CurCondition = CreateCondition(ConditionType.NoCondition),
                CurTargetPicker = CreateTargetPicker(TargetPickerType.NoTarget),
            };
            return emptyTrigger;
        }
        public static OneTrigger CreateNewEmptyTrigger()
        {
            var emptyTrigger = new OneTrigger();
            return emptyTrigger;
        }

        public static Buff CreateNewBuff()
        {
            var newBuff = new Buff();
            return newBuff;
        }

        public static Skill CreateNewSkill()
        {
            var newSkill = new Skill();
            newSkill.OwnTriggerList = CreateNewEmptyTriggerList();
            return newSkill;
        }

        public static Skill CreateDefaultSkill()
        {
            var newSkill = new Skill();
            newSkill.OwnTriggerList = CreateNewEmptyTriggerList();
            
            var playAniTrigger = CreateNewEmptyTrigger();
            playAniTrigger.CurTriggerType = TriggerType.OnActive;
            playAniTrigger.CurCondition = CreateCondition(ConditionType.NoCondition);;
            playAniTrigger.CurTargetPicker = CreateTargetPicker(TargetPickerType.SkillCaster);
            CommandPlayAnim playAniCommand = CreateCommand(CommandType.PlayAnim) as CommandPlayAnim;
            if (playAniCommand != null)
            {
                playAniCommand.AnimName.CurMatchTable = GenerateEnumDataTables.Skill;
                playAniCommand.AnimName.CurMatchPropertyIndex = (int)DRSkillField.SkillAnim;
                playAniTrigger.CurCommandList.Add(playAniCommand);
            }
            newSkill.OwnTriggerList.CurTriggerList.Add(playAniTrigger);

            var beforeShakeEndTrigger = CreateNewEmptyTrigger();
            beforeShakeEndTrigger.CurTriggerType = TriggerType.SkillBeforeShakeEnd;
            beforeShakeEndTrigger.CurCondition = CreateCondition(ConditionType.NoCondition);;
            beforeShakeEndTrigger.CurTargetPicker = CreateTargetPicker(TargetPickerType.SkillCasterCurTarget);
            newSkill.OwnTriggerList.CurTriggerList.Add(beforeShakeEndTrigger);
            
            return newSkill;
        }
    }
}