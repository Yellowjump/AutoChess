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
        internal WorldTimeline.CombatCast CombatCast;
        internal bool CriticalHit;
        public EntityQizi Target;
        public float DamageValue;
        public bool TriggerDamageRelate;
        public int HealthLost;
        internal bool SharedLoss;
        public int BrokenShieldValue;
        public bool TargetWasBleeding;
        public bool TargetWasWeakened;
        public int BurnStacksBeforeHit;
        public int OpeningStacksBeforeHit;
        public bool BurnDetonated;
        public bool HasSignatureImpactSound;
        public CauseDamageData()
        {
        }

        public void Clear()
        {
            TriggerDamageRelate = true;
            Caster = null;
            SourceSkill = null;
            CombatCast = null;
            CriticalHit = false;
            Target = null;
            DamageValue = 0;
            HealthLost = 0; SharedLoss = false;
            BrokenShieldValue = 0;
            TargetWasBleeding = false;
            TargetWasWeakened = false;
            BurnStacksBeforeHit = 0;
            OpeningStacksBeforeHit = 0;
            BurnDetonated = false;
            HasSignatureImpactSound = false;
        }
    }

    public class CommandCauseDamage : CommandBase
    {
        internal static int DealSecondaryDamage(EntityQizi target, long damage, Skill source = null, bool sharedLoss = false)
        {
            if (damage <= 0) return 0;
            var hit = ReferencePool.Acquire<CauseDamageData>();
            try
            {
                hit.Target = target;
                hit.SharedLoss = sharedLoss;
                hit.DamageValue = System.Math.Min(damage, 1000000000L);
                hit.TriggerDamageRelate = false;
                int effective = target.BeCauseDamage(hit);
                EntityQizi.RecordBattleOutput(source, damage: effective);
                return effective;
            }
            finally { ReferencePool.Release(hit); }
        }

        public override CommandType CurCommandType => CommandType.CauseDamage;
        public DamageComputeType CurDamageComputeType = DamageComputeType.NormalDamage;
        public bool TriggerDamageRelate = true;
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
                    finalDamage = damageParam;

                    CauseDamageData beforeCauseDamageData = ReferencePool.Acquire<CauseDamageData>();
                    beforeCauseDamageData.Caster = caster;
                    beforeCauseDamageData.SourceSkill = trigger.ParentTriggerList.ParentSkill;
                    beforeCauseDamageData.CombatCast = trigger.ParentTriggerList.CombatCast;
                    beforeCauseDamageData.Target = target;
                    beforeCauseDamageData.DamageValue = finalDamage;
                    beforeCauseDamageData.TriggerDamageRelate = TriggerDamageRelate;
                    if (TriggerDamageRelate)
                    {
                        caster.OnTrigger(TriggerType.BeforeCauseDamage, beforeCauseDamageData);
                        target.OnTrigger(TriggerType.BeforeBeCauseDamage, beforeCauseDamageData);    
                    }
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
