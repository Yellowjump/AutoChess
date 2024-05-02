using System;
using System.IO;
using liuchengguanli;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TargetPickerBase
    {
        public OneTrigger ParentTrigger;
        public virtual TargetPickerType CurTargetPickerType => TargetPickerType.NoTarget;
        public EntityBase GetTarget(OneTrigger trigger,object arg = null)
        {
            return null;
        }
        public virtual void WriteToFile(BinaryWriter writer)
        {
            
        }

        public virtual void ReadFromFile(BinaryReader reader)
        {
            
        }

        public virtual void Clone(TargetPickerBase copy)
        {
            
        }
        public virtual void SetSkillValue(DataRowBase dataTable)
        {
            
        }
    }
}