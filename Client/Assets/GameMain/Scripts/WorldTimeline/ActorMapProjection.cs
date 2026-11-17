using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public struct ActorMapPoint
    {
        public double Q;
        public double R;
        public bool IsMoving;
        public ActorMapPoint(double q, double r, bool moving) { Q = q; R = r; IsMoving = moving; }
    }

    public static class ActorMapProjection
    {
        public enum LegProgress { Planned, Travelled }

        public static IReadOnlyList<(int ActorId, int Years)> NextArrivals(WorldSimulation simulation, int nodeId)
        {
            var world = simulation.State;
            var arrivals = new List<(int ActorId, int Years)>();
            if (world.IsEnded) return arrivals;
            var chosen = simulation.Chosen;
            long? chosenYear = chosen?.NextArrival(world, nodeId);
            if (chosen != null) Add(chosen.ActorId, chosenYear);
            foreach (var actor in world.Actors.Values)
            {
                if (!actor.IsAlive || actor.Id == chosen?.ActorId) continue;
                if (actor.LeaderId.HasValue)
                {
                    if (actor.LeaderId == chosen?.ActorId) Add(actor.Id, chosenYear);
                    continue;
                }
                if (actor.Id == world.PlayerActorId)
                {
                    if (actor.CurrentAction?.Kind == WorldActionKind.Move && actor.CurrentAction.TargetNodeId == nodeId)
                        Add(actor.Id, world.Year + (long)actor.RemainingYears);
                    continue;
                }
                var history = simulation.Histories.FirstOrDefault(entry => entry.ActorId == actor.Id);
                Add(actor.Id, history?.NextArrival(simulation, nodeId));
            }
            return arrivals.OrderBy(entry => entry.Years).ThenBy(entry => entry.ActorId).ToArray();

            void Add(int actorId, long? year)
            {
                if (year.HasValue && year.Value > world.Year && year.Value <= int.MaxValue)
                    arrivals.Add((actorId, (int)year.Value - world.Year));
            }
        }

        public static ActorMapPoint Position(WorldState world, ActorState actor)
        {
            if (!actor.IsAlive) throw new InvalidOperationException("Dead actors have no live map marker.");
            if (actor.FrozenMapPoint.HasValue) return actor.FrozenMapPoint.Value;
            if (actor.LeaderId.HasValue) actor = world.Actors[actor.LeaderId.Value];
            if (actor.FrozenMapPoint.HasValue) return actor.FrozenMapPoint.Value;
            if (actor.NodeId.HasValue)
            {
                var node = world.Nodes[actor.NodeId.Value];
                return new ActorMapPoint(node.Q, node.R, false);
            }
            if (!actor.TravelOriginNodeId.HasValue || actor.CurrentAction == null || actor.CurrentAction.Kind != WorldActionKind.Move)
                throw new InvalidOperationException("Living actor has no valid map position.");
            var origin = world.Nodes[actor.TravelOriginNodeId.Value];
            var target = world.Nodes[actor.CurrentAction.TargetNodeId];
            double fraction = 1 - actor.RemainingYears / (double)actor.CurrentAction.Years;
            return new ActorMapPoint(origin.Q + (target.Q - origin.Q) * fraction, origin.R + (target.R - origin.R) * fraction, true);
        }

        public static Dictionary<int, Dictionary<(int From, int To), LegProgress>> Routes(WorldSimulation simulation)
        {
            var world = simulation.State;
            var routes = new Dictionary<int, Dictionary<(int, int), LegProgress>>();
            var lastNodes = new Dictionary<int, int>();
            foreach (var actor in world.Actors.Values)
            {
                if (!actor.IsAlive || actor.Id == world.PlayerActorId) continue;
                routes.Add(actor.Id, new Dictionary<(int, int), LegProgress>());
                lastNodes.Add(actor.Id, actor.BirthNodeId);
            }

            var chosen = simulation.Chosen;
            if (chosen != null && routes.ContainsKey(chosen.ActorId))
                AddPlan(chosen.ActorId, chosen.Route);
            foreach (var history in simulation.Histories)
            {
                if (!routes.ContainsKey(history.ActorId) || history.Status != HistoryReplayStatus.Replaying) continue;
                int from = history.Record.BirthNodeId;
                foreach (var entry in history.Record.Actions)
                {
                    if (entry.Action.Kind == WorldActionKind.JoinChosen) break;
                    if (entry.Action.Kind != WorldActionKind.Move) continue;
                    int to = entry.Action.TargetNodeId;
                    if (!AddLeg(history.ActorId, from, to, LegProgress.Planned)) break;
                    from = to;
                }
            }

            var followers = new HashSet<int>();
            foreach (var entry in simulation.Events)
            {
                if (!entry.NodeId.HasValue) continue;
                int node = entry.NodeId.Value;
                if (entry.Kind == WorldEventKind.Joined && routes.ContainsKey(entry.ActorId))
                {
                    lastNodes[entry.ActorId] = node;
                    followers.Add(entry.ActorId);
                }
                if (entry.Kind != WorldEventKind.Arrived) continue;
                Arrive(entry.ActorId, node);
                if (entry.ActorId == world.ChosenActorId)
                    foreach (int follower in followers)
                        Arrive(follower, node);
            }

            foreach (var actor in world.Actors.Values)
            {
                if (!routes.ContainsKey(actor.Id)) continue;
                var traveller = actor.LeaderId.HasValue ? world.Actors[actor.LeaderId.Value] : actor;
                if (traveller.TravelOriginNodeId is int origin && traveller.CurrentAction?.Kind == WorldActionKind.Move)
                    AddLeg(actor.Id, origin, traveller.CurrentAction.TargetNodeId, LegProgress.Planned);
            }
            return routes;

            void AddPlan(int id, IReadOnlyList<int> nodes)
            {
                for (int i = 1; i < nodes.Count; i++)
                    AddLeg(id, nodes[i - 1], nodes[i], LegProgress.Planned);
            }

            void Arrive(int id, int node)
            {
                if (!lastNodes.TryGetValue(id, out int from)) return;
                AddLeg(id, from, node, LegProgress.Travelled);
                lastNodes[id] = node;
            }

            bool AddLeg(int id, int from, int to, LegProgress progress)
            {
                if (!world.Nodes.TryGetValue(from, out var node) || !node.Links.Contains(to)) return false;
                var edge = from < to ? (from, to) : (to, from);
                var route = routes[id];
                if (progress == LegProgress.Travelled || !route.ContainsKey(edge)) route[edge] = progress;
                return true;
            }
        }
    }
}
