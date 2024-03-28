using System.IO;

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

        public virtual void WriteToFile(BinaryWriter writer)
        {
            
        }

        public virtual void ReadFromFile(BinaryReader reader)
        {
            
        }
    }
}