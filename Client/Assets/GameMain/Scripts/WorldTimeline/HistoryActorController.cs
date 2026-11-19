namespace WorldTimeline
{
    public enum HistoryReplayStatus { Replaying, RecordFinished, FateChanged, Dead, RouteBlocked, JoinedPlayer }

    public sealed class HistoryActorController
    {
        public int ActorId { get; }
        public LifeRecord Record { get; }
        public HistoryReplayStatus Status { get; private set; } = HistoryReplayStatus.Replaying;
        private int nextAction;
        private int partyRecord;
        private readonly System.Collections.Generic.Dictionary<(int Year, int Node), int> battles = new System.Collections.Generic.Dictionary<(int, int), int>();

        private readonly System.Collections.Generic.Dictionary<int,(int Id,int Definition)> recordedRewards =
            new System.Collections.Generic.Dictionary<int,(int,int)>();
        private readonly System.Collections.Generic.Dictionary<int,int> rewardOrigins =
            new System.Collections.Generic.Dictionary<int,int>();
        private bool indexedRewards;
        internal int RewardDefinition(int node) => recordedRewards.TryGetValue(node,out var item) ? item.Definition : 0;

        internal void EnableSharedRewards(WorldState world, ExpeditionParty party)
        {
            IndexRewards(world);
            party.EnableProgression();
            party.Equipment.EnableArtifacts();
            party.Equipment.ReconcileArchivedRewards(recordedRewards);
        }

        private void IndexRewards(WorldState world)
        {
            if (!indexedRewards)
            {
                int previous = 0;
                for (int index = 0; index < Record.Expedition.Count; index++)
                {
                    var entry = Record.Expedition[index];
                    int count = ExpeditionEquipment.ReadArchivedReward(entry.Equipment,out var reward);
                    if (count > previous && reward.Id > 0)
                    {
                        recordedRewards[entry.NodeId] = reward;
                        rewardOrigins[reward.Id] = entry.NodeId;
                        if (index < partyRecord && world.Nodes.TryGetValue(entry.NodeId,out var node) &&
                            node.ClearedInBattle && node.FirstClear?.ActorId == ActorId && node.FirstArtifact == 0)
                            node.FirstArtifact = reward.Definition;
                    }
                    previous = count;
                }
                indexedRewards = true;
            }
        }

        internal void RestoreParty(WorldState world, int battleNode = -1, int battle = 0, bool endOfYear = false)
        {
            var party = world.ExpeditionFor(ActorId);
            if (party == null) return;
            if (world.HistoricalGrowth)
            {
                IndexRewards(world);
                var actor = world.Actors[ActorId];
                if (!actor.IsAlive || actor.LeaderId.HasValue || actor.HasJoinedChosen) return;
                while (partyRecord < Record.Expedition.Count)
                {
                    var entry = Record.Expedition[partyRecord];
                    if (entry.Year > world.Year) break;
                    if (entry.Year == world.Year && entry.Battle > 0 && !endOfYear &&
                        (entry.NodeId != battleNode || entry.Battle > battle)) break;
                    if (entry.Year == world.Year && entry.Battle == 0 && battleNode >= 0) break;

                    var previous = partyRecord == 0 ? null : Record.Expedition[partyRecord-1];
                    partyRecord++;
                    if (entry.Year == world.Year && entry.NodeId == actor.NodeId &&
                        (entry.Battle == 0 || (entry.Battle == battle && entry.NodeId == battleNode)))
                        party.ApplyHistoricalPlan(previous,entry,rewardOrigins);
                }
                return;
            }
            bool reachedBattle = false;
            while (partyRecord < Record.Expedition.Count)
            {
                var entry = Record.Expedition[partyRecord];
                if (entry.Year > world.Year) break;
                // Map edits recorded after this battle belong to the following planning phase.
                if (entry.Year == world.Year && reachedBattle && entry.Battle == 0) break;
                if (entry.Year == world.Year && (entry.Battle > 0
                    ? entry.NodeId != battleNode || entry.Battle > battle
                    : world.Actors[ActorId].NodeId != entry.NodeId)) break;
                party.Restore(entry); partyRecord++;
                if (world.SharedClears) EnableSharedRewards(world,party);
                reachedBattle |= entry.Year == world.Year && entry.Battle > 0;
            }
        }

        internal void PrepareBattle(WorldState world, int node)
        {
            var key = (world.Year, node);
            battles.TryGetValue(key, out int count); battles[key] = ++count;
            RestoreParty(world, node, count);
        }

        internal HistoryActorController(int actorId, LifeRecord record) { ActorId = actorId; Record = record; }

        internal long? NextArrival(WorldSimulation simulation, int nodeId)
        {
            var world = simulation.State;
            var actor = world.Actors[ActorId];
            int? current = actor.NodeId;
            long year = world.Year + (long)actor.RemainingYears;
            if (actor.CurrentAction?.Kind == WorldActionKind.Move)
            {
                current = actor.CurrentAction.TargetNodeId;
                if (current == nodeId && year > world.Year &&
                    (Status != HistoryReplayStatus.Replaying || !Record.EndedInDeath || year <= Record.EndYear))
                    return year;
            }
            year += actor.StoryDelayYears;
            if (!current.HasValue) return null;
            if (Status == HistoryReplayStatus.FateChanged || actor.HasJoinedChosen)
                return year == world.Year && simulation.Chosen?.NextFollowerStep(world, current.Value) == nodeId
                    ? year + 1 : null;
            if (Status != HistoryReplayStatus.Replaying) return null;

            for (int i = nextAction; i < Record.Actions.Count; i++)
            {
                var entry = Record.Actions[i];
                year = System.Math.Max(year, entry.StartYear);
                if (entry.Action.Kind == WorldActionKind.JoinChosen || (Record.EndedInDeath && year >= Record.EndYear)) break;
                year += entry.Action.Years;
                if (Record.EndedInDeath && year > Record.EndYear) break;
                if (entry.Action.Kind != WorldActionKind.Move) continue;
                int destination = entry.Action.TargetNodeId;
                if (!world.Nodes[current.Value].Links.Contains(destination) || !world.Nodes.ContainsKey(destination)) break;
                if (destination == nodeId) return year;
                current = destination;
            }
            return null;
        }

        internal void Prepare(WorldSimulation simulation)
        {
            RestoreParty(simulation.State);
            var actor = simulation.State.Actors[ActorId];
            UpdateStatus(simulation.State);
            if (actor.LeaderId.HasValue) return;
            if (actor.IsAlive && actor.HasJoinedChosen) Status = HistoryReplayStatus.FateChanged;
            if (Status == HistoryReplayStatus.FateChanged && simulation.Chosen != null)
            {
                simulation.Chosen.PrepareFollower(simulation, actor);
                return;
            }
            if (Status != HistoryReplayStatus.Replaying || actor.CurrentAction != null || nextAction >= Record.Actions.Count) return;
            var entry = Record.Actions[nextAction];
            if (entry.StartYear > simulation.State.Year) return;
            if (entry.Action.Kind == WorldActionKind.JoinChosen && !simulation.CanJoinChosen(ActorId))
            {
                Status = HistoryReplayStatus.FateChanged;
                return;
            }
            if (entry.Action.Kind == WorldActionKind.Move &&
                (!simulation.State.Nodes.ContainsKey(entry.Action.TargetNodeId) ||
                 !simulation.State.Nodes[actor.NodeId.Value].Links.Contains(entry.Action.TargetNodeId)))
            {
                Status = HistoryReplayStatus.RouteBlocked;
                return;
            }
            var action = simulation.State.HistoricalGrowth && entry.Action.Kind == WorldActionKind.Cultivate
                ? WorldAction.Wait(entry.Action.Years) : entry.Action;
            simulation.AssignAction(ActorId, action);
            nextAction++;
        }

        internal void UpdateStatus(WorldState state)
        {
            var actor = state.Actors[ActorId];
            if (actor.HasJoinedPlayer) { Status = HistoryReplayStatus.JoinedPlayer; return; }
            if (!actor.IsAlive) { Status = HistoryReplayStatus.Dead; return; }
            if (Status != HistoryReplayStatus.Replaying || state.Year < Record.EndYear) return;
            // Death is an observed outcome, never an instruction imposed on the new life.
            if (Record.EndedInDeath)
            {
                Status = HistoryReplayStatus.FateChanged;
                actor.CurrentAction = null;
                actor.RemainingYears = 0;
            }
            else if (nextAction == Record.Actions.Count && actor.CurrentAction == null)
                Status = HistoryReplayStatus.RecordFinished;
        }
    }
}
