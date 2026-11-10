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
    private const int SwordArtId = 1;
    private const int TechniqueExperiencePerBattle = 10;
    private readonly Dictionary<int, int> heroByItem = new Dictionary<int, int>();
    private readonly Dictionary<int, int> nativeItems = new Dictionary<int, int>();
    private readonly int chosenHero;
    private readonly int monsterHero;

    private static TimelineArenaStyle ResolveArena(int nodeId, int depth)
    {
        var rows = GameEntry.DataTable.GetDataTable<DRBattleArena>("BattleArena")?.GetAllDataRows();
        if (rows == null) return TimelineArenaStyle.Forest;
        var rule = rows.FirstOrDefault(row => row.Rule == "Node" && HexWorldMap.Id(row.Q, row.R) == nodeId)
            ?? rows.Where(row => row.Rule == "Depth" && row.MinDepth <= depth)
                .OrderByDescending(row => row.MinDepth).FirstOrDefault();
        return Enum.TryParse<TimelineArenaStyle>(rule?.Arena, out var style) && Enum.IsDefined(typeof(TimelineArenaStyle), style)
            ? style : TimelineArenaStyle.Forest;
    }

    private static int EquivalentLegacyItem(int timelineItem) => timelineItem switch
    {
        9102 => 9, // 古修残剑：自动飞剑
        9103 => 8, // 敛息诀：主动技能触发技能急速
        9104 => 3, // 镇水铃：生命比例护盾
        _ => 0
    };

    public static int StorySkillItem(int timelineItem) => timelineItem switch
    {
        9121 or 9122 or 9124 or 9131 or 9151 or 9152 or 9153 => timelineItem,
        _ => EquivalentLegacyItem(timelineItem) > 0 ? timelineItem : 0
    };

    public static int InventorySkillItem(int itemId, bool storyCombatSkills)
    {
        if (itemId < 0) return -itemId;
        if (storyCombatSkills)
        {
            int storySkill = StorySkillItem(itemId);
            if (storySkill != 0) return storySkill;
        }
        var mappings = GameEntry.DataTable.GetDataTable<DRBattleMapping>("BattleMapping");
        return mappings?.GetAllDataRows().FirstOrDefault(row => row.Kind == "Item" &&
            row.TimelineItemId == itemId)?.NativeItemId ?? 0;
    }

#if UNITY_EDITOR
    public static bool TryPreviewCurrentCharacter(WorldTimelineDebug.WorldTimelineDebugView view, bool threeTargets)
        => TryPreview(view, null, threeTargets);

    public static bool TryPreview(WorldTimelineDebug.WorldTimelineDebugView view, IReadOnlyList<int> itemIds,
        bool threeTargets = false, bool seedBurn = false, bool previewCast = false, string previewMonster = null,
        bool lightningFormation = false, bool slashFormation = false)
    {
        var world = view.Simulation?.State;
        var table = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        bool currentLoadout = itemIds == null;
        if (world == null || table == null ||
            (!currentLoadout && (itemIds.Count == 0 || itemIds.Any(id => !table.HasDataRow(id)))) ||
            !world.Actors.TryGetValue(world.PlayerActorId, out var player) ||
            !player.IsAlive || !player.NodeId.HasValue) return false;

        int node = player.NodeId.Value;
        var resolver = new WorldTimelineBattleResolver();
        const int healthMultiplier = 50;
        var tester = new ActorState(player.Id, node,
            (int)Math.Min(int.MaxValue, (long)player.MaxHealth * healthMultiplier), player.CombatAttack);
        // Presets copy only the starting identity. The current-character preview copies the whole loadout.
        foreach (int item in player.Items)
            if (currentLoadout || resolver.heroByItem.ContainsKey(item)) tester.Items.Add(item);
        int previewTechnique = slashFormation ? 1001 : currentLoadout && world.TechniqueCombatEnabled &&
            player.Techniques.TryGetValue(SwordArtId, out var progress) ? progress.SkillId : 0;
        int dummyHealth = (int)Math.Min(int.MaxValue, Math.Max(600L, player.MaxHealth * 4L) * healthMultiplier);
        int dummyAttack = Mathf.Max(1, player.Attack / 8);
        var opponents = new ActorState[slashFormation || threeTargets || previewCast ? 3 : 1];
        for (int i = 0, id = -1; i < opponents.Length; i++, id--)
        {
            if (id == player.Id) id--;
            int targetHealth = slashFormation ? 100 : lightningFormation
                ? (int)Math.Min(int.MaxValue, (i == 0 ? 6L : 10L) * player.CombatAttack + 100)
                : dummyHealth;
            opponents[i] = new ActorState(id, node, targetHealth, dummyAttack);
        }
        var sandbox = new WorldState(player.Id)
        {
            StoryCombatSkillsEnabled = currentLoadout && world.StoryCombatSkillsEnabled,
            SwordRecallEnabled = currentLoadout && world.SwordRecallEnabled,
            WreckBeastEncounterEnabled = currentLoadout && world.WreckBeastEncounterEnabled
        };
        if (world.Nodes.TryGetValue(node, out var location)) sandbox.Nodes.Add(node, location);
        return view.TryRunBattlePreview(resolver.Run(sandbox, node,
            new[] { tester }, opponents, true, _ => { }, itemIds?.ToArray() ?? Array.Empty<int>(),
            seedBurn, previewCast, previewMonster, lightningFormation, previewTechnique, slashFormation));
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
        bool trialFormation = world.Nodes.TryGetValue(nodeId, out var node) &&
            node.StoryTrialStarted && second[0].Id <= -nodeId && !world.Actors.ContainsKey(second[0].Id);
        return Run(world, nodeId, first, second, visible, completed, lightningFormation: trialFormation);
    }

    private IEnumerator Run(WorldState world, int nodeId, IReadOnlyList<ActorState> first,
        IReadOnlyList<ActorState> second, bool visible, Action<int[]> completed, IReadOnlyList<int> previewItems = null,
        bool seedBurn = false, bool previewCast = false, string previewMonster = null, bool lightningFormation = false,
        int previewTechnique = 0, bool slashFormation = false)
    {
        int Hero(ActorState actor)
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
            if (IsGuardian(actor) || (previewItems != null && !previewCast && second.Contains(actor)))
            {
                if (nodeId == WorldTimelineDebug.WorldTimelineDebugView.LinxiEliteNode &&
                    actor.Id == -nodeId && world.Nodes[nodeId].IsGuardianGroup && IsGuardian(actor))
                    result.Add(3); // The first guardian carries the existing max-health shield skill.
                result.Add(Appearance(actor) switch
                {
                    "WreckBeast" => 9191,
                    "GateGolem" => 9192,
                    "RitualWraith" => 9193,
                    _ => 9190
                });
                return result;
            }
            // The default axe still supplies a basic attack for legacy cultivator lives.
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
        bool tidegateArena = nodeId == WorldTimelineDebug.WorldTimelineDebugView.LinxiEliteNode;
        string CombatAppearance(ActorState actor)
        {
            if (previewCast)
            {
                if (actor == first[0]) return "Chongshan";
                int index = 0;
                while (index < second.Count && second[index] != actor) index++;
                return new[] { "ShenYan", "LuYing", "Zhuxing" }[index];
            }
            if (previewMonster != null && actor == second[0]) return previewMonster;
            // A node guardian is a temporary combatant, never a saved timeline actor.
            if (IsGuardian(actor))
            {
                if (world.Ritual?.NodeId == nodeId) return "RitualWraith";
                if (nodeId == HexWorldMap.Id(3, -9) || nodeId == HexWorldMap.Id(-8, 0)) return "WreckBeast";
                if (regionDistance <= 6) return "GateGolem";
            }
            if (actor.Items.Contains(8106)) return "Chongshan";
            if (actor.Items.Contains(8110)) return "Zhuxing";
            return null;
        }
        string Appearance(ActorState actor)
        {
            if (tidegateArena && IsGuardian(actor)) return "GateGolem";
            return CombatAppearance(actor) ?? (tidegateArena ? "LuYing" : null);
        }
        bool IsGuardian(ActorState actor) => previewItems == null && actor.Id <= -nodeId
            && !world.Actors.ContainsKey(actor.Id);
        var swordArts = new Dictionary<ActorState, (int Rank, int SkillId)>();
        if (world.TechniqueCombatEnabled && previewItems == null)
            foreach (var actor in first.Concat(second))
                if (actor.Techniques.TryGetValue(SwordArtId, out var progress))
                    swordArts.Add(actor, (progress.Rank, progress.SkillId));
        int BattleMusicId()
        {
            if (previewMonster == "RitualWraith") return (int)EnumSound.TimelineBossBattleBgm;
            if (previewMonster is "WreckBeast" or "GateGolem" || lightningFormation)
                return (int)EnumSound.TimelineEliteBattleBgm;

            var guardian = first.Concat(second).FirstOrDefault(IsGuardian);
            if (guardian == null) return (int)EnumSound.TimelineNormalBattleBgm;
            if (world.Ritual?.NodeId == nodeId) return (int)EnumSound.TimelineBossBattleBgm;
            return node.IsGuardianGroup || Appearance(guardian) is "WreckBeast" or "GateGolem"
                ? (int)EnumSound.TimelineEliteBattleBgm : (int)EnumSound.TimelineNormalBattleBgm;
        }

        using (visible ? HotGameEntry.Sound?.PlayTemporaryMusic(BattleMusicId()) : null)
        using (visible ? WorldTimelineDebug.WorldTimelineBoard.SuspendMapPresentation() : null)
            yield return HotGameEntry.HeroManager.RunTimelineBattle(first, second, Hero, Items, seed, visible,
                completed, unlimitedDuration: previewItems != null, seedPreviewBurn: seedBurn,
                appearance: Appearance, enableUndertow: world.WreckBeastEncounterEnabled || previewMonster != null,
                enableSwordRecall: world.SwordRecallEnabled || (previewItems != null && previewItems.Count > 0),
                lightningFormation: lightningFormation, slashFormation: slashFormation,
                arenaStyle: visible ? ResolveArena(nodeId, node == null ? 0 : HexWorldMap.Radius - regionDistance) : TimelineArenaStyle.Forest,
                closeGuardFormation: node?.GuardiansStartClose == true && second.All(IsGuardian),
                staggerStoneGuards: tidegateArena && second.All(IsGuardian),
                reportOwner: visible && previewItems == null ? world.Actors[world.PlayerActorId] : null,
                techniqueSkill: actor => swordArts.TryGetValue(actor, out var art) ? art.SkillId
                    : previewItems != null && actor.Id == world.PlayerActorId ? previewTechnique : 0,
                settleTechnique: (actor, kills) =>
                {
                    if (swordArts.TryGetValue(actor, out var art))
                        actor.SettleTechniqueBattle(SwordArtId, art.Rank, TechniqueExperiencePerBattle, kills);
                });
    }
}
