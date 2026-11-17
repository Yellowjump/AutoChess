using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace Entity
{
    internal sealed class TimelineNamelessSteleCast
    {
        internal const int SkillId = 9193;

        internal enum Pattern { Smash, Line, Seal }

        // Both the warning and the impact outline sample the boundary used for hit checks.
        internal readonly struct ImpactArea
        {
            internal readonly Vector3 Origin, Forward, Side;
            internal readonly float Radius, Length;
            internal int OutlinePoints => Length > 0f ? 4 : 48;

            internal ImpactArea(Vector3 origin, Vector3 forward, float radius, float length = 0f)
            {
                Origin = origin;
                Forward = forward;
                Side = Vector3.Cross(Vector3.up, forward);
                Radius = radius;
                Length = length;
            }

            internal bool Contains(Vector3 point)
            {
                var offset = point - Origin;
                offset.y = 0f;
                if (Length <= 0f) return offset.sqrMagnitude <= Radius * Radius;
                float along = Vector3.Dot(offset, Forward);
                return along >= 0f && along <= Length && Mathf.Abs(Vector3.Dot(offset, Side)) <= Radius;
            }

            internal Vector3 OutlinePoint(int index)
            {
                if (Length > 0f)
                {
                    Vector3 end = index is 1 or 2 ? Forward * Length : Vector3.zero;
                    return Origin + end + Side * (index < 2 ? -Radius : Radius);
                }
                float angle = index * (2f * Mathf.PI / OutlinePoints);
                return Origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Radius;
            }
        }

        private readonly Pattern pattern;
        private readonly ImpactArea area;
        private LineRenderer warning, buildup;

        internal int WindupMs => pattern switch { Pattern.Smash => 650, Pattern.Line => 1050, _ => 1850 };
        internal int RecoveryMs => pattern switch { Pattern.Smash => 450, Pattern.Line => 580, _ => 850 };
        internal bool IsLine => pattern == Pattern.Line;

        private TimelineNamelessSteleCast(EntityQizi owner, EntityQizi target, Pattern pattern)
        {
            this.pattern = pattern;
            var origin = owner.LogicPosition;
            var aim = target.LogicPosition;
            var direction = aim - origin;
            direction.y = 0f;
            var forward = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.forward;
            area = pattern switch
            {
                Pattern.Smash => new ImpactArea(aim, forward, .76f),
                Pattern.Line => new ImpactArea(origin, forward, .82f, 4.4f),
                _ => new ImpactArea(origin, forward, 2.7f)
            };

            if (owner.GObj == null) return;
            var root = owner.GObj.transform;
            if (IsLine) root.rotation = Quaternion.LookRotation(forward);
            var visuals = HotGameEntry.HeroManager.BurnVisuals;
            if (visuals != null)
                warning = visuals.NamelessWarning(root, pattern, area, out buildup);
            if (pattern == Pattern.Seal)
                HotGameEntry.Sound.PlayTimelineCue(DataTable.EnumSound.ContractLift, area.Origin);
        }

        internal static TimelineNamelessSteleCast Begin(EntityQizi owner, Skill skill)
        {
            if (skill.SkillID != SkillId || owner.TimelineAppearance != TimelineMonsterAnimation.NamelessSteleAppearance ||
                owner.CurAttackTarget is not EntityQizi target || !target.IsValid)
                return null;
            int health = (int)owner.GetAttribute(AttributeType.Hp).GetFinalValue();
            int maxHealth = (int)owner.GetAttribute(AttributeType.MaxHp).GetFinalValue();
            var pattern = !owner.NamelessSteleSealed && health <= maxHealth / 2 ? Pattern.Seal
                : (owner.NamelessSteleAttackCount + 1) % 3 == 0 ? Pattern.Line : Pattern.Smash;
            return new TimelineNamelessSteleCast(owner, target, pattern);
        }

        internal void UpdateWindup(float progress)
        {
            if (warning != null)
                TimelineBurnVisuals.UpdateNamelessWarning(warning, buildup, pattern, area, progress);
        }

        internal void Strike(EntityQizi owner, Skill skill)
        {
            Clear();
            if (!owner.IsValid) return;

            if (pattern == Pattern.Seal) owner.NamelessSteleSealed = true;
            else owner.NamelessSteleAttackCount++;

            var battle = HotGameEntry.HeroManager;
            var enemies = ListPool<EntityQizi>.Get();
            bool hitAny = false;
            try
            {
                battle.GetEntityQiziList(CampType.Enemy, owner.BelongCamp, ref enemies);
                int attack = (int)owner.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
                float damage = attack * (pattern switch { Pattern.Smash => 1.25f, Pattern.Line => 1.15f, _ => 1.55f });
                foreach (var enemy in enemies)
                {
                    if (!enemy.IsValid) continue;
                    foreach (var point in battle.GetRangePoints(enemy))
                    {
                        if (!area.Contains(point)) continue;
                        hitAny |= TimelineRegionalGuardianCast.Hit(owner, enemy, skill, damage);
                        break;
                    }
                }
            }
            finally { ListPool<EntityQizi>.Release(enemies); }

            if (owner.GObj != null)
                battle.BurnVisuals?.NamelessImpact(pattern, area, hitAny);
        }

        internal void Clear()
        {
            TimelineRegionalGuardianCast.ClearWarning(ref buildup);
            TimelineRegionalGuardianCast.ClearWarning(ref warning);
        }
    }
}
