using System.Collections.Generic;
using System.Linq;
using Entity;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;
using WorldTimeline;

internal static class ExpeditionBattleSetup
{
    public static TimelineBattleSetup Create(WorldState world, IReadOnlyList<ActorState> first,
        IReadOnlyList<ActorState> second, bool visible, bool vanguard = false)
    {
        var sides = new[] { first, second };
        var parties = sides.Select(side => side.Select(a => !a.HasJoinedChosen ? world.ExpeditionFor(a.Id) : null)
            .FirstOrDefault(p => p != null)).ToArray();
        var fighters = new Dictionary<int, EntityQizi>();
        float pulse = 3f;
        return new TimelineBattleSetup
        {
            AdjustPosition = (actor, original) =>
            {
                int side = first.Contains(actor) ? 0 : 1;
                int cell = parties[side]?.CellOf(actor.Id) ?? -1;
                if (cell >= 0) return Position(cell, side == 0);
                int slot = 0; while (slot < sides[side].Count && sides[side][slot] != actor) slot++;
                if (parties[side]?.Owns(actor.Id) == true)
                    return new Vector2Int(slot % 7, side == 0 ? 1 : 6); // Preview bench, hidden by planning HUD.
                if (vanguard && parties[side] == null) return new Vector2Int(2 + slot % 2 * 2, side == 1 ? 5 : 2);
                return parties[1 - side] != null ? new Vector2Int(slot % 7, side == 1 ? 7 - slot / 7 : slot / 7) : original;
            },
            Configure = (actor, entity) =>
            {
                fighters.Add(actor.Id, entity);
                int role = ExpeditionParty.Role(actor.Id);
                if (role >= 0)
                    foreach (var skill in entity.NormalSkillList.Where(skill => skill.IsBasicAttack))
                        skill.SkillRange = role >= 2 ? 6000 : 1100;
                UpdateFocus();
            },
            Tick = delta =>
            {
                UpdateFocus(); pulse -= delta;
                if (pulse > 0) return;
                pulse += 3;
                for (int side = 0; side < 2; side++)
                {
                    var party = parties[side];
                    if (party == null || !fighters.TryGetValue(party.CompanionId(3), out var healer) || !healer.IsValid ||
                        !sides[1 - side].Any(actor => fighters[actor.Id].IsValid)) continue;
                    var target = sides[side].Select(actor => fighters[actor.Id]).Where(unit => unit.IsValid &&
                            unit.GetDistanceSquare(healer) <= 4.5f * 4.5f && Hp(unit) < MaxHp(unit))
                        .OrderBy(unit => (float)Hp(unit) / MaxHp(unit)).ThenBy(unit => unit.HeroUID).FirstOrDefault();
                    if (target == null) continue;
                    int amount = System.Math.Min(MaxHp(target) - Hp(target),
                        System.Math.Max(1, world.Actors[party.LeaderId].MaxHealth * 18 / 100));
                    target.GetAttribute(AttributeType.Hp).AddNum(amount);
                    EntityQizi.RecordBattleOutput(healer.NormalSkillList.FirstOrDefault(skill => skill.IsBasicAttack), healing: amount);
                    if (visible) HotGameEntry.HeroManager.BurnVisuals?.LanternHeal(target);
                }
            }
        };
        void UpdateFocus()
        {
            for (int side = 0; side < 2; side++)
            {
                var party = parties[side]; if (party == null) continue;
                fighters.TryGetValue(party.CompanionId(0), out var defender);
                foreach (var actor in sides[1 - side])
                    if (fighters.TryGetValue(actor.Id, out var enemy))
                        enemy.PriorityAttackTarget = defender is { IsValid: true } && enemy.IsValid &&
                            enemy.GetDistanceSquare(defender) <= (party.VanguardEnabled ? 2.25f : 9f) ? defender : null;
            }
        }
    }
    public static Vector2Int Position(int cell, bool first) => new Vector2Int(1 + cell % 3 * 2,
        first ? (cell < 3 ? 2 : 0) : (cell < 3 ? 5 : 7));
    private static int Hp(EntityQizi unit) => (int)unit.GetAttribute(AttributeType.Hp).GetFinalValue();
    private static int MaxHp(EntityQizi unit) => (int)unit.GetAttribute(AttributeType.MaxHp).GetFinalValue();
}
