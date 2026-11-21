using System.Collections.Generic;
using System.Linq;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    // Timed damage advances on combat ticks, also used in forecasts without a scene.
    internal sealed class SummonedArt
    {
        internal const int Cloud = 9210, Mirror = 9211, EmberStamp = 9212, TideChime = 9213;
        internal const float BeamHalfWidth = .42f, ChargeSeconds = .8f, BeamSeconds = .4f, AfterglowSeconds = .3f;
        private sealed class Cast
        {
            internal Vector3 CloudCenter;
            internal Vector3 Origin, Direction;
            internal Transform Visual;
            internal HashSet<int> BeamHits;
            internal LineRenderer Beam;
            internal float Age;
            internal int Strikes;
        }

        private readonly Skill skill;
        private readonly int attackPercent;
        private readonly List<Cast> casts = new();
        internal SummonedArt(Skill skill, int attackPercent)
        {
            this.skill = skill;
            this.attackPercent = attackPercent;
        }

        internal static bool Handles(int id) => id is Cloud or Mirror or EmberStamp or TideChime;
        private static bool Alive(EntityQizi unit) => unit != null && unit.IsValid &&
            (int)unit.GetAttribute(AttributeType.Hp).GetFinalValue() > 0;

        internal bool TryBegin()
        {
            var owner = skill.Caster;
            var manager = HotGameEntry.HeroManager;
            float reach = skill.SkillRange / 1000f;
            float best = reach * reach;
            EntityQizi target = null;
            Vector3 aim = default;
            foreach (var enemy in manager.GetEnemyList(owner.BelongCamp))
            {
                if (!Alive(enemy)) continue;
                foreach (var point in manager.GetRangePoints(enemy))
                {
                    var delta = point - owner.LogicPosition;
                    delta.y = 0f;
                    float distance = delta.sqrMagnitude;
                    if (distance > best || (distance == best && target != null && enemy.HeroUID >= target.HeroUID)) continue;
                    target = enemy;
                    aim = delta;
                    best = distance;
                }
            }
            if (target == null) return false;
            var cast = new Cast { CloudCenter = owner.LogicPosition + aim, Origin = owner.LogicPosition,
                Direction = aim.sqrMagnitude > .0001f ? aim.normalized : Vector3.forward };
            bool cloud = skill.SkillID == Cloud;
            if (skill.SkillID is EmberStamp or TideChime)
            {
                if (skill.SkillID == EmberStamp && manager.BurnVisuals != null)
                    cast.Visual = manager.BurnVisuals.StoneWarning(manager.BurnVisuals.transform, cast.CloudCenter, 1.6f)?.transform;
                if (skill.SkillID == TideChime && manager.BurnVisuals != null)
                    cast.Visual = manager.BurnVisuals.RaiseTideBell(owner.LogicPosition);
                casts.Add(cast);
                return true;
            }
            if (!cloud)
            {
                cast.BeamHits = new HashSet<int>();
                cast.Origin += Vector3.Cross(Vector3.up, cast.Direction) * .72f;
                cast.Direction = (cast.CloudCenter - cast.Origin).normalized;
            }
            var visuals = manager.BurnVisuals;
            if (owner.GObj != null && visuals != null)
            {
                cast.Visual = visuals.SummonFocus(cloud, cloud ? cast.CloudCenter : cast.Origin, cast.Direction);

            }
            casts.Add(cast);
            return true;
        }

        internal void Tick(float delta)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                var cast = casts[i];
                cast.Age += delta;
                bool cloud = skill.SkillID == Cloud;
                if (skill.SkillID == EmberStamp)
                {
                    if (cast.Age < .65f) continue;
                    Pulse(cast.CloudCenter, 1.6f, true);
                    if (!Alive(skill.Caster)) return;
                }
                else if (skill.SkillID == TideChime)
                {
                    while (cast.Strikes < 2 && cast.Age >= .1f + .9f * cast.Strikes)
                    {
                        cast.Strikes++;
                        Pulse(skill.Caster.LogicPosition, 3f, false);
                        if (!Alive(skill.Caster)) return;
                    }
                    if (cast.Visual != null)
                        TimelineBurnVisuals.PoseTideBell(cast.Visual, skill.Caster.LogicPosition, cast.Age, cast.Strikes);
                    if (cast.Age < 1.85f) continue;
                }
                else if (cloud)
                {
                    while (cast.Strikes < 3 && cast.Age + .00001f >= (cast.Strikes + 1) * 2f)
                    {
                        cast.Strikes++;
                        StrikeCloud(cast.CloudCenter);
                        if (!Alive(skill.Caster)) return;
                    }
                    if (cast.Strikes < 3) continue;
                }
                else
                {
                    float firingAge = cast.Age - ChargeSeconds;
                    if (cast.Visual != null)
                        HotGameEntry.HeroManager.BurnVisuals.PoseMirror(cast.Visual, cast.Origin, cast.Direction, cast.Age);
                    if (firingAge < 0f) continue;
                    if (firingAge - delta < BeamSeconds)
                    {
                        if (cast.Visual != null && cast.Beam == null)
                            cast.Beam = HotGameEntry.HeroManager.BurnVisuals.MirrorBeam(cast.Visual, cast.Direction, skill.SkillRange / 1000f);
                        if (cast.Beam != null)
                            cast.Beam.widthMultiplier = BeamHalfWidth * 2f * Mathf.SmoothStep(0f, 1f, (BeamSeconds - firingAge) / .18f);
                        FireBeam(cast);
                        if (!Alive(skill.Caster)) return;
                    }
                    if (firingAge < BeamSeconds + AfterglowSeconds) continue;
                }
                Remove(i);
            }
        }

        private void Pulse(Vector3 center, float radius, bool fire)
        {
            var manager = HotGameEntry.HeroManager;
            if (fire) manager.BurnVisuals?.Detonate(center, radius);
            else manager.BurnVisuals?.TideChimePulse(center, radius);
            foreach (var enemy in manager.GetEnemyList(skill.Caster.BelongCamp).ToArray())
            {
                if (!Alive(skill.Caster)) return;
                if (Alive(enemy) && manager.GetRangePoints(enemy).Any(point =>
                    (new Vector2(point.x - center.x, point.z - center.z)).sqrMagnitude <= radius * radius))
                    Hit(enemy);
            }
        }

        private void StrikeCloud(Vector3 center)
        {
            const float radius = 1.1f;
            var manager = HotGameEntry.HeroManager;
            EntityQizi target = null;
            Vector3 impact = center;
            float nearest = radius * radius;
            foreach (var enemy in manager.GetEnemyList(skill.Caster.BelongCamp))
            {
                if (!Alive(enemy)) continue;
                foreach (var point in manager.GetRangePoints(enemy))
                {
                    Vector3 offset = point - center;
                    float distance = offset.x * offset.x + offset.z * offset.z;
                    if (distance > nearest || (distance == nearest && target != null && enemy.HeroUID >= target.HeroUID)) continue;
                    nearest = distance;
                    target = enemy;
                    impact = point;
                }
            }
            manager.BurnVisuals?.LightningStrike(center + Vector3.up * 2f, impact, 0);
            if (target != null) Hit(target);
        }

        private void FireBeam(Cast cast)
        {
            float length = skill.SkillRange / 1000f;
            Vector3 side = Vector3.Cross(Vector3.up, cast.Direction);
            var manager = HotGameEntry.HeroManager;
            foreach (var enemy in manager.GetEnemyList(skill.Caster.BelongCamp).ToArray())
            {
                if (!Alive(skill.Caster)) return;
                if (!Alive(enemy)) continue;
                foreach (var point in manager.GetRangePoints(enemy))
                {
                    Vector3 offset = point - cast.Origin;
                    float along = Vector3.Dot(offset, cast.Direction);
                    if (along < 0 || along > length || Mathf.Abs(Vector3.Dot(offset, side)) > BeamHalfWidth) continue;
                    if (!cast.BeamHits.Add(enemy.HeroUID)) break;
                    if (cast.Visual != null) manager.BurnVisuals.MirrorImpact(point + Vector3.up * 1.2f);
                    Hit(enemy);
                    break;
                }
            }
        }

        private void Hit(EntityQizi target) => TimelineRegionalGuardianCast.Hit(skill.Caster, target, skill,
            (int)skill.Caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue() * (float)attackPercent / 100f);

        private void Remove(int index)
        {
            if (casts[index].Visual != null) Object.Destroy(casts[index].Visual.gameObject);
            casts.RemoveAt(index);
        }

        internal void Clear()
        {
            for (int i = casts.Count - 1; i >= 0; i--) Remove(i);
        }
    }
}
