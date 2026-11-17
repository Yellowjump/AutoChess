using System;
using System.Collections.Generic;

namespace WorldTimeline
{
    public enum RunOutcome { Running, Cleared, WorldDestroyed, PlayerKilledChosen, LifeRecorded }

    public sealed class WorldState
    {
        // Year zero is 元年; completing one annual step advances to year one.
        public int Year { get; internal set; }
        public int PlayerActorId { get; }
        public int? ChosenActorId { get; internal set; }
        public RunOutcome Outcome { get; private set; }
        public bool IsEnded => Outcome != RunOutcome.Running;
        public bool PlayerKilledChosen => Outcome == RunOutcome.PlayerKilledChosen;
        public FinalRitualState Ritual { get; internal set; }
        public (int ActorId, int Year)? SluiceOpening { get; internal set; }
        public bool SluiceDrainsBoss { get; internal set; }
        public (int ActorId, int Year)? NorthRelayRestored { get; internal set; }
        public int? NorthRelaySpringNodeId { get; internal set; }
        public (int ActorId, int Year)? ContractRevoked { get; internal set; }
        public bool PlayerCompletedPersonalStory => SluiceOpening?.ActorId == PlayerActorId ||
            NorthRelayRestored?.ActorId == PlayerActorId || ContractRevoked?.ActorId == PlayerActorId;
        public bool PlayerControlEnded { get; internal set; }
        public bool StoryCombatSkillsEnabled { get; set; }
        public bool WreckBeastEncounterEnabled { get; set; }
        public bool SwordRecallEnabled { get; set; }
        public bool TechniqueCombatEnabled { get; set; }
        public bool BattleExperienceEnabled { get; set; }
        public int BattleRewardsFromYear { get; set; } = int.MaxValue;
        public int RegionalGuardiansFromYear { get; set; } = int.MaxValue;
        public int OuterGuardiansFromYear { get; set; } = int.MaxValue;
        public int LegacyTradeThroughYear { get; set; } = -1;
        public bool BalancedLightningTrials { get; set; } = true;
        public bool CaveRegionsEnabled { get; set; }
        public bool SaltChannelDrained { get; internal set; }
        // Compatibility for the portion of a checkpoint originally played with lifespan deaths.
        public int LegacyLifespanDeathThroughYear { get; set; }
        public Dictionary<int, ActorState> Actors { get; } = new Dictionary<int, ActorState>();
        public ExpeditionParty Expedition { get; private set; }
        internal int FormationBattle;
        public IReadOnlyList<ActorState> FormationFirst { get; internal set; }
        public IReadOnlyList<ActorState> FormationSecond { get; internal set; }
        internal Action<int, int> PrepareHistoricalParty;
        public Dictionary<int, ExpeditionParty> HistoricalExpeditions { get; } = new Dictionary<int, ExpeditionParty>();
        public ExpeditionParty ExpeditionFor(int actorId) => actorId == PlayerActorId ? Expedition :
            HistoricalExpeditions.TryGetValue(actorId, out var party) ? party : null;
        public void EnableExpedition() { if (Expedition == null) Expedition = new ExpeditionParty(this); }
        public Dictionary<int, NodeState> Nodes { get; } = new Dictionary<int, NodeState>();
        public WorldState(int playerActorId) { PlayerActorId = playerActorId; }

        public Action<BalanceSample> BalanceSink { get; set; }

        internal bool TracksBalance(ActorState actor) => BalanceSink != null && actor.Id == PlayerActorId;

        public void RecordBalance(ActorState actor, BalanceSample sample)
        {
            if (!TracksBalance(actor)) return;
            sample.year = Year;
            sample.nodeId = sample.nodeId >= 0 ? sample.nodeId : actor.NodeId ?? -1;
            if (Nodes.TryGetValue(sample.nodeId, out var node))
            {
                sample.q = node.Q;
                sample.r = node.R;
                sample.qi = node.CultivationPerYear;
            }
            if (sample.level == 0) sample.level = actor.Level;
            sample.experience = actor.Experience;
            sample.levelExperience = actor.LevelExperience;
            BalanceSink(sample);
        }

        internal void Finish(RunOutcome outcome)
        {
            if (IsEnded) return;
            Outcome = outcome;
            if (BalanceSink != null && Actors.TryGetValue(PlayerActorId, out var player))
                RecordBalance(player, new BalanceSample { type = "run_finished", outcome = outcome.ToString() });
            PlayerControlEnded = true;
            foreach (var actor in Actors.Values)
                if (actor.IsAlive) actor.FrozenMapPoint = ActorMapProjection.Position(this, actor);
            foreach (var actor in Actors.Values)
            {
                actor.CurrentAction = null;
                actor.RemainingYears = 0;
            }
        }
    }

    public enum WorldEventKind { ActionStarted, Arrived, Cultivated, ActionCompleted, Battle, Died, Looted, Joined, Disguised, Absorbed, RitualReset, RitualCompleted, WorldCleared, RunEnded, Breakthrough, StoryResolved, FateChanged, ChosenEncounter, SluiceOpened, NorthRelayRestored, ContractRevoked, NorthRelaySpringUsed, Explored }

    public sealed class WorldEvent
    {
        public int Year { get; }
        public WorldEventKind Kind { get; }
        public int ActorId { get; }
        public int? NodeId { get; }
        public int? OtherActorId { get; }
        public string Detail { get; }

        public WorldEvent(int year, WorldEventKind kind, int actorId, int? nodeId, string detail = "", int? otherActorId = null)
        {
            Year = year; Kind = kind; ActorId = actorId; NodeId = nodeId;
            Detail = detail; OtherActorId = otherActorId;
        }
    }
}
