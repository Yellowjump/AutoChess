using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public enum TechniqueRequirement
    {
        None,
        BattleExperience,
        SkillKills,
        Item
    }

    // Requirement and Value describe how to enter this stage. For Item, Value is the item ID.
    public readonly struct TechniqueStage
    {
        public int SkillId { get; }
        public TechniqueRequirement Requirement { get; }
        public int Value { get; }

        public TechniqueStage(int skillId, TechniqueRequirement requirement = TechniqueRequirement.None, int value = 0)
        {
            if (skillId <= 0) throw new ArgumentOutOfRangeException(nameof(skillId));
            if (!Enum.IsDefined(typeof(TechniqueRequirement), requirement))
                throw new ArgumentOutOfRangeException(nameof(requirement));
            if (requirement == TechniqueRequirement.None ? value != 0 : value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            SkillId = skillId;
            Requirement = requirement;
            Value = value;
        }
    }

    public sealed class TechniqueDefinition
    {
        public int Id { get; }
        public IReadOnlyList<TechniqueStage> Stages { get; }

        public TechniqueDefinition(int id, IEnumerable<TechniqueStage> stages)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            var ordered = stages.ToArray();
            if (ordered.Length == 0 || ordered[0].Requirement != TechniqueRequirement.None ||
                ordered.Skip(1).Any(stage => stage.Requirement == TechniqueRequirement.None) ||
                ordered.Any(stage => stage.SkillId <= 0))
                throw new ArgumentException("A technique needs a starting stage and a requirement for each upgrade.", nameof(stages));
            Id = id;
            Stages = Array.AsReadOnly(ordered);
        }
    }

    public sealed class TechniqueProgress
    {
        private readonly TechniqueDefinition definition;
        public int Rank { get; private set; } = 1;
        public int SkillId => definition.Stages[Rank - 1].SkillId;
        public int Progress { get; private set; }
        public TechniqueStage? NextStage => Rank < definition.Stages.Count ? definition.Stages[Rank] : (TechniqueStage?)null;

        internal TechniqueDefinition Definition => definition;
        internal TechniqueProgress Copy() => new TechniqueProgress(definition, Rank, Progress);
        internal TechniqueProgress(TechniqueDefinition definition, int rank, int progress) : this(definition)
        {
            if (rank < 1 || rank > definition.Stages.Count || progress < 0 ||
                (rank == definition.Stages.Count ? progress != 0 : progress >= definition.Stages[rank].Value))
                throw new ArgumentException("Invalid saved technique.");
            Rank = rank;
            Progress = progress;
        }

        internal TechniqueProgress(TechniqueDefinition definition) { this.definition = definition; }

        internal void SettleBattle(int usedRank, int experience, int skillKills, List<int> items)
        {
            // Only the stage equipped at battle start gets credit for this battle.
            if (usedRank != Rank || !NextStage.HasValue) return;
            var next = NextStage.Value;
            int earned = next.Requirement == TechniqueRequirement.BattleExperience ? experience :
                next.Requirement == TechniqueRequirement.SkillKills ? skillKills : 0;
            Progress += Math.Min(earned, next.Value - Progress);
            if (Progress == next.Value) Advance();
            CheckItems(items);
        }

        internal void CheckItems(List<int> items)
        {
            while (NextStage is TechniqueStage next && next.Requirement == TechniqueRequirement.Item &&
                items.Contains(next.Value))
                Advance();
        }

        private void Advance()
        {
            Rank++;
            Progress = 0;
        }
    }
}
