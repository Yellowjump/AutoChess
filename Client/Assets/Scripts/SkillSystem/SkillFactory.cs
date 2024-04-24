using System.Collections.Generic;

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

        public static TriggerList CreateNewEmptyTriggerList()
        {
            var emptyTriggerList = new TriggerList();
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
    }
}