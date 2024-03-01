namespace SkillSystem
{
    public class ConditionBase
    {
        public bool ReverseResult = false;
        public bool OnCheck(OneTrigger trigger)
        {
            return false;
        }
    }
}