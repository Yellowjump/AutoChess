using System.Collections.Generic;

namespace WorldTimeline
{
    public enum RunOutcome { Running, Cleared, WorldDestroyed, PlayerKilledChosen }

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
        public bool PlayerControlEnded { get; internal set; }
        public Dictionary<int, ActorState> Actors { get; } = new Dictionary<int, ActorState>();
        public Dictionary<int, NodeState> Nodes { get; } = new Dictionary<int, NodeState>();
        public WorldState(int playerActorId) { PlayerActorId = playerActorId; }

        internal void Finish(RunOutcome outcome)
        {
            if (IsEnded) return;
            Outcome = outcome;
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

    public enum WorldEventKind { ActionStarted, Arrived, Cultivated, ActionCompleted, Battle, Died, Looted, Joined, Disguised, Absorbed, RitualReset, RitualCompleted, WorldCleared, RunEnded, Breakthrough }

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
