using System.Collections.Generic;
using System.IO;
using Entity;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public sealed class CommandLightningEffect : CommandBase
    {
        public enum Effect { Mark, Transfer, Discharge }
        private const int MaxMarks = 3;
        private const float JumpRangeSquared = 2.5f * 2.5f;
        public override CommandType CurCommandType => CommandType.LightningEffect;
        public Effect Mode;
        public TableParamInt Param;

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            var source = trigger.ParentTriggerList.ParentSkill;
            var caster = source.Caster;
            if (caster == null || !caster.IsValid) return;
            switch (Mode)
            {
                case Effect.Mark:
                    if (arg is not CauseDamageData hit || hit.SourceSkill == null || hit.Target == null ||
                        !hit.Target.IsValid || hit.Target.BelongCamp == caster.BelongCamp || hit.DamageValue <= 0 ||
                        (int)hit.Target.GetAttribute(AttributeType.Hp).GetFinalValue() <= 0) return;
                    bool mainSkill = caster.TechniqueSkillId != 0 ? caster.IsTechniqueSkill(hit.SourceSkill)
                        : hit.SourceSkill.CurSkillType == SkillType.NormalSkill;
                    if (mainSkill && hit.SourceSkill.TryMarkLightningTarget(hit.Target.HeroUID))
                    {
                        if (AddMark(caster, hit.Target, Param.Value)) caster.LightningApplications++;
                        caster.OnTrigger(TriggerType.OnLightningMarked, hit.Target);
                    }
                    break;
                case Effect.Transfer:
                    if (arg is EntityQizi fallen && Nearest(caster, fallen, fallen, null, float.PositiveInfinity) is EntityQizi next)
                    {
                        if (AddMark(caster, next, Param.Value, .23f))
                        {
                            caster.LightningTransfers++;
                            HotGameEntry.HeroManager.BurnVisuals?.LightningTransfer(fallen.LogicPosition, next.LogicPosition);
                        }
                    }
                    break;
                case Effect.Discharge:
                    if (arg is EntityQizi target && target.LightningMarks != null &&
                        target.LightningMarks.TryGetValue(caster.HeroUID, out int marks) && marks >= MaxMarks)
                        Discharge(caster, target, source);
                    break;
            }
        }

        private static bool AddMark(EntityQizi caster, EntityQizi target, int amount, float visualDelay = 0f)
        {
            if (amount <= 0) return false;
            var marks = target.LightningMarks ??= new Dictionary<int, int>();
            marks.TryGetValue(caster.HeroUID, out int count);
            int total = Mathf.Min(MaxMarks, count + amount);
            if (total == count) return false;
            marks[caster.HeroUID] = total;
            HotGameEntry.HeroManager.BurnVisuals?.LightningMark(target.LogicPosition, total, visualDelay);
            return true;
        }

        private void Discharge(EntityQizi caster, EntityQizi target, Skill source)
        {
            target.LightningMarks.Remove(caster.HeroUID);
            // Lock the chain before resolving damage, so deaths and transfers cannot add hops to it.
            var first = Nearest(caster, target, target, null, JumpRangeSquared);
            var second = first == null ? null : Nearest(caster, first, target, first, JumpRangeSquared);
            int attack = (int)caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
            long damage = (long)attack * Param.Value / 100;
            var visuals = HotGameEntry.HeroManager.BurnVisuals;
            visuals?.LightningStrike(target.LogicPosition + Vector3.up * 4f, target.LogicPosition, 0);
            CommandCauseDamage.DealSecondaryDamage(target, damage, source);
            if (first == null) return;
            if (first.IsValid && damage * 2 / 3 > 0)
            {
                visuals?.LightningStrike(target.LogicPosition, first.LogicPosition, 1);
                CommandCauseDamage.DealSecondaryDamage(first, damage * 2 / 3, source);
                caster.LightningJumps++;
            }
            if (second == null) return;
            if (second.IsValid && damage * 2 / 3 * 2 / 3 > 0)
            {
                visuals?.LightningStrike(first.LogicPosition, second.LogicPosition, 2);
                CommandCauseDamage.DealSecondaryDamage(second, damage * 2 / 3 * 2 / 3, source);
                caster.LightningJumps++;
            }
        }

        private static EntityQizi Nearest(EntityQizi caster, EntityQizi from, EntityQizi skip,
            EntityQizi alsoSkip, float maxDistance)
        {
            EntityQizi nearest = null;
            foreach (var enemy in HotGameEntry.HeroManager.GetEnemyList(caster.BelongCamp))
            {
                if (enemy == skip || enemy == alsoSkip || !enemy.IsValid ||
                    (int)enemy.GetAttribute(AttributeType.Hp).GetFinalValue() <= 0) continue;
                Vector3 offset = enemy.LogicPosition - from.LogicPosition;
                float distance = offset.x * offset.x + offset.z * offset.z;
                if (distance > maxDistance || (distance == maxDistance && nearest != null &&
                    enemy.HeroUID > nearest.HeroUID)) continue;
                maxDistance = distance;
                nearest = enemy;
            }
            return nearest;
        }

        internal static void SpreadOnDeath(EntityQizi victim)
        {
            if (victim.LightningMarks == null || victim.LightningMarks.Count == 0) return;
            foreach (var caster in HotGameEntry.HeroManager.GetEnemyList(victim.BelongCamp))
                if (caster.IsValid && victim.LightningMarks.ContainsKey(caster.HeroUID))
                    caster.OnTrigger(TriggerType.OnLightningSpread, victim);
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
            if (copy is not CommandLightningEffect effect) return;
            effect.Mode = Mode;
            Param.Clone(effect.Param);
        }
    }
}
