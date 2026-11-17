using System;
using System.Collections.Generic;
using System.Linq;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        private const int FirstRelayYear = 30;
        private const int SecondRelayYear = 60;
        private const int ThirdRelayYear = 90;
        private const int SecondRelayCultivator = 8109;
        private const int CampaignDeadline = 110;
        private const int OpenSluiceOption = 4201;
        private const string KilnClue = "border_kiln_copy";
        private const string SluicePlan = "border_sluice_plan";
        private static readonly int SwordwashSpring = HexWorldMap.Id(0, -7);
        private static readonly int SealmudKiln = HexWorldMap.Id(6, -8);
        private static readonly int AnchorstoneWorkshop = HexWorldMap.Id(-4, -3);
        private static readonly int OldSluice = HexWorldMap.Id(-3, -2);
        private bool sluiceCampaignActive;
        private bool northRelayActive;
        private bool contractStoryActive;
        private const int ContractAltarStory = 45, ContractAltarLocation = 71;
        private const int ReadContractLedgerOption = 3002, RevokeContractOption = 4501;
        private const string ContractLedgerRead = "qi_contract_ledger";
        private static readonly int ContractGallery = HexWorldMap.Id(-8, 2);
        private static readonly int ContractAltar = HexWorldMap.Id(-6, 1);
        private static readonly int TwinGolemGate = HexWorldMap.Id(-4, 0);
        private const int NorthRelayRosterStory = 43, NorthRelayGateStory = 44;
        private const int LegacyTrailOption = 2901, CopyTrailNamesOption = 2902;
        private const int ReadNorthRelayRosterOption = 4301, RestoreNorthRelayOption = 4401;
        private const string NorthTrailNames = "north_relay_trail_names", NorthRoster = "north_relay_roster";
        private static readonly int PineTrail = HexWorldMap.Id(-2, 8);
        private static readonly int TrailkeepersHut = HexWorldMap.Id(-3, 7);
        private static readonly int NorthernRelay = HexWorldMap.Id(-3, 5);
        private static readonly int NorthRelaySpring = HexWorldMap.Id(-3, 0);

        private sealed class RelayStep
        {
            public readonly int NodeId, TrainUntil, SuggestedLevel;
            public readonly string ArrivalHint;

            public RelayStep(int q, int r, int trainUntil = 0, int suggestedLevel = 0, string arrivalHint = null)
            {
                NodeId = HexWorldMap.Id(q, r);
                TrainUntil = trainUntil;
                SuggestedLevel = suggestedLevel;
                ArrivalHint = arrivalHint;
            }
        }

        private static readonly RelayStep[] FirstRelayRoute =
        {
            new(2, -10, arrivalHint: "Timeline_Relay_Blade"), new(1, -9, arrivalHint: "Timeline_Relay_Gate"),
            new(1, -8), new(0, -7, 22, 5), new(0, -6), new(-1, -5), new(-1, -4), new(-1, -3),
            new(-1, -2), new(-1, -1), new(0, -1), new(0, 0)
        };
        private static readonly RelayStep[] SecondRelayRoute =
        {
            new(-4, 9, arrivalHint: "Timeline_Relay_Wedge"), new(-3, 9, 51, 6, "Timeline_Relay_Grotto"),
            new(-2, 8), new(-2, 7), new(-2, 6), new(-1, 5), new(-1, 4), new(-1, 3),
            new(0, 2), new(0, 1), new(0, 0)
        };
        private static readonly RelayStep[] ThirdRelayRoute =
        {
            new(-10, 2, 3, 2), new(-9, 2, 13, 3, "Timeline_Relay_Seal"),
            new(-8, 2), new(-7, 1), new(-6, 1), new(-5, 1), new(-4, 1), new(-3, 1),
            new(-3, 2, 87, 6), new(-2, 2), new(-1, 1), new(0, 0)
        };

        private static int NextRelayYear(IEnumerable<LifeRecord> records)
        {
            var years = new HashSet<int?>(records.Select(record => record.RitualBreakYear));
            if (!years.Contains(FirstRelayYear)) return FirstRelayYear;
            if (!years.Contains(SecondRelayYear)) return SecondRelayYear;
            return years.Contains(ThirdRelayYear) ? 0 : ThirdRelayYear;
        }

        private static int LaterRelayCultivator(int year) => year == SecondRelayYear ? SecondRelayCultivator : QiMoStartingItem;
        private static string LaterRelayCultivatorName(int year) => TimelineText(year == SecondRelayYear
            ? "Timeline_StartingCultivators_9_Name" : "Timeline_QiMo_Name");

        public bool CanRecordPersonalLife => !preparing && !advancing && saveReadable &&
            simulation?.WaitingForPlayer == true && simulation.State.PlayerCompletedPersonalStory;

        private (string Text, int? NodeId) PersonalStoryGuide()
        {
            if (simulation.State.IsEnded) return SluiceGuide();
            var world = simulation.State;
            if (contractStoryActive && startingCultivator?.Item == QiMoStartingItem)
            {
                if (world.ContractRevoked is { } revoked)
                    return (TimelineText("Timeline_QiStory_Result", revoked.Year), TwinGolemGate);
                var player = world.Actors[world.PlayerActorId];
                if (player.IsAlive && player.NodeId is int current && !world.PlayerControlEnded)
                {
                    if (!player.Items.Contains(9146))
                        return StoryWaypoint(current, ContractPostNode, "Timeline_QiStory_Learn");
                    if (!simulation.Stories.Flags.Contains(ContractLedgerRead))
                        return StoryWaypoint(current, ContractGallery, "Timeline_QiStory_Read");
                    return StoryWaypoint(current, ContractAltar, "Timeline_QiStory_Revoke");
                }
            }
            if (northRelayActive && startingCultivator?.Item == SecondRelayCultivator)
            {
                if (world.NorthRelayRestored is { } restored)
                    return (TimelineText(world.NorthRelaySpringNodeId.HasValue ? "Timeline_NorthRelay_SpringYourResult" : "Timeline_NorthRelay_YourResult",
                        restored.Year, FinalRitualSystem.NorthRelayAttackReductionPercent),
                        world.NorthRelaySpringNodeId ?? NorthernRelay);
                var player = world.Actors[world.PlayerActorId];
                if (player.IsAlive && player.NodeId is int current && !world.PlayerControlEnded)
                {
                    var flags = simulation.Stories.Flags;
                    if (flags.Contains(NorthRoster)) return StoryWaypoint(current, NorthernRelay, "Timeline_NorthRelay_Restore");
                    if (flags.Contains(NorthTrailNames)) return StoryWaypoint(current, TrailkeepersHut, "Timeline_NorthRelay_ReadRoster");
                    return StoryWaypoint(current, PineTrail, "Timeline_NorthRelay_Trail");
                }
            }

            var sluiceGuide = SluiceGuide();
            if (startingCultivator?.Item == OpeningCultivator && sluiceGuide.NodeId.HasValue) return sluiceGuide;
            if (world.SluiceDrainsBoss && world.SluiceOpening.HasValue && world.NorthRelayRestored.HasValue &&
                world.ContractRevoked.HasValue && world.Actors[world.ChosenActorId.Value].IsAlive &&
                world.Nodes[HexWorldMap.Id(-2, 0)].GuardianDefeat?.ActorId == world.ChosenActorId)
                return (TimelineText("Timeline_Sluice_FinaleReady"), world.Ritual.NodeId);
            if (contractStoryActive)
            {
                if (world.NorthRelaySpringNodeId is int spring && world.NorthRelayRestored.HasValue &&
                    world.ContractRevoked.HasValue && world.Actors[world.ChosenActorId.Value].IsAlive)
                {
                    var recovery = simulation.Events.LastOrDefault(entry => entry.Kind == WorldEventKind.NorthRelaySpringUsed);
                    if (recovery != null)
                        return (TimelineText("Timeline_NorthRelay_SpringMapResult", recovery.Detail), spring);
                    if (!simulation.Events.Any(entry => entry.Kind == WorldEventKind.Arrived &&
                        entry.ActorId == world.ChosenActorId && entry.NodeId == spring))
                        return (TimelineText("Timeline_NorthRelay_BothReady"), spring);
                }
                if (world.ContractRevoked is { } revoked)
                    return (TimelineText("Timeline_QiStory_WorldResult", revoked.Year), TwinGolemGate);
                var qi = simulation.Histories.FirstOrDefault(record => record.Record.ContractRevokedYear.HasValue);
                if (qi != null)
                {
                    int year = qi.Record.ContractRevokedYear.Value;
                    string key = !world.Actors[qi.ActorId].IsAlive || qi.Status != HistoryReplayStatus.Replaying
                        ? "Timeline_QiStory_Interrupted" : world.Year < year
                        ? "Timeline_QiStory_Forecast" : "Timeline_QiStory_Delayed";
                    return (TimelineText(key, year), ContractAltar);
                }
            }
            if (!northRelayActive) return sluiceGuide;
            if (world.NorthRelayRestored is { } opening)
                return (TimelineText(world.NorthRelaySpringNodeId.HasValue ? "Timeline_NorthRelay_SpringWorldResult" : "Timeline_NorthRelay_WorldResult",
                    opening.Year, FinalRitualSystem.NorthRelayAttackReductionPercent),
                    world.NorthRelaySpringNodeId ?? NorthernRelay);
            var history = simulation.Histories.FirstOrDefault(entry => entry.Record.NorthRelayRestoredYear.HasValue);
            if (history == null) return sluiceGuide;
            int plannedYear = history.Record.NorthRelayRestoredYear.Value;
            string hint = !world.Actors[history.ActorId].IsAlive || history.Status != HistoryReplayStatus.Replaying
                ? "Timeline_NorthRelay_ReplayInterrupted" : world.Year < plannedYear
                ? "Timeline_NorthRelay_ReplayPlanned" : "Timeline_NorthRelay_ReplayDelayed";
            return (TimelineText(hint, plannedYear), NorthernRelay);
        }

        private (string Text, int? NodeId) SluiceGuide()
        {
            var world = simulation.State;
            if (world.SluiceOpening is { } opening)
            {
                bool playerOpened = opening.ActorId == world.PlayerActorId;
                string key = world.SluiceDrainsBoss
                    ? (playerOpened ? "Timeline_Sluice_DrainYourResult" : "Timeline_Sluice_DrainWorldResult")
                    : (playerOpened ? "Timeline_Sluice_YourResult" : "Timeline_Sluice_WorldResult");
                return (TimelineText(key, opening.Year), OldSluice);
            }
            if (world.IsEnded || simulation.Stories == null) return (message, null);
            var player = world.Actors[world.PlayerActorId];
            if (startingCultivator?.Item == OpeningCultivator && player.IsAlive && player.NodeId is int current && !world.PlayerControlEnded)
            {
                var flags = simulation.Stories.Flags;
                if (flags.Contains(SluicePlan)) return StoryWaypoint(current, OldSluice, "Timeline_Sluice_Open");
                if (flags.Contains(KilnClue) || simulation.Events.Any(entry =>
                    entry.ActorId == player.Id && entry.Kind == WorldEventKind.StoryResolved && entry.NodeId == SealmudKiln))
                    return StoryWaypoint(current, AnchorstoneWorkshop, "Timeline_Sluice_CopyPlan");
                if (simulation.Events.Any(entry =>
                    entry.ActorId == player.Id && entry.Kind == WorldEventKind.StoryResolved && entry.NodeId == SwordwashSpring))
                    return StoryWaypoint(current, SealmudKiln, "Timeline_Sluice_CopyKiln");
                return StoryWaypoint(current, SwordwashSpring, "Timeline_Sluice_Spring");
            }

            var history = simulation.Histories.FirstOrDefault(entry =>
                entry.Record.InitialItems.Contains(OpeningCultivator) && entry.Record.SluiceOpeningYear.HasValue);
            if (history == null) return (message, null);
            var actor = world.Actors[history.ActorId];
            int plannedYear = history.Record.SluiceOpeningYear.Value;
            return (!actor.IsAlive || history.Status != HistoryReplayStatus.Replaying
                ? TimelineText("Timeline_Sluice_ReplayInterrupted", plannedYear)
                : TimelineText(world.Year < plannedYear ? "Timeline_Sluice_ReplayPlanned" :
                    "Timeline_Sluice_ReplayDelayed", plannedYear), OldSluice);
        }

        private (string Text, int? NodeId) StoryWaypoint(int current, int destination, string instruction)
        {
            var world = simulation.State;
            string objective = instruction == "Timeline_QiStory_Read"
                ? "祁墨旧事 · 沿途探索、招募队友，再去残印回廊击败守怪、核对废簿。"
                : TimelineText(instruction);
            if (current == destination) return (objective, destination);
            if (WorldPathfinder.NextStep(world, current, new[] { destination }) is not int next)
                return (objective, destination);
            var node = world.Nodes[next];
            return (objective + "\n" + TimelineText("Timeline_Relay_Next", LocationName(node), node.Q, node.R, ""), next);
        }

        private (string Text, int? NodeId) RelayGuide()
        {
            var world = simulation?.State;
            if (startingCultivator == null || world?.Ritual == null || simulation.Stories == null ||
                world.Ritual.NodeId != HexWorldMap.Id(0, 0) || world.Ritual.PeriodYears <= FirstRelayYear)
                return (message, null);

            int targetYear = NextRelayYear(simulation.Histories.Select(history => history.Record));
            if (targetYear == 0) return (message, null);

            var actor = world.Actors[world.PlayerActorId];
            var reset = simulation.Events.LastOrDefault(entry =>
                entry.Kind == WorldEventKind.RitualReset && entry.ActorId == actor.Id);
            if (reset != null)
                return (TimelineText(world.IsEnded ? "Timeline_Relay_Completed" : "Timeline_Relay_Observe", reset.Year), null);
            if (world.IsEnded || world.PlayerControlEnded || !actor.IsAlive || !actor.NodeId.HasValue)
                return (message, null);

            string title = world.Year < targetYear ? TimelineText(targetYear switch
                {
                    FirstRelayYear => "Timeline_Relay_Target",
                    SecondRelayYear => "Timeline_Relay_TargetSecond",
                    _ => "Timeline_Relay_TargetThird"
                }, targetYear) : TimelineText("Timeline_Relay_Late", targetYear, world.Ritual.DeadlineYear);
            bool first = targetYear == FirstRelayYear;
            if ((targetYear >= SecondRelayYear && world.Year >= FirstRelayYear && world.Ritual.DeadlineYear <= SecondRelayYear) ||
                (targetYear == ThirdRelayYear && world.Year >= SecondRelayYear && world.Ritual.DeadlineYear <= ThirdRelayYear))
                return (title + "\n" + TimelineText("Timeline_Relay_ShortDeadline", world.Ritual.DeadlineYear), null);
            bool recommended = first ? startingCultivator.Item is 8102 or 8104
                : startingCultivator.Item == LaterRelayCultivator(targetYear);
            if (!recommended)
                return (title + "\n" + (first ? TimelineText("Timeline_Relay_Recommend",
                    TimelineText("Timeline_StartingCultivators_2_Name"))
                    : TimelineText("Timeline_Relay_RecommendLater", LaterRelayCultivatorName(targetYear))), null);

            var route = targetYear == FirstRelayYear ? FirstRelayRoute : targetYear == SecondRelayYear ? SecondRelayRoute : ThirdRelayRoute;
            int current = actor.NodeId.Value;
            int index = Array.FindIndex(route, step => step.NodeId == current);
            int targetIndex = index + 1;
            if (index < 0)
            {
                var visited = new HashSet<int?>(simulation.Events.Where(entry => entry.ActorId == actor.Id &&
                    entry.Kind == WorldEventKind.Arrived).Select(entry => entry.NodeId));
                int last = Array.FindLastIndex(route, step => visited.Contains(step.NodeId));
                targetIndex = last + 1;
            }
            if (targetIndex >= route.Length) return (title, null);

            int destination = route[targetIndex].NodeId;
            if (destination == world.Ritual.NodeId && world.Nodes[current].Links.Contains(destination) &&
                world.Year < targetYear - 1)
                return (title + "\n" + TimelineText("Timeline_Relay_Wait", targetYear - 1), null);

            if (!world.Nodes[current].Links.Contains(destination))
            {
                if (WorldPathfinder.NextStep(world, current, new[] { destination }) is not int next) return (title, null);
                destination = next;
            }
            var node = world.Nodes[destination];
            string instruction = destination == route[targetIndex].NodeId && route[targetIndex].ArrivalHint is string hint
                ? TimelineText(hint) : "";
            return (title + "\n" + TimelineText("Timeline_Relay_Next", LocationName(node), node.Q, node.R, instruction), destination);
        }

        public bool FocusRelayStep()
        {
            if (advancing || simulation == null || MapNotice.NodeId is not int nodeId)
                return false;
            selectedNode = nodeId;
            if (board != null)
            {
                var node = simulation.State.Nodes[nodeId];
                board.SelectNode(selectedNode);
                board.Focus(node.Q, node.R);
            }
            return true;
        }
    }
}
