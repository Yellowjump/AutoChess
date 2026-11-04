using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public sealed class RecordedAction
    {
        public int StartYear { get; }
        public WorldAction Action { get; }
        public RecordedAction(int startYear, WorldAction action)
        {
            if (startYear < 0) throw new ArgumentOutOfRangeException(nameof(startYear));
            StartYear = startYear;
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }
    }

    // Immutable source life. A replay creates a separate ActorState, never modifies this record.
    public sealed class LifeRecord
    {
        public int SourceActorId { get; }
        public int BirthNodeId { get; }
        public int InitialHealth { get; }
        public int InitialAttack { get; }
        public int Lifespan { get; }
        public int InitialLevel { get; }
        public TalentProfile Talents { get; }
        public IReadOnlyList<int> InitialItems { get; }
        public IReadOnlyList<RecordedAction> Actions { get; }
        public IReadOnlyList<WorldEvent> Events { get; }
        public int EndYear { get; }
        public bool EndedInDeath { get; }

        internal LifeRecord(ActorState initial, IEnumerable<int> items, IEnumerable<RecordedAction> actions,
            IEnumerable<WorldEvent> events, int endYear, bool endedInDeath)
        {
            SourceActorId = initial.Id; BirthNodeId = initial.NodeId.Value;
            InitialHealth = initial.Health; InitialAttack = initial.Attack; Lifespan = initial.Lifespan;
            InitialLevel = initial.Level; Talents = initial.Talents;
            InitialItems = Array.AsReadOnly(items.ToArray());
            Actions = Array.AsReadOnly(actions.ToArray());
            Events = Array.AsReadOnly(events.ToArray());
            EndYear = endYear; EndedInDeath = endedInDeath;
        }

        public ActorState CreateActor(int instanceId)
        {
            var actor = new ActorState(instanceId, BirthNodeId, InitialHealth, InitialAttack, Lifespan);
            actor.Level = InitialLevel; actor.Talents = Talents;
            actor.Items.AddRange(InitialItems);
            return actor;
        }
    }
}
