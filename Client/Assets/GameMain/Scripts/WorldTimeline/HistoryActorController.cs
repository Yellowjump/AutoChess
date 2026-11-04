namespace WorldTimeline
{
    public enum HistoryReplayStatus { Replaying, RecordFinished, FateChanged, Dead, RouteBlocked }

    public sealed class HistoryActorController
    {
        public int ActorId { get; }
        public LifeRecord Record { get; }
        public HistoryReplayStatus Status { get; private set; } = HistoryReplayStatus.Replaying;
        private int nextAction;

        internal HistoryActorController(int actorId, LifeRecord record) { ActorId = actorId; Record = record; }

        internal void Prepare(WorldSimulation simulation)
        {
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
