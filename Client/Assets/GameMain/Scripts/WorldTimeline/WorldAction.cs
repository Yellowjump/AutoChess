using System;

namespace WorldTimeline
{
    public enum WorldActionKind { Wait, Move, Cultivate, Disguise, JoinChosen }

    // Action requests are immutable; progress belongs to the actor.
    public sealed class WorldAction
    {
        public WorldActionKind Kind { get; }
        public int Years { get; }
        public int TargetNodeId { get; }

        private WorldAction(WorldActionKind kind, int years, int targetNodeId = 0)
        {
            if (years < 1 && !(kind == WorldActionKind.JoinChosen && years == 0)) throw new ArgumentOutOfRangeException(nameof(years));
            Kind = kind;
            Years = years;
            TargetNodeId = targetNodeId;
        }

        public static WorldAction Move(int targetNodeId, int years = 1) =>
            new WorldAction(WorldActionKind.Move, years, targetNodeId);
        public static WorldAction Cultivate(int years) => new WorldAction(WorldActionKind.Cultivate, years);
        public static WorldAction Wait(int years = 1) => new WorldAction(WorldActionKind.Wait, years);
        public static WorldAction Disguise() => new WorldAction(WorldActionKind.Disguise, 1);
        public static WorldAction JoinChosen() => new WorldAction(WorldActionKind.JoinChosen, 0);
    }
}
