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

    public sealed class BattleSettlement
    {
        public int NodeId { get; internal set; }
        public int TrialItemId { get; internal set; }
        public bool IsTreasure { get; internal set; }
        public (int Applications, int Transfers, int Jumps) Lightning { get; internal set; }
        public IReadOnlyList<ActorBattleOutput> CombatOutput { get; internal set; } = Array.Empty<ActorBattleOutput>();
        public bool Victory { get; internal set; }
        public bool RitualBacklash { get; internal set; }
        public int Health { get; internal set; }
        public int MaxHealth { get; internal set; }
        public long Experience { get; internal set; }
        public int PreviousLevel { get; internal set; }
        public int Level { get; internal set; }
        public IReadOnlyList<int> Items { get; internal set; } = Array.Empty<int>();
        public IReadOnlyList<(int Id, int PreviousRank, int Rank, int SkillId, int Progress, TechniqueRequirement Requirement)> Techniques
            { get; internal set; }
    }

    public sealed class EncounterSystem
    {
        private readonly IWorldBattleResolver battle;
        private ChosenActorController chosen;
        private FinalRitualSystem ritual;
        internal Func<ActorState, int, IEnumerator> StoryResolver;
        internal Func<BattleSettlement, IEnumerator> PresentSettlement;
        internal CultivationConfig Growth;
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
                    AwardVictoryRewards(world, survivor, nodeId, emit);
                    yield return ShowSettlement(world, nodeId, outcome, survivor.Id == world.PlayerActorId);
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

        private IEnumerator ResolveNode(WorldState world, ActorState survivor, int nodeId, Action<WorldEvent> emit,
            bool resolveStory = true)
        {
                NodeState node = world.Nodes[nodeId];
            ResolvedBattle outcome = null;
            bool victory = false;
            int[] drops = Array.Empty<int>();
                if (node.MonsterHealth > 0)
                {
                    bool isChosen = survivor.Id == world.ChosenActorId;
                    var party = isChosen ? Party(world, survivor) : new List<ActorState> { survivor };
                    int health = SumHealth(party);
                    int enemyHealth = node.MonsterHealth, enemyAttack = node.MonsterAttack;
                    bool grouped = node.GroupGuardianHealth.Count > 0;
                    var guardians = grouped
                        ? node.GroupGuardianHealth.Select((hp, index) => new ActorState(-nodeId - index, nodeId, hp, enemyAttack)).ToList()
                        : new List<ActorState> { new ActorState(-nodeId, nodeId, enemyHealth, enemyAttack) };
                    outcome = new ResolvedBattle();
                    yield return ResolveRoutine(world, nodeId, party, guardians, outcome);
                    var result = outcome.Result;
                    Validate(result);
                    node.MonsterHealth = result.SecondHealth;
                    if (grouped)
                    {
                        node.GroupGuardianHealth.Clear();
                        if (outcome.Remaining != null)
                            node.GroupGuardianHealth.AddRange(outcome.Remaining.Skip(party.Count).Where(hp => hp > 0));
                        else
                        {
                            int loss = enemyHealth - result.SecondHealth;
                            foreach (var guardian in guardians)
                            {
                                int damage = Math.Min(loss, guardian.Health);
                                if (guardian.Health > damage) node.GroupGuardianHealth.Add(guardian.Health - damage);
                                loss -= damage;
                            }
                        }
                    }
                    emit(new WorldEvent(world.Year, WorldEventKind.Battle, survivor.Id, nodeId, "Node monster"));
                    ApplyPartyResult(world, party, health - result.FirstHealth, outcome.Remaining, nodeId, emit);
                    victory = result.SecondHealth == 0 && survivor.IsAlive;
                    if (victory)
                    {
                        node.GuardianDefeat = (survivor.Id, world.Year);
                        AwardVictoryRewards(world, survivor, nodeId, emit);
                    }
                    if (ritual != null && nodeId == world.Ritual.NodeId && victory)
                    {
                        ritual.BossDefeated(world, survivor, emit);
                        yield return ShowSettlement(world, nodeId, outcome, true, ritualBacklash: !survivor.IsAlive);
                        yield break;
                    }
                    if (isChosen && result.SecondHealth == 0 && survivor.IsAlive)
                        Absorb(world, survivor, enemyHealth, SumAttack(guardians), nodeId, emit);
                }
                if (survivor.IsAlive && node.Items.Count > 0)
                {
                    bool showTreasure = outcome == null && PresentSettlement != null &&
                        survivor.Id == world.PlayerActorId && !world.PlayerControlEnded;
                    if (outcome?.Player != null || showTreasure) drops = node.Items.ToArray();
                    survivor.Attack = checked(survivor.Attack + node.LootAttackBonus);
                    survivor.Items.AddRange(node.Items);
                    survivor.CheckTechniqueItems();
                    node.Items.Clear();
                    emit(new WorldEvent(world.Year, WorldEventKind.Looted, survivor.Id, nodeId));
                    if (showTreasure)
                        yield return PresentSettlement(new BattleSettlement
                        {
                            NodeId = nodeId, IsTreasure = true, Items = drops,
                            Techniques = Array.Empty<(int, int, int, int, int, TechniqueRequirement)>()
                        });
                }
                if (outcome != null) yield return ShowSettlement(world, nodeId, outcome, victory, drops,
                    trialItemId: resolveStory ? 0 : node.StoryTrialItemId);
                if (resolveStory && survivor.IsAlive && !world.IsEnded && StoryResolver != null &&
                    (!node.StoryRequiresGuardianDefeat || node.MonsterHealth == 0))
                {
                    bool canStartTrial = node.StoryTrialItemId != 0 && !node.StoryTrialStarted && node.MonsterHealth == 0 &&
                        !survivor.Items.Contains(node.StoryTrialItemId);
                    yield return StoryResolver(survivor, nodeId);
                    if (canStartTrial && survivor.IsAlive && !world.IsEnded && survivor.Items.Contains(node.StoryTrialItemId))
                    {
                        StartStoryTrial(node, survivor, world.BalancedLightningTrials);
                        yield return ResolveNode(world, survivor, nodeId, emit, resolveStory: false);
                    }
                }
        }

        private static void StartStoryTrial(NodeState node, ActorState actor, bool balanced)
        {
            // Allow at least two opening hits on the weaker target so it can carry a mark before falling.
            int attack = Math.Max(1, actor.CombatAttack);
            int Hp(int multiplier) => (int)Math.Min(int.MaxValue / 3, attack * (long)multiplier + 60);
            // Three attackers share the pressure budget so the player has time to trigger a chain.
            int guardAttack = Math.Max(1, actor.MaxHealth / (node.StoryTrialStage == 3 ? (balanced ? 96 : 32) : 40));
            int[] health = node.StoryTrialStage switch
            {
                1 => new[] { Hp(5) },
                2 => new[] { Hp(2), Hp(5) },
                _ => new[] { Hp(5), Hp(5), Hp(5) }
            };
            node.SetGuardianFormation(guardAttack, health);
            node.StoryTrialStarted = true;
            node.GuardianDefeat = null;
            node.HasExplorationGuardian = true;
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
            if (enemy.IsAlive) AwardVictoryRewards(world, enemy, nodeId, emit);
            yield return ShowSettlement(world, nodeId, outcome, enemy.IsAlive);
        }

        private sealed class ResolvedBattle
        {
            public BattleResult Result;
            public int[] Remaining;
            public ActorState Player;
            public long PreviousExperience;
            public int PreviousLevel;
            public Dictionary<int, (int Rank, int Progress, TechniqueRequirement Requirement)> PreviousTechniques;
        }

        private IEnumerator ResolveRoutine(WorldState world, int nodeId, List<ActorState> first,
            List<ActorState> second, ResolvedBattle outcome)
        {
            if (PresentSettlement != null && !world.PlayerControlEnded)
            {
                outcome.Player = first.Concat(second).FirstOrDefault(actor => actor.Id == world.PlayerActorId);
                if (outcome.Player != null)
                {
                    outcome.Player.LastLightningBattle = default;
                    outcome.Player.LastBattleOutput = Array.Empty<ActorBattleOutput>();
                    outcome.PreviousExperience = outcome.Player.Experience;
                    outcome.PreviousLevel = outcome.Player.Level;
                    outcome.PreviousTechniques = outcome.Player.Techniques.ToDictionary(pair => pair.Key,
                        pair => (pair.Value.Rank, pair.Value.Progress, pair.Value.NextStage?.Requirement ?? TechniqueRequirement.None));
                }
            }
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

        private void AwardVictoryRewards(WorldState world, ActorState winner, int nodeId, Action<WorldEvent> emit)
        {
            if (winner.Id == world.ChosenActorId || winner.HasJoinedChosen) return;
            int qi = Math.Max(1, world.Nodes[nodeId].CultivationPerYear);
            bool newRewards = world.Year >= world.BattleRewardsFromYear;
            if (world.BattleExperienceEnabled || newRewards)
                CultivationSystem.Gain(winner, checked(3 * qi), Growth, world.Year, emit);
            if (newRewards && world.Year <= world.LegacyTradeThroughYear)
                winner.AwardLegacySpiritStones(checked(2 * qi));
        }

        private IEnumerator ShowSettlement(WorldState world, int nodeId, ResolvedBattle outcome, bool victory,
            IReadOnlyList<int> drops = null, bool ritualBacklash = false, int trialItemId = 0)
        {
            var actor = outcome.Player;
            if (actor == null) yield break;
            var techniques = new List<(int, int, int, int, int, TechniqueRequirement)>();
            foreach (var pair in actor.Techniques.OrderBy(pair => pair.Key))
            {
                var art = pair.Value;
                if (outcome.PreviousTechniques.TryGetValue(pair.Key, out var before) &&
                    (art.Rank > before.Rank || art.Progress > before.Progress))
                    techniques.Add((pair.Key, before.Rank, art.Rank, art.SkillId,
                        art.Rank == before.Rank ? art.Progress - before.Progress : 0, before.Requirement));
            }
            yield return PresentSettlement(new BattleSettlement
            {
                NodeId = nodeId, TrialItemId = trialItemId, Victory = victory, RitualBacklash = ritualBacklash,
                Health = actor.Health, MaxHealth = actor.MaxHealth,
                PreviousLevel = outcome.PreviousLevel, Level = actor.Level,
                Experience = actor.Experience - outcome.PreviousExperience,
                Items = drops ?? Array.Empty<int>(), Techniques = techniques, Lightning = actor.LastLightningBattle,
                CombatOutput = actor.LastBattleOutput
            });
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
        private static int SumAttack(List<ActorState> party) => checked(party.Sum(a => a.CombatAttack));
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
