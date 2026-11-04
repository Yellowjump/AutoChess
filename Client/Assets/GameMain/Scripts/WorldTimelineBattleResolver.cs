using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DataTable;
using UnityGameFramework.Runtime;
using WorldTimeline;

// Lives in the main game assembly so the pure timeline assembly does not depend on Unity's combat code.
public sealed class WorldTimelineBattleResolver : IWorldAsyncPartyBattleResolver
{
    private readonly Dictionary<int, int> heroByItem = new Dictionary<int, int>();
    private readonly Dictionary<int, int> nativeItems = new Dictionary<int, int>();
    private readonly int chosenHero;
    private readonly int monsterHero;

    public WorldTimelineBattleResolver()
    {
        var table = GameEntry.DataTable.GetDataTable<DRBattleMapping>("BattleMapping");
        if (table == null) throw new InvalidOperationException("轮回战斗映射表尚未加载。");
        foreach (var row in table.GetAllDataRows())
        {
            int id = row.TimelineItemId, hero = row.HeroId, item = row.NativeItemId;
            if (row.Kind == "Chosen") chosenHero = hero;
            else if (row.Kind == "Monster") monsterHero = hero;
            else if (row.Kind == "Item")
            {
                if (hero > 0) heroByItem.Add(id, hero);
                nativeItems.Add(id, item);
            }
            else throw new InvalidOperationException("未知轮回战斗映射类型：" + row.Kind);
        }
        if (chosenHero == 0 || monsterHero == 0) throw new InvalidOperationException("缺少天选或守怪的战斗映射。");
    }

    public BattleResult Resolve(int firstHealth, int firstAttack, int secondHealth, int secondAttack) =>
        throw new InvalidOperationException("自走棋战斗需要完整的角色、装备和关卡信息。");

    public int[] ResolveParty(WorldState world, int nodeId, IReadOnlyList<ActorState> first,
        IReadOnlyList<ActorState> second)
    {
        int[] result = null;
        var routine = Run(world, nodeId, first, second, false, health => result = health);
        using (routine as IDisposable)
            while (routine.MoveNext())
            {
                // The synchronous entry point only runs hidden battles.
                if (routine.Current is IEnumerator nested)
                {
                    using (nested as IDisposable)
                        while (nested.MoveNext()) throw new InvalidOperationException("Unexpected visible battle.");
                }
            }
        return result;
    }

    public IEnumerator ResolvePartyRoutine(WorldState world, int nodeId, IReadOnlyList<ActorState> first,
        IReadOnlyList<ActorState> second, Action<int[]> completed)
    {
        bool playerParticipates = first.Concat(second).Any(actor => actor.Id == world.PlayerActorId);
        bool visible = playerParticipates && !world.PlayerControlEnded
            && !WorldTimelineDebug.WorldTimelineDebugView.IsRestoringCheckpoint;
        return Run(world, nodeId, first, second, visible, completed);
    }

    private IEnumerator Run(WorldState world, int nodeId, IReadOnlyList<ActorState> first,
        IReadOnlyList<ActorState> second, bool visible, Action<int[]> completed)
    {        int Hero(ActorState actor)
        {
            if (actor.Id == world.ChosenActorId) return chosenHero;
            foreach (int item in actor.Items)
                if (heroByItem.TryGetValue(item, out int hero)) return hero;
            return monsterHero;
        }

        List<int> Items(ActorState actor)
        {
            var result = new List<int>();
            foreach (int item in actor.Items)
                if (nativeItems.TryGetValue(item, out int native) && native > 0) result.Add(native);
            // The default axe supplies a basic attack for unarmed monsters and legacy lives.
            if (result.Count == 0) result.Add(1);
            return result;
        }

        int seed = unchecked(world.Year * 73856093 ^ nodeId * 19349663);
        foreach (var actor in first.Concat(second))
            seed = unchecked(seed * 31 + actor.Health * 7 + actor.Attack);
        yield return HotGameEntry.HeroManager.RunTimelineBattle(first, second, Hero, Items, seed, visible, completed);
    }
}
