using System;

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
    }
}
