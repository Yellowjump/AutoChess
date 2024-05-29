using System.IO;
using Entity;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandCauseDamage:CommandBase
    {
        public override CommandType CurCommandType => CommandType.CauseDamage;
        public DamageComputeType CurDamageComputeType = DamageComputeType.CommonDamage;
        public DamageType CurDamageType;
        public TableParamInt ParamInt1 = new TableParamInt();
        public TableParamInt ParamInt2 = new TableParamInt();
        public TableParamInt ParamInt3 = new TableParamInt();
        public override void OnExecute(OneTrigger trigger,object arg=null)
        {
            if (trigger != null && trigger.CurTarget != null)
            {
                //对当前 target 造成伤害
                EntityQizi caster = GetDamageCaster();
                if (caster != null)
                {
                    if (CurDamageComputeType == DamageComputeType.CommonDamage)
                    {
                        
                    }
                }
            }
        }

        private EntityQizi GetDamageCaster()
        {
            if (ParentTrigger != null)
            {
                return ParentTrigger.ParentTriggerList.ParentSkill.Caster;
            }
            return null;
        }
        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)CurDamageComputeType);
            ParamInt1.WriteToFile(writer);
            ParamInt2.WriteToFile(writer);
            ParamInt3.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurDamageComputeType = (DamageComputeType)reader.ReadInt32();
            ParamInt1.ReadFromFile(reader);
            ParamInt2.ReadFromFile(reader);
            ParamInt3.ReadFromFile(reader);
        }

        public override void Clone(CommandBase copy)
        {
            base.Clone(copy);
            if (copy is CommandCauseDamage copyDamage)
            {
                copyDamage.CurDamageComputeType = CurDamageComputeType;
                ParamInt1.Clone(copyDamage.ParamInt1);
                ParamInt2.Clone(copyDamage.ParamInt2);
                ParamInt3.Clone(copyDamage.ParamInt3);
            }
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            base.SetSkillValue(dataTable);
            ParamInt1.SetSkillValue(dataTable);
            ParamInt2.SetSkillValue(dataTable);
            ParamInt3.SetSkillValue(dataTable);
        }
    }
}