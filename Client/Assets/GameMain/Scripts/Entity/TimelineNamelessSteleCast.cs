using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace Entity
{
    internal sealed class TimelineNamelessSteleCast
    {
        internal const int SkillId = 9193;
        private const float LineLength = 4.4f;
        private const float LineHalfWidth = .82f;
        private const float SealRadius = 2.7f;

        private enum Pattern { Smash, Line, Seal }

        private readonly Pattern pattern;
        private readonly EntityQizi primaryTarget;
        private readonly Vector3 origin;
        private readonly Vector3 forward;
        private readonly Vector3 side;
        private LineRenderer warning;

        internal int WindupMs => pattern switch { Pattern.Smash => 650, Pattern.Line => 1050, _ => 1550 };
        internal bool IsLine => pattern == Pattern.Line;

        private TimelineNamelessSteleCast(EntityQizi owner, EntityQizi target, Pattern pattern)
        {
            this.pattern = pattern;
            primaryTarget = target;
            origin = owner.LogicPosition;
            var direction = target.LogicPosition - origin;
            direction.y = 0;
            forward = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.forward;
            side = Vector3.Cross(Vector3.up, forward);
            if (IsLine && owner.GObj != null)
                owner.GObj.transform.rotation = Quaternion.LookRotation(forward);
            if (owner.GObj != null && pattern != Pattern.Smash)
                warning = HotGameEntry.HeroManager.BurnVisuals?.NamelessWarning(owner.GObj.transform,
                    origin, forward, pattern == Pattern.Seal ? SealRadius : LineHalfWidth,
                    pattern == Pattern.Line ? LineLength : 0f);
        }

        internal static TimelineNamelessSteleCast Begin(EntityQizi owner, Skill skill)
        {
            if (skill.SkillID != SkillId || owner.TimelineAppearance != TimelineMonsterAnimation.NamelessSteleAppearance ||
                owner.CurAttackTarget is not EntityQizi target || !target.IsValid)
                return null;
            int health = (int)owner.GetAttribute(AttributeType.Hp).GetFinalValue();
            int maxHealth = (int)owner.GetAttribute(AttributeType.MaxHp).GetFinalValue();
            bool halfHealth = health <= maxHealth / 2;
            var pattern = !owner.NamelessSteleSealed && halfHealth ? Pattern.Seal
                : (owner.NamelessSteleAttackCount + 1) % 3 == 0 ? Pattern.Line : Pattern.Smash;
            return new TimelineNamelessSteleCast(owner, target, pattern);
        }

        internal void Strike(EntityQizi owner, Skill skill)
        {
            Clear();
            if (!owner.IsValid) return;

            var battle = HotGameEntry.HeroManager;
            var visuals = owner.GObj != null ? battle.BurnVisuals : null;
            int attack = (int)owner.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
            if (pattern == Pattern.Smash)
            {
                owner.NamelessSteleAttackCount++;
                if (primaryTarget.IsValid &&
                    battle.IsInSkillRange(battle.GetCombatDistanceSquare(owner, primaryTarget), skill.SkillRange))
                {
                    var position = primaryTarget.LogicPosition;
                    if (Hit(owner, primaryTarget, skill, attack * 1.25f))
                        visuals?.StoneImpact(position);
                }
                return;
            }

            if (pattern == Pattern.Seal) owner.NamelessSteleSealed = true;
            else owner.NamelessSteleAttackCount++;

            var enemies = ListPool<EntityQizi>.Get();
            bool hitAny = false;
            try
            {
                battle.GetEntityQiziList(CampType.Enemy, owner.BelongCamp, ref enemies);
                float damage = attack * (pattern == Pattern.Seal ? 1.55f : 1.15f);
                foreach (var enemy in enemies)
                {
                    if (!enemy.IsValid) continue;
                    foreach (var point in battle.GetRangePoints(enemy))
                    {
                        if (!Contains(point)) continue;
                        hitAny |= Hit(owner, enemy, skill, damage);
                        break;
                    }
                }
            }
            finally { ListPool<EntityQizi>.Release(enemies); }
            visuals?.NamelessImpact(origin, forward, pattern == Pattern.Seal ? SealRadius : LineHalfWidth,
                pattern == Pattern.Line ? LineLength : 0f, hitAny);
        }

        private bool Contains(Vector3 point)
        {
            var offset = point - origin;
            offset.y = 0;
            if (pattern == Pattern.Seal) return offset.sqrMagnitude <= SealRadius * SealRadius;
            float along = Vector3.Dot(offset, forward);
            return along >= 0f && along <= LineLength && Mathf.Abs(Vector3.Dot(offset, side)) <= LineHalfWidth;
        }

        private static bool Hit(EntityQizi caster, EntityQizi target, Skill skill, float damage)
        {
            var hit = ReferencePool.Acquire<CauseDamageData>();
            try
            {
                hit.Caster = caster;
                hit.SourceSkill = skill;
                hit.Target = target;
                hit.DamageValue = damage;
                hit.TriggerDamageRelate = true;
                caster.OnTrigger(TriggerType.BeforeCauseDamage, hit);
                target.OnTrigger(TriggerType.BeforeBeCauseDamage, hit);
                return target.BeCauseDamage(hit) > 0;
            }
            finally { ReferencePool.Release(hit); }
        }

        internal void Clear()
        {
            if (warning == null) return;
            warning.gameObject.SetActive(false);
            Object.Destroy(warning.gameObject);
            warning = null;
        }
    }
}
