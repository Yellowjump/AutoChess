using System;
using System.IO;
using Entity;
using GameFramework;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TargetPickerBase:IReference
    {
        public OneTrigger ParentTrigger;
        public virtual TargetPickerType CurTargetPickerType => TargetPickerType.NoTarget;
        public virtual EntityBase GetTarget(OneTrigger trigger,object arg = null)
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

        public void Clear()
        {
            
        }
    }
}