using System;
using System.Collections.Generic;
using DataTable;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;
using WorldTimeline;

namespace Entity
{
    internal sealed class TimelineOuterGuardianCast
    {
        internal const int Eel = 9197, Rope = 9198, Lantern = 9199, Marten = 9200,
            Kite = 9201, Crab = 9202, Clerk = 9203;
        internal const int ClerkBasicAttack = 9190;
        internal const float ShellDuration = 2.1f;
        internal const int LanternHitsToInterrupt = 3;
        internal const string ClerkAppearance = "NamelessClerk";

        private static readonly (int Node, string Appearance, int Skill)[] Guardians =
        {
            (HexWorldMap.Id(9,-7), "SaltspineEel", Eel),
            (HexWorldMap.Id(-3,8), "RopeApe", Rope),
            (HexWorldMap.Id(2,7), "LanternWatch", Lantern),
            (HexWorldMap.Id(3,7), "SnowMarten", Marten),
            (HexWorldMap.Id(8,-2), "CliffKite", Kite),
            (HexWorldMap.Id(6,2), "TideboneCrab", Crab),
            (HexWorldMap.Id(-8,2), ClerkAppearance, Clerk)
        };

        private readonly EntityQizi owner, target;
        private readonly Skill skill;
        private readonly int kind;
        private readonly Vector3 origin, forward, side, aim;
        private readonly float length, halfWidth;
        private LineRenderer warning;
        private int needles;
        private float nextNeedle;

        internal int WindupMs => kind switch
        {
            Eel => owner.IsCoastalShooter ? 500 : 660, Rope => 740, Lantern => 1850, Marten => 570,
            Kite => 1010, Crab => 600, Clerk => owner.PaperShadowWaves == 0 ? 890 : 420, _ => 890
        };
        internal bool UseSignaturePose => kind is Lantern or Crab or Marten;
        internal int RecoveryMs => kind == Crab ? 2450 : kind == Eel ? 630 : 460;
        internal static bool IsSkill(int id) => id >= Eel && id <= Clerk;

        internal static string AppearanceAt(int node)
        {
            foreach (var entry in Guardians)
                if (entry.Node == node) return entry.Appearance;
            return null;
        }

        internal static int SkillFor(string appearance)
        {
            foreach (var entry in Guardians)
                if (entry.Appearance == appearance) return entry.Skill;
            return 0;
        }

        private TimelineOuterGuardianCast(EntityQizi owner, EntityQizi target, Skill skill)
        {
            this.owner = owner;
            this.target = target;
            this.skill = skill;
            kind = skill.SkillID;
            origin = owner.LogicPosition;
            aim = target.LogicPosition;
            var direction = aim - origin;
            direction.y = 0f;
            forward = direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward;
            side = Vector3.Cross(Vector3.up, forward);
            length = Mathf.Max(.35f, direction.magnitude);
            if (kind == Marten) aim = origin + forward * .76f;
            halfWidth = kind switch { Kite => 1.45f, Marten => .76f, _ => .30f };
            if (kind == Lantern) owner.LanternHitsLeft = LanternHitsToInterrupt;
            if (owner.GObj == null || HotGameEntry.HeroManager.BurnVisuals is not TimelineBurnVisuals visuals) return;
            warning = visuals.OuterWarning(kind, owner.GObj.transform, origin, aim, forward, length, halfWidth);
            HotGameEntry.Sound.PlayTimelineCue(Cue(kind, false), origin);
        }

        internal static TimelineOuterGuardianCast Begin(EntityQizi owner, Skill skill)
        {
            int kind = skill.SkillID;
            if (!IsSkill(kind) || owner.CurAttackTarget is not EntityQizi target || !target.IsValid) return null;
            int attack = owner.OuterAttackCount++;
            switch (kind)
            {
                case Lantern:
                    if (owner.OuterGuardianUsed ||
                        (int)owner.GetAttribute(AttributeType.Hp).GetFinalValue() >
                        (int)owner.GetAttribute(AttributeType.MaxHp).GetFinalValue() * .55f) return null;
                    owner.OuterGuardianUsed = true;
                    break;
                case Clerk:
                    break;
                case Marten:
                    if (owner.MartenMeleeHits < 2) return null;
                    owner.MartenMeleeHits = 0;
                    break;
                default:
                    if (!owner.IsCoastalShooter && attack % 3 != 0) return null;
                    break;
            }
            return new TimelineOuterGuardianCast(owner, target, skill);
        }

        internal static void TrackHit(EntityQizi victim, CauseDamageData hit)
        {
            if (!hit.TriggerDamageRelate || hit.Caster == null || hit.Caster.BelongCamp == victim.BelongCamp) return;
            if (victim.LanternHitsLeft > 0) victim.LanternHitsLeft--;
            if (victim.TimelineAppearance == "SnowMarten")
            {
                bool melee = hit.SourceSkill != null && hit.SourceSkill.SkillRange <= 1300 &&
                    HotGameEntry.HeroManager.GetCombatDistanceSquare(victim, hit.Caster) <= 2.5f;
                victim.MartenMeleeHits = melee ? Math.Min(victim.MartenMeleeHits + 1, 2) : 0;
            }
        }

        internal static bool ShellBlocks(EntityQizi victim, CauseDamageData hit)
        {
            if (victim.OuterShellSeconds <= 0f || !hit.TriggerDamageRelate || hit.Caster == null ||
                hit.Caster.BelongCamp == victim.BelongCamp) return false;
            return Vector3.Dot(victim.OuterShellForward, hit.Caster.LogicPosition - victim.LogicPosition) >= 0f;
        }

        internal void UpdateWindup(float progress)
        {
            if (warning == null) return;
            if (kind is Eel or Rope or Kite or Marten)
                TimelineBurnVisuals.UpdateOuterAreaWarning(warning, kind, aim, halfWidth, progress);
            else if (kind == Lantern)
                TimelineBurnVisuals.UpdateLanternWarning(warning, owner.LogicPosition, owner.LanternHitsLeft, progress);
            else if (kind == Clerk)
                TimelineBurnVisuals.UpdateRegisterWarning(warning, owner.LogicPosition, progress);
            else if (kind == Crab)
                TimelineBurnVisuals.UpdateShellArc(warning, owner.LogicPosition, forward, progress, active: false);
        }

        internal void Strike()
        {
            if (kind != Eel) TimelineRegionalGuardianCast.ClearWarning(ref warning);
            if (!owner.IsValid) return;
            var battle = HotGameEntry.HeroManager;
            var visuals = owner.GObj != null ? battle.BurnVisuals : null;
            int attack = (int)owner.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
            var impact = target.IsValid ? target.LogicPosition : aim;
            switch (kind)
            {
                case Eel:
                    needles = owner.IsCoastalShooter ? 1 : 3;
                    FireNeedle();
                    return;
                case Rope:
                    StrikeArea(attack * 1.05f);
                    visuals?.OuterCorridorImpact(kind, origin, forward, length, halfWidth);
                    if (visuals != null) HotGameEntry.Sound.PlayTimelineCue(Cue(kind, true), aim);
                    return;
                case Lantern:
                    if (owner.LanternHitsLeft > 0)
                    {
                        var hp = owner.GetAttribute(AttributeType.Hp);
                        int max = (int)owner.GetAttribute(AttributeType.MaxHp).GetFinalValue();
                        int restored = Mathf.Clamp(max - (int)hp.GetFinalValue(), 0, Mathf.Max(1, max / 5));
                        hp.AddNum(restored);
                        if (restored > 0) visuals?.LanternHeal(owner);
                        EntityQizi.RecordBattleOutput(skill, healing: restored);
                    }
                    impact = owner.LogicPosition;
                    break;
                case Marten:
                    StrikeArea(attack * 1.3f);
                    visuals?.OuterMartenImpact(aim, forward, halfWidth);
                    if (visuals != null) HotGameEntry.Sound.PlayTimelineCue(Cue(kind, true), aim);
                    return;
                case Kite:
                    StrikeArea(attack * 1.43f);
                    visuals?.OuterKiteImpact(aim, forward, halfWidth);
                    if (visuals != null) HotGameEntry.Sound.PlayTimelineCue(Cue(kind, true), aim);
                    return;
                case Crab:
                    owner.OuterShellForward = forward;
                    owner.OuterShellSeconds = ShellDuration;
                    visuals?.ShowShellGuard(owner);
                    if (visuals != null) HotGameEntry.Sound.PlayTimelineCue(Cue(kind, true), owner.LogicPosition);
                    return;
                case Clerk:
                    bool summoned = false;
                    for (int i = 0; i < 3; i++)
                    {
                        var shadow = battle.SummonPaperShadow(owner, target, aim);
                        if (shadow == null) break;
                        summoned = true;
                        visuals?.PaperSummon(shadow);
                    }
                    if (summoned)
                    {
                        owner.PaperShadowWaves++;
                        if (visuals != null) HotGameEntry.Sound.PlayTimelineCue(Cue(kind, true), origin);
                    }
                    return;
            }
            if (visuals != null)
            {
                bool completed = kind != Lantern || owner.LanternHitsLeft > 0;
                visuals.LanternImpact(impact, completed);
                if (completed) HotGameEntry.Sound.PlayTimelineCue(Cue(kind, true), impact);
            }
            if (kind == Lantern) owner.LanternHitsLeft = 0;
        }

        internal void Advance(float seconds)
        {
            if (needles == 0) return;
            nextNeedle -= seconds;
            while (needles > 0 && nextNeedle <= 0f) FireNeedle();
        }

        private void FireNeedle()
        {
            if (!owner.IsValid)
            {
                needles = 0;
                TimelineRegionalGuardianCast.ClearWarning(ref warning);
                return;
            }

            needles--;
            nextNeedle += .16f;
            float damage = (int)owner.GetAttribute(AttributeType.AttackDamage).GetFinalValue() *
                (owner.IsCoastalShooter ? 1f : .51f);
            StrikeArea(damage);
            if (owner.GObj != null)
            {
                HotGameEntry.HeroManager.BurnVisuals?.OuterCorridorImpact(kind, origin, forward, length, halfWidth);
                HotGameEntry.Sound.PlayTimelineCue(Cue(kind, true), origin);
            }
            if (needles == 0) TimelineRegionalGuardianCast.ClearWarning(ref warning);
        }

        private void StrikeArea(float damage)
        {
            var battle = HotGameEntry.HeroManager;
            foreach (var enemy in battle.GetEnemyList(owner.BelongCamp))
            {
                if (!enemy.IsValid) continue;
                foreach (var point in battle.GetRangePoints(enemy))
                {
                    if (!Contains(point)) continue;
                    Vector3 hitPosition = enemy.LogicHitPosition;
                    if (TimelineRegionalGuardianCast.Hit(owner, enemy, skill, damage))
                    {
                        if (kind == Eel && owner.GObj != null)
                            battle.BurnVisuals?.SaltNeedleHit(hitPosition, forward);
                        if (kind == Rope && enemy.IsValid)
                        {
                            battle.TryPullOneCell(enemy, owner);
                            enemy.RopeSnareSeconds = 3f;
                        }
                    }
                    break;
                }
            }
        }

        private bool Contains(Vector3 point)
        {
            var offset = point - (kind is Kite or Marten ? aim : origin);
            offset.y = 0f;
            if (kind is Kite or Marten) return offset.sqrMagnitude <= halfWidth * halfWidth;
            float along = Vector3.Dot(offset, forward);
            return along >= 0f && along <= length + .0001f && Mathf.Abs(Vector3.Dot(offset, side)) <= halfWidth;
        }

        internal void Clear()
        {
            TimelineRegionalGuardianCast.ClearWarning(ref warning);
            if (kind == Lantern) owner.LanternHitsLeft = 0;
        }

        private static EnumSound Cue(int id, bool impact) => (EnumSound)(2020 + (id - Eel) * 2 + (impact ? 1 : 0));
    }
}
