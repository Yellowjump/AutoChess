using System;
using System.Linq;

namespace WorldTimeline
{
    public static class HexTimelineDemoFactory
    {
        public static int PlayerStart => HexWorldMap.Id(0, -10);
        public static int FinalNode => HexWorldMap.Id(0, 0);
        public static int FateNode => HexWorldMap.Id(0, -9);

        public static WorldSimulation Create(int? playerNode = null, int health = 220, int attack = 100, int period = 35,
            CultivationConfig cultivation = null, int talentSeed = 1, int initialLevel = 1, TalentProfile talents = null,
            int startingItem = 0, int weaponAttack = 0, Action<WorldState> configureLocations = null, IWorldBattleResolver battle = null)
        {
            var world = new WorldState(1);
            foreach (var pair in HexWorldMap.Create()) world.Nodes.Add(pair.Key, pair.Value);
            // Keep the relay corridor predictable; exploration elsewhere remains populated.
            for (int r = 1; r <= 10; r++) world.Nodes[HexWorldMap.Id(0, r)].MonsterHealth = 0;
            for (int q = -10; q < 0; q++)
            {
                var node = world.Nodes[HexWorldMap.Id(q, 0)];
                node.MonsterHealth = q % 2 == 0 ? 60 + (q + 10) * 8 : 0;
                node.MonsterAttack = 6 + q + 10;
            }
            world.Nodes[PlayerStart].MonsterHealth = 0;
            world.Nodes[HexWorldMap.Id(1, -10)].MonsterHealth = 0;
            world.Nodes[FateNode].MonsterHealth = cultivation == null ? 180 : 240;
            world.Nodes[FateNode].MonsterAttack = cultivation == null ? 60 : 80;
            var player = new ActorState(1, playerNode ?? PlayerStart, health, attack, 180);
            if (cultivation != null) CultivationSystem.Initialize(player, cultivation, talents ?? TalentProfile.Generate(talentSeed), initialLevel);
            if (startingItem != 0) player.Items.Add(startingItem);
            player.Attack += weaponAttack;
            configureLocations?.Invoke(world);
            world.Actors.Add(1, player);
            var simulation = new WorldSimulation(world, battle, cultivation);
            var chosen = new ActorState(1000, HexWorldMap.Id(-10, 0), 900, 100, 200);
            if (cultivation != null) CultivationSystem.Initialize(chosen, cultivation, TalentProfile.Generate(1000), Math.Min(3, cultivation.Levels.Count));
            simulation.ConfigureChosen(chosen,
                Enumerable.Range(-10, 11).Select(q => HexWorldMap.Id(q, 0)));
            simulation.ConfigureFinalRitual(360, 30, period);
            return simulation;
        }

        public static LifeRecord CreateRelayRecord(int year, CultivationConfig cultivation = null)
        {
            if (year <= 10 || year >= 180) throw new ArgumentOutOfRangeException(nameof(year));
            var source = Create(HexWorldMap.Id(0, 10), 800, 180, 200, cultivation, year, cultivation == null ? 1 : Math.Min(3, cultivation.Levels.Count));
            source.SubmitPlayerAction(WorldAction.Wait(year - 10));
            for (int r = 9; r >= 0; r--) source.SubmitPlayerAction(WorldAction.Move(HexWorldMap.Id(0, r)));
            return source.RecordPlayerLife();
        }

        public static LifeRecord CreateFateRecord(CultivationConfig cultivation = null)
        {
            var weak = new AttributeTalent(TalentGrade.C, 0);
            var source = Create(HexWorldMap.Id(1, -10), 200, 40, cultivation: cultivation, talents: new TalentProfile(weak, weak, weak));
            source.SubmitPlayerAction(WorldAction.Move(FateNode, 5));
            return source.RecordPlayerLife();
        }
    }
}
