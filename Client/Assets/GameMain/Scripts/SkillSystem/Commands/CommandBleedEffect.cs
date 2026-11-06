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
            switch (Mode)
            {
                case Effect.ApplyBleed:
                    if (hit.SourceSkill?.IsBasicAttack == true && (int)hit.Target.GetAttribute(AttributeType.Hp).GetFinalValue() > 0)
                        ApplyBleed(hit);
                    break;
                case Effect.Amplify:
                    if (hit.Target.HasBleeding)
                        hit.DamageValue *= 1f + Param.Value / 100f;
                    break;
                case Effect.Heal:
                    if (hit.TargetWasBleeding && hit.HealthLost > 0 && hit.Caster.IsValid)
                        hit.Caster.GetAttribute(AttributeType.Hp).AddNum((int)(hit.HealthLost * (long)Param.Value / 100));
                    break;
            }
        }

        private void ApplyBleed(CauseDamageData hit)
        {
            var rows = GameEntry.DataTable.GetDataTable<DRBuff>("Buff");
            var templates = GameEntry.DataTable.GetDataTable<DRBuffTemplate>("BuffTemplate");
            var row = rows[Param.Value];
            var bleed = hit.Target.FindBleedingBuff(row.Id);
            bool isNew = bleed == null;
            if (isNew)
            {
                bleed = SkillFactory.CreateNewBuff();
                templates[row.TemplateID].BuffTemplate.Clone(bleed);
                bleed.BuffID = row.Id;
                bleed.DurationMs = row.Duration;
                bleed.Owner = hit.Target;
            }
            int attack = (int)hit.Caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
            bleed.AddDamageLayer(Math.Max(1, (int)(attack * (long)row.IntParam1 / 100)));
            if (isNew) hit.Target.AddBuff(bleed);
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
