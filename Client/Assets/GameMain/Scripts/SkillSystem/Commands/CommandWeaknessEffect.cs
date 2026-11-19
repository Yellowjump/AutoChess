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
        internal const int OpeningBuffId = 30402;
        public enum Effect { Weaken, Mark, Strike, Retaliate }
        public override CommandType CurCommandType => CommandType.WeaknessEffect;
        public Effect Mode;
        public TableParamInt Param;
        private bool basicAttackCombo;

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (arg is not CauseDamageData hit || hit.DamageValue <= 0) return;
            var source = trigger.ParentTriggerList.ParentSkill;
            var caster = source.Caster;
            if (caster == null || !caster.IsValid) return;
            if (Mode == Effect.Retaliate)
            {
                if (hit.Target == caster && hit.Caster != null && hit.Caster.IsValid &&
                    hit.Caster.BelongCamp != caster.BelongCamp)
                    CommandCauseDamage.DealSecondaryDamage(hit.Caster, (long)hit.BrokenShieldValue * Param.Value / 100, source);
                return;
            }

            var target = hit.Target;
            if (hit.Caster != caster || !target.IsValid || target.BelongCamp == caster.BelongCamp ||
                (int)target.GetAttribute(AttributeType.Hp).GetFinalValue() <= 0) return;

            switch (Mode)
            {
                case Effect.Weaken:
                    if (basicAttackCombo && hit.SourceSkill?.IsBasicAttack == true)
                    {
                        ApplyStatus(target, BuffTag.Weakened, WeaknessBuffId);
                        var existingOpening = target.FindStatusBuff(BuffTag.Exposed, OpeningBuffId);
                        if (hit.OpeningStacksBeforeHit == 0 ||
                            (existingOpening != null && hit.OpeningStacksBeforeHit < existingOpening.MaxLayerNum))
                            ApplyStatus(target, BuffTag.Exposed, OpeningBuffId);
                    }
                    else if (!basicAttackCombo && hit.SourceSkill?.IsActiveDamageSkill == true)
                        ApplyStatus(target, BuffTag.Weakened, WeaknessBuffId);
                    break;
                case Effect.Mark:
                    if (hit.SourceSkill?.IsBasicAttack == true && target.FindStatusBuff(BuffTag.Weakened, WeaknessBuffId) != null)
                        ApplyStatus(target, BuffTag.Exposed, OpeningBuffId);
                    break;
                case Effect.Strike:
                    var opening = target.FindStatusBuff(BuffTag.Exposed, OpeningBuffId);
                    if (opening != null && (basicAttackCombo
                        ? hit.SourceSkill?.IsBasicAttack == true && hit.OpeningStacksBeforeHit >= opening.MaxLayerNum
                        : hit.SourceSkill?.IsActiveDamageSkill == true && opening.StackCount >= opening.MaxLayerNum))
                    {
                        opening.OnDestory();
                        int attack = (int)caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
                        long damage = (long)attack * Param.Value / 100;
                        if (damage > 0)
                        {
                            var visuals = HotGameEntry.HeroManager.BurnVisuals;
                            if (basicAttackCombo) visuals?.OpeningSealStamped(target.LogicPosition);
                            else visuals?.OpeningStruck(target.LogicPosition);
                        }
                        CommandCauseDamage.DealSecondaryDamage(target, damage, source);
                    }
                    break;
            }
        }

        internal static void OnFalterMirrorHit(EntityQizi attacker, float elapsed, bool front, WorldTimeline.ArtifactBattleOutput output)
        {
            bool alreadyWeak = attacker.FindStatusBuff(BuffTag.Weakened, WeaknessBuffId) != null;
            output.ResolveFalterMirror(elapsed, front, alreadyWeak,
                () => ApplyStatus(attacker, BuffTag.Weakened, WeaknessBuffId),
                () => ApplyStatus(attacker, BuffTag.Exposed, OpeningBuffId));
        }

        private static bool ApplyStatus(EntityQizi target, BuffTag tag, int buffId)
        {
            var existing = target.FindStatusBuff(tag, buffId);
            if (existing != null)
            {
                int before = existing.StackCount;
                existing.AddMarkLayer();
                return existing.StackCount > before;
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
            return true;
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
        public override void SetSkillValue(DataRowBase row)
        {
            Param.SetSkillValue(row);
            basicAttackCombo = row is DRSkill skill && skill.IntParam2 == 1;
        }
        public override void Clone(CommandBase copy)
        {
            if (copy is not CommandWeaknessEffect effect) return;
            effect.Mode = Mode;
            effect.basicAttackCombo = basicAttackCombo;
            Param.Clone(effect.Param);
        }
    }
}
