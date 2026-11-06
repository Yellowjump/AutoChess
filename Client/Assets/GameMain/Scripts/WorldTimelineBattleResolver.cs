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

    private static int EquivalentLegacyItem(int timelineItem) => timelineItem switch
    {
        9102 => 9, // 古修残剑：自动飞剑
        9103 => 8, // 敛息诀：主动技能触发技能急速
        9104 => 3, // 镇水铃：生命比例护盾
        _ => 0
    };

    public static int StorySkillItem(int timelineItem) => timelineItem switch
    {
        9121 or 9122 or 9124 => timelineItem,
        _ => EquivalentLegacyItem(timelineItem) > 0 ? timelineItem : 0
    };

#if UNITY_EDITOR
    public static bool TryPreview(WorldTimelineDebug.WorldTimelineDebugView view, IReadOnlyList<int> itemIds,
        bool threeTargets = false, bool seedBurn = false)
    {
        var world = view.Simulation?.State;
        var table = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        if (world == null || table == null || itemIds.Count == 0 || itemIds.Any(id => !table.HasDataRow(id)) ||
            !world.Actors.TryGetValue(world.PlayerActorId, out var player) ||
            !player.IsAlive || !player.NodeId.HasValue) return false;

        int node = player.NodeId.Value;
        var resolver = new WorldTimelineBattleResolver();
        const int healthMultiplier = 50;
        var tester = new ActorState(player.Id, node,
            (int)Math.Min(int.MaxValue, (long)player.MaxHealth * healthMultiplier), player.CombatAttack);
        // Keep the character model and basic weapon; only add the selected test loadout.
        foreach (int item in player.Items)
            if (resolver.heroByItem.ContainsKey(item)) tester.Items.Add(item);
        int dummyHealth = (int)Math.Min(int.MaxValue, Math.Max(600L, player.MaxHealth * 4L) * healthMultiplier);
        int dummyAttack = Mathf.Max(1, player.Attack / 8);
        var opponents = new ActorState[threeTargets ? 3 : 1];
        for (int i = 0, id = -1; i < opponents.Length; i++, id--)
        {
            if (id == player.Id) id--;
            opponents[i] = new ActorState(id, node, dummyHealth, dummyAttack);
        }
        var sandbox = new WorldState(player.Id);
        if (world.Nodes.TryGetValue(node, out var location)) sandbox.Nodes.Add(node, location);
        return view.TryRunBattlePreview(resolver.Run(sandbox, node,
            new[] { tester }, opponents, true, _ => { }, itemIds.ToArray(), seedBurn));
    }
#endif

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
        IReadOnlyList<ActorState> second, bool visible, Action<int[]> completed, IReadOnlyList<int> previewItems = null,
        bool seedBurn = false)
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
            if (result.Count == 0 ||
                (world.StoryCombatSkillsEnabled && actor.Items.Contains(9122) &&
                    !result.Contains(1) && !result.Contains(2) && !result.Contains(1001)))
                result.Add(1);
            if (world.StoryCombatSkillsEnabled)
                foreach (int item in actor.Items)
                {
                    int skillItem = StorySkillItem(item);
                    if (skillItem > 0 && !result.Contains(skillItem) &&
                        !result.Contains(EquivalentLegacyItem(item))) result.Add(skillItem);
                }
            foreach (int item in actor.Items)
                if (item < 0) result.Add(-item);
            if (previewItems != null && actor.Id == world.PlayerActorId)
            {
                // Replace an equivalent starting weapon unless both versions were explicitly selected.
                foreach (int item in previewItems)
                {
                    if (result.Contains(item)) continue;
                    int legacy = EquivalentLegacyItem(item);
                    int equivalent = legacy > 0 && !previewItems.Contains(legacy) ? result.IndexOf(legacy) : -1;
                    if (equivalent >= 0) result[equivalent] = item;
                    else result.Add(item);
                }
            }
            return result;
        }

        int seed = unchecked(world.Year * 73856093 ^ nodeId * 19349663);
        foreach (var actor in first.Concat(second))
            seed = unchecked(seed * 31 + actor.Health * 7 + actor.Attack);
        int regionDistance = world.Nodes.TryGetValue(nodeId, out var node)
            ? HexWorldMap.Distance(node.Q, node.R) : 0;
        using (visible ? WorldTimelineDebug.WorldTimelineBoard.SuspendMapPresentation() : null)
            yield return HotGameEntry.HeroManager.RunTimelineBattle(first, second, Hero, Items, seed, visible,
                completed, regionDistance, unlimitedDuration: previewItems != null, seedPreviewBurn: seedBurn);
    }
}
