using System;

namespace WorldTimeline
{
    // Small, deterministic fixture shared by the Unity playground and executable tests.
    public static class WorldTimelineDemoFactory
    {
        public const int PlayerId = 1;
        public const int ChosenId = 1000;
        public const int GridSize = 5;
        public const int FinalNodeId = 12;

        public static WorldSimulation Create(int playerNode = 24, int health = 220, int attack = 100, int ritualPeriod = 12)
        {
            var world = new WorldState(PlayerId);
            for (int id = 0; id < 25; id++)
            {
                int x = id % 5, y = id / 5;
                var node = new NodeState(id) { CultivationPerYear = 5 - Math.Abs(x - 2) - Math.Abs(y - 2) };
                if (x > 0) node.Links.Add(id - 1);
                if (x < 4) node.Links.Add(id + 1);
                if (y > 0) node.Links.Add(id - 5);
                if (y < 4) node.Links.Add(id + 5);
                world.Nodes.Add(id, node);
            }
            SetMonster(world, 1, 60, 6);
            SetMonster(world, 2, 100, 10);
            SetMonster(world, 7, 160, 16);
            SetMonster(world, 18, 180, 60);
            world.Nodes[6].Items.Add(601);
            world.Nodes[22].Items.Add(2201);
            world.Actors.Add(PlayerId, new ActorState(PlayerId, playerNode, health, attack, 80));
            var simulation = new WorldSimulation(world);
            simulation.ConfigureChosen(new ActorState(ChosenId, 0, 320, 60, 100), new[] { 0, 1, 2, 7, 12 });
            simulation.ConfigureFinalRitual(360, 30, ritualPeriod);
            return simulation;
        }

        public static LifeRecord CreateRelayRecord(int arrivalYear)
        {
            if (arrivalYear < 5 || arrivalYear >= 80) throw new ArgumentOutOfRangeException(nameof(arrivalYear));
            var source = Create(playerNode: 20, health: 800, attack: 180, ritualPeriod: 100);
            source.SubmitPlayerAction(WorldAction.Wait(arrivalYear - 4));
            foreach (int next in new[] { 15, 10, 11, 12 }) source.SubmitPlayerAction(WorldAction.Move(next));
            return source.RecordPlayerLife();
        }

        public static LifeRecord CreateFateRecord()
        {
            var source = Create(playerNode: 19, health: 200, attack: 40);
            source.SubmitPlayerAction(WorldAction.Move(18, 5));
            return source.RecordPlayerLife();
        }

        private static void SetMonster(WorldState world, int nodeId, int health, int attack)
        {
            world.Nodes[nodeId].MonsterHealth = health;
            world.Nodes[nodeId].MonsterAttack = attack;
        }
    }
}
