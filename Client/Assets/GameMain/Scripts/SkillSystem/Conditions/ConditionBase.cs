using System.IO;
using GameFramework;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class ConditionBase:IReference
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
            writer.Write(ReverseResult);
        }

        public virtual void ReadFromFile(BinaryReader reader)
        {
            ReverseResult = reader.ReadBoolean();
        }

        public virtual void Clone(ConditionBase copy)
        {
            copy.ReverseResult = ReverseResult;
        }
        public virtual void SetSkillValue(DataRowBase dataTable)
        {
            
        }

        public virtual void Clear()
        {
            
        }
    }
}