using System.IO;
using Entity;
using GameFramework;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CauseDamageData : IReference
    {
        public EntityQizi Caster;
        public Skill SourceSkill;
        public EntityQizi Target;
        public DamageType CurDamageType;
        public float DamageValue;

        public CauseDamageData()
        {
        }

        public void Clear()
        {
            Caster = null;
            SourceSkill = null;
            Target = null;
            CurDamageType = DamageType.PhysicalDamage;
            DamageValue = 0;
        }
    }

    public class CommandCauseDamage : CommandBase
    {
        public override CommandType CurCommandType => CommandType.CauseDamage;
        public DamageComputeType CurDamageComputeType = DamageComputeType.NormalDamage;
        public DamageType CurDamageType;
        public TableParamInt ParamInt1;
        public TableParamInt ParamInt2;
        public TableParamInt ParamInt3;

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.CurTargetList != null && trigger.CurTargetList.Count > 0)
            {
                foreach (var oneTarget in trigger.CurTargetList)
                {
                    EntityQizi target = oneTarget as EntityQizi;
                    if (target == null || target.IsValid == false)
                    {
                        continue;
                    }
                    //对当前 target 造成伤害
                    EntityQizi caster = GetDamageCaster();
                    float damageParam = 0;
                    float finalDamage = 0;
                    if (caster != null)
                    {
                        if (CurDamageComputeType == DamageComputeType.NormalDamage)
                        {
                            damageParam = (int)caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
                        }
                        else if (CurDamageComputeType == DamageComputeType.FixNumAddAttrPercent)
                        {
                            var fixValue = ParamInt1.Value;
                            var attrValue = 0f;
                            if (ParamInt3.Value > 0)
                            {
                                var attr = (int)caster.GetAttribute((AttributeType)ParamInt2.Value).GetFinalValue();
                                attrValue = attr * ParamInt3.Value / 100;
                            }

                            damageParam = fixValue + attrValue;
                        }
                    }

                    if (CurDamageType == DamageType.PhysicalDamage)
                    {
                        var armor = (int)target.GetAttribute(AttributeType.Armor).GetFinalValue();
                        var app = (int)target.GetAttribute(AttributeType.ArmorPenetrationPercent).GetFinalValue();
                        var appn = armor * app / 100f;
                        var apn = (int)target.GetAttribute(AttributeType.ArmorPenetrationNum).GetFinalValue();
                        finalDamage = 100 / (armor - appn - apn + 100) * damageParam;
                    }
                    else if (CurDamageType == DamageType.MagicDamage)
                    {
                        var magicResist = (int)target.GetAttribute(AttributeType.MagicResist).GetFinalValue();
                        var mpp = (int)target.GetAttribute(AttributeType.MagicPenetrationPercent).GetFinalValue();
                        var mppn = magicResist * mpp / 100f;
                        var mpn = (int)target.GetAttribute(AttributeType.MagicPenetrationNum).GetFinalValue();
                        finalDamage = 100 / (magicResist - mppn - mpn + 100) * damageParam;
                    }
                    else
                    {
                        finalDamage = damageParam;
                    }

                    CauseDamageData beforeCauseDamageData = ReferencePool.Acquire<CauseDamageData>();
                    beforeCauseDamageData.Caster = caster;
                    beforeCauseDamageData.SourceSkill = trigger.ParentTriggerList.ParentSkill;
                    beforeCauseDamageData.Target = target;
                    beforeCauseDamageData.CurDamageType = CurDamageType;
                    beforeCauseDamageData.DamageValue = finalDamage;
                    caster.OnTrigger(TriggerType.BeforeCauseDamage, beforeCauseDamageData);
                    target.OnTrigger(TriggerType.BeforeBeCauseDamage, beforeCauseDamageData);
                    target.BeCauseDamage(beforeCauseDamageData);
                    ReferencePool.Release(beforeCauseDamageData);
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
            writer.Write((int)CurDamageType);
            ParamInt1.WriteToFile(writer);
            ParamInt2.WriteToFile(writer);
            ParamInt3.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurDamageComputeType = (DamageComputeType)reader.ReadInt32();
            CurDamageType = (DamageType)reader.ReadInt32();
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
                copyDamage.CurDamageType = CurDamageType;
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
            ReferencePool.Release(ParamInt1);
            ReferencePool.Release(ParamInt2);
            ReferencePool.Release(ParamInt3);
            ParamInt1 = null;
            ParamInt2 = null;
            ParamInt3 = null;
        }
    }
}