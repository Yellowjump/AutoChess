using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        private const int OpeningCultivator = 8102;
        private const int OpeningSword = 9102;
        private int ChosenGateYear => stagedRoutes ? 4 : 3;
        private bool stagedRoutes, guidedDiscovery;
        private static readonly int Fork = HexWorldMap.Id(1, -9);
        private static readonly int OpeningBirth = HexWorldMap.Id(1, -10);
        private static readonly int OpeningGate = HexWorldMap.Id(2, -9);
        private static readonly int[] LegacyShortcut = { HexWorldMap.Id(2, -10), OpeningGate };
        private static readonly int[] LegacyDetour = { HexWorldMap.Id(1, -9), HexWorldMap.Id(1, -8), HexWorldMap.Id(2, -8), OpeningGate };
        private static readonly int[] ForkShortcut = { Fork, HexWorldMap.Id(2, -10), OpeningGate };
        private static readonly int[] ForkDetour = { Fork, HexWorldMap.Id(0, -8), HexWorldMap.Id(1, -8), HexWorldMap.Id(2, -8), OpeningGate };
        private int[] Shortcut => stagedRoutes ? ForkShortcut : LegacyShortcut;
        private int[] Detour => stagedRoutes ? ForkDetour : LegacyDetour;
        public bool OpeningApproach => OpeningActive && stagedRoutes &&
            (OpeningPlayer.NodeId ?? OpeningPlayer.TravelOriginNodeId) == OpeningBirth;
        private bool openingMap, openingPrompt;
        private int openingChoice;

        public bool OpeningActive => openingPrompt && simulation != null && !simulation.State.IsEnded;
        public bool OpeningOfferRoutes => OpeningActive && !OpeningApproach && openingChoice == 0;
        public bool OpeningAtStop => OpeningActive && openingChoice != 0 &&
            OpeningPlayer.NodeId is int current && current != OpeningBirth;
        private ActorState OpeningPlayer => simulation.State.Actors[simulation.State.PlayerActorId];
        private bool OpeningReachedGate => OpeningPlayer.NodeId == OpeningGate;
        public int OpeningShortcutYears => Shortcut.Length - (stagedRoutes ? 1 : 0);
        public int OpeningDetourYears => Detour.Length - (stagedRoutes ? 1 : 0);
        public string OpeningDeadlineLabel => OpeningApproach ? string.Empty : OpeningChosenHasSword
            ? TimelineText("Timeline_Opening_Lost", ChosenGateYear).Split('\n')[0]
            : TimelineText("Timeline_Opening_Deadline", ChosenGateYear);
        private bool OpeningChosenHasSword => simulation != null &&
            simulation.State.Actors[simulation.Chosen.ActorId].Items.Contains(OpeningSword);
        public string OpeningPrimaryLabel => OpeningOfferRoutes ? TimelineText("Timeline_Opening_Short", OpeningShortcutYears) :
            TimelineText(OpeningReachedGate ? "Timeline_Opening_Explore" : "Timeline_Opening_Continue");

        public bool CanBeginOpening => preparing && !advancing && saveReadable && savedLives.Count == 0 &&
            scenarioData?.ChosenRoute != null && OpeningChosenRoute(scenarioData.ChosenRoute) != null &&
            playableRoster.Exists(character => character.Item == OpeningCultivator && character.Birth == OpeningBirth);

        public bool TryBeginOpening()
        {
            if (!CanBeginOpening) return false;
            int index = playableRoster.FindIndex(character => character.Item == OpeningCultivator && character.Birth == OpeningBirth);
            if (index < 0 || !SelectPreparationCharacter(index)) return false;
            openingPrompt = true;
            openingChoice = 0;
            return BeginPreparedRun();
        }

        private static ChosenRouteStage[] OpeningChosenRoute(ChosenRouteStage[] route)
        {
            var legs = new (int Q, int R, int Years)[]
            {
                (4,-10,0), (3,-10,2), (2,-9,2), (2,-8,2), (2,-7,1), (1,-6,1),
                (0,-6,1), (-1,-6,1), (-2,-6,1), (-3,-5,1), (-4,-4,1), (-5,-3,1), (-6,-2,1), (-7,-1,1)
            };
            int merge = Array.FindIndex(route, stage => stage.Q == -8 && stage.R == 0);
            int mergeYear = route.Take(merge + 1).Sum(stage => stage.TravelYears);
            int lastLeg = mergeYear - legs.Sum(leg => leg.Years);
            if (merge < 1 || lastLeg <= 0 || route.Skip(merge).Any(stage => legs.Any(leg => leg.Q == stage.Q && leg.R == stage.R)))
                return null;
            var trial = route[merge];
            return legs.Select(leg => new ChosenRouteStage { Q = leg.Q, R = leg.R, TravelYears = leg.Years })
                .Append(new ChosenRouteStage
                {
                    Q = trial.Q, R = trial.R, TravelYears = lastLeg, GuardianCount = trial.GuardianCount,
                    GuardianHealth = trial.GuardianHealth, GuardianAttack = trial.GuardianAttack,
                    GuardianAppearance = trial.GuardianAppearance, RitualPeriod = trial.RitualPeriod
                }).Concat(route.Skip(merge + 1)).ToArray();
        }

        private void ConfigureOpeningLocations(WorldState world)
        {
            foreach (int id in Shortcut.Concat(Detour).Distinct())
            {
                var node = world.Nodes[id];
                node.MonsterHealth = 0;
                node.MonsterAttack = 0;
                node.Items.Clear();
                node.LootAttackBonus = 0;
            }
            var guard = world.Nodes[LegacyShortcut[0]];
            guard.MonsterHealth = 56;
            guard.MonsterAttack = 5;
            guard.BattleExperienceReward = 6;
            world.Nodes[OpeningGate].Items.Add(OpeningSword);
            world.Nodes[OpeningGate].LootAttackBonus = 12;
        }

        private bool SuppressOpeningStory(ActorState actor, int nodeId)
        {
            var world = simulation.State;
            if (actor.Id == world.ChosenActorId) return nodeId == HexWorldMap.Id(2, -7);
            int leg = world.Year - 1;
            if (actor.Id == world.PlayerActorId)
            {
                if (guidedDiscovery && leg == 0 && nodeId == Fork) return true;
                if (openingChoice == 0) return false;
                var path = openingChoice == 2 ? Detour : Shortcut;
                return IsOpeningStop(path, leg, nodeId) ||
                    Array.IndexOf(path, nodeId) > 0 && IsDelayedOpeningStop(path, simulation.RecordPlayerLife().Actions, world.Year, nodeId);
            }

            var record = simulation.Histories.FirstOrDefault(history => history.ActorId == actor.Id)?.Record;
            if (record == null || record.BirthNodeId != OpeningBirth) return false;
            if (stagedRoutes && leg == 0 && nodeId == Fork) return true;
            foreach (var route in new[] { LegacyShortcut, LegacyDetour, ForkShortcut, ForkDetour })
                if (IsRecordedOpeningStop(route, record, leg, nodeId) ||
                    IsDelayedOpeningStop(route, record.Actions, world.Year, nodeId)) return true;
            return false;
        }

        private static bool IsOpeningStop(int[] path, int leg, int nodeId) => leg >= 0 && leg < path.Length && path[leg] == nodeId;

        private static bool IsRecordedOpeningStop(int[] path, LifeRecord record, int leg, int nodeId)
        {
            if (!IsOpeningStop(path, leg, nodeId) || record.Actions.Count < path.Length) return false;
            return path.Select((destination, index) =>
            {
                var step = record.Actions[index];
                return step.StartYear == index && step.Action.Kind == WorldActionKind.Move &&
                    step.Action.TargetNodeId == destination && step.Action.Years == 1;
            }).All(matches => matches);
        }

        private static bool IsDelayedOpeningStop(int[] path, IReadOnlyList<RecordedAction> actions, int year, int nodeId)
        {
            int stop = Array.IndexOf(path, nodeId);
            if (stop < 1 || year <= stop + 1) return false;
            int leg = 0, expectedYear = 0;
            foreach (var step in actions)
            {
                var action = step.Action;
                if (step.StartYear != expectedYear++ || action.Years != 1) return false;
                if (leg > 0 && action.Kind is WorldActionKind.Wait or WorldActionKind.Cultivate) continue;
                if (action.Kind != WorldActionKind.Move || action.TargetNodeId != path[leg]) return false;
                if (leg++ == stop) return expectedYear == year;
            }
            return false;
        }

        private string OpeningNotice
        {
            get
            {
                if (!OpeningActive) return null;
                if (OpeningApproach) return TimelineText("Timeline_Opening_Continue");
                var world = simulation.State;
                var player = world.Actors[world.PlayerActorId];
                if (player.Items.Contains(OpeningSword))
                {
                    int arrival = simulation.Events.FirstOrDefault(entry =>
                        entry.Kind == WorldEventKind.Looted && entry.ActorId == player.Id && entry.NodeId == OpeningGate)?.Year
                        ?? world.Year;
                    return TimelineText("Timeline_Opening_Won", arrival);
                }
                var chosen = world.Actors[simulation.Chosen.ActorId];
                if (chosen.Items.Contains(OpeningSword)) return TimelineText("Timeline_Opening_Lost", ChosenGateYear);
                return openingChoice == 0
                    ? TimelineText("Timeline_Opening_Intro", world.Ritual.DeadlineYear).Split('\n').Last()
                    : TimelineText("Timeline_Opening_Travel", ChosenGateYear);
            }
        }

        private void ShowOpeningMap()
        {
            if (board == null) return;
            if (OpeningActive)
            {
                int start = stagedRoutes ? Fork : OpeningBirth;
                board.SetOpeningPaths(start, stagedRoutes ? Shortcut.Skip(1).ToArray() : Shortcut,
                    stagedRoutes ? Detour.Skip(1).ToArray() : Detour, OpeningSword);
                if (stagedRoutes) board.AddApproach(OpeningBirth, Fork);
                SyncOpeningRoute();
            }
            if (guidedDiscovery || OpeningActive)
            {
                board.Sync(simulation, selectedNode);
                if (OpeningPlayer.NodeId is int id)
                    board.Focus(simulation.State.Nodes[id].Q, simulation.State.Nodes[id].R);
            }
        }

        internal HashSet<int> OpeningVisibleNodes()
        {
            if (!guidedDiscovery || simulation.State.IsEnded || simulation.State.PlayerControlEnded || !OpeningPlayer.IsAlive)
                return null;
            var visible = new HashSet<int> { OpeningBirth, Fork };
            if (OpeningApproach) return visible;
            visible.UnionWith(Shortcut);
            visible.UnionWith(Detour);
            if (openingPrompt) return visible;
            visible.Add(HexWorldMap.Id(3, -9));
            visible.Add(HexWorldMap.Id(3, -10));
            bool passedGate = false;
            foreach (var entry in simulation.Events)
            {
                if (entry.Kind != WorldEventKind.Arrived || entry.ActorId != simulation.State.PlayerActorId ||
                    entry.NodeId is not int id) continue;
                if (passedGate) visible.UnionWith(simulation.State.Nodes[id].Links);
                passedGate |= id == OpeningGate;
            }
            return visible;
        }

        public void ClickOpeningNode(Vector2 screen)
        {
            if (board == null || !OpeningActive || advancing) return;
            if (OpeningOfferRoutes)
            {
                int route = board.OpeningRouteAt(screen);
                if (route != 0) SelectOpeningRoute(route == 2);
            }
            else if (TryGetOpeningStop(openingChoice == 2 ? 2 : 1, out int next) && board.Pick(screen) == next)
                SelectOpeningRoute(false);
        }

        public void PreviewOpeningRoute(int route)
        {
            if (OpeningOfferRoutes && !advancing) board?.SetOpeningRouteFocus(route);
        }

        public void PreviewOpeningRouteAt(Vector2 screen) => PreviewOpeningRoute(board?.OpeningRouteAt(screen) ?? 0);

        private bool TryGetOpeningStop(int route, out int target)
        {
            target = 0;
            var world = simulation.State;
            var player = world.Actors[world.PlayerActorId];
            if (player.NodeId is not int current) return false;
            var path = route == 2 ? Detour : Shortcut;
            int last = current == OpeningBirth ? -1 : Array.IndexOf(path, current);
            if ((last < 0 && current != OpeningBirth) || last + 1 >= path.Length) return false;
            target = path[last + 1];
            return world.Nodes[current].Links.Contains(target);
        }

        private void SyncOpeningRoute()
        {
            if (board == null) return;
            if (openingChoice == 0)
            {
                board.SetOpeningNextStop(OpeningApproach ? Fork : (int?)null);
                return;
            }
            board.SetOpeningRouteFocus(openingChoice, committed: true);
            board.SetOpeningNextStop(TryGetOpeningStop(openingChoice, out int next) ? next : (int?)null);
        }

        public bool CanAdvanceOpening(bool detour) => OpeningActive && !advancing && simulation.WaitingForPlayer &&
            (OpeningReachedGate || TryGetOpeningStop(openingChoice != 0 ? openingChoice : detour ? 2 : 1, out _));

        public void SelectOpeningRoute(bool detour)
        {
            if (!CanAdvanceOpening(detour)) return;
            if (OpeningReachedGate)
            {
                openingPrompt = false;
                board?.SetOpeningPaths(OpeningBirth, null, null);
                board?.Sync(simulation, selectedNode);
                SaveCheckpoint();
                return;
            }
            int route = openingChoice != 0 ? openingChoice : detour ? 2 : 1;
            if (!TryGetOpeningStop(route, out int target)) return;
            if (openingChoice == 0 && !OpeningApproach)
            {
                openingChoice = route;
                SyncOpeningRoute();
                SaveCheckpoint();
            }
            StartOpeningAction(WorldAction.Move(target));
        }

        public void StayAtOpeningStop()
        {
            if (!OpeningAtStop || advancing || !simulation.WaitingForPlayer) return;
            StartOpeningAction(WorldAction.Wait());
        }

        private void StartOpeningAction(WorldAction action)
        {
            advancing = true;
            StartCoroutine(PlayOpeningAction(action));
        }

        private IEnumerator PlayOpeningAction(WorldAction action)
        {
            var world = simulation.State;
            var player = OpeningPlayer;
            try
            {
                Func<IEnumerator> presentArrival = action.Kind == WorldActionKind.Move
                    ? () => PresentOpeningArrival(action.TargetNodeId) : null;
                yield return AdvanceRoutine(simulation.SubmitPlayerActionRoutine(action,
                    presentArrival), false, action);
            }
            finally
            {
                advancing = false;
                if (!player.IsAlive)
                {
                    openingPrompt = false;
                    board?.SetOpeningPaths(OpeningBirth, null, null);
                    board?.Sync(simulation, selectedNode);
                    autoObserve = !world.IsEnded;
                    SaveCheckpoint();
                }
                else
                {
                    SyncOpeningRoute();
                    board?.Sync(simulation, selectedNode);
                    if (OpeningOfferRoutes && board != null)
                    {
                        var gate = world.Nodes[OpeningGate];
                        board.Focus(gate.Q, gate.R);
                    }
                }
            }
        }

        private IEnumerator PresentOpeningArrival(int target)
        {
            selectedNode = target;
            if (board == null) yield break;
            const float travelSeconds = .55f;
            board.Sync(simulation, selectedNode, travelSeconds);
            yield return new WaitForSecondsRealtime(travelSeconds);
        }
    }
}
