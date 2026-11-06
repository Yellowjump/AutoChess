using System.IO;
using DataTable;
using Entity;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public sealed class CommandWeaknessEffect : CommandBase
    {
        public const int WeaknessBuffId = 30401;
        private const int OpeningBuffId = 30402;
        public enum Effect { Weaken, Mark, Strike, Retaliate }
        public override CommandType CurCommandType => CommandType.WeaknessEffect;
        public Effect Mode;
        public TableParamInt Param;

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (arg is not CauseDamageData hit || hit.DamageValue <= 0) return;
            var caster = trigger.ParentTriggerList.ParentSkill.Caster;
            if (caster == null || !caster.IsValid) return;
            if (Mode == Effect.Retaliate)
            {
                if (hit.Target == caster && hit.Caster != null && hit.Caster.IsValid &&
                    hit.Caster.BelongCamp != caster.BelongCamp)
                    CommandCauseDamage.DealSecondaryDamage(hit.Caster, (long)hit.BrokenShieldValue * Param.Value / 100);
                return;
            }

            var target = hit.Target;
            if (hit.Caster != caster || !target.IsValid || target.BelongCamp == caster.BelongCamp ||
                (int)target.GetAttribute(AttributeType.Hp).GetFinalValue() <= 0) return;

            switch (Mode)
            {
                case Effect.Weaken:
                    if (hit.SourceSkill?.IsActiveDamageSkill == true)
                        ApplyStatus(target, BuffTag.Weakened, WeaknessBuffId);
                    break;
                case Effect.Mark:
                    if (hit.SourceSkill?.IsBasicAttack == true && target.FindStatusBuff(BuffTag.Weakened, WeaknessBuffId) != null)
                        ApplyStatus(target, BuffTag.Exposed, OpeningBuffId);
                    break;
                case Effect.Strike:
                    var opening = target.FindStatusBuff(BuffTag.Exposed, OpeningBuffId);
                    if (hit.SourceSkill?.IsActiveDamageSkill == true && opening != null &&
                        opening.StackCount >= opening.MaxLayerNum)
                    {
                        opening.OnDestory();
                        int attack = (int)caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
                        CommandCauseDamage.DealSecondaryDamage(target, (long)attack * Param.Value / 100);
                    }
                    break;
            }
        }

        private static void ApplyStatus(EntityQizi target, BuffTag tag, int buffId)
        {
            var existing = target.FindStatusBuff(tag, buffId);
            if (existing != null)
            {
                existing.AddMarkLayer();
                return;
            }
            var row = GameEntry.DataTable.GetDataTable<DRBuff>("Buff")[buffId];
            var template = GameEntry.DataTable.GetDataTable<DRBuffTemplate>("BuffTemplate")[row.TemplateID];
            var buff = SkillFactory.CreateNewBuff();
            template.BuffTemplate.Clone(buff);
            buff.BuffID = buffId;
            buff.Owner = target;
            buff.DurationMs = row.Duration;
            buff.RemainMs = row.Duration;
            buff.MaxLayerNum = row.MaxLayerNum;
            buff.FreshOtherBuffDuration = row.FreshDuration;
            buff.paramInt = Mathf.Clamp(row.IntParam1, 0, 100);
            target.AddBuff(buff);
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
            if (copy is not CommandWeaknessEffect effect) return;
            effect.Mode = Mode;
            Param.Clone(effect.Param);
        }
    }
}
