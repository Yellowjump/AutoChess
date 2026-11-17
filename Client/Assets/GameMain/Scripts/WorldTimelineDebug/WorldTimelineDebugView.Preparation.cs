using System;
using System.Collections.Generic;
using System.Linq;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        public struct PreparationCharacter
        {
            public string Name, Technique, Birthplace;
            public int Q, R, AttackBonus, PortraitIndex;
            public bool Used, Selected;
        }
        public struct PreparationHistory
        {
            public string Name, Detail;
            public bool JoinsRun;
        }
        private readonly List<StartingCultivator> playableRoster = new List<StartingCultivator>();
        private const int FinaleCultivator = 8101;

        private void UnlockFinaleCultivator()
        {
            if (playableRoster.Any(character => character.Item == FinaleCultivator)) return;
            bool allStoriesRecorded = library.Any(entry => entry.Record.SluiceOpeningYear.HasValue) &&
                library.Any(entry => entry.Record.NorthRelayRestoredYear.HasValue) &&
                library.Any(entry => entry.Record.ContractRevokedYear.HasValue);
            var candidate = roster.FirstOrDefault(character => character.Item == FinaleCultivator);
            if (candidate != null && (allStoriesRecorded ||
                library.Any(entry => entry.Record.BirthNodeId == candidate.Birth)))
                playableRoster.Add(candidate);
        }
        public static Action<WorldTimelineDebugView> OpenPreparation { get; set; }
        public event Action PreparationChanged;
        public int PreparationCharacterCount => playableRoster.Count;
        public int PreparationHistoryCount => library.Count;
        private int PreparationCycleSize => playableRoster.Count(character => character.Item != FinaleCultivator);
        public int PreparationCycle => (round - 1) / Math.Max(1, PreparationCycleSize) + 1;
        public int PreparationCompanionCount => CompanionRecords().Count();
        public int PreparationAvailableCount => playableRoster.Count(character => !CharacterUsed(character));
        public int PreparationSelectedIndex => playableRoster.IndexOf(startingCultivator);
        public bool CanBeginPreparation => preparing && !advancing && saveReadable && startingCultivator != null
            && !CharacterUsed(startingCultivator);
        public string PreparationIssue => saveIssue;
        public string RelayPreparationHint => TimelineText(startingCultivator?.Item switch
        {
            QiMoStartingItem => "Timeline_QiStory_Prepare",
            SecondRelayCultivator => "Timeline_NorthRelay_Prepare",
            FinaleCultivator => "Timeline_Sluice_FinalePrepare",
            _ => "Timeline_Sluice_Prepare"
        });

        private IEnumerable<LifeRecord> CompanionRecords() =>
            library.Where(entry => entry.Record.BirthNodeId != startingCultivator?.Birth).Select(entry => entry.Record);
        public string RelayPreparationStatus
        {
            get
            {
                var companions = CompanionRecords().ToArray();
                var qi = companions.FirstOrDefault(record => record.ContractRevokedYear.HasValue);
                var gusong = companions.FirstOrDefault(record => record.NorthRelayRestoredYear.HasValue);
                var chili = companions.FirstOrDefault(record => record.SluiceOpeningYear.HasValue);
                if (qi != null && gusong != null && chili != null) return TimelineText("Timeline_Sluice_FinalePlanned");
                if (qi != null && gusong != null) return TimelineText("Timeline_NorthRelay_BothPlanned");
                if (qi?.ContractRevokedYear is int revoked)
                    return TimelineText("Timeline_QiStory_Forecast", revoked);
                if (gusong?.NorthRelayRestoredYear is int restored)
                    return TimelineText("Timeline_NorthRelay_PrepareReplay", restored);
                return chili?.SluiceOpeningYear is int year ? TimelineText("Timeline_Sluice_PrepareReplay", year) :
                    TimelineText("Timeline_Sluice_Select");
            }
        }

        private bool CharacterUsed(StartingCultivator character)
        {
            if (character.Item == FinaleCultivator) return false;
            int cycleStart = (PreparationCycle - 1) * PreparationCycleSize + 1;
            return savedLives.Any(entry => entry.Round >= cycleStart && entry.Record.BirthNodeId == character.Birth);
        }

        public PreparationCharacter GetPreparationCharacter(int index)
        {
            var character = playableRoster[index];
            locations.TryGetValue(character.Birth, out var place);
            place ??= scenarioData?.Locations?.FirstOrDefault(row => HexWorldMap.Id(row.Q, row.R) == character.Birth);
            return new PreparationCharacter { Name = character.Name,
                Technique = character.Item == 8104 && scenarioData?.Techniques?.ContainsKey(1) == true
                    ? TechniqueName(1) : character.Technique,
                Birthplace = place?.Name ?? TimelineText("Timeline_UI_115"),
                Q = place?.Q ?? 0, R = place?.R ?? 0, AttackBonus = character.Attack,
                PortraitIndex = roster.IndexOf(character),
                Used = CharacterUsed(character), Selected = character == startingCultivator };
        }

        public PreparationHistory GetPreparationHistory(int index)
        {
            var entry = library[index];
            return new PreparationHistory { Name = entry.Name,
                Detail = entry.Record.ContractRevokedYear is int revokedYear
                    ? TimelineText("Timeline_QiStory_Record", revokedYear)
                    : entry.Record.NorthRelayRestoredYear is int restoredYear
                    ? TimelineText("Timeline_NorthRelay_Record", restoredYear)
                    : entry.Record.SluiceOpeningYear is int sluiceYear
                    ? TimelineText("Timeline_Sluice_Record", sluiceYear)
                    : entry.Record.RitualBreakYear is int breakYear
                    ? TimelineText("Timeline_Relay_Record", breakYear)
                    : TimelineText("Timeline_UI_RecordSummary", entry.Record.Actions.Count,
                        entry.Record.EndedInDeath ? TimelineText("Timeline_UI_116") : TimelineText("Timeline_UI_117")),
                JoinsRun = entry.Record.BirthNodeId != startingCultivator?.Birth };
        }

        public bool SelectPreparationCharacter(int index)
        {
            if (!preparing || advancing || index < 0 || index >= playableRoster.Count || CharacterUsed(playableRoster[index])) return false;
            startingCultivator = playableRoster[index];
            PreparationChanged?.Invoke();
            return true;
        }

        public bool BeginPreparedRun()
        {
            if (!CanBeginPreparation) return false;
            NewRound(useOpeningMap: returnToMenu != null);
            preparing = false;
            if (board == null) InitializeBoard();
            ShowOpeningMap();
            if (!UsesPrefabHud) OpenMapHud?.Invoke(this);
            return true;
        }
    }
}
