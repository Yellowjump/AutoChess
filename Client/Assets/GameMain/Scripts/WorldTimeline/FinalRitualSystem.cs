using System;
using System.Linq;

namespace WorldTimeline
{
    public sealed class FinalRitualState
    {
        public int NodeId { get; }
        public int PeriodYears { get; }
        public int BossHealth { get; }
        public int BossAttack { get; }
        public int DeadlineYear { get; internal set; }
        public int ResetCount { get; internal set; }

        internal FinalRitualState(int nodeId, int periodYears, int health, int attack)
        {
            if (periodYears < 1 || health < 1 || attack < 1) throw new ArgumentOutOfRangeException();
            NodeId = nodeId; PeriodYears = periodYears; BossHealth = health; BossAttack = attack;
            DeadlineYear = periodYears;
        }
    }

    public sealed class FinalRitualSystem
    {
        internal bool ExpireIfDue(WorldState world, Action<WorldEvent> emit)
        {
            var ritual = world.Ritual;
            if (ritual == null || world.Year < ritual.DeadlineYear) return false;
            emit(new WorldEvent(world.Year, WorldEventKind.RitualCompleted, world.PlayerActorId, ritual.NodeId));
            foreach (var actor in world.Actors.Values.Where(a => a.IsAlive).OrderBy(a => a.Id))
            {
                int? nodeId = actor.NodeId;
                actor.Die();
                emit(new WorldEvent(world.Year, WorldEventKind.Died, actor.Id, nodeId, "World destroyed"));
            }
            world.Finish(RunOutcome.WorldDestroyed);
            emit(new WorldEvent(world.Year, WorldEventKind.RunEnded, world.PlayerActorId, ritual.NodeId, "World destroyed"));
            return true;
        }

        // Called only after the challenger wins the battle. Reset immediately restores the boss.
        internal void BossDefeated(WorldState world, ActorState challenger, Action<WorldEvent> emit)
        {
            var ritual = world.Ritual;
            if (challenger.Id == world.ChosenActorId)
            {
                world.Finish(RunOutcome.Cleared);
                emit(new WorldEvent(world.Year, WorldEventKind.WorldCleared, challenger.Id, ritual.NodeId));
                emit(new WorldEvent(world.Year, WorldEventKind.RunEnded, challenger.Id, ritual.NodeId, "Chosen defeated final boss"));
                return;
            }

            ritual.DeadlineYear = checked(world.Year + ritual.PeriodYears);
            ritual.ResetCount++;
            var node = world.Nodes[ritual.NodeId];
            node.MonsterHealth = ritual.BossHealth;
            node.MonsterAttack = ritual.BossAttack;
            emit(new WorldEvent(world.Year, WorldEventKind.RitualReset, challenger.Id, ritual.NodeId,
                "Deadline year " + ritual.DeadlineYear));
            challenger.Die();
            emit(new WorldEvent(world.Year, WorldEventKind.Died, challenger.Id, ritual.NodeId, "Ritual backlash"));
        }
    }
}
