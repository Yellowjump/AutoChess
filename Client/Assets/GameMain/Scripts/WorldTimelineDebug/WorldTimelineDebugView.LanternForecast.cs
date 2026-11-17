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
        private static readonly int LanternShoal = HexWorldMap.Id(7, -9);
        private static readonly int LanternSpring = HexWorldMap.Id(5, -9);
        private readonly Stack<IEnumerator> lanternForecast = new Stack<IEnumerator>();
        private (WorldSimulation Simulation, int Year, int Health, int MaxHealth, int Attack, int CombatAttack,
            int TechniqueSkill, int GuardianHealth, int GuardianAttack, string Appearance) lanternInputs;
        private int[] lanternItems;
        private int? lanternHealth;
        private bool lanternForecastComplete;

        public bool CanForecastLanternShoal => !OpeningActive && !advancing && simulation?.WaitingForPlayer == true &&
            battleResolver is IWorldBattleForecaster && selectedNode == LanternShoal &&
            locations.ContainsKey(LanternShoal) && SelectedLocation.GuardianCount == 1 && !SelectedLocation.IsGuardianGroup;

        public int? LanternForecastHealth => LanternForecastMatches ? lanternHealth : null;
        public bool LanternForecastPending => CanForecastLanternShoal && (!LanternForecastMatches || !lanternForecastComplete);
        public bool LanternForecastHighRisk => LanternForecastHealth is int health &&
            health * 4L <= simulation.State.Actors[simulation.State.PlayerActorId].MaxHealth;

        private bool LanternForecastMatches => CanForecastLanternShoal && lanternItems != null &&
            lanternInputs == CurrentLanternInputs() &&
            simulation.State.Actors[simulation.State.PlayerActorId].Items.SequenceEqual(lanternItems);

        private (WorldSimulation, int, int, int, int, int, int, int, int, string) CurrentLanternInputs()
        {
            var world = simulation.State;
            var player = world.Actors[world.PlayerActorId];
            player.Techniques.TryGetValue(1, out var swordArt);
            var node = world.Nodes[LanternShoal];
            return (simulation, world.Year, player.Health, player.MaxHealth, player.Attack, player.CombatAttack,
                world.TechniqueCombatEnabled ? swordArt?.SkillId ?? 0 : 0,
                node.MonsterHealth, node.MonsterAttack, node.GuardianAppearance);
        }

        // The map HUD advances this once per frame. The stack owns the shared native board until disposed.
        public bool UpdateLanternForecast()
        {
            if (!CanForecastLanternShoal)
            {
                CancelLanternForecast();
                return false;
            }
            try
            {
                if (!LanternForecastMatches)
                {
                    CancelLanternForecast();
                    lanternInputs = CurrentLanternInputs();
                    var world = simulation.State;
                    var player = world.Actors[world.PlayerActorId];
                    lanternItems = player.Items.ToArray();
                    lanternForecastComplete = false;
                    lanternHealth = null;
                    lanternForecast.Push(((IWorldBattleForecaster)battleResolver).ForecastGuardian(
                        world, LanternShoal, player, health => lanternHealth = health));
                    return true;
                }
                if (lanternForecastComplete) return false;
                while (lanternForecast.Count > 0)
                {
                    var current = lanternForecast.Peek();
                    if (!current.MoveNext())
                        (lanternForecast.Pop() as IDisposable)?.Dispose();
                    else if (current.Current is IEnumerator nested)
                        lanternForecast.Push(nested);
                    else
                        return false;
                }
            }
            catch (Exception error)
            {
                CancelLanternForecast();
                lanternItems = simulation.State.Actors[simulation.State.PlayerActorId].Items.ToArray();
                lanternHealth = null;
                Debug.LogException(error);
            }
            lanternForecastComplete = true;
            return true;
        }

        public void CancelLanternForecast()
        {
            if (!lanternForecastComplete) lanternItems = null;
            while (lanternForecast.Count > 0)
                (lanternForecast.Pop() as IDisposable)?.Dispose();
        }

        public int? LanternDetourYears
        {
            get
            {
                var route = GetLanternDetour();
                return route.Count > 0 ? route.Count : (int?)null;
            }
        }

        private IReadOnlyList<int> GetLanternDetour()
        {
            if (!LanternForecastHighRisk || !CanMoveSelected || !locations.ContainsKey(LanternSpring))
                return Array.Empty<int>();
            var world = simulation.State;
            var player = world.Actors[world.PlayerActorId];
            return WorldPathfinder.FindRoute(world, player.NodeId.Value, new[] { LanternSpring },
                node => node.Id != LanternShoal && node.MonsterHealth == 0);
        }

        public void TakeLanternDetour()
        {
            var route = GetLanternDetour();
            if (route.Count == 0) return;
            selectedNode = LanternSpring;
            board?.SelectNode(selectedNode);
            StartAdvancing(FollowLanternDetour(route), observeAfter: true, recordStep: false);
        }

        private IEnumerator FollowLanternDetour(IReadOnlyList<int> route)
        {
            var world = simulation.State;
            var player = world.Actors[world.PlayerActorId];
            foreach (int next in route)
            {
                if (!simulation.WaitingForPlayer || player.NodeId is not int current) yield break;
                if (!world.Nodes[current].Links.Contains(next) || world.Nodes[next].MonsterHealth > 0)
                {
                    message = TimelineText("Timeline_LanternDetourInterrupted");
                    yield break;
                }
                var step = WorldAction.Move(next);
                yield return simulation.SubmitPlayerActionRoutine(step);
                if (checkpoint != null)
                {
                    checkpoint.actions.Add(new CheckpointAction(step));
                    SaveCheckpoint();
                }
            }
        }
    }
}
