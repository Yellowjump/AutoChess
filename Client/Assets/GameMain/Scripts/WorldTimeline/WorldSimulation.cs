using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public sealed class WorldSimulation
    {
        public WorldState State { get; }
        public IReadOnlyList<WorldEvent> Events => events;
        public bool WaitingForPlayer => Player.IsAlive && !State.PlayerControlEnded && Player.CurrentAction == null && !State.IsEnded;
        public ChosenActorController Chosen { get; private set; }
        public CultivationConfig Cultivation { get; }
        public WorldStorySystem Stories { get; private set; }
        private readonly List<WorldEvent> events = new List<WorldEvent>();
        private readonly EncounterSystem encounters;
        private readonly FinalRitualSystem ritual = new FinalRitualSystem();
        private readonly LifeRecorder recorder;
        private readonly List<HistoryActorController> histories = new List<HistoryActorController>();
        public IReadOnlyList<HistoryActorController> Histories => histories.AsReadOnly();
        private ActorState Player => State.Actors[State.PlayerActorId];

        public WorldSimulation(WorldState state, IWorldBattleResolver battle = null, CultivationConfig cultivation = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Cultivation = cultivation;
            if (!state.Actors.ContainsKey(state.PlayerActorId)) throw new ArgumentException("Player actor missing.");
            foreach (var actor in state.Actors.Values)
                if (!actor.NodeId.HasValue || !state.Nodes.ContainsKey(actor.NodeId.Value))
                    throw new ArgumentException("Initial actor node missing.");
            encounters = new EncounterSystem(battle ?? new SimpleWorldBattleResolver()) { Growth = cultivation };
            recorder = new LifeRecorder(Player);
            State.PrepareHistoricalParty = (actor, node) => histories.FirstOrDefault(h => h.ActorId == actor)?.PrepareBattle(State, node);
        }

        public LifeRecord RecordPlayerLife() => recorder.Snapshot(State.Year, State.Expedition?.Records);
        public bool RecordCompletedLife()
        {
            if (!WaitingForPlayer || !State.PlayerCompletedPersonalStory) return false;
            State.Finish(RunOutcome.LifeRecorded);
            return true;
        }
        public void SetBattleSettlementPresenter(Func<BattleSettlement, IEnumerator> presenter)
            => encounters.PresentSettlement = presenter;
        public void SetBattlePreparationPresenter(Func<int, IEnumerator> presenter)
            => encounters.PrepareBattle = presenter;
        public void ConfigureStories(StoryConfig config, int seed = 1, bool interactivePlayer = false,
            IEnumerable<RecordedStoryChoice> replayPlayerChoices = null)
        {
            if (State.Year != 0 || Stories != null) throw new InvalidOperationException("Configure stories once at year zero.");
            Stories = new WorldStorySystem(State, config, Cultivation, seed, Emit, recorder.RecordStoryChoice,
                (actor, story, occurrence) => histories.FirstOrDefault(h => h.ActorId == actor && h.Status == HistoryReplayStatus.Replaying)
                    ?.Record.StoryChoices.Where(c => c.EventId == story && c.Occurrence == occurrence),
                replayPlayerChoices) { InteractivePlayer = interactivePlayer };
            encounters.StoryResolver = Stories.ResolveRoutine;
        }

        public void ConfigureFinalRitual(int bossHealth, int bossAttack, int periodYears = 100, int? initialDeadlineYear = null)
        {
            if (State.Year != 0 || State.IsEnded || Chosen == null || State.Ritual != null)
                throw new InvalidOperationException("Configure the ritual once at year zero after configuring chosen.");
            var data = new FinalRitualState(Chosen.FinalNodeId, periodYears, bossHealth, bossAttack, initialDeadlineYear);
            State.Ritual = data;
            State.Nodes[data.NodeId].MonsterHealth = data.BossHealth;
            State.Nodes[data.NodeId].MonsterAttack = data.BossAttack;
            encounters.ConfigureRitual(ritual);
        }

        public void ConfigureChosen(ActorState actor, IEnumerable<int> route, int yearsPerStep = 10, int absorptionPercent = 10,
            IEnumerable<int> travelYears = null)
        {
            if (State.Year != 0 || Chosen != null || State.Actors.ContainsKey(actor.Id))
                throw new InvalidOperationException("Configure one chosen actor at year zero using a unique ID.");
            var controller = new ChosenActorController(State, actor, route, yearsPerStep, absorptionPercent, travelYears);
            State.Actors.Add(actor.Id, actor);
            State.ChosenActorId = actor.Id;
            Chosen = controller;
            encounters.ConfigureChosen(controller);
        }

        public bool CanJoinChosen(int actorId)
        {
            if (Chosen == null || State.IsEnded) return false;
            var actor = State.Actors[actorId];
            var leader = State.Actors[Chosen.ActorId];
            return actor.Id != leader.Id && actor.IsAlive && leader.IsAlive && !actor.LeaderId.HasValue &&
                actor.NodeId.HasValue && actor.NodeId == leader.NodeId;
        }

        // Call before the first annual step. Instance IDs must be unique within this world.
        public HistoryActorController AddHistory(LifeRecord record, int instanceId)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (State.Year != 0 || State.Actors.ContainsKey(instanceId) || !State.Nodes.ContainsKey(record.BirthNodeId))
                throw new InvalidOperationException("History must spawn at year zero with a unique ID and valid birth node.");
            var controller = new HistoryActorController(instanceId, record);
            State.Actors.Add(instanceId, record.CreateActor(instanceId));
            if (record.Expedition.Count > 0)
            {
                State.HistoricalExpeditions.Add(instanceId, new ExpeditionParty(State, instanceId, (histories.Count + 1) * 4));
                controller.RestoreParty(State);
            }
            histories.Add(controller);
            return controller;
        }

        // Controllers enqueue actions without advancing time; the player's submission starts the clock.
        public void AssignAction(int actorId, WorldAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            ActorState actor = State.Actors[actorId];
            if (actorId == State.PlayerActorId && State.Expedition?.ProgressionEnabled == true && action.Kind == WorldActionKind.Cultivate)
                throw new InvalidOperationException("Growth comes from exploration and victories, not stationary cultivation.");
            if (State.IsEnded || (actorId == State.PlayerActorId && State.PlayerControlEnded) || !actor.IsAlive || actor.LeaderId.HasValue || actor.CurrentAction != null || !actor.NodeId.HasValue)
                throw new InvalidOperationException("Actor cannot start an action.");
            if (action.Kind == WorldActionKind.JoinChosen)
            {
                if (!CanJoinChosen(actorId)) throw new InvalidOperationException("Chosen must be alive at the same node.");
                if (actorId == State.PlayerActorId) recorder.RecordAction(State.Year, action);
                actor.LeaderId = Chosen.ActorId;
                actor.HasJoinedChosen = true;
                if (actorId == State.PlayerActorId) State.PlayerControlEnded = true;
                actor.IsDisguised = false;
                Emit(new WorldEvent(State.Year, WorldEventKind.Joined, actorId, actor.NodeId));
                return;
            }
            if (action.Kind == WorldActionKind.Move &&
                (!State.Nodes.ContainsKey(action.TargetNodeId) || !State.Nodes[actor.NodeId.Value].Links.Contains(action.TargetNodeId)))
                throw new InvalidOperationException("Target is not connected.");
            if (action.Kind == WorldActionKind.Disguise && CanJoinChosen(actorId))
                throw new InvalidOperationException("Cannot set an ambush while chosen is already present.");
            if (actorId == State.PlayerActorId) recorder.RecordAction(State.Year, action);
            if (action.Kind != WorldActionKind.Wait) actor.IsDisguised = action.Kind == WorldActionKind.Disguise;
            if (action.Kind == WorldActionKind.Disguise)
            {
                actor.DisguisedYear = State.Year;
                Emit(new WorldEvent(State.Year, WorldEventKind.Disguised, actorId, actor.NodeId));
            }
            actor.CurrentAction = action;
            actor.RemainingYears = action.Years;
            if (action.Kind == WorldActionKind.Move)
            {
                actor.TravelOriginNodeId = actor.NodeId;
                actor.NodeId = null;
            }
            Emit(new WorldEvent(State.Year, WorldEventKind.ActionStarted, actor.Id, actor.NodeId, action.Kind.ToString()));
        }

        public void SubmitPlayerAction(WorldAction action) => CompleteSynchronously(SubmitPlayerActionRoutine(action));
        public void AdvanceOneYear() => CompleteSynchronously(AdvanceOneYearRoutine());

        public IEnumerator SubmitPlayerActionRoutine(WorldAction action, Func<IEnumerator> presentArrivals = null)
        {
            AssignAction(State.PlayerActorId, action);
            while (Player.IsAlive && !State.IsEnded && Player.CurrentAction != null)
                yield return AdvanceOneYearRoutine(presentArrivals);
        }

        private static void CompleteSynchronously(IEnumerator routine)
        {
            using (routine as IDisposable)
            {
                while (routine.MoveNext())
                {
                    if (routine.Current is IEnumerator nested) CompleteSynchronously(nested);
                    else throw new InvalidOperationException("This battle requires asynchronous advancement.");
                }
            }
        }

        public IEnumerator AdvanceOneYearRoutine(Func<IEnumerator> presentArrivals = null)
        {
            if (State.IsEnded) yield break;
            foreach (var history in histories.OrderBy(h => h.ActorId)) history.Prepare(this);
            Chosen?.Prepare(this);
            SyncPartyPositions();
            State.Year = checked(State.Year + 1);
            // The deadline is exclusive: arriving exactly on it is already too late.
            if (ritual.ExpireIfDue(State, Emit))
            {
                UpdateHistoryStatuses();
                yield break;
            }
            var actors = State.Actors.Values.OrderBy(a => a.Id).ToList();
            var arrivals = new List<int>();

            bool replayLegacyLifespan = State.Year <= State.LegacyLifespanDeathThroughYear;
            foreach (var actor in actors.Where(a => a.IsAlive))
            {
                actor.Age++;
                if (!replayLegacyLifespan || actor.Age < actor.Lifespan) continue;
                int? node = actor.NodeId;
                if (actor.Id == State.ChosenActorId && !node.HasValue && actor.CurrentAction != null)
                {
                    foreach (var member in actors.Where(a => a.IsAlive && a.LeaderId == actor.Id))
                        member.NodeId = actor.TravelOriginNodeId;
                }
                actor.Die();
                Emit(new WorldEvent(State.Year, WorldEventKind.Died, actor.Id, node, "Lifespan"));
            }
            // Register ALL arrivals before resolving any encounter.
            foreach (var actor in actors.Where(a => a.IsAlive && a.CurrentAction != null))
            {
                actor.RemainingYears--;
                if (actor.CurrentAction.Kind != WorldActionKind.Move || actor.RemainingYears != 0) continue;
                actor.NodeId = actor.CurrentAction.TargetNodeId;
                arrivals.Add(actor.NodeId.Value);
                Emit(new WorldEvent(State.Year, WorldEventKind.Arrived, actor.Id, actor.NodeId));
            }
            SyncPartyPositions();
            foreach (var history in histories.Where(h => h.Status == HistoryReplayStatus.FateChanged))
                if (CanJoinChosen(history.ActorId))
                {
                    var follower = State.Actors[history.ActorId];
                    follower.CurrentAction = null;
                    follower.RemainingYears = 0;
                    AssignAction(follower.Id, WorldAction.JoinChosen());
                }
            if (presentArrivals != null) yield return presentArrivals();
            yield return encounters.ResolveRoutine(State, arrivals, Emit);
            if (!State.IsEnded)
                foreach (var actor in actors.Where(a => a.NodeId.HasValue && arrivals.Contains(a.NodeId.Value) &&
                    a.CurrentAction?.Kind == WorldActionKind.Move && a.RemainingYears == 0))
                    State.ExpeditionFor(actor.Id)?.Explore(actor.NodeId.Value, Cultivation, Emit);
            if (State.IsEnded)
            {
                foreach (var history in histories) history.UpdateStatus(State);
                yield break;
            }
            foreach (var actor in actors.Where(a => a.IsAlive && a.CurrentAction != null))
            {
                if (actor.CurrentAction.Kind == WorldActionKind.Cultivate)
                {
                    if (actor.Id != State.ChosenActorId)
                        CultivationSystem.Gain(actor, State.Nodes[actor.NodeId.Value].CultivationPerYear, Cultivation, State.Year, Emit, State, "cultivation");
                    Emit(new WorldEvent(State.Year, WorldEventKind.Cultivated, actor.Id, actor.NodeId));
                }
                if (actor.RemainingYears != 0) continue;
                Emit(new WorldEvent(State.Year, WorldEventKind.ActionCompleted, actor.Id, actor.NodeId, actor.CurrentAction.Kind.ToString()));
                actor.CurrentAction = null;
                if (actor.StoryDelayYears > 0)
                {
                    actor.CurrentAction = WorldAction.Wait(actor.StoryDelayYears);
                    actor.RemainingYears = actor.StoryDelayYears; actor.StoryDelayYears = 0;
                    Emit(new WorldEvent(State.Year, WorldEventKind.ActionStarted, actor.Id, actor.NodeId, "Story delay"));
                }
            }
            if (State.BalanceSink != null && State.Actors.TryGetValue(State.PlayerActorId, out var player) &&
                player.IsAlive && !State.PlayerControlEnded)
                State.RecordBalance(player, new BalanceSample
                {
                    type = "year_snapshot", hp = player.Health, maxHp = player.MaxHealth, attack = player.CombatAttack
                });
            UpdateHistoryStatuses();
            if (Chosen != null && !State.Actors[Chosen.ActorId].IsAlive)
                foreach (var actor in actors.Where(a => a.LeaderId == Chosen.ActorId)) actor.LeaderId = null;
        }

        private void UpdateHistoryStatuses()
        {
            foreach (var history in histories)
            {
                var previous = history.Status;
                history.UpdateStatus(State);
                if (previous != HistoryReplayStatus.Replaying || history.Status != HistoryReplayStatus.FateChanged)
                    continue;

                var actor = State.Actors[history.ActorId];
                int? clearer = null;
                if (history.Record.GuardianDeathNodeId is int nodeId && actor.NodeId == nodeId &&
                    State.Nodes.TryGetValue(nodeId, out var node) &&
                    node.GuardianDefeat is { } defeat && defeat.ActorId != actor.Id && defeat.Year < State.Year)
                    clearer = defeat.ActorId;
                Emit(new WorldEvent(State.Year, WorldEventKind.FateChanged, actor.Id, actor.NodeId, otherActorId: clearer));
            }
        }

        private void SyncPartyPositions()
        {
            if (Chosen == null) return;
            if (!State.Actors[Chosen.ActorId].IsAlive) return;
            foreach (var actor in State.Actors.Values.Where(a => a.IsAlive && a.LeaderId == Chosen.ActorId))
                actor.NodeId = State.Actors[Chosen.ActorId].NodeId;
        }

        private void Emit(WorldEvent entry)
        {
            events.Add(entry);
            recorder.RecordEvent(entry);
            if (State.BalanceSink == null || entry.ActorId != State.PlayerActorId) return;
            if (entry.Kind == WorldEventKind.Arrived || entry.Kind == WorldEventKind.Died ||
                entry.Kind == WorldEventKind.ActionStarted || entry.Kind == WorldEventKind.ChosenEncounter)
                State.RecordBalance(State.Actors[entry.ActorId], new BalanceSample
                {
                    type = entry.Kind.ToString(), source = entry.Detail, nodeId = entry.NodeId ?? -1
                });
        }
    }
}
