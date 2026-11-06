using System;
using System.Linq;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        public struct PreparationCharacter
        {
            public string Name, Weapon, Birthplace;
            public int Q, R, AttackBonus;
            public bool Used, Selected;
        }
        public struct PreparationHistory
        {
            public string Name, Detail;
            public bool Selected, CanSelect;
        }
        public static Action<WorldTimelineDebugView> OpenPreparation { get; set; }
        public event Action PreparationChanged;
        public int PreparationCharacterCount => roster.Count;
        public int PreparationHistoryCount => library.Count;
        public int PreparationCycle => (round - 1) / Math.Max(1, roster.Count) + 1;
        public int PreparationSelectedCount => library.Count(entry => entry.Selected);
        public int PreparationAvailableCount => roster.Count(character => !CharacterUsed(character));
        public int PreparationSelectedIndex => roster.IndexOf(startingCultivator);
        public bool CanBeginPreparation => preparing && !advancing && saveReadable && startingCultivator != null
            && !CharacterUsed(startingCultivator);
        public string PreparationIssue => saveIssue;

        private bool CharacterUsed(StartingCultivator character)
        {
            int cycleStart = ((round - 1) / Math.Max(1, roster.Count)) * roster.Count + 1;
            return savedLives.Any(entry => entry.Round >= cycleStart && entry.Record.BirthNodeId == character.Birth);
        }

        public PreparationCharacter GetPreparationCharacter(int index)
        {
            var character = roster[index];
            locations.TryGetValue(character.Birth, out var place);
            return new PreparationCharacter { Name = character.Name, Weapon = character.Weapon,
                Birthplace = place?.Name ?? TimelineText("Timeline_UI_115"),
                Q = place?.Q ?? 0, R = place?.R ?? 0, AttackBonus = character.Attack,
                Used = CharacterUsed(character), Selected = character == startingCultivator };
        }

        public PreparationHistory GetPreparationHistory(int index)
        {
            var entry = library[index];
            return new PreparationHistory { Name = entry.Name,
                Detail = TimelineText("Timeline_UI_RecordSummary", entry.Record.Actions.Count,
                    entry.Record.EndedInDeath ? TimelineText("Timeline_UI_116") : TimelineText("Timeline_UI_117")),
                Selected = entry.Selected,
                CanSelect = startingCultivator == null || entry.Record.BirthNodeId != startingCultivator.Birth };
        }

        public bool SelectPreparationCharacter(int index)
        {
            if (!preparing || advancing || index < 0 || index >= roster.Count || CharacterUsed(roster[index])) return false;
            startingCultivator = roster[index];
            foreach (var entry in library.Where(entry => entry.Record.BirthNodeId == startingCultivator.Birth)) entry.Selected = false;
            PreparationChanged?.Invoke();
            return true;
        }

        public bool SelectPreparationHistory(int index, bool selected)
        {
            if (!preparing || advancing || index < 0 || index >= library.Count) return false;
            var entry = library[index];
            if (selected && entry.Record.BirthNodeId == startingCultivator?.Birth) return false;
            if (selected)
            {
                // A cultivator's different lives can never coexist in the same run.
                int others = library.Count(candidate => candidate.Selected && candidate.Record.BirthNodeId != entry.Record.BirthNodeId);
                if (others >= 10) return false;
                foreach (var candidate in library.Where(candidate => candidate.Record.BirthNodeId == entry.Record.BirthNodeId))
                    candidate.Selected = false;
            }
            entry.Selected = selected;
            PreparationChanged?.Invoke();
            return true;
        }

        public void ClearPreparationHistories()
        {
            if (!preparing || advancing) return;
            foreach (var entry in library) entry.Selected = false;
            PreparationChanged?.Invoke();
        }

        public bool BeginPreparedRun()
        {
            if (!CanBeginPreparation) return false;
            NewRound();
            preparing = false;
            if (board == null) InitializeBoard();
            if (!UsesPrefabHud) OpenMapHud?.Invoke(this);
            return true;
        }
    }
}
