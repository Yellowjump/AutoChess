using System;
using System.Collections.Generic;

namespace WorldTimeline
{
    public enum RunOutcome { Running, Cleared, WorldDestroyed, PlayerKilledChosen, LifeRecorded }

    public sealed class WorldState
    {
        // Year zero is 元年; completing one annual step advances to year one.
        public int Year { get; internal set; }
        public HashSet<int> AcquiredPlayerItems { get; } = new HashSet<int>();
        public HashSet<string> DefeatedGuardianSpecies { get; } = new HashSet<string>();
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
        // Populated from the same unlocked selection roster that starts a life.
        public HashSet<int> PlayableBirths { get; } = new HashSet<int>();
        public bool PastInvitations { get; internal set; }
        private readonly Dictionary<int,int> returnedStageHealth = new Dictionary<int,int>();
        public int? ReturnedStageHealth(int role) => returnedStageHealth.TryGetValue(role,out int hp) ? hp : (int?)null;
        internal void ReturnStage(int role,int actor,int health)
        {
            if (StageOwner(role) != actor) return;
            returnedStageHealth[role] = health;
            if (health > 0) stageOwners.Remove(role);
        }
        internal void TakeReturnedStage(int role) => returnedStageHealth.Remove(role);
        public bool HistoricalGrowth { get; internal set; }
        public bool RainTasselEnabled { get; private set; }
        public void EnableRainTassel() => RainTasselEnabled = true;
        public bool RelayClaspEnabled { get; private set; }
        public void EnableRelayClasp() => RelayClaspEnabled = true;
        public bool ReprisalSealEnabled { get; private set; }
        public void EnableReprisalSeal() => ReprisalSealEnabled = true;
        public bool HeartLampEnabled { get; private set; }
        public void EnableHeartLamp() => HeartLampEnabled = true;
        public bool TinderPouchEnabled { get; private set; }
        public void EnableTinderPouch() => TinderPouchEnabled = true;
        public bool MeridianSpikeEnabled { get; private set; }
        public void EnableMeridianSpike() => MeridianSpikeEnabled = true;
        public bool ThunderBellEnabled { get; private set; }
        public void EnableThunderBell() => ThunderBellEnabled = true;
        public bool FalterMirrorEnabled { get; private set; }
        public void EnableFalterMirror() => FalterMirrorEnabled = true;
        public bool LastStandCharmEnabled { get; private set; }
        public void EnableLastStandCharm() => LastStandCharmEnabled = true;
        public bool AfflictionSealEnabled { get; private set; }
        public void EnableAfflictionSeal() => AfflictionSealEnabled = true;
        public bool SupportArtifactsEnabled { get; private set; }
        public void EnableSupportArtifacts() => SupportArtifactsEnabled = true;
        public bool CriticalArtifactsEnabled { get; private set; }
        public void EnableCriticalArtifacts() => CriticalArtifactsEnabled = true;
        public bool QuakeSpikeEnabled { get; private set; }
        public void EnableQuakeSpike() => QuakeSpikeEnabled = true;
        public bool SwordTraining { get; private set; }
        public void EnableSwordTraining() => SwordTraining = true;
        public bool SignatureArtifacts { get; internal set; }
        public bool SharedClears { get; internal set; }
        private readonly Dictionary<int,int> stageOwners = new Dictionary<int,int>();
        public int? StageOwner(int role) => stageOwners.TryGetValue(role, out var owner) ? owner : (int?)null;
        internal bool ClaimStage(int role,int actor)
        {
            if (!stageOwners.ContainsKey(role)) stageOwners.Add(role,actor);
            return !SharedClears || stageOwners[role]==actor;
        }
        internal Func<int,int,int> HistoricalRewardDefinition;
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

        public bool ShortAncientSwordOpening { get; set; } = true;
        public bool AncientSwordWaitsForHit { get; set; } = true;
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
