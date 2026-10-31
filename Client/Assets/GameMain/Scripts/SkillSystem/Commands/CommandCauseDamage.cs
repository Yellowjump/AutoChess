using System.IO;
using Entity;
using GameFramework;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    public class CauseDamageData : IReference
    {
        public EntityQizi Caster;
        public Skill SourceSkill;
        public EntityQizi Target;
        public float DamageValue;
        public bool TriggerDamageRelate;
        public CauseDamageData()
        {
        }

        public void Clear()
        {
            TriggerDamageRelate = true;
            Caster = null;
            SourceSkill = null;
            Target = null;
            DamageValue = 0;
        }
    }

    public class CommandCauseDamage : CommandBase
    {
        public override CommandType CurCommandType => CommandType.CauseDamage;
        public DamageComputeType CurDamageComputeType = DamageComputeType.NormalDamage;
        public bool TriggerDamageRelate = true;
        public TableParamInt ParamInt1;
        public TableParamInt ParamInt2;
        public TableParamInt ParamInt3;


        internal void ApplyDamage(OneTrigger trigger, EntityQizi target,
            EntityQizi caster, float finalDamage, bool triggerDamageRelate)
        {
            ApplyDamage(trigger.ParentTriggerList.ParentSkill, target, caster,
                finalDamage, triggerDamageRelate);
        }

        internal static void ApplyDamage(Skill sourceSkill, EntityQizi target,
            EntityQizi caster, float finalDamage, bool triggerDamageRelate)
        {
            CauseDamageData beforeCauseDamageData = ReferencePool.Acquire<CauseDamageData>();
            beforeCauseDamageData.Caster = caster;
            beforeCauseDamageData.SourceSkill = sourceSkill;
            beforeCauseDamageData.Target = target;
            beforeCauseDamageData.DamageValue = finalDamage;
            beforeCauseDamageData.TriggerDamageRelate = triggerDamageRelate;
            if (triggerDamageRelate)
            {
                caster.OnTrigger(TriggerType.BeforeCauseDamage, beforeCauseDamageData);
                target.OnTrigger(TriggerType.BeforeBeCauseDamage, beforeCauseDamageData);
            }
            target.BeCauseDamage(beforeCauseDamageData);
            ReferencePool.Release(beforeCauseDamageData);
        }

        internal EntityQizi GetDamageCaster()
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
            writer.Write(TriggerDamageRelate);
            ParamInt1.WriteToFile(writer);
            ParamInt2.WriteToFile(writer);
            ParamInt3.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurDamageComputeType = (DamageComputeType)reader.ReadInt32();
            TriggerDamageRelate = reader.ReadBoolean();
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
                copyDamage.TriggerDamageRelate = TriggerDamageRelate;
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

        public override void Clear()
        {
            // ReferencePool.Release(ParamInt1);
            // ReferencePool.Release(ParamInt2);
            // ReferencePool.Release(ParamInt3);
            // ParamInt1 = null;
            // ParamInt2 = null;
            // ParamInt3 = null;
        }
    }
}
