using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DataTable;
using UnityGameFramework.Runtime;
using WorldTimeline;
using Entity;

// Lives in the main game assembly so the pure timeline assembly does not depend on Unity's combat code.
public sealed class WorldTimelineBattleResolver : IWorldAsyncPartyBattleResolver, IWorldBattleForecaster
{
    private const int SwordArtId = 1;
    private const int TechniqueExperiencePerBattle = 10;
    private const int BrushAttackItem = 9147;
    private static readonly int BlackfangNode = HexWorldMap.Id(0, -9);
    private static readonly int AshridgeNode = HexWorldMap.Id(-8, 5);
    private readonly Dictionary<int, int> heroByItem = new Dictionary<int, int>();
    private readonly Dictionary<int, int> nativeItems = new Dictionary<int, int>();
    private readonly Dictionary<int, string> appearanceByBirth = new Dictionary<int, string>();
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

    private static string CultivatorAppearance(int startingItem) => startingItem switch
    {
        8101 => "Qinghuang",
        8102 => "Chili",
        8103 => "Baixi",
        8104 => "Hegui",
        8105 => "Xuansha",
        8106 => "Chongshan",
        8107 => "Wenchao",
        8108 => "Zhaoye",
        8109 => "Gusong",
        8110 => "Zhuxing",
        WorldTimelineDebug.WorldTimelineDebugView.QiMoStartingItem => TimelineMonsterAnimation.QiMoAppearance,
        _ => null
    };

    public static int StorySkillItem(int timelineItem) => timelineItem switch
    {
        9121 or 9122 or 9124 or 9131 or 9135 or 9146 or 9151 or 9152 or 9153 or
            TimelineCaveHazards.EmberHeart or TimelineCaveHazards.IceBead or SaltChannel.RewardItemId => timelineItem,
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
        var tester = new ActorState(player.Id, player.BirthNodeId,
            (int)Math.Min(int.MaxValue, (long)player.MaxHealth * healthMultiplier), player.CombatAttack);
        // Presets retain starting equipment. The current-character preview copies the whole loadout.
        foreach (int item in player.Items)
            if (currentLoadout || resolver.heroByItem.ContainsKey(item)) tester.Items.Add(item);
        int previewTechnique = slashFormation ? 1001 : currentLoadout && world.TechniqueCombatEnabled &&
            player.Techniques.TryGetValue(SwordArtId, out var progress) ? progress.SkillId : 0;
        bool stelePreview = previewMonster == TimelineMonsterAnimation.NamelessSteleAppearance;
        int dummyHealth = (int)Math.Min(int.MaxValue, stelePreview
            ? Math.Max(600L, player.CombatAttack * 16L)
            : Math.Max(600L, player.MaxHealth * 4L) * healthMultiplier);
        int dummyAttack = Mathf.Max(1, player.Attack / (stelePreview ? 3 : 8));
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
            CaveRegionsEnabled = world.CaveRegionsEnabled,
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
        var cultivators = GameEntry.DataTable.GetDataTable<DRStartingCultivators>("StartingCultivators");
        if (cultivators != null)
            foreach (var row in cultivators.GetAllDataRows())
            {
                string appearance = CultivatorAppearance(row.ItemId);
                if (appearance != null) appearanceByBirth.Add(HexWorldMap.Id(row.Q, row.R), appearance);
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
        bool chosenFinal = world.Ritual?.NodeId == nodeId && first[0].Id == world.ChosenActorId &&
            second.Any(actor => actor.Id < 0 && !world.Actors.ContainsKey(actor.Id));
        bool visible = !WorldTimelineDebug.WorldTimelineDebugView.IsRestoringCheckpoint &&
            ((playerParticipates && !world.PlayerControlEnded) || chosenFinal);
        bool trialFormation = world.Nodes.TryGetValue(nodeId, out var node) &&
            node.StoryTrialStarted && second[0].Id <= -nodeId && !world.Actors.ContainsKey(second[0].Id);
        return Run(world, nodeId, first, second, visible, completed, lightningFormation: trialFormation);
    }

    internal IEnumerator PreviewFormation(WorldState world, int nodeId,
        Func<IReadOnlyList<ActorState>, IReadOnlyList<EntityQizi>, IEnumerator> planning)
    {
        bool playerFirst = world.FormationFirst.Any(a => a.Id == world.PlayerActorId);
        var own = new List<ActorState> { world.Actors[world.PlayerActorId] };
        for (int role = 0; role < 4; role++)
            if (world.Expedition.HasRecruited(role)) own.Add(world.Expedition.CreateCompanion(role, nodeId));
        var first = playerFirst ? own : world.FormationFirst;
        var second = playerFirst ? world.FormationSecond : own;
        bool trial = world.Nodes[nodeId].StoryTrialStarted && second.All(a => a.Id <= -nodeId);
        return Run(world, nodeId, first, second, true, _ => { }, lightningFormation: trial, planning: planning);
    }

    public IEnumerator ForecastGuardian(WorldState world, int nodeId, ActorState player, Action<int> remainingHealth)
    {
        var node = world.Nodes[nodeId];
        if (node.GuardianCount != 1 || node.IsGuardianGroup)
            throw new InvalidOperationException("The guardian forecast requires a single guardian.");
        var guardian = new ActorState(-nodeId, nodeId, node.MonsterHealth, node.MonsterAttack);
        var party = world.Expedition?.CreateCombatants(nodeId) ?? new List<ActorState> { player };
        return Run(world, nodeId, party, new[] { guardian }, false,
            result => remainingHealth(result.Take(party.Count).Any(hp => hp > 0) ? Math.Max(1, result[0]) : 0), forecast: true);
    }

    private IEnumerator Run(WorldState world, int nodeId, IReadOnlyList<ActorState> first,
        IReadOnlyList<ActorState> second, bool visible, Action<int[]> completed, IReadOnlyList<int> previewItems = null,
        bool seedBurn = false, bool previewCast = false, string previewMonster = null, bool lightningFormation = false,
        int previewTechnique = 0, bool slashFormation = false, bool forecast = false,
        Func<IReadOnlyList<ActorState>, IReadOnlyList<EntityQizi>, IEnumerator> planning = null)
    {
        world.Nodes.TryGetValue(nodeId, out var node);
        int Hero(ActorState actor)
        {
            if (ExpeditionParty.IsCompanion(actor.Id)) return 2;
            if (actor.Id == world.ChosenActorId) return chosenHero;
            foreach (int item in actor.Items)
                if (heroByItem.TryGetValue(item, out int hero)) return hero;
            return monsterHero;
        }

        bool IsCoastalGuardian(ActorState actor) => node?.HasCoastalFormation == true && IsGuardian(actor);
        bool IsCoastalRanged(ActorState actor) => IsCoastalGuardian(actor) &&
            -nodeId - actor.Id < NodeState.CoastalRangedCount;

        List<int> Items(ActorState actor)
        {
            if (ExpeditionParty.IsCompanion(actor.Id))
                return ExpeditionParty.Role(actor.Id) switch
                {
                    0 => new List<int> { 1, 3 }, 1 => new List<int> { 1, 9122 }, _ => new List<int> { 2 }
                };
            var result = new List<int>();
            foreach (int item in actor.Items)
                if (nativeItems.TryGetValue(item, out int native) && native > 0) result.Add(native);
            if (IsGuardian(actor) || (previewItems != null && !previewCast && second.Contains(actor)))
            {
                if (node?.IsExpeditionVanguard == true && IsGuardian(actor)) return new List<int> { 9190 };
                if (IsCoastalGuardian(actor))
                {
                    result.Add(IsCoastalRanged(actor) ? TimelineOuterGuardianCast.Eel : 9190);
                    return result;
                }
                int caveSkill = node?.IsCaveEncounter == true && IsGuardian(actor)
                    ? TimelineCaveHazards.SkillFor(node.GuardianAppearance) : 0;
                if (caveSkill != 0)
                {
                    result.Add(caveSkill);
                    return result;
                }
                if (nodeId == WorldTimelineDebug.WorldTimelineDebugView.LinxiEliteNode &&
                    actor.Id == -nodeId && world.Nodes[nodeId].IsGuardianGroup && IsGuardian(actor))
                    result.Add(3); // The first guardian carries the existing max-health shield skill.
                string guardianAppearance = Appearance(actor);
                int outerSkill = TimelineOuterGuardianCast.SkillFor(guardianAppearance);
                result.Add(outerSkill != 0 ? outerSkill : guardianAppearance switch
                {
                    TimelineRegionalGuardianCast.BadgerAppearance => TimelineRegionalGuardianCast.BadgerSkill,
                    TimelineRegionalGuardianCast.KilnAppearance => TimelineRegionalGuardianCast.KilnSkill,
                    TimelineRegionalGuardianCast.ContractAppearance => TimelineRegionalGuardianCast.ContractSkill,
                    SaltChannel.CrabAppearance => SaltChannel.CrabSkillId,
                    SaltChannel.BeastAppearance => SaltChannel.BeastSkillId,
                    "WreckBeast" => 9191,
                    "GateGolem" => 9192,
                    "RitualWraith" or TimelineMonsterAnimation.NamelessSteleAppearance => 9193,
                    _ => 9190
                });
                return result;
            }
            if (actor.Items.Contains(WorldTimelineDebug.WorldTimelineDebugView.QiMoStartingItem))
            {
                result.Remove(1);
                result.Add(BrushAttackItem);
            }
            // The default axe still supplies a basic attack for legacy cultivator lives.
            else if (result.Count == 0 ||
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
            if (world.CaveRegionsEnabled && actor.Items.Contains(TimelineCaveHazards.FrostWedge))
                result.Add(TimelineCaveHazards.FrostWedge);
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

        int battleYear = forecast ? world.Year + 1 : world.Year;
        int seed = unchecked(battleYear * 73856093 ^ nodeId * 19349663);
        foreach (var actor in first.Concat(second))
            seed = unchecked(seed * 31 + actor.Health * 7 + actor.Attack);
        int regionDistance = node == null ? 0 : HexWorldMap.Distance(node.Q, node.R);
        bool tidegateArena = nodeId == WorldTimelineDebug.WorldTimelineDebugView.LinxiEliteNode;
        string CombatAppearance(ActorState actor)
        {
            if (ExpeditionParty.IsCompanion(actor.Id))
                return new[] { "Chongshan", "Chili", "Wenchao", "Baixi" }[ExpeditionParty.Role(actor.Id)];
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
                if (node?.IsExpeditionVanguard == true) return "LuYing";
                if (IsCoastalGuardian(actor))
                    return IsCoastalRanged(actor) ? "SaltspineEel" : "TideboneCrab";
                if (battleYear >= world.OuterGuardiansFromYear &&
                    TimelineOuterGuardianCast.AppearanceAt(nodeId) is string outerAppearance)
                    return outerAppearance;
                if (battleYear >= world.RegionalGuardiansFromYear)
                {
                    if (nodeId == BlackfangNode) return TimelineRegionalGuardianCast.BadgerAppearance;
                    if (nodeId == AshridgeNode) return TimelineRegionalGuardianCast.KilnAppearance;
                    if (nodeId == WorldTimelineDebug.WorldTimelineDebugView.ContractPostNode)
                        return TimelineRegionalGuardianCast.ContractAppearance;
                }
                if (!string.IsNullOrEmpty(node.GuardianAppearance)) return node.GuardianAppearance;
                if (world.Ritual?.NodeId == nodeId) return TimelineMonsterAnimation.NamelessSteleAppearance;
                if (nodeId == WorldTimelineDebug.WorldTimelineDebugView.ContractPostNode && node.StoryRequiresGuardianDefeat) return "GateGolem";
                if (nodeId == HexWorldMap.Id(3, -9) || nodeId == HexWorldMap.Id(-8, 0)) return "WreckBeast";
                if (regionDistance <= 6) return "GateGolem";
                return null;
            }
            if (actor.Id == world.ChosenActorId) return "ChosenSwordsman";
            if (previewItems != null && second.Contains(actor)) return null;
            if (appearanceByBirth.TryGetValue(actor.BirthNodeId, out string appearance)) return appearance;
            return actor.Items.Select(CultivatorAppearance).FirstOrDefault(model => model != null);
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
            if (previewMonster is "RitualWraith" or TimelineMonsterAnimation.NamelessSteleAppearance)
                return (int)EnumSound.TimelineBossBattleBgm;
            if (previewMonster is "WreckBeast" or "GateGolem" || lightningFormation)
                return (int)EnumSound.TimelineEliteBattleBgm;

            var guardian = first.Concat(second).FirstOrDefault(IsGuardian);
            if (guardian == null) return (int)EnumSound.TimelineNormalBattleBgm;
            if (world.Ritual?.NodeId == nodeId || Appearance(guardian) == TimelineOuterGuardianCast.ClerkAppearance)
                return (int)EnumSound.TimelineBossBattleBgm;
            return node.IsGuardianGroup || Appearance(guardian) is "WreckBeast" or "GateGolem"
                ? (int)EnumSound.TimelineEliteBattleBgm : (int)EnumSound.TimelineNormalBattleBgm;
        }

        var formationSetup = previewItems == null && first.Concat(second).Any(a => world.ExpeditionFor(a.Id) != null && !a.HasJoinedChosen)
            ? ExpeditionBattleSetup.Create(world, first, second, visible, node?.IsExpeditionVanguard == true && second.All(IsGuardian)) : null;
        if (formationSetup != null) formationSetup.ExpeditionView = true;
        if (planning != null)
        {
            formationSetup ??= new TimelineBattleSetup();
            formationSetup.Planning = planning;
        }
        using (visible ? HotGameEntry.Sound?.PlayTemporaryMusic(BattleMusicId()) : null)
        using (visible ? WorldTimelineDebug.WorldTimelineBoard.SuspendMapPresentation() : null)
            yield return HotGameEntry.HeroManager.RunTimelineBattle(first, second, Hero, Items, seed, visible,
                completed, unlimitedDuration: previewItems != null, seedPreviewBurn: seedBurn,
                appearance: Appearance, enableUndertow: world.WreckBeastEncounterEnabled || previewMonster != null,
                enableSwordRecall: world.SwordRecallEnabled || (previewItems != null && previewItems.Count > 0),
                lightningFormation: lightningFormation, slashFormation: slashFormation,
                arenaStyle: previewMonster == TimelineMonsterAnimation.NamelessSteleAppearance
                    ? ResolveArena(HexWorldMap.Id(0, 0), HexWorldMap.Radius)
                    : node?.HasCoastalFormation == true ? TimelineArenaStyle.SaltFlat
                    : (visible || forecast || node?.IsCaveEncounter == true)
                        ? ResolveArena(nodeId, node == null ? 0 : HexWorldMap.Radius - regionDistance)
                    : TimelineArenaStyle.Forest,
                closeGuardFormation: node?.GuardiansStartClose == true && second.All(IsGuardian),
                coastalFormation: second.All(IsCoastalGuardian) ? actor => -nodeId - actor.Id : null,
                staggerStoneGuards: tidegateArena && second.All(IsGuardian),
                saltChannelDrained: world.SaltChannelDrained,
                openingGuardianPounce: node?.GuardianOpensWithPounce == true && second.Count == 1 && IsGuardian(second[0]),
                suppressNamelessSeal: world.Ritual?.NodeId == nodeId && world.SluiceOpening.HasValue && second.Any(IsGuardian),
                reportOwner: (visible || node?.IsExpeditionVanguard == true) && previewItems == null &&
                    first.Concat(second).Any(actor => actor.Id == world.PlayerActorId)
                    ? world.Actors[world.PlayerActorId] : null,
                techniqueSkill: actor => swordArts.TryGetValue(actor, out var art) ? art.SkillId
                    : previewItems != null && actor.Id == world.PlayerActorId ? previewTechnique : 0,
                settleTechnique: forecast ? null : (actor, kills) =>
                {
                    if (swordArts.TryGetValue(actor, out var art))
                        actor.SettleTechniqueBattle(SwordArtId, art.Rank, TechniqueExperiencePerBattle, kills);
                }, timeSliceSimulation: forecast,
                setup: formationSetup);
    }
}
