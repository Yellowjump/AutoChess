using System.Collections.Generic;
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
            newBuff.OwnTriggerList = CreateNewEmptyTriggerList();
            return newBuff;
        }

        public static Skill CreateNewSkill()
        {
            var newSkill = new Skill();
            newSkill.OwnTriggerList = CreateNewEmptyTriggerList();
            return newSkill;
        }
    }
}