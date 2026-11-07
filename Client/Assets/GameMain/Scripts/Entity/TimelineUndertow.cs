using System.Collections.Generic;
using GameFramework;
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
        private readonly Vector3 origin;
        private readonly Vector3 forward;
        private readonly Vector3 side;
        private LineRenderer warning;

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

        internal void Strike(EntityQizi owner, Skill skill)
        {
            Clear();
            if (!owner.IsValid) return;

            var visuals = HotGameEntry.HeroManager.BurnVisuals;
            visuals?.WaterSweep(origin, forward, side, Length, HalfWidth);
            var enemies = ListPool<EntityQizi>.Get();
            Vector3? firstImpact = null;
            try
            {
                HotGameEntry.HeroManager.GetEntityQiziList(CampType.Enemy, owner.BelongCamp, ref enemies);
                foreach (var target in enemies)
                {
                    if (!target.IsValid) continue;
                    Vector3 offset = target.LogicPosition - origin;
                    float along = Vector3.Dot(offset, forward);
                    if (along < 0f || along > Length || Mathf.Abs(Vector3.Dot(offset, side)) > HalfWidth)
                        continue;

                    Vector3 position = target.LogicPosition;
                    float damage = (int)owner.GetAttribute(AttributeType.AttackDamage).GetFinalValue() * 1.35f;
                    var hit = ReferencePool.Acquire<CauseDamageData>();
                    try
                    {
                        hit.Caster = owner;
                        hit.SourceSkill = skill;
                        hit.Target = target;
                        hit.DamageValue = damage;
                        hit.TriggerDamageRelate = true;
                        owner.OnTrigger(TriggerType.BeforeCauseDamage, hit);
                        target.OnTrigger(TriggerType.BeforeBeCauseDamage, hit);
                        target.BeCauseDamage(hit);
                    }
                    finally { ReferencePool.Release(hit); }
                    visuals?.WaterSplash(position);
                    firstImpact ??= position;
                }
            }
            finally { ListPool<EntityQizi>.Release(enemies); }
            if (firstImpact.HasValue) visuals?.WaterImpact(firstImpact.Value);
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
