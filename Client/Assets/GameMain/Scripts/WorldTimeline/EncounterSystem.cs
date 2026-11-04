using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public struct BattleResult
    {
        public int FirstHealth;
        public int SecondHealth;
        public BattleResult(int firstHealth, int secondHealth)
        { FirstHealth = firstHealth; SecondHealth = secondHealth; }
    }

    public interface IWorldBattleResolver
    {
        BattleResult Resolve(int firstHealth, int firstAttack, int secondHealth, int secondAttack);
    }

    public interface IWorldPartyBattleResolver : IWorldBattleResolver
    {
        // Return remaining health in the same order as the two participant lists.
        int[] ResolveParty(WorldState world, int nodeId, IReadOnlyList<ActorState> first,
            IReadOnlyList<ActorState> second);
    }

    public interface IWorldAsyncPartyBattleResolver : IWorldPartyBattleResolver
    {
        IEnumerator ResolvePartyRoutine(WorldState world, int nodeId, IReadOnlyList<ActorState> first,
            IReadOnlyList<ActorState> second, Action<int[]> completed);
    }

    // Prototype only: first combatant attacks first; survivors retain damage.
    public sealed class SimpleWorldBattleResolver : IWorldBattleResolver
    {
        public BattleResult Resolve(int firstHealth, int firstAttack, int secondHealth, int secondAttack)
        {
            if (firstHealth < 1 || secondHealth < 1 || firstAttack < 1 || secondAttack < 1)
                throw new ArgumentOutOfRangeException();
            long firstTurns = ((long)secondHealth + firstAttack - 1) / firstAttack;
            long secondTurns = ((long)firstHealth + secondAttack - 1) / secondAttack;
            return firstTurns <= secondTurns
                ? new BattleResult((int)(firstHealth - (firstTurns - 1) * secondAttack), 0)
                : new BattleResult(0, (int)(secondHealth - secondTurns * firstAttack));
        }
    }

    public sealed class EncounterSystem
    {
        private readonly IWorldBattleResolver battle;
        private ChosenActorController chosen;
        private FinalRitualSystem ritual;
        public EncounterSystem(IWorldBattleResolver battle) { this.battle = battle ?? throw new ArgumentNullException(nameof(battle)); }
        internal void ConfigureChosen(ChosenActorController controller) => chosen = controller;
        internal void ConfigureRitual(FinalRitualSystem system) => ritual = system;

        internal IEnumerator ResolveRoutine(WorldState world, IEnumerable<int> arrivalNodes, Action<WorldEvent> emit)
        {
            foreach (int nodeId in arrivalNodes.Distinct().OrderBy(id => id))
            {
                if (world.IsEnded) yield break;
                ActorState leader = chosen == null ? null : world.Actors[chosen.ActorId];
                bool leaderPresent = leader != null && leader.IsAlive && leader.NodeId == nodeId;
                var occupants = world.Actors.Values.Where(a => a.IsAlive && a.NodeId == nodeId &&
                    !a.LeaderId.HasValue && a.Id != world.ChosenActorId).OrderBy(a => a.Id).ToList();
                ActorState survivor = occupants.Count > 0 ? occupants[0] : null;
                for (int i = 1; i < occupants.Count; i++)
                {
                    ActorState other = occupants[i];
                    var outcome = new ResolvedBattle();
                    yield return ResolveRoutine(world, nodeId, new List<ActorState> { survivor }, new List<ActorState> { other }, outcome);
                    var result = outcome.Result;
                    Validate(result);
                    emit(new WorldEvent(world.Year, WorldEventKind.Battle, survivor.Id, nodeId, "Actor battle", other.Id));
                    Apply(world, survivor, result.FirstHealth, nodeId, emit);
                    Apply(world, other, result.SecondHealth, nodeId, emit);
                    if (!survivor.IsAlive) survivor = other;
                }

                if (survivor != null && leaderPresent && survivor.IsDisguised && survivor.DisguisedYear < world.Year)
                {
                    int enemyHealth = survivor.MaxHealth, enemyAttack = survivor.Attack;
                    yield return FightChosen(world, leader, survivor, nodeId, emit);
                    if (leader.IsAlive) Absorb(world, leader, enemyHealth, enemyAttack, nodeId, emit);
                    else if (survivor.Id == world.PlayerActorId)
                    {
                        world.Finish(RunOutcome.PlayerKilledChosen);
                        emit(new WorldEvent(world.Year, WorldEventKind.RunEnded, survivor.Id, nodeId, "Player killed chosen"));
                        yield break;
                    }
                }
                if (survivor != null && survivor.IsAlive) yield return ResolveNode(world, survivor, nodeId, emit);
                if (world.IsEnded) yield break;
                if (leaderPresent && leader.IsAlive) yield return ResolveNode(world, leader, nodeId, emit);
            }
        }

        private IEnumerator ResolveNode(WorldState world, ActorState survivor, int nodeId, Action<WorldEvent> emit)
        {
                NodeState node = world.Nodes[nodeId];
                if (node.MonsterHealth > 0)
                {
                    bool isChosen = survivor.Id == world.ChosenActorId;
                    var party = isChosen ? Party(world, survivor) : new List<ActorState> { survivor };
                    int health = SumHealth(party);
                    int enemyHealth = node.MonsterHealth, enemyAttack = node.MonsterAttack;
                    var monster = new ActorState(-nodeId, nodeId, enemyHealth, enemyAttack);
                    var outcome = new ResolvedBattle();
                    yield return ResolveRoutine(world, nodeId, party, new List<ActorState> { monster }, outcome);
                    var result = outcome.Result;
                    Validate(result);
                    node.MonsterHealth = result.SecondHealth;
                    emit(new WorldEvent(world.Year, WorldEventKind.Battle, survivor.Id, nodeId, "Node monster"));
                    ApplyPartyResult(world, party, health - result.FirstHealth, outcome.Remaining, nodeId, emit);
                    if (ritual != null && nodeId == world.Ritual.NodeId && result.SecondHealth == 0 && survivor.IsAlive)
                    {
                        ritual.BossDefeated(world, survivor, emit);
                        yield break;
                    }
                    if (isChosen && result.SecondHealth == 0 && survivor.IsAlive)
                        Absorb(world, survivor, enemyHealth, enemyAttack, nodeId, emit);
                }
                if (survivor.IsAlive && node.Items.Count > 0)
                {
                    survivor.Attack = checked(survivor.Attack + node.LootAttackBonus);
                    survivor.Items.AddRange(node.Items);
                    node.Items.Clear();
                    emit(new WorldEvent(world.Year, WorldEventKind.Looted, survivor.Id, nodeId));
                }
        }

        private IEnumerator FightChosen(WorldState world, ActorState leader, ActorState enemy, int nodeId, Action<WorldEvent> emit)
        {
            var party = Party(world, leader);
            int health = SumHealth(party);
            var outcome = new ResolvedBattle();
            yield return ResolveRoutine(world, nodeId, party, new List<ActorState> { enemy }, outcome);
            var result = outcome.Result;
            Validate(result);
            emit(new WorldEvent(world.Year, WorldEventKind.Battle, leader.Id, nodeId, "Disguised enemy", enemy.Id));
            ApplyPartyResult(world, party, health - result.FirstHealth, outcome.Remaining, nodeId, emit);
            Apply(world, enemy, result.SecondHealth, nodeId, emit);
        }

        private sealed class ResolvedBattle
        {
            public BattleResult Result;
            public int[] Remaining;
        }

        private IEnumerator ResolveRoutine(WorldState world, int nodeId, List<ActorState> first,
            List<ActorState> second, ResolvedBattle outcome)
        {
            if (battle is IWorldAsyncPartyBattleResolver asynchronous)
                yield return asynchronous.ResolvePartyRoutine(world, nodeId, first.AsReadOnly(),
                    second.AsReadOnly(), result => outcome.Remaining = result);
            else if (battle is IWorldPartyBattleResolver native)
                outcome.Remaining = native.ResolveParty(world, nodeId, first.AsReadOnly(), second.AsReadOnly());
            else
            {
                outcome.Result = battle.Resolve(SumHealth(first), SumAttack(first), SumHealth(second), SumAttack(second));
                yield break;
            }

            var remaining = outcome.Remaining;
            if (remaining == null || remaining.Length != first.Count + second.Count || remaining.Any(hp => hp < 0))
                throw new InvalidOperationException("Invalid party battle result.");
            outcome.Result = new BattleResult(checked(remaining.Take(first.Count).Sum()), checked(remaining.Skip(first.Count).Sum()));
            Validate(outcome.Result);
        }
        private static void ApplyPartyResult(WorldState world, List<ActorState> party, int damage,
            int[] remaining, int nodeId, Action<WorldEvent> emit)
        {
            if (remaining == null)
            {
                ApplyPartyDamage(world, party, damage, nodeId, emit);
                return;
            }
            for (int i = 0; i < party.Count; i++) Apply(world, party[i], remaining[i], nodeId, emit);
        }

        private static List<ActorState> Party(WorldState world, ActorState leader)
        {
            // Prototype formation: companions take damage by ID, chosen takes damage last.
            var party = world.Actors.Values.Where(a => a.IsAlive && a.LeaderId == leader.Id).OrderBy(a => a.Id).ToList();
            party.Add(leader);
            return party;
        }
        private static int SumHealth(List<ActorState> party) => checked(party.Sum(a => a.Health));
        private static int SumAttack(List<ActorState> party) => checked(party.Sum(a => a.Attack));
        private static void ApplyPartyDamage(WorldState world, List<ActorState> party, int damage, int nodeId, Action<WorldEvent> emit)
        {
            foreach (var member in party)
            {
                int loss = Math.Min(damage, member.Health);
                Apply(world, member, member.Health - loss, nodeId, emit);
                damage -= loss;
            }
        }
        private void Absorb(WorldState world, ActorState leader, int health, int attack, int nodeId, Action<WorldEvent> emit)
        {
            int gainedHealth = checked((int)((long)health * chosen.AbsorptionPercent / 100));
            int gainedAttack = checked((int)((long)attack * chosen.AbsorptionPercent / 100));
            leader.MaxHealth = checked(leader.MaxHealth + gainedHealth);
            leader.Health = checked(leader.Health + gainedHealth);
            leader.Attack = checked(leader.Attack + gainedAttack);
            emit(new WorldEvent(world.Year, WorldEventKind.Absorbed, leader.Id, nodeId, "Health +" + gainedHealth + ", attack +" + gainedAttack));
        }

        private static void Validate(BattleResult result)
        {
            if (result.FirstHealth < 0 || result.SecondHealth < 0 || (result.FirstHealth > 0) == (result.SecondHealth > 0))
                throw new InvalidOperationException("Battle must return exactly one survivor and non-negative health.");
        }

        private static void Apply(WorldState world, ActorState actor, int health, int nodeId, Action<WorldEvent> emit)
        {
            actor.Health = health;
            if (actor.IsAlive) return;
            actor.Die();
            emit(new WorldEvent(world.Year, WorldEventKind.Died, actor.Id, nodeId, "Battle"));
        }
    }
}
