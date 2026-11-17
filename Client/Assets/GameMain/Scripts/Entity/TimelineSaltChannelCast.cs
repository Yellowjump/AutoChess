using System.Linq;
using DataTable;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;
using WorldTimeline;

namespace Entity
{
    internal sealed class TimelineSaltChannelCast
    {
        private const float Length = 3.7f, HalfWidth = .67f;
        private readonly EntityQizi owner, target;
        private readonly Skill skill;
        private readonly Vector3 origin, forward, side;
        private LineRenderer warning;
        private bool IsBeast => skill.SkillID == SaltChannel.BeastSkillId;
        internal bool UseHeavyPose => IsBeast;
        internal int WindupMs => IsBeast ? 1000 : 560;

        private TimelineSaltChannelCast(EntityQizi owner, EntityQizi target, Skill skill)
        {
            this.owner = owner;
            this.target = target;
            this.skill = skill;
            origin = owner.LogicPosition;
            var direction = target.LogicPosition - origin;
            direction.y = 0f;
            forward = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.forward;
            side = Vector3.Cross(Vector3.up, forward);
            if (owner.GObj != null)
            {
                var visuals = HotGameEntry.HeroManager.BurnVisuals;
                warning = IsBeast
                    ? visuals?.SaltSurgeWarning(owner.GObj.transform, origin, forward, side, Length, HalfWidth, owner.SaltChannelDrained)
                    : visuals?.WaterWarning(owner.GObj.transform, origin, forward, side, skill.SkillRange / 1000f, .44f);
            }
        }

        internal void UpdateWindup(float progress)
        {
            if (IsBeast && warning != null)
                TimelineBurnVisuals.UpdateSaltSurgeWarning(warning, progress, owner.SaltChannelDrained);
        }

        internal static bool IsGuardianSkill(int id) => id is SaltChannel.CrabSkillId or SaltChannel.BeastSkillId;

        internal static TimelineSaltChannelCast Begin(EntityQizi owner, Skill skill)
        {
            if (!IsGuardianSkill(skill.SkillID) || ++owner.SaltGuardianAttacks % 3 != 0 ||
                owner.CurAttackTarget is not EntityQizi target || !target.IsValid) return null;
            return new TimelineSaltChannelCast(owner, target, skill);
        }

        internal void Strike()
        {
            Clear();
            if (!owner.IsValid) return;
            var battle = HotGameEntry.HeroManager;
            var visuals = owner.GObj != null ? battle.BurnVisuals : null;
            var attack = (int)owner.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
            if (!IsBeast)
            {
                if (target.IsValid && battle.IsInSkillRange(battle.GetCombatDistanceSquare(owner, target), skill.SkillRange))
                    Hit(target, attack, true);
                return;
            }

            bool drained = owner.SaltChannelDrained;
            visuals?.SaltSurgeRelease(origin, forward, side, Length, HalfWidth, drained);
            Vector3? impact = null;
            foreach (var enemy in battle.GetEnemyList(owner.BelongCamp))
            {
                if (!enemy.IsValid) continue;
                foreach (var point in battle.GetRangePoints(enemy))
                {
                    var delta = point - origin;
                    var distance = Vector3.Dot(delta, forward);
                    if (distance < 0f || distance > Length || Mathf.Abs(Vector3.Dot(delta, side)) > HalfWidth) continue;
                    var position = enemy.LogicPosition;
                    if (Hit(enemy, attack * 1.10f, !drained)) impact ??= position;
                    break;
                }
            }
            if (impact is Vector3 contact) visuals?.SaltSurgeImpact(contact, forward, drained);
        }

        private bool Hit(EntityQizi unit, float damage, bool pull)
        {
            var position = unit.LogicPosition;
            bool hit = TimelineRegionalGuardianCast.Hit(owner, unit, skill, damage);
            bool pulled = pull && unit.IsValid && HotGameEntry.HeroManager.TryPullOneCell(unit, owner);
            if (owner.GObj == null) return hit || pulled;
            var visuals = HotGameEntry.HeroManager.BurnVisuals;
            if (pulled)
            {
                unit.PlaySaltPullSlide(position);
                visuals?.SaltPullTrail(position, unit.LogicPosition);
            }
            if (hit || pulled) visuals?.WaterSplash(position);
            if (!IsBeast && (hit || pulled)) visuals?.WaterImpact(position);
            return hit || pulled;
        }

        internal void Clear() => TimelineRegionalGuardianCast.ClearWarning(ref warning);

        internal static void ShieldBeforeFirstDirectHit(EntityQizi target, CauseDamageData damage)
        {
            if (target.SaltShieldUsed || !target.IsValid || damage.Caster == null ||
                damage.Caster.BelongCamp == target.BelongCamp || !damage.TriggerDamageRelate ||
                damage.DamageValue < 1f) return;
            var source = target.PassiveSkillList.FirstOrDefault(skill => skill.SkillID == SaltChannel.RewardItemId);
            if (source == null) return;
            var row = GameEntry.DataTable.GetDataTable<DRBuff>("Buff").GetDataRow(10503);
            var template = row == null ? null : GameEntry.DataTable.GetDataTable<DRBuffTemplate>("BuffTemplate").GetDataRow(row.TemplateID);
            if (template == null) return;
            var shield = SkillFactory.CreateNewBuff();
            template.BuffTemplate.Clone(shield);
            shield.BuffID = row.Id;
            shield.Owner = target;
            shield.ParentSkill = source;
            shield.DurationMs = row.Duration;
            shield.RemainMs = row.Duration;
            shield.MaxLayerNum = row.MaxLayerNum;
            shield.SetSkillValue(row);
            target.SaltShieldUsed = true;
            target.AddBuff(shield);
        }
    }
}
