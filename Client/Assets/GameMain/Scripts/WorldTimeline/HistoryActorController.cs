namespace WorldTimeline
{
    public enum HistoryReplayStatus { Replaying, RecordFinished, FateChanged, Dead, RouteBlocked }

    public sealed class HistoryActorController
    {
        public int ActorId { get; }
        public LifeRecord Record { get; }
        public HistoryReplayStatus Status { get; private set; } = HistoryReplayStatus.Replaying;
        private int nextAction;
        private int partyRecord;
        private readonly System.Collections.Generic.Dictionary<(int Year, int Node), int> battles = new System.Collections.Generic.Dictionary<(int, int), int>();

        internal void RestoreParty(WorldState world, int battleNode = -1, int battle = 0)
        {
            var party = world.ExpeditionFor(ActorId);
            if (party == null) return;
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
            simulation.AssignAction(ActorId, entry.Action);
            nextAction++;
        }

        internal void UpdateStatus(WorldState state)
        {
            var actor = state.Actors[ActorId];
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
