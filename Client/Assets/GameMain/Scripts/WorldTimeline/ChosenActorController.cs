using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public sealed class ChosenActorController
    {
        public int ActorId { get; }
        public IReadOnlyList<int> Route { get; }
        public int YearsPerStep { get; }
        public int AbsorptionPercent { get; }
        public int FinalNodeId => Route[Route.Count - 1];
        private int routeIndex;

        internal ChosenActorController(WorldState state, ActorState actor, IEnumerable<int> route, int yearsPerStep, int absorptionPercent)
        {
            var nodes = route.ToArray();
            if (nodes.Length < 2 || actor.NodeId != nodes[0] || yearsPerStep < 1 || absorptionPercent < 0 || absorptionPercent > 100)
                throw new ArgumentException("Invalid chosen route or configuration.");
            for (int i = 0; i < nodes.Length; i++)
                if (!state.Nodes.ContainsKey(nodes[i]) || (i > 0 && !state.Nodes[nodes[i - 1]].Links.Contains(nodes[i])))
                    throw new ArgumentException("Chosen route must be connected.");
            ActorId = actor.Id; Route = Array.AsReadOnly(nodes); YearsPerStep = yearsPerStep; AbsorptionPercent = absorptionPercent;
        }

        internal void Prepare(WorldSimulation simulation)
        {
            var actor = simulation.State.Actors[ActorId];
            if (!actor.IsAlive || actor.CurrentAction != null) return;
            if (routeIndex + 1 < Route.Count && actor.NodeId == Route[routeIndex + 1]) routeIndex++;
            if (routeIndex + 1 < Route.Count) simulation.AssignAction(ActorId, WorldAction.Move(Route[routeIndex + 1], YearsPerStep));
        }

        internal void PrepareFollower(WorldSimulation simulation, ActorState actor)
        {
            if (!actor.IsAlive || actor.LeaderId.HasValue || actor.CurrentAction != null || !actor.NodeId.HasValue) return;
            var chosen = simulation.State.Actors[ActorId];
            if (chosen.IsAlive && actor.NodeId == chosen.NodeId)
            {
                simulation.AssignAction(actor.Id, WorldAction.JoinChosen());
                return;
            }
            int current = actor.NodeId.Value;
            bool onRoute = Route.Contains(current);
            IEnumerable<int> targets;
            if (!chosen.IsAlive) targets = new[] { FinalNodeId };
            // Join the travel route before chasing; do not use the boss room as a shortcut
            // while chosen is still travelling through earlier checkpoints.
            else if (!onRoute) targets = chosen.NodeId == FinalNodeId ? Route : Route.Take(Route.Count - 1);
            else targets = new[] { chosen.NodeId ?? chosen.CurrentAction.TargetNodeId };
            int? step = WorldPathfinder.NextStep(simulation.State, current, targets);
            if (step.HasValue) simulation.AssignAction(actor.Id, WorldAction.Move(step.Value));
        }
    }
}
