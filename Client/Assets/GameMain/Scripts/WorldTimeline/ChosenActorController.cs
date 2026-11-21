using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public sealed class NamelessSeal
    {
        public int AbsorptionPercent { get; }
        internal NamelessSeal(int percent) { AbsorptionPercent = percent; }

        internal (int Health, int Attack) Absorb(ActorState owner, int health, int attack)
        {
            int gainHp = checked((int)((long)health * AbsorptionPercent / 100));
            int gainAtk = checked((int)((long)attack * AbsorptionPercent / 100));
            owner.MaxHealth = checked(owner.MaxHealth + gainHp);
            owner.Health = checked(owner.Health + gainHp);
            owner.Attack = checked(owner.Attack + gainAtk);
            return (gainHp, gainAtk);
        }
    }

    public sealed class ChosenActorController
    {
        public int ActorId { get; }
        public IReadOnlyList<int> Route { get; }
        public int YearsPerStep { get; }
        public int AbsorptionPercent { get; }
        public int FinalNodeId => Route[Route.Count - 1];
        public int ArrivalYear { get; }
        private readonly int[] travelYears;
        private int routeIndex;

        internal ChosenActorController(WorldState state, ActorState actor, IEnumerable<int> route, int yearsPerStep, int absorptionPercent,
            IEnumerable<int> travelYears = null)
        {
            var nodes = route.ToArray();
            if (nodes.Length < 2 || actor.NodeId != nodes[0] || yearsPerStep < 1 || absorptionPercent < 0 || absorptionPercent > 100)
                throw new ArgumentException("Invalid chosen route or configuration.");
            for (int i = 0; i < nodes.Length; i++)
                if (!state.Nodes.ContainsKey(nodes[i]) || (i > 0 && !state.Nodes[nodes[i - 1]].Links.Contains(nodes[i])))
                    throw new ArgumentException("Chosen route must be connected.");
            this.travelYears = travelYears?.ToArray() ?? Enumerable.Repeat(yearsPerStep, nodes.Length - 1).ToArray();
            if (this.travelYears.Length != nodes.Length - 1 || this.travelYears.Any(years => years <= 0))
                throw new ArgumentException("Each chosen route leg needs a positive travel time.");
            ArrivalYear = this.travelYears.Sum();
            ActorId = actor.Id; Route = Array.AsReadOnly(nodes); YearsPerStep = yearsPerStep; AbsorptionPercent = absorptionPercent;
        }

        internal void Prepare(WorldSimulation simulation)
        {
            var actor = simulation.State.Actors[ActorId];
            if (!actor.IsAlive || actor.CurrentAction != null) return;
            if (routeIndex + 1 < Route.Count && actor.NodeId == Route[routeIndex + 1]) routeIndex++;
            if (routeIndex + 1 < Route.Count) simulation.AssignAction(ActorId, WorldAction.Move(Route[routeIndex + 1], travelYears[routeIndex]));
        }

        internal long? NextArrival(WorldState world, int nodeId)
        {
            var actor = world.Actors[ActorId];
            if (!actor.IsAlive) return null;
            long year = world.Year;
            for (int leg = routeIndex; leg < travelYears.Length; leg++)
            {
                int destination = Route[leg + 1];
                if (leg == routeIndex)
                {
                    if (actor.NodeId == destination)
                    {
                        year += actor.RemainingYears + (long)actor.StoryDelayYears;
                        continue;
                    }
                    if (actor.CurrentAction?.Kind == WorldActionKind.Move &&
                        actor.CurrentAction.TargetNodeId == destination)
                        year += actor.RemainingYears;
                    else
                        year += actor.RemainingYears + (long)actor.StoryDelayYears + travelYears[leg];
                }
                else year += travelYears[leg];
                if (destination == nodeId && year > world.Year) return year;
                if (leg == routeIndex && actor.CurrentAction?.Kind == WorldActionKind.Move)
                    year += actor.StoryDelayYears;
            }
            return null;
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
            int? step = NextFollowerStep(simulation.State, actor.NodeId.Value);
            if (step.HasValue) simulation.AssignAction(actor.Id, WorldAction.Move(step.Value));
        }

        internal int? NextFollowerStep(WorldState world, int current)
        {
            var chosen = world.Actors[ActorId];
            if (chosen.IsAlive && chosen.NodeId == current) return null;
            bool onRoute = Route.Contains(current);
            IEnumerable<int> targets;
            if (!chosen.IsAlive) targets = new[] { FinalNodeId };
            // Join the travel route before chasing; do not use the boss room as a shortcut
            // while chosen is still travelling through earlier checkpoints.
            else if (!onRoute) targets = chosen.NodeId == FinalNodeId ? Route : Route.Take(Route.Count - 1);
            else targets = new[] { chosen.NodeId ?? chosen.CurrentAction.TargetNodeId };
            return WorldPathfinder.NextStep(world, current, targets);
        }
    }
}
