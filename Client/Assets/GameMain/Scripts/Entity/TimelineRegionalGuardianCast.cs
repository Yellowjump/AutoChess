using System.Linq;
using DataTable;
using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    internal sealed class TimelineRegionalGuardianCast
    {
        internal const string BadgerAppearance = "BlackfangBadger";
        internal const string KilnAppearance = "AshKilnMacaque";
        internal const string ContractAppearance = "ContractGolem";
        internal const int BadgerSkill = 9194;
        internal const int KilnSkill = 9195;
        internal const int ContractSkill = 9196;
        internal const int PounceWindupMs = 610;
        internal const int MaxLavaTargets = 3;
        private const float FireLength = 4.6f;
        private const float FireHalfWidth = .77f;
        private const float SealHalfWidth = .81f;

        private readonly int skillId;
        private readonly EntityQizi target;
        private readonly Vector3 origin, forward, side;
        private LineRenderer warning;
        private readonly Vector3[] lavaCenters;
        private readonly LineRenderer[] lavaWarnings;
        internal bool UseLavaPose => lavaCenters != null;
        internal int WindupMs { get; }

        private TimelineRegionalGuardianCast(EntityQizi owner, EntityQizi target, int skillId, bool pounced, Vector3[] lavaCenters = null)
        {
            this.skillId = skillId;
            this.target = target;
            this.lavaCenters = lavaCenters;
            origin = skillId == ContractSkill ? target.LogicPosition : owner.LogicPosition;
            var direction = target.LogicPosition - owner.LogicPosition;
            direction.y = 0f;
            forward = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.forward;
            side = Vector3.Cross(Vector3.up, forward);
            WindupMs = UseLavaPose ? 1150 : skillId switch
            {
                BadgerSkill => pounced ? 130 : 480,
                KilnSkill => 950,
                _ => 820
            };
            var visuals = owner.GObj != null ? HotGameEntry.HeroManager.BurnVisuals : null;
            if (visuals == null || pounced) return;
            if (UseLavaPose)
            {
                lavaWarnings = new LineRenderer[lavaCenters.Length];
                for (int i = 0; i < lavaCenters.Length; i++)
                    lavaWarnings[i] = visuals.LavaWarning(owner.GObj.transform, lavaCenters[i]);
                HotGameEntry.Sound.PlayTimelineCue(EnumSound.KilnRattle, origin);
                return;
            }
            switch (skillId)
            {
                case BadgerSkill:
                    warning = WarnPounce(owner, target);
                    break;
                case KilnSkill:
                    warning = visuals.RegionalWarning("翻炉裂地", owner.GObj.transform,
                        origin, forward, FireLength, FireHalfWidth, TimelineBurnVisuals.KilnWarningColor);
                    HotGameEntry.Sound.PlayTimelineCue(EnumSound.KilnRattle, origin);
                    break;
                case ContractSkill:
                    warning = visuals.RegionalWarning("催契盖印", owner.GObj.transform,
                        origin - forward * SealHalfWidth, forward, SealHalfWidth * 2f, SealHalfWidth,
                        TimelineBurnVisuals.ContractWarningColor);
                    HotGameEntry.Sound.PlayTimelineCue(EnumSound.ContractLift, origin);
                    break;
            }
        }

        internal static TimelineRegionalGuardianCast Begin(EntityQizi owner, Skill skill, bool pounced)
        {
            int id = skill.SkillID;
            if (id is not (BadgerSkill or KilnSkill or ContractSkill or TimelineCaveHazards.Macaque) ||
                owner.CurAttackTarget is not EntityQizi target || !target.IsValid)
                return null;

            int attack = owner.RegionalGuardianAttacks++;
            bool lava = id == KilnSkill && attack % 6 == 0 ||
                id == TimelineCaveHazards.Macaque && attack % 3 == 2;
            if (lava)
            {
                var origin = owner.LogicPosition;
                var centers = HotGameEntry.HeroManager.GetEnemyList(owner.BelongCamp)
                    .Where(enemy => enemy.IsValid)
                    .Select(enemy => enemy.LogicPosition)
                    .Distinct()
                    .Select(center => (Center: center, Distance: HorizontalDistanceSquared(center, origin)))
                    .Where(entry => entry.Distance <= 25f)
                    .OrderBy(entry => entry.Distance)
                    .Take(MaxLavaTargets)
                    .Select(entry => entry.Center)
                    .ToArray();
                if (centers.Length > 0)
                    return new TimelineRegionalGuardianCast(owner, target, id, pounced, centers);
            }

            bool signature = id == BadgerSkill ? attack == 0 : id != TimelineCaveHazards.Macaque && attack % 3 == 0;
            return signature ? new TimelineRegionalGuardianCast(owner, target, id, pounced) : null;
        }

        internal static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        {
            var offset = a - b;
            return offset.x * offset.x + offset.z * offset.z;
        }

        internal static LineRenderer WarnPounce(EntityQizi owner, EntityQizi target)
        {
            var visuals = owner.GObj != null ? HotGameEntry.HeroManager.BurnVisuals : null;
            if (visuals == null || target == null || owner.RegionalPounceWarned) return null;
            var origin = owner.LogicPosition;
            var direction = target.LogicPosition - origin;
            direction.y = 0f;
            float length = direction.magnitude;
            if (length < .01f) return null;
            owner.RegionalPounceWarned = true;
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.BadgerRubble, origin);
            visuals.RegionalDust(origin, direction / length);
            return visuals.RegionalWarning("伏牙追扑", owner.GObj.transform, origin, direction / length,
                Mathf.Min(length, 8.5f), .44f, TimelineBurnVisuals.BadgerWarningColor);
        }

        internal void UpdateWindup(float progress)
        {
            if (lavaWarnings == null) return;
            for (int i = 0; i < lavaWarnings.Length; i++)
                if (lavaWarnings[i] != null)
                    TimelineBurnVisuals.UpdateLavaWarning(lavaWarnings[i], origin, lavaCenters[i], progress);
        }

        internal void Strike(EntityQizi owner, Skill skill)
        {
            Clear();
            if (!owner.IsValid) return;
            var battle = HotGameEntry.HeroManager;
            var visuals = owner.GObj != null ? battle.BurnVisuals : null;
            int attack = (int)owner.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
            if (UseLavaPose)
            {
                battle.CastLavaSplash(owner, skill, lavaCenters, Mathf.Max(1, Mathf.RoundToInt(attack * .32f)));
                if (visuals != null) HotGameEntry.Sound.PlayTimelineCue(EnumSound.KilnCrack, origin);
                return;
            }
            if (skillId == BadgerSkill)
            {
                if (target.IsValid && battle.IsInSkillRange(battle.GetCombatDistanceSquare(owner, target), skill.SkillRange) &&
                    Hit(owner, target, skill, attack * 1.28f))
                    visuals?.RegionalPounceImpact(target.LogicPosition, forward);
                return;
            }

            bool hitAny = false;
            foreach (var enemy in battle.GetEnemyList(owner.BelongCamp))
            {
                if (!enemy.IsValid) continue;
                foreach (var point in battle.GetRangePoints(enemy))
                {
                    if (!Contains(point)) continue;
                    if (Hit(owner, enemy, skill, attack * (skillId == KilnSkill ? 1.16f : 1.08f)))
                    {
                        hitAny = true;
                        if (enemy.IsValid)
                        {
                            if (skillId == KilnSkill) CommandBurnEffect.Apply(owner, enemy, 2, skill);
                            else enemy.ContractSealSeconds = 6f;
                        }
                    }
                    break;
                }
            }
            if (skillId == KilnSkill) visuals?.RegionalKilnImpact(origin, forward, FireLength, FireHalfWidth, hitAny);
            else visuals?.RegionalContractImpact(origin, forward, SealHalfWidth, hitAny);
        }

        private bool Contains(Vector3 point)
        {
            var offset = point - origin;
            float along = Vector3.Dot(offset, forward);
            float across = Mathf.Abs(Vector3.Dot(offset, side));
            return skillId == KilnSkill
                ? along >= -.1f && along <= FireLength && across <= FireHalfWidth
                : Mathf.Abs(along) <= SealHalfWidth && across <= SealHalfWidth;
        }

        internal static bool Hit(EntityQizi caster, EntityQizi target, Skill skill, float damage)
        {
            var hit = ReferencePool.Acquire<CauseDamageData>();
            try
            {
                hit.Caster = caster;
                hit.SourceSkill = skill;
                hit.CombatCast = skill?.CombatCast;
                hit.Target = target;
                hit.DamageValue = damage;
                hit.TriggerDamageRelate = true;
                hit.HasSignatureImpactSound = true;
                caster.OnTrigger(TriggerType.BeforeCauseDamage, hit);
                target.OnTrigger(TriggerType.BeforeBeCauseDamage, hit);
                return target.BeCauseDamage(hit) > 0;
            }
            finally { ReferencePool.Release(hit); }
        }

        internal void Clear()
        {
            ClearWarning(ref warning);
            if (lavaWarnings == null) return;
            for (int i = 0; i < lavaWarnings.Length; i++)
                ClearWarning(ref lavaWarnings[i]);
        }

        internal static void ClearWarning(ref LineRenderer line)
        {
            if (line == null) return;
            line.enabled = false;
            Object.Destroy(line.gameObject);
            line = null;
        }
    }
}
