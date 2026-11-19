using System;
using System.IO;
using DataTable;
using Entity;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public sealed class CommandBleedEffect : CommandBase
    {
        public enum Effect { AttackSpeed, ApplyBleed, Amplify, Heal }

        public override CommandType CurCommandType => CommandType.BleedEffect;
        public Effect Mode;
        public TableParamInt Param;

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (Mode == Effect.AttackSpeed)
            {
                if (arg is Skill attack && attack.IsBasicAttack)
                    attack.BasicAttackSpeedBonus += Math.Max(0, Param.Value);
                return;
            }

            if (arg is not CauseDamageData hit || hit.DamageValue <= 0) return;
            var source = trigger.ParentTriggerList.ParentSkill;
            switch (Mode)
            {
                case Effect.ApplyBleed:
                    if (hit.SourceSkill?.IsBasicAttack == true && (int)hit.Target.GetAttribute(AttributeType.Hp).GetFinalValue() > 0)
                        ApplyBleed(hit.Caster, hit.Target, Param.Value, source);
                    break;
                case Effect.Amplify:
                    if (hit.Target.HasBleeding)
                        hit.DamageValue *= 1f + Param.Value / 100f;
                    break;
                case Effect.Heal:
                    if (hit.TargetWasBleeding && hit.HealthLost > 0 && hit.Caster.IsValid)
                    {
                        var health = hit.Caster.GetAttribute(AttributeType.Hp);
                        int before = (int)health.GetFinalValue();
                        hit.Caster.ReceiveHealing((int)(hit.HealthLost * (long)Param.Value / 100));
                        EntityQizi.RecordBattleOutput(source, healing: (int)health.GetFinalValue() - before);
                    }
                    break;
            }
        }

        internal static void OnMeridianHit(EntityQizi attacker, EntityQizi target, WorldTimeline.ArtifactBattleOutput output)
        {
            const int bleedId = 30201;
            var bleed = target.FindBleedingBuff(bleedId);
            if (bleed != null && bleed.TryConsumeMeridianLayers(out long damage))
            {
                output.MeridianBursts++;
                output.MeridianBurstDamage += CommandCauseDamage.DealSecondaryDamage(target, damage);
                return;
            }
            ApplyBleed(attacker, target, bleedId, artifact: output);
            output.MeridianLayers++;
        }

        private static void ApplyBleed(EntityQizi caster, EntityQizi target, int id, Skill source = null,
            WorldTimeline.ArtifactBattleOutput artifact = null)
        {
            var rows = GameEntry.DataTable.GetDataTable<DRBuff>("Buff");
            var templates = GameEntry.DataTable.GetDataTable<DRBuffTemplate>("BuffTemplate");
            var row = rows[id];
            var bleed = target.FindBleedingBuff(row.Id);
            bool isNew = bleed == null;
            if (isNew)
            {
                bleed = SkillFactory.CreateNewBuff();
                templates[row.TemplateID].BuffTemplate.Clone(bleed);
                bleed.BuffID = row.Id;
                bleed.DurationMs = row.Duration;
                bleed.Owner = target;
            }
            int attack = (int)caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
            bleed.AddDamageLayer(Math.Max(1, (int)(attack * (long)row.IntParam1 / 100)), source, artifact);
            if (isNew)
            {
                target.AddBuff(bleed);
                HotGameEntry.HeroManager.BurnVisuals?.BleedApplied(target.LogicPosition);
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)Mode);
            Param.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            Mode = (Effect)reader.ReadInt32();
            Param.ReadFromFile(reader);
        }

        public override void SetSkillValue(DataRowBase row) => Param.SetSkillValue(row);

        public override void Clone(CommandBase copy)
        {
            if (copy is not CommandBleedEffect effect) return;
            effect.Mode = Mode;
            Param.Clone(effect.Param);
        }
    }
}
