using System;
using System.IO;
using liuchengguanli;

namespace SkillSystem
{
    public class TargetPickerBase
    {
        public virtual TargetPickerType CurTargetPickerType => TargetPickerType.NoTarget;
        public EntityBase GetTarget(OneTrigger trigger)
        {
            return null;
        }
        public virtual void WriteToFile(BinaryWriter writer)
        {
            
        }

        public virtual void ReadFromFile(BinaryReader reader)
        {
            
        }
    }
}