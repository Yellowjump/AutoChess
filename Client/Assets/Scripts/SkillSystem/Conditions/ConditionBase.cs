namespace SkillSystem
{
    public class ConditionBase
    {
        public virtual ConditionType CurConditionType => ConditionType.NoCondition;
        public bool ReverseResult = false;
        public virtual bool OnCheck(OneTrigger trigger)
        {
            return false;
        }
    }
}