using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Entity;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;
using WorldTimeline;

public sealed class TacticalTrialResult
{
    public bool Victory;
    public bool TimedOut;
    public float Seconds;
    public int[] Health;
    public int[] Roles;
    public long[] Damage;
    public int AlliedHealing, EnemyHealing;
    public int FirstFallenId;
    public float FirstFallenAt;
}

public static class TacticalTrialBattle
{
    public const float TimeLimit = 45f;
    public const float GuardRadius = 3f;
    public static IEnumerator Run(TrialDeployment deployment, bool visible, Action<TacticalTrialResult> completed,
        Action<float> advanced = null)
    {
        var party = deployment.CreateParty();
        var enemies = new[] { new ActorState(11, 0, 740, 84), new ActorState(12, 0, 740, 84),
            new ActorState(13, 0, 420, 18) };
        var fighters = new Dictionary<int, EntityQizi>();
        var result = new TacticalTrialResult { Roles = deployment.Roles };
        float friendPulse = 3f, enemyPulse = 2.5f;
        var setup = new TimelineBattleSetup
        {
            DurationSeconds = TimeLimit,
            Position = actor => actor.Id switch
            {
                11 => new Vector2Int(1, 5), 12 => new Vector2Int(5, 5), 13 => new Vector2Int(5, 7),
                _ => CellPosition(deployment.CellOf(actor.Id - 1))
            },
            Configure = (actor, entity) =>
            {
                fighters.Add(actor.Id, entity);
                foreach (var skill in entity.NormalSkillList.Where(skill => skill.IsBasicAttack))
                    skill.SkillRange = actor.Id is 3 or 4 or 13 ? 6000 : 1100;
                if (actor.Id is 11 or 12)
                {
                    // Guards carry one opening shield; they do not endlessly recast it between attacks.
                    var shield = entity.NormalSkillList.Single(skill => skill.SkillID == 6);
                    entity.NormalSkillList.Remove(shield);
                    entity.PassiveSkillList.Add(shield); // Retain ownership for output accounting and pool cleanup.
                    shield.Cast();
                    shield.OnSkillBeforeShakeEnd();
                }
                UpdateGuardFocus();
            },
            Tick = delta =>
            {
                result.Seconds += delta;
                advanced?.Invoke(result.Seconds);
                UpdateGuardFocus();
                if (result.FirstFallenId == 0)
                {
                    var fallen = fighters.FirstOrDefault(pair => !pair.Value.IsValid);
                    if (fallen.Value != null)
                    { result.FirstFallenId = fallen.Key; result.FirstFallenAt = result.Seconds; }
                }
                // No healing after a side has been wiped, and no resurrection on the lethal tick.
                if (!fighters.Any(pair => pair.Key < 10 && pair.Value.IsValid) ||
                    !fighters.Any(pair => pair.Key > 10 && pair.Value.IsValid)) return;
                friendPulse -= delta;
                enemyPulse -= delta;
                if (friendPulse <= 0)
                { friendPulse += 3f; result.AlliedHealing += Heal(4, 115); }
                if (enemyPulse <= 0)
                { enemyPulse += 2.5f; result.EnemyHealing += Heal(13, 90); }
            }
        };

        yield return HotGameEntry.HeroManager.RunTimelineBattle(party, enemies, _ => 2,
            actor => actor.Id switch
            {
                1 or 11 or 12 => new List<int> { 1, 3 },
                2 => new List<int> { 1, 9122 },
                _ => new List<int> { 2 }
            }, TacticalTrial.Seed, visible, health =>
            {
                result.Health = health;
                result.Victory = health.Take(party.Length).Any(hp => hp > 0) &&
                    health.Skip(party.Length).All(hp => hp == 0);
                result.TimedOut = !result.Victory && result.Seconds >= TimeLimit - .1f;
                if (result.TimedOut)
                    result.Health = party.Concat(enemies).Select(actor => fighters[actor.Id].IsValid
                        ? (int)fighters[actor.Id].GetAttribute(AttributeType.Hp).GetFinalValue() : 0).ToArray();
                result.Damage = party.Select(actor => party[0].LastBattleOutput
                    .FirstOrDefault(row => row.ActorId == actor.Id)?.Skills.Sum(skill => skill.Damage) ?? 0).ToArray();
                completed(result);
            }, appearance: actor => actor.Id switch
            {
                1 => "Chongshan", 2 => "Chili", 3 => "Wenchao", 4 => "Baixi",
                11 or 12 => "LuYing", _ => "ShenYan"
            }, reportOwner: party[0], timeSliceSimulation: !visible, setup: setup);

        int Heal(int healerId, int amount)
        {
            if (!fighters.TryGetValue(healerId, out var healer) || !healer.IsValid) return 0;
            var target = fighters.Values.Where(unit => unit.IsValid && unit.BelongCamp == healer.BelongCamp &&
                    (healerId != 13 || unit != healer) &&
                    unit.GetDistanceSquare(healer) <= 4.5f * 4.5f &&
                    (int)unit.GetAttribute(AttributeType.Hp).GetFinalValue() < (int)unit.GetAttribute(AttributeType.MaxHp).GetFinalValue())
                .OrderBy(unit => (float)(int)unit.GetAttribute(AttributeType.Hp).GetFinalValue() /
                    (int)unit.GetAttribute(AttributeType.MaxHp).GetFinalValue()).ThenBy(unit => unit.HeroUID).FirstOrDefault();
            if (target == null) return 0;
            int healed = Math.Min(amount, (int)target.GetAttribute(AttributeType.MaxHp).GetFinalValue() -
                (int)target.GetAttribute(AttributeType.Hp).GetFinalValue());
            target.GetAttribute(AttributeType.Hp).AddNum(healed);
            if (visible) HotGameEntry.HeroManager.BurnVisuals?.LanternHeal(target);
            return healed;
        }

        void UpdateGuardFocus()
        {
            fighters.TryGetValue(1, out var defender);
            foreach (var pair in fighters.Where(pair => pair.Key > 10))
            {
                var enemy = pair.Value;
                enemy.PriorityAttackTarget = defender is { IsValid: true } && enemy.IsValid &&
                    enemy.GetDistanceSquare(defender) <= GuardRadius * GuardRadius ? defender : null;
            }
        }
    }

    private static Vector2Int CellPosition(int cell)
    {
        if (cell < 0 || cell >= TacticalTrial.CellCount) throw new ArgumentOutOfRangeException(nameof(cell));
        return new Vector2Int(1 + cell % 3 * 2, cell < 3 ? 3 : 1);
    }
}
