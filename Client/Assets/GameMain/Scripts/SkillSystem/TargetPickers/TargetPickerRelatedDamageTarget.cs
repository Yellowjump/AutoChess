using System.Collections.Generic;
using System.IO;
using Entity;
using UnityEngine.Pool;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    public class TargetPickerRelatedDamageTarget: TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.RelatedDamageTarget;
        public DamageDataTargetType CurDamageDataTargetType;

        public override void Clone(TargetPickerBase copy)
        {
            if (copy is TargetPickerRelatedDamageTarget TargetPicker)
            {
                TargetPicker.CurDamageDataTargetType = CurDamageDataTargetType;
            }
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurDamageDataTargetType = (DamageDataTargetType)reader.ReadInt32();
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)CurDamageDataTargetType);
        }
    }
}
