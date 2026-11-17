using System.Collections.Generic;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace Entity
{
    internal sealed class TimelineUndertow
    {
        internal const int WindupMs = 1200;
        private const float Length = 3.5f;
        private const float HalfWidth = .6f;
        internal const float TravelSeconds = .40f;
        private readonly HashSet<EntityQizi> contacted = new();
        private readonly Vector3 origin;
        private readonly Vector3 forward;
        private readonly Vector3 side;
        private LineRenderer warning, crest;
        private float front;
        private bool finished;
        private bool playedImpact;

        private TimelineUndertow(EntityQizi owner, Vector3 forward)
        {
            origin = owner.LogicPosition;
            this.forward = forward;
            side = Vector3.Cross(Vector3.up, forward);
            if (owner.GObj != null)
                warning = HotGameEntry.HeroManager.BurnVisuals?.WaterWarning(owner.GObj.transform,
                    origin, forward, side, Length, HalfWidth);
        }

        internal static TimelineUndertow Begin(EntityQizi owner)
        {
            if (!owner.UsesUndertow || ++owner.UndertowAttackCount % 3 != 0) return null;
            var target = owner.CurAttackTarget;
            if (target == null || !target.IsValid) return null;
            var direction = target.LogicPosition - owner.LogicPosition;
            direction.y = 0f;
            return direction.sqrMagnitude > .0001f ? new TimelineUndertow(owner, direction.normalized) : null;
        }

        internal void Release(EntityQizi owner)
        {
            TimelineRegionalGuardianCast.ClearWarning(ref warning);
            if (owner.GObj != null)
                crest = HotGameEntry.HeroManager.BurnVisuals?.ReleaseUndertow(owner.GObj.transform,
                    origin, forward, Length, HalfWidth);
        }

        internal void Advance(EntityQizi owner, Skill skill, float secondsSinceRelease)
        {
            if (finished || !owner.IsValid) return;
            float progress = Mathf.Clamp01(secondsSinceRelease / TravelSeconds);
            float nextFront = Length * progress;
            if (crest != null)
                TimelineBurnVisuals.UpdateWaterCrest(crest, origin + forward * nextFront, side, HalfWidth, progress);

            var battle = HotGameEntry.HeroManager;
            var visuals = owner.GObj != null ? battle.BurnVisuals : null;
            float damage = (int)owner.GetAttribute(AttributeType.AttackDamage).GetFinalValue() * 1.35f;
            var enemies = ListPool<EntityQizi>.Get();
            try
            {
                battle.GetEntityQiziList(CampType.Enemy, owner.BelongCamp, ref enemies);
                foreach (var target in enemies)
                {
                    if (!target.IsValid || contacted.Contains(target)) continue;
                    foreach (var point in battle.GetRangePoints(target))
                    {
                        var offset = point - origin;
                        float along = Vector3.Dot(offset, forward);
                        if (along < front || along > nextFront || Mathf.Abs(Vector3.Dot(offset, side)) > HalfWidth)
                            continue;

                        contacted.Add(target);
                        if (TimelineRegionalGuardianCast.Hit(owner, target, skill, damage))
                        {
                            visuals?.WaterSplash(point);
                            if (!playedImpact)
                            {
                                visuals?.WaterImpact(point);
                                playedImpact = true;
                            }
                        }
                        break;
                    }
                }
            }
            finally { ListPool<EntityQizi>.Release(enemies); }
            front = nextFront;
            if (progress < 1f) return;
            finished = true;
            TimelineRegionalGuardianCast.ClearWarning(ref crest);
        }

        internal void Clear()
        {
            TimelineRegionalGuardianCast.ClearWarning(ref warning);
            TimelineRegionalGuardianCast.ClearWarning(ref crest);
        }
    }
}
