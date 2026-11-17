using System.Collections.Generic;
using System.Linq;
using DataTable;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    internal sealed class TimelineCaveHazards
    {
        internal const int Marten = 9204, Golem = 9205, Lizard = 9206, Macaque = 9207;
        internal const int FrostWedge = 9163, EmberHeart = 9177, IceBead = 9178, FrostShieldBuff = 10502;
        internal const string FrostMarten = "FrostfangMarten", IceGolem = "ColdcoreGolem", LavaLizard = "LavascaleLizard";
        internal static int SkillFor(string appearance) => appearance switch
        {
            FrostMarten => Marten, IceGolem => Golem, LavaLizard => Lizard,
            TimelineRegionalGuardianCast.KilnAppearance => Macaque, _ => 0
        };
        private const float CellRadius = .51f;
        internal const float LavaRadius = .75f;
        private const int LavaDurationSeconds = 5;
        private readonly List<LavaField> lavaFields = new();
        private readonly List<GroundCast> casts = new();
        private readonly HeroComponent battle;

        private sealed class LavaField
        {
            internal IReadOnlyList<Vector3> Centers;
            internal MeshRenderer Surface;
            internal CampType Camp;
            internal Skill Skill;
            internal int Damage;
            internal float Elapsed;
            internal int NextTick = 1;
        }

        private sealed class GroundCast
        {
            internal readonly List<Patch> Patches = new();
            internal float Elapsed;
            internal float Windup;
            internal int Kind;
            internal bool Erupted;
            internal EntityQizi Caster;
            internal Skill Skill;
            internal bool IsFrost => Kind is Marten or Golem;
            internal float Duration => Windup + (IsFrost ? 3.4f : .36f);
        }

        private sealed class Patch
        {
            internal Vector3 Center;
            internal LineRenderer Outline, Charge;
        }

        internal TimelineCaveHazards(HeroComponent battle) => this.battle = battle;

        internal void AddLava(EntityQizi caster, Skill skill, IReadOnlyList<Vector3> centers, int damage)
        {
            var field = new LavaField
            {
                Centers = centers, Camp = caster.BelongCamp, Skill = skill, Damage = damage
            };
            if (caster.GObj != null && battle.BurnVisuals is TimelineBurnVisuals visuals)
                field.Surface = visuals.LavaPool(centers);
            lavaFields.Add(field);
        }

        private void UpdateLava(float seconds)
        {
            for (int i = lavaFields.Count - 1; i >= 0; i--)
            {
                var field = lavaFields[i];
                field.Elapsed += seconds;
                while (field.NextTick <= LavaDurationSeconds && field.Elapsed >= field.NextTick)
                {
                    field.NextTick++;
                    foreach (var unit in battle.GetEnemyList(field.Camp))
                        if (unit.IsValid && OccupiesLava(field, unit))
                            CommandCauseDamage.DealSecondaryDamage(unit, field.Damage, field.Skill);
                }
                bool expired = field.NextTick > LavaDurationSeconds;
                if (field.Surface != null && battle.BurnVisuals is TimelineBurnVisuals visuals)
                {
                    if (expired)
                    {
                        foreach (var center in field.Centers) visuals.LavaExtinguish(center);
                    }
                    else
                    {
                        float sinceTick = field.Elapsed - (field.NextTick - 1);
                        float pulse = field.NextTick > 1 ? 1f - Mathf.Clamp01(sinceTick / .26f) : 0f;
                        visuals.UpdateLavaPool(field.Surface, field.Elapsed, pulse);
                    }
                }
                if (!expired) continue;
                ClearLava(field);
                lavaFields.RemoveAt(i);
            }
        }

        private bool OccupiesLava(LavaField field, EntityQizi unit)
        {
            foreach (var point in battle.GetRangePoints(unit))
            foreach (var center in field.Centers)
                if (TimelineRegionalGuardianCast.HorizontalDistanceSquared(point, center) <= LavaRadius * LavaRadius)
                    return true;
            return false;
        }

        private static void ClearLava(LavaField field)
        {
            if (field.Surface == null) return;
            field.Surface.enabled = false;
            Object.Destroy(field.Surface.gameObject);
        }

        internal void Cast(EntityQizi caster, EntityQizi target, Skill skill)
        {
            if (!target.IsValid) return;
            int kind = skill.SkillID;
            var cast = new GroundCast
            {
                Windup = kind is Marten or Lizard ? .65f : 1.05f,
                Caster = caster, Skill = skill, Kind = kind
            };
            var visuals = battle.BurnVisuals;
            foreach (var cell in battle.CaveHazardCells(caster, target, kind))
            {
                var patch = new Patch { Center = cell };
                if (visuals != null)
                    patch.Outline = visuals.CaveWarning(cell, kind, CellRadius, out patch.Charge);
                cast.Patches.Add(patch);
            }
            if (cast.Patches.Count == 0) return;
            casts.Add(cast);
            if (caster.GObj != null)
                HotGameEntry.Sound.PlayTimelineCue(cast.IsFrost ? EnumSound.MartenWindup : EnumSound.KilnRattle,
                    cast.Patches[0].Center);
        }

        private bool Occupies(GroundCast cast, EntityQizi unit)
        {
            foreach (var point in battle.GetRangePoints(unit))
            foreach (var patch in cast.Patches)
            {
                var offset = point - patch.Center;
                if (offset.x * offset.x + offset.z * offset.z <= CellRadius * CellRadius)
                    return true;
            }
            return false;
        }

        internal bool InFrost(EntityQizi unit)
        {
            foreach (var cast in casts)
                if (cast.Erupted && cast.IsFrost && Occupies(cast, unit))
                    return true;
            return false;
        }

        internal void Update(float seconds)
        {
            UpdateLava(seconds);
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                var cast = casts[i];
                if (!cast.Erupted && !cast.Caster.IsValid)
                {
                    Clear(cast);
                    casts.RemoveAt(i);
                    continue;
                }

                cast.Elapsed += seconds;
                if (!cast.Erupted && cast.Elapsed >= cast.Windup)
                {
                    Erupt(cast);
                    cast.Erupted = true;
                }
                if (cast.Elapsed >= cast.Duration)
                {
                    Clear(cast);
                    casts.RemoveAt(i);
                    continue;
                }
                if (!cast.Erupted)
                {
                    float progress = cast.Elapsed / cast.Windup;
                    foreach (var patch in cast.Patches)
                        if (patch.Outline != null)
                            TimelineBurnVisuals.UpdateCaveWarning(patch.Outline, patch.Charge, patch.Center,
                                cast.Kind, CellRadius, progress);
                }
            }
        }

        private void Erupt(GroundCast cast)
        {
            var visuals = battle.BurnVisuals;
            foreach (var patch in cast.Patches)
            {
                TimelineRegionalGuardianCast.ClearWarning(ref patch.Charge);
                visuals?.CaveImpact(patch.Outline, patch.Center, cast.Kind, CellRadius);
            }
            if (cast.Caster.GObj != null)
                HotGameEntry.Sound.PlayTimelineCue(cast.IsFrost ? EnumSound.MartenBite : EnumSound.KilnCrack,
                    cast.Patches[0].Center);

            int attack = (int)cast.Caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
            foreach (var unit in battle.CaveCombatants())
            {
                if (!unit.IsValid || (cast.IsFrost && unit.BelongCamp == cast.Caster.BelongCamp) ||
                    !Occupies(cast, unit)) continue;
                if (cast.IsFrost)
                {
                    bool chilled = unit.FrostSlowSeconds > 0f;
                    unit.FrostSlowSeconds = 1.15f;
                    if (cast.Kind == Golem)
                        CommandCauseDamage.DealSecondaryDamage(unit, Mathf.RoundToInt(attack * (chilled ? 1.55f : 1.05f)), cast.Skill);
                }
                else
                {
                    CommandCauseDamage.DealSecondaryDamage(unit,
                        Mathf.RoundToInt(attack * (cast.Kind == Macaque ? 1.2f : .75f)), cast.Skill);
                    if (unit.IsValid) CommandBurnEffect.Apply(cast.Caster, unit, cast.Kind == Macaque ? 2 : 1, cast.Skill);
                }
            }
        }

        private static void Clear(GroundCast cast)
        {
            foreach (var patch in cast.Patches)
            {
                TimelineRegionalGuardianCast.ClearWarning(ref patch.Charge);
                TimelineRegionalGuardianCast.ClearWarning(ref patch.Outline);
            }
        }

        internal void Clear()
        {
            foreach (var cast in casts) Clear(cast);
            casts.Clear();
            foreach (var field in lavaFields) ClearLava(field);
            lavaFields.Clear();
        }

        internal static void OnBasicHit(EntityQizi caster, EntityQizi target, float effectiveDamage)
        {
            if (effectiveDamage <= 0f || !caster.IsValid) return;
            if (caster.PassiveSkillList.Any(skill => skill.SkillID == IceBead) && ++caster.IceBeadHits % 3 == 0)
                target.FrostSlowSeconds = Mathf.Max(target.FrostSlowSeconds, 2f);
            if (target.FrostSlowSeconds <= 0f || caster.FrostShieldCooldown > 0f) return;
            var source = caster.PassiveSkillList.FirstOrDefault(skill => skill.SkillID == FrostWedge);
            if (source == null) return;
            var row = GameEntry.DataTable.GetDataTable<DRBuff>("Buff").GetDataRow(FrostShieldBuff);
            var template = row == null ? null : GameEntry.DataTable.GetDataTable<DRBuffTemplate>("BuffTemplate").GetDataRow(row.TemplateID);
            if (template == null) return;
            var shield = SkillFactory.CreateNewBuff();
            template.BuffTemplate.Clone(shield);
            shield.BuffID = row.Id;
            shield.Owner = caster;
            shield.ParentSkill = source;
            shield.DurationMs = row.Duration;
            shield.RemainMs = row.Duration;
            shield.MaxLayerNum = row.MaxLayerNum;
            shield.SetSkillValue(row);
            caster.FrostShieldCooldown = 4f;
            caster.AddBuff(shield);
        }

        internal static void OnBurnApplied(EntityQizi caster, EntityQizi target, int previousLayers, Buff burn)
        {
            if (previousLayers >= 3 || burn.StackCount < 3 || !caster.IsValid ||
                caster.BelongCamp == target.BelongCamp || caster.EmberHeartUsed) return;
            var source = caster.PassiveSkillList.FirstOrDefault(skill => skill.SkillID == EmberHeart);
            if (source == null) return;
            caster.EmberHeartUsed = true;
            var battle = HotGameEntry.HeroManager;
            var center = target.LogicPosition;
            const float radius = 1.65f;
            battle.BurnVisuals?.Detonate(center, radius);
            int damage = Mathf.Max(1, (int)caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue() * 45 / 100);
            foreach (var enemy in battle.GetEnemyList(caster.BelongCamp))
                if (enemy.IsValid && (enemy.LogicPosition - center).sqrMagnitude <= radius * radius)
                    CommandCauseDamage.DealSecondaryDamage(enemy, damage, source);
        }
    }
}
