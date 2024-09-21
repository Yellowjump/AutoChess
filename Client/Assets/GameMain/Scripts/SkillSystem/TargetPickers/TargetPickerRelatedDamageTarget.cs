using System.Collections.Generic;
using System.IO;
using Entity;
using UnityEngine.Pool;

namespace SkillSystem
{
    public class TargetPickerRelatedDamageTarget: TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.RelatedDamageTarget;
        public DamageDataTargetType CurDamageDataTargetType;
        public override List<EntityBase> GetTarget(OneTrigger trigger, object arg = null)
        {
            if (arg is CauseDamageData damageData)
            {
                List<EntityBase> targetList = ListPool<EntityBase>.Get();
                if (CurDamageDataTargetType==DamageDataTargetType.Caster)
                {
                    targetList.Add(damageData.Caster);
                    return targetList;
                }
                else//后面需更改添加list列表，完善目标
                {
                    targetList.Add(damageData.Target);
                    return targetList;
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