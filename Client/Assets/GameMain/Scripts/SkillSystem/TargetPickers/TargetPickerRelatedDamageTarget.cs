using System.IO;
using Entity;

namespace SkillSystem
{
    public class TargetPickerRelatedDamageTarget: TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.RelatedDamageTarget;
        public DamageDataTargetType CurDamageDataTargetType;
        public override EntityBase GetTarget(OneTrigger trigger, object arg = null)
        {
            if (arg is CauseDamageData damageData)
            {
                if (CurDamageDataTargetType==DamageDataTargetType.Caster)
                {
                    return damageData.Caster;
                }
                else//后面需更改添加list列表，完善目标
                {
                    return damageData.Target;
                }
            }
            return null;
        }

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