using System.IO;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class ConditionBase
    {
        public OneTrigger ParentTrigger;
        public virtual ConditionType CurConditionType => ConditionType.NoCondition;
        public bool ReverseResult = false;
        public virtual bool OnCheck(OneTrigger trigger,object arg = null)
        {
            return true;
        }

        public virtual void WriteToFile(BinaryWriter writer)
        {
            
        }

        public virtual void ReadFromFile(BinaryReader reader)
        {
            
        }

        public virtual void Clone(ConditionBase copy)
        {
            copy.ReverseResult = ReverseResult;
        }
        public virtual void SetSkillValue(DataRowBase dataTable)
        {
            
        }
    }
}