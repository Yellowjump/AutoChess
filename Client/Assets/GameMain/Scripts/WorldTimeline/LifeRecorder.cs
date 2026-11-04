using System.Collections.Generic;

namespace WorldTimeline
{
    public sealed class LifeRecorder
    {
        private readonly ActorState initial;
        private readonly List<int> initialItems;
        private readonly List<RecordedAction> actions = new List<RecordedAction>();
        private readonly List<WorldEvent> events = new List<WorldEvent>();
        private int? deathYear;

        internal LifeRecorder(ActorState actor)
        {
            initial = new ActorState(actor.Id, actor.NodeId.Value, actor.Health, actor.Attack, actor.Lifespan);
            initial.Talents = actor.Talents;
            initial.Level = actor.Level;
            initialItems = new List<int>(actor.Items);
        }

        internal void RecordAction(int year, WorldAction action) => actions.Add(new RecordedAction(year, action));
        internal void RecordEvent(WorldEvent entry)
        {
            if (entry.ActorId != initial.Id && entry.OtherActorId != initial.Id) return;
            events.Add(entry);
            if (entry.Kind == WorldEventKind.Died && entry.ActorId == initial.Id) deathYear = entry.Year;
        }

        public LifeRecord Snapshot(int currentYear) =>
            new LifeRecord(initial, initialItems, actions, events, deathYear ?? currentYear, deathYear.HasValue);
    }
}
