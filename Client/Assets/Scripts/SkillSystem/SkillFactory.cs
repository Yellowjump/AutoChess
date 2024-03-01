namespace SkillSystem
{
    public class SkillFactory
    {
        public CommandBase CreateCommand(CommandType type)
        {
            switch (type)
            {
                case CommandType.CauseDamage:
                    return new CommandBase();
            }
            return null;
        }
        public ConditionBase CreateCondition(ConditionType type)
        {
            switch (type)
            {
                case ConditionType.NoCondition:
                    return new ConditionBase();
            }
            return null;
        }
        public TargetPickerBase CreateCommand(TargetPickerType type)
        {
            switch (type)
            {
                case TargetPickerType.Nearest:
                    return new TargetPickerBase();
            }
            return null;
        }
    }
}