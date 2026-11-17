using System.Collections.Generic;
using Entity;
using SkillSystem;
using UnityEngine;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        internal TimelineCaveHazards CaveHazards { get; private set; }

        internal IEnumerable<EntityQizi> CaveCombatants()
        {
            foreach (var actor in QiziCSList) yield return actor;
            foreach (var actor in DirenList) yield return actor;
        }

        internal IEnumerable<Vector3> CaveHazardCells(EntityQizi caster, EntityQizi target, int skill)
        {
            var center = GetIndexQizi(target);
            if (!IsBoardCell(center)) yield break;
            var aim = GetGeziPos(center.y, center.x);
            if (target.ReservedMoveAnchor is Vector2Int next && IsBoardCell(next))
            {
                var nextPosition = GetGeziPos(next.y, next.x);
                if ((nextPosition - target.LogicPosition).sqrMagnitude < (aim - target.LogicPosition).sqrMagnitude)
                {
                    center = next;
                    aim = nextPosition;
                }
            }
            yield return aim;
            if (skill is not (TimelineCaveHazards.Golem or TimelineCaveHazards.Macaque)) yield break;
            var forward = (aim - caster.LogicPosition).normalized;
            foreach (var adjacent in AdjacentTo(center))
            {
                var position = GetGeziPos(adjacent.y, adjacent.x);
                float alignment = Vector3.Dot((position - aim).normalized, forward);
                if (skill == TimelineCaveHazards.Golem ? alignment > .10f : Mathf.Abs(alignment) > .82f)
                    yield return position;
            }
        }

        internal bool TryPullOneCell(EntityQizi target, EntityQizi source)
        {
            var start = GetIndexQizi(target);
            var destination = GetIndexQizi(source);
            float best = getDistance(start, destination);
            var choice = start;
            foreach (var step in AdjacentTo(start))
            {
                float distance = getDistance(step, destination);
                if (distance >= best || !CanPlace(target, step)) continue;
                best = distance;
                choice = step;
            }
            return choice != start && Relocate(target, choice);
        }

        internal void CastLavaSplash(EntityQizi caster, Skill skill, IReadOnlyList<Vector3> centers, int damage)
        {
            (CaveHazards ??= new TimelineCaveHazards(this)).AddLava(caster, skill, centers, damage);
        }

        private bool Relocate(EntityQizi entity, Vector2Int to)
        {
            entity.InterruptStep();
            if (!TryPlace(entity, to)) return false;
            entity.LogicPosition = GetGeziPos(to.y, to.x);
            return true;
        }

        private IEnumerable<Vector2Int> AdjacentTo(Vector2Int cell)
        {
            var offsets = cell.y % 2 == 0 ? adjacentOffsetDoubleRow : adjacentOffsetSingleRow;
            foreach (var offset in offsets)
                if (IsBoardCell(cell + offset)) yield return cell + offset;
        }

        internal EntityQizi SummonPaperShadow(EntityQizi owner, EntityQizi target, Vector3 castAim)
        {
            var anchor = GetIndexQizi(owner);
            var shadow = GetNewEntityQizi();
            shadow.BelongCamp = owner.BelongCamp;
            shadow.TimelineAppearance = TimelineOuterGuardianCast.ClerkAppearance;
            shadow.TechniqueSkillId = 0;
            shadow.Init(owner.HeroID, new List<int> { TimelineOuterGuardianCast.ClerkBasicAttack });
            shadow.IsPaperShadow = true;
            shadow.NextCastNormalSkillIndex = shadow.CurCastNormalSkillIndex = 0;
            shadow.CurAttackTarget = target.IsValid ? target : null;

            var chosen = new Vector2Int(-1, -1);
            if (target.IsValid)
            {
                var targetAnchor = GetIndexQizi(target);
                if (IsBoardCell(targetAnchor))
                {
                    float nearest = float.PositiveInfinity;
                    foreach (var occupied in GetFootprint(target, targetAnchor))
                    foreach (var cell in AdjacentTo(occupied))
                    {
                        if (!CanPlace(shadow, cell)) continue;
                        float distance = getDistance(cell, anchor);
                        if (distance >= nearest) continue;
                        nearest = distance;
                        chosen = cell;
                    }
                }
            }

            if (!IsBoardCell(chosen))
            {
                Vector3 aim = target.IsValid ? target.LogicPosition : castAim;
                float nearest = float.PositiveInfinity;
                foreach (var cell in AdjacentTo(anchor))
                {
                    if (!CanPlace(shadow, cell)) continue;
                    float distance = (GetGeziPos(cell.y, cell.x) - aim).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    chosen = cell;
                }
            }
            if (!TryPlace(shadow, chosen))
            {
                shadow.Remove();
                return null;
            }

            int health = Mathf.Clamp((int)owner.GetAttribute(AttributeType.MaxHp).GetFinalValue() / 18, 12, 30);
            int attack = Mathf.Max(1, (int)owner.GetAttribute(AttributeType.AttackDamage).GetFinalValue() / 3);
            shadow.GetAttribute(AttributeType.MaxHp).SetBaseNum(health);
            shadow.GetAttribute(AttributeType.Hp).SetBaseNum(health);
            shadow.GetAttribute(AttributeType.AttackDamage).SetBaseNum(attack);
            shadow.LogicPosition = GetGeziPos(chosen.y, chosen.x);
            shadow.SavePos = chosen;
            (owner.PaperShadows ??= new List<EntityQizi>(6)).Add(shadow);
            (shadow.BelongCamp == CampType.Friend ? QiziCSList : DirenList).Add(shadow);
            if (IsTimelineSimulation) return shadow;
            shadow.InitGObj();
            shadow.GObj.transform.localScale *= .66f;
            var tint = new MaterialPropertyBlock();
            foreach (var renderer in shadow.GObj.GetComponentsInChildren<Renderer>())
            {
                Color paper = renderer.sharedMaterial != null && renderer.sharedMaterial.name.Contains("Seal")
                    ? new Color(.66f, .25f, .18f) : new Color(.87f, .79f, .60f);
                tint.SetColor("_BaseColor", paper);
                tint.SetColor("_Color", paper);
                renderer.SetPropertyBlock(tint);
            }
            return shadow;
        }
    }
}
