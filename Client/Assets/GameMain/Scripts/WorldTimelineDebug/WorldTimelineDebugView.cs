using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldTimeline;

namespace WorldTimelineDebug
{
    // Deliberately independent of GameFramework/UI bootstrapping and input-system bindings.
    public sealed partial class WorldTimelineDebugView : MonoBehaviour
    {
        private sealed class HistoryEntry
        {
            public string Name;
            public LifeRecord Record;
            public bool Selected;
        }

        private readonly List<HistoryEntry> library = new List<HistoryEntry>();
        private readonly List<SavedLife> savedLives = new List<SavedLife>();
        private Action returnToMenu;
        private bool saveDirty;
        private bool saveReadable = true;
        private string saveIssue;
#if UNITY_EDITOR
        // Editor smoke checks use disposable saves without replacing the player's current run.
        public static string EditorSaveDirectory { get; set; }
#endif
        private static string SaveDirectory
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorSaveDirectory)) return EditorSaveDirectory;
#endif
                return Path.Combine(Application.persistentDataPath, "WorldTimeline");
            }
        }
        private string SavePath => Path.Combine(SaveDirectory, "cultivator-lives.dat");
        public sealed class StartingCultivator
        {
            public string Name;
            public int Birth, Item, Attack;
        }

        public sealed class LocationDefinition
        {
            public int Q, R, CultivationPerYear, MonsterHealth, MonsterAttack, LootItemId, LootAttackBonus;
            public string Name, LootName;
        }
        public sealed class ScenarioData
        {
            public CultivationConfig Cultivation;
            public StartingCultivator[] Cultivators;
            public LocationDefinition[] Locations;
            public StoryConfig Stories;
            public IReadOnlyDictionary<int, TechniqueDefinition> Techniques;
        }
        public static Func<ScenarioData> ScenarioFactory { get; set; }
        private ScenarioData scenarioData;

        private readonly List<StartingCultivator> roster = new List<StartingCultivator>();
        private readonly Dictionary<int, LocationDefinition> locations = new Dictionary<int, LocationDefinition>();
        private StartingCultivator startingCultivator;
        private bool preparing;

        public static Func<IWorldBattleResolver> BattleResolverFactory { get; set; }
        private IWorldBattleResolver battleResolver;
        private bool advancing;
        private WorldSimulation simulation;
        private WorldTimelineBoard board;
        private Vector2 logScroll, historyScroll, growthScroll;
        private CultivationConfig cultivation;
        private bool showGrowth;
        private int inspectedActorId = 1;
        private int selectedNode, round = 1;
        private float hexSize = 12f;
        private Vector2 mapPan, mouseDown;
        private bool mapDragging;
        private Texture2D hexTexture;
        private GUIStyle hexText;
        private bool archived, autoObserve;
        private float nextStepTime;
        private int yearsPerSecond = 5;
        private string message = TimelineText("Timeline_UI_119");
        private GUIStyle title, text, muted, card, toggle, small, button;
        private Font font;
        private bool stylesReady;
        public WorldSimulation Simulation => simulation;
        public bool IsReady => preparing || simulation != null;

        public static WorldTimelineDebugView OpenFromMenu(Action onReturn)
        {
            var root = new GameObject("WorldTimeline");
            root.SetActive(false);
            var view = root.AddComponent<WorldTimelineDebugView>();
            view.returnToMenu = onReturn;
            root.SetActive(true);
            return view;
        }

        private void Awake()
        {
            if (ScenarioFactory != null)
            {
                scenarioData = ScenarioFactory();
                cultivation = scenarioData.Cultivation;
            }
#if UNITY_EDITOR
            else if (returnToMenu == null)
            {
                // Standalone diagnostic scene uses test fixtures, never shipped runtime resources.
                cultivation = CultivationConfig.ParseCsv(File.ReadAllText(Path.Combine(Application.dataPath,
                    "../../Tests/WorldTimeline/Fixtures/CultivationLevels.csv")));
            }
#endif
            else throw new InvalidOperationException("轮回数据表尚未初始化，请从 bootstrap 主菜单进入。");
            if (returnToMenu == null)
            {
                library.Add(new HistoryEntry { Name = "破仪 A · 第30年", Record = HexTimelineDemoFactory.CreateRelayRecord(30, cultivation), Selected = true });
                library.Add(new HistoryEntry { Name = "破仪 B · 第60年", Record = HexTimelineDemoFactory.CreateRelayRecord(60, cultivation), Selected = true });
                library.Add(new HistoryEntry { Name = "破仪 C · 第90年", Record = HexTimelineDemoFactory.CreateRelayRecord(90, cultivation), Selected = true });
                library.Add(new HistoryEntry { Name = "改命 A · 第5年死于 (0,-9)", Record = HexTimelineDemoFactory.CreateFateRecord(cultivation) });
            }
            if (returnToMenu != null)
            {
                LoadScenario();
                try
                {
                    foreach (var entry in WorldTimelineSaveStore.Load(SavePath))
                    {
                        savedLives.Add(entry);
                        AddLifeToLibrary(entry);
                        round = Math.Max(round, entry.Round + 1);
                    }
                }
                catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
                {
                    saveReadable = false;
                    saveIssue = TimelineText("Timeline_UI_123") + error.Message;
                }
            }
            if (returnToMenu == null) NewRound();
            else preparing = true;
        }

        private void InitializeBoard()
        {
            var map = new GameObject("轮回地形地图");
            map.transform.SetParent(transform, false);
            board = map.AddComponent<WorldTimelineBoard>();
            if (board.Initialize(simulation.State))
            {
                board.Sync(simulation.State, selectedNode);
                board.FocusActor(simulation.State.PlayerActorId);
            }
            else
            {
                Destroy(map);
                board = null;
                message = TimelineText("Timeline_UI_120");
            }
        }

        private void LoadScenario()
        {
            roster.AddRange(scenarioData.Cultivators);
        }

        private void ConfigureLocations(WorldState world, StoryConfig stories, bool closeLinxiFormation)
        {
            foreach (var pair in locations)
            {
                var node = world.Nodes[pair.Key];
                var row = pair.Value;
                node.CultivationPerYear = row.CultivationPerYear;
                node.MonsterHealth = row.MonsterHealth;
                node.MonsterAttack = row.MonsterAttack;
                if (pair.Key == LinxiEliteNode)
                    ConfigureLinxiGuardians(node, row, closeLinxiFormation);
                if (pair.Key == HexWorldMap.Id(4, -10) || pair.Key == GorgeGateNode)
                    node.StoryRequiresGuardianDefeat = true;
                node.Items.Clear();
                int loot = row.LootItemId;
                if (loot != 0) node.Items.Add(loot);
                node.LootAttackBonus = row.LootAttackBonus;
            }
            foreach (var cultivator in roster) world.Nodes[cultivator.Birth].MonsterHealth = 0;
            if (stories != null)
                foreach (var story in stories.Events.Values)
                {
                    if (story.IsMerchant) continue;
                    // These nodes now award through choices, never via the old automatic loot path.
                    world.Nodes[story.NodeId].Items.Clear();
                    world.Nodes[story.NodeId].LootAttackBonus = 0;
                }
        }

        private void AddLifeToLibrary(SavedLife entry)
        {
            var character = roster.FirstOrDefault(candidate => candidate.Birth == entry.Record.BirthNodeId);
            library.Add(new HistoryEntry
            {
                Name = TimelineText("Timeline_UI_ArchivedLife",
                    character?.Name ?? TimelineText("Timeline_UI_RoundName", entry.Round),
                    entry.Record.EndYear, TimelineText(entry.Record.EndedInDeath ? "Timeline_UI_116" : "Timeline_UI_118")),
                Record = entry.Record
            });
        }

        private bool SaveLives()
        {
            if (!saveDirty || !saveReadable) return true;
            try
            {
                WorldTimelineSaveStore.Save(SavePath, savedLives);
                saveDirty = false;
                saveIssue = null;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                saveIssue = TimelineText("Timeline_UI_124") + error.Message;
                return false;
            }
        }

        private void PopulateExploration(WorldState world, StoryConfig stories)
        {
            var reserved = new HashSet<int>(locations.Keys);
            reserved.UnionWith(roster.Select(cultivator => cultivator.Birth));
            if (stories != null)
                reserved.UnionWith(stories.Events.Values.Select(story => story.NodeId));
            foreach (var node in world.Nodes.Values)
            {
                // The ritual relay and the Chosen's route use scripted difficulty.
                if (reserved.Contains(node.Id) || (node.Q == 0 && node.R >= 0)
                    || (node.R == 0 && node.Q <= 0) || node.MonsterHealth > 0
                    || node.Items.Count > 0 || node.CultivationPerYear >= 8) continue;

                int depth = HexWorldMap.Radius - HexWorldMap.Distance(node.Q, node.R);
                int roll = (node.Q * node.Q * 17 + node.R * node.R * 31 + (node.Q + 10) * 13
                    + (node.R + 10) * 7 + (node.Q + 10) * (node.R + 10) * 11) % 10;
                if (roll < 3)
                {
                    node.MonsterHealth = 55 + depth * 22;
                    node.MonsterAttack = 7 + depth * 4;
                    node.HasExplorationGuardian = true;
                }
                else if (roll < 5)
                {
                    node.Items.Add(node.Id);
                    node.LootAttackBonus = 2 + depth;
                }
                else if (roll < 8)
                {
                    node.IsCultivationSite = true;
                    node.CultivationPerYear += 2;
                }
            }
        }

        private static readonly int SwordTrialNode = HexWorldMap.Id(2, -9);
        public static readonly int LinxiEliteNode = HexWorldMap.Id(3, -8);
        private static void ConfigureLinxiGuardians(NodeState node, LocationDefinition row, bool startClose = true)
        {
            node.SetGuardianGroup(2, row.MonsterHealth, row.MonsterAttack);
            node.GuardiansStartClose = startClose;
        }
        private const int LinxiRouteStory = 12;
        private static bool IsEastRouteLocation(int q, int r) =>
            (q == 8 || q == 9) && (r == -8 || r == -7);
        private static bool IsNorthRouteLocation(int q, int r) =>
            (q == -4 && r == 9) || (q == -3 && (r == 8 || r == 9));
        private static bool IsWestRouteLocation(int q, int r) =>
            (q == -9 && r == 5) || (q == -8 && (r == 4 || r == 5));
        private static bool IsSnowRouteLocation(int q, int r) =>
            (q == 2 && r == 7) || (q == 3 && (r == 6 || r == 7));
        private static readonly int GorgeGateNode = HexWorldMap.Id(8, -2);
        private static bool IsGorgeRouteLocation(int q, int r) =>
            r == -2 && q >= 7 && q <= 9;
        private static bool IsTideRouteLocation(int q, int r) =>
            (q == 7 && r == 2) || (q == 6 && (r == 2 || r == 3));

        private static bool IsLinxiRouteLocation(int q, int r) =>
            (q, r) is (4, -10) or (1, -8) or (1, -7) or (0, -7) or (3, -8);

        private void NewRound(IEnumerable<LifeRecord> historyOverride = null, bool enableStories = true,
            IEnumerable<RecordedStoryChoice> replayChoices = null, bool expandedExploration = true,
            bool storyCombatSkills = true, bool bleedRewardRoute = true, bool wreckBeastEncounter = true,
            bool swordRecall = true, bool techniqueCombat = true, bool swordTrial = true, bool lightningRoute = true,
            bool battleExperience = true, bool lightningTrials = true, bool balancedLightningTrials = true,
            bool merchants = true, bool linxiRoute = true, bool closeLinxiFormation = true,
            bool eastRoute = true, bool northRoute = true, bool westRoute = true, bool snowRoute = true,
            bool gorgeRoute = true, bool tideRoute = true)
        {
            battleResolver = returnToMenu == null ? null : BattleResolverFactory?.Invoke();
            locations.Clear();
            if (scenarioData != null)
                foreach (var location in scenarioData.Locations)
                    if ((linxiRoute || !IsLinxiRouteLocation(location.Q, location.R))
                        && (eastRoute || !IsEastRouteLocation(location.Q, location.R))
                        && (northRoute || !IsNorthRouteLocation(location.Q, location.R))
                        && (westRoute || !IsWestRouteLocation(location.Q, location.R))
                        && (snowRoute || !IsSnowRouteLocation(location.Q, location.R))
                        && (gorgeRoute || !IsGorgeRouteLocation(location.Q, location.R))
                        && (tideRoute || !IsTideRouteLocation(location.Q, location.R)))
                        locations.Add(HexWorldMap.Id(location.Q, location.R), location);
            var stories = enableStories ? scenarioData?.Stories : null;
            if (!linxiRoute) stories = stories?.WithoutEvents(LinxiRouteStory);
            if (!eastRoute) stories = stories?.WithoutEvents(13, 14, 15);
            if (!northRoute) stories = stories?.WithoutEvents(16, 17);
            if (!westRoute) stories = stories?.WithoutEvents(18, 19);
            if (!snowRoute) stories = stories?.WithoutEvents(20);
            if (!gorgeRoute) stories = stories?.WithoutEvents(21, 22);
            if (!tideRoute) stories = stories?.WithoutEvents(23, 24);
            simulation = HexTimelineDemoFactory.Create(playerNode: startingCultivator?.Birth,
                cultivation: cultivation, talentSeed: round, startingItem: startingCultivator?.Item ?? 0,
                weaponAttack: startingCultivator?.Attack ?? 0,
                configureLocations: startingCultivator == null ? (Action<WorldState>)null :
                    world => ConfigureLocations(world, stories, closeLinxiFormation),
                battle: battleResolver);
            if (!merchants && stories != null)
                stories = stories.WithoutEvents(stories.Events.Values.Where(story => story.IsMerchant).Select(story => story.Id).ToArray());
            // Keep pre-route checkpoints on the map and event set they originally replayed.
            if (!bleedRewardRoute) stories = stories?.WithoutEvents(9, 10);
            if (!lightningRoute) stories = stories?.WithoutOptions(504, 602, 702);
            if (enableStories && stories != null)
                foreach (var shop in stories.Events.Values.Where(story => story.IsMerchant))
                {
                    var node = simulation.State.Nodes[shop.NodeId];
                    node.IsMerchant = true;
                    node.MonsterHealth = 0;
                    node.Items.Clear();
                    node.LootAttackBonus = 0;
                }
            if (expandedExploration && startingCultivator != null) PopulateExploration(simulation.State, stories);
            simulation.State.StoryCombatSkillsEnabled = storyCombatSkills && enableStories;
            simulation.State.WreckBeastEncounterEnabled = wreckBeastEncounter && enableStories;
            simulation.State.SwordRecallEnabled = swordRecall && enableStories;
            simulation.State.TechniqueCombatEnabled = techniqueCombat && enableStories;
            simulation.State.BattleExperienceEnabled = battleExperience;
            simulation.State.BattleRewardsFromYear = 1;
            simulation.State.BalancedLightningTrials = balancedLightningTrials;
            simulation.SetBattleSettlementPresenter(PresentBattleSettlement);
            if (swordTrial && enableStories && startingCultivator != null &&
                simulation.State.Nodes.TryGetValue(SwordTrialNode, out var trial))
            {
                trial.SetGuardianGroup(3, 42, 7);
                trial.HasExplorationGuardian = true;
                trial.Items.Clear();
                trial.LootAttackBonus = 0;
            }
            if (simulation.State.WreckBeastEncounterEnabled && stories != null &&
                stories.Events.TryGetValue(8, out var wreckStory) &&
                simulation.State.Nodes.TryGetValue(wreckStory.NodeId, out var wreckNode))
            {
                wreckNode.MonsterHealth = 135;
                wreckNode.MonsterAttack = 10;
                wreckNode.StoryRequiresGuardianDefeat = true;
            }
            inspectedActorId = 1;
            int id = 10;
            foreach (var record in historyOverride ?? library.Where(e => e.Selected).Take(10).Select(e => e.Record))
                simulation.AddHistory(record, id++);
            if (scenarioData?.Techniques?.TryGetValue(1, out var swordArt) == true)
                foreach (var actor in simulation.State.Actors.Values.Where(actor => actor.Items.Contains(8104)))
                    actor.LearnTechnique(swordArt);
            if (enableStories && stories != null)
            {
                simulation.ConfigureStories(stories, round, returnToMenu != null && !IsRestoringCheckpoint, replayChoices);
                if (lightningRoute && lightningTrials)
                {
                    simulation.Stories.AllowOptionsOnRevisit(702, 602);
                    ConfigureLightningTrial(stories, 504, 1);
                    ConfigureLightningTrial(stories, 702, 2);
                    ConfigureLightningTrial(stories, 602, 3);
                }
            }
            archived = false;
            autoObserve = false;
            selectedNode = simulation.State.Actors[1].NodeId.Value;
            hexSize = 12f;
            mapPan = Vector2.zero;
            logScroll = Vector2.zero;
            message = TimelineText("Timeline_UI_121");
            showGrowth = false;
            if (board != null)
            {
                board.ClearPieces();
                board.Overview();
                board.Sync(simulation.State, selectedNode);
            }
            BeginCheckpoint();
            if (simulation.Stories != null && !UsesStoryHud && !IsRestoringCheckpoint) OpenStoryHud?.Invoke(this);
        }

        private void ConfigureLightningTrial(StoryConfig stories, int optionId, int stage)
        {
            if (stories.Options.TryGetValue(optionId, out var option) && option.ItemId != 0 &&
                stories.Events.TryGetValue(option.EventId, out var story) &&
                simulation.State.Nodes.TryGetValue(story.NodeId, out var node))
            {
                node.StoryTrialItemId = option.ItemId;
                node.StoryTrialStage = stage;
            }
        }

        private void Update()
        {
            if (advancing || simulation == null || !autoObserve || simulation.WaitingForPlayer || simulation.State.IsEnded) return;
            if (Time.unscaledTime < nextStepTime) return;
            nextStepTime = Time.unscaledTime + 1f / yearsPerSecond;
            StartAdvancing(simulation.AdvanceOneYearRoutine());
        }

        private void Execute(Action action)
        {
            try
            {
                action();
                if (board != null) board.Sync(simulation.State, selectedNode);
                logScroll.y = float.MaxValue;
                var player = simulation.State.Actors[WorldTimelineDemoFactory.PlayerId];
                if (!archived && (simulation.State.IsEnded || (returnToMenu == null && (!player.IsAlive || player.HasJoinedChosen))))
                {
                    archived = true;
                    if (startingCultivator != null)
                    {
                        library.RemoveAll(entry => entry.Record.BirthNodeId == startingCultivator.Birth);
                        savedLives.RemoveAll(entry => entry.Record.BirthNodeId == startingCultivator.Birth);
                        var entry = new SavedLife(round, simulation.RecordPlayerLife());
                        AddLifeToLibrary(entry);
                        savedLives.Add(entry);
                        saveDirty = true;
                        SaveLives();
                    }
                    else if (library.Count < 4 + WorldTimelineSaveStore.Capacity)
                    {
                        var entry = new SavedLife(round, simulation.RecordPlayerLife());
                        AddLifeToLibrary(entry);
                        if (returnToMenu != null && saveReadable)
                        {
                            savedLives.Add(entry);
                            saveDirty = true;
                            SaveLives();
                        }
                    }
                    else message = TimelineText("Timeline_UI_122");
                }
                if (simulation.State.IsEnded) autoObserve = false;
            }
            catch (InvalidOperationException error)
            {
                autoObserve = false;
                message = TimelineText("Timeline_UI_125") + error.Message;
                Debug.LogWarning(message);
            }
        }

        private void Submit(WorldAction action) => StartAdvancing(simulation.SubmitPlayerActionRoutine(action), true, action);

        private void StartAdvancing(IEnumerator routine, bool observeAfter = false, WorldAction action = null)
        {
            if (advancing) return;
            advancing = true;
            StartCoroutine(AdvanceRoutine(routine, observeAfter, action));
        }

#if UNITY_EDITOR
        public string BattleComparisonLabel { get; private set; }

        public bool TryRunLinxiComparison(IWorldBattleResolver resolver, bool growthRoute)
        {
            var gate = scenarioData?.Locations?.FirstOrDefault(location => HexWorldMap.Id(location.Q, location.R) == LinxiEliteNode);
            if (gate == null ||
                scenarioData?.Techniques?.TryGetValue(1, out var swordArt) != true ||
                swordArt.Stages.Count < 2 ||
                swordArt.Stages[1].Requirement != TechniqueRequirement.BattleExperience)
                return false;

            var hegui = roster.FirstOrDefault(cultivator => cultivator.Item == 8104);
            if (hegui == null) return false;

            var comparison = HexTimelineDemoFactory.Create(HexWorldMap.Id(2, -8),
                cultivation: cultivation, talentSeed: 1, startingItem: hegui.Item, weaponAttack: hegui.Attack,
                configureLocations: world => ConfigureLinxiGuardians(world.Nodes[LinxiEliteNode], gate),
                battle: resolver);
            comparison.State.StoryCombatSkillsEnabled = true;
            comparison.State.TechniqueCombatEnabled = true;
            var actor = comparison.State.Actors[comparison.State.PlayerActorId];
            actor.LearnTechnique(swordArt);
            if (growthRoute)
                actor.SettleTechniqueBattle(swordArt.Id, 1, swordArt.Stages[1].Value, 0);
            else
                actor.Items.Add(9104);

            comparison.SetBattleSettlementPresenter(PresentComparison);
            return TryRunBattlePreview(comparison.SubmitPlayerActionRoutine(WorldAction.Move(LinxiEliteNode)));

            IEnumerator PresentComparison(BattleSettlement result)
            {
                BattleComparisonLabel = growthRoute ? "GM 对照 · 二阶御剑诀" : "GM 对照 · 一阶御剑诀 + 镇水铃";
                try { yield return PresentBattleSettlement(result); }
                finally { BattleComparisonLabel = null; }
            }
        }

        public bool TryRunBattlePreview(IEnumerator routine)
        {
            if (advancing || preparing || simulation == null || !simulation.WaitingForPlayer ||
                simulation.Stories?.PendingChoice != null || simulation.Stories?.PendingResult != null) return false;
            advancing = true;
            StartCoroutine(AdvanceRoutine(routine, false, null, true));
            return true;
        }
#endif

        private IEnumerator AdvanceRoutine(IEnumerator routine, bool observeAfter, WorldAction action, bool preview = false)
        {
            var player = simulation.State.Actors[simulation.State.PlayerActorId];
            player.Techniques.TryGetValue(1, out var swordArt);
            int startingRank = swordArt?.Rank ?? 0;
            int startingProgress = swordArt?.Progress ?? 0;
            var requirement = swordArt?.NextStage?.Requirement;
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    var current = stack.Peek();
                    bool moved = false;
                    Exception failure = null;
                    try { moved = current.MoveNext(); }
                    catch (Exception error) { failure = error; }
                    if (failure != null)
                    {
                        autoObserve = false;
                        message = TimelineText("Timeline_UI_126") + failure.Message;
                        Debug.LogException(failure);
                        yield break;
                    }
                    if (!moved)
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested) stack.Push(nested);
                    else yield return current.Current;
                }
                if (preview) yield break;
                Execute(() => { });
                if (swordArt != null && swordArt.Rank > startingRank)
                    message = TimelineText("Timeline_SwordTrial_RankUp", swordArt.Rank, TechniqueSkillName(swordArt.SkillId));
                else if (swordArt != null && swordArt.Progress > startingProgress)
                    message = TimelineText(requirement == TechniqueRequirement.SkillKills
                        ? "Timeline_SwordTrial_Kills" : "Timeline_SwordTrial_Experience",
                        swordArt.Progress - startingProgress, swordArt.Progress, swordArt.NextStage.Value.Value);
                if (checkpoint != null)
                {
                    checkpoint.actions.Add(new CheckpointAction(action));
                    SaveCheckpoint();
                }
                if (observeAfter && !simulation.WaitingForPlayer && !simulation.State.IsEnded) autoObserve = true;
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                advancing = false;
            }
        }
        private void OnGUI()
        {
            if (simulation == null && !preparing) return;
            // The production flow uses a native UGUI preparation prefab.
            if (returnToMenu != null) return;
            if (!preparing) return;
            EnsureStyles();
            Matrix4x4 previous = GUI.matrix;
            Color previousBackground = GUI.backgroundColor;
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1440 * scale) / 2, (Screen.height - 900 * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                if (advancing)
                {
                    GUI.Label(new Rect(35, 20, 1000, 45), TimelineText("Timeline_UI_129"), title);
                    return;
                }
                if (returnToMenu != null && board == null)
                {
                    GUI.color = new Color(.07f, .09f, .12f);
                    GUI.DrawTexture(new Rect(-1440, -900, 4320, 2700), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                GUI.backgroundColor = new Color(.1f, .15f, .2f);
                if (board == null) GUI.Box(new Rect(0, 0, 1440, 900), GUIContent.none);
                else GUI.Box(new Rect(20, 6, 1400, 82), GUIContent.none, card);
                GUI.enabled = !showGrowth;
                if (preparing)
                {
                    DrawPreparation();
                    return;
                }
                DrawHeader();
                if (board == null) DrawMap();
                else
                {
                    DrawBoard(scale);
                    if (!showBoardDetails) DrawLocationDetails();
                }
                if (board == null) DrawActions();
                else
                {
                    GUI.BeginGroup(new Rect(0, 156, 880, 732));
                    DrawActions();
                    GUI.EndGroup();
                }
                if (board == null || showBoardDetails)
                {
                    DrawActors();
                    if (returnToMenu == null) DrawHistory();
                    DrawLogs();
                }
                GUI.enabled = true;
                if (showGrowth) GUI.ModalWindow(901, new Rect(150, 95, 1140, 730), DrawGrowthWindow, GUIContent.none, card);
            }
            finally { GUI.matrix = previous; GUI.backgroundColor = previousBackground; GUI.enabled = true; }
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
            title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 26, fontStyle = FontStyle.Bold };
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true };
            muted = new GUIStyle(text) { normal = { textColor = new Color(.68f, .77f, .8f) } };
            small = new GUIStyle(text) { fontSize = 13 };
            toggle = new GUIStyle(GUI.skin.toggle) { font = font, fontSize = 13 };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 15, wordWrap = true };
            card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10) };
            if (returnToMenu != null) card.normal.background = Texture2D.blackTexture;
            hexText = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            hexTexture = new Texture2D(64, 74, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[64 * 74];
            for (int y = 0; y < 74; y++)
                for (int x = 0; x < 64; x++)
                    pixels[y * 64 + x] = InsideHex(new Vector2((x + .5f - 32) / 32f * .8660254f, (y + .5f - 37) / 37f), 1) ? Color.white : Color.clear;
            hexTexture.SetPixels(pixels);
            hexTexture.Apply();
            stylesReady = true;
        }

        private void DrawPreparation()
        {
            GUI.Box(new Rect(230, 80, 980, 740), GUIContent.none, card);
            GUI.Label(new Rect(260, 108, 880, 40), "轮回准备", title);
            GUI.Label(new Rect(260, 156, 880, 32), "选择本世修士，再决定哪些前世从元年一同登场。", text);
            int cycleStart = ((round - 1) / roster.Count) * roster.Count + 1;
            for (int i = 0; i < roster.Count; i++)
            {
                var character = roster[i];
                bool used = savedLives.Any(entry => entry.Round >= cycleStart && entry.Record.BirthNodeId == character.Birth);
                var rect = new Rect(260 + i % 2 * 450, 200 + i / 2 * 42, 430, 36);
                string birth = locations.TryGetValue(character.Birth, out var location) ? location.Name : "外围 " + character.Birth;
                GUI.enabled = !used;
                if (GUI.Button(rect, $"{(used ? "已结算" : startingCultivator == character ? "已选" : TimelineText("Timeline_UI_63"))} · {character.Name} · {birth}（攻+{character.Attack}）", button))
                {
                    startingCultivator = character;
                    foreach (var entry in library.Where(entry => entry.Record.BirthNodeId == character.Birth))
                        entry.Selected = false;
                }
            }

            GUI.enabled = true;
            GUILayout.BeginArea(new Rect(260, 425, 900, 290));
            GUILayout.Label(library.Count == 0 ? "当前没有前世记录" : "勾选前世路线", text);
            historyScroll = GUILayout.BeginScrollView(historyScroll);
            foreach (var entry in library)
            {
                GUI.enabled = entry.Record.BirthNodeId != startingCultivator?.Birth;
                entry.Selected = GUILayout.Toggle(entry.Selected, entry.Name, toggle);
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (GUI.Button(new Rect(260, 746, 160, 42), TimelineText("Timeline_UI_61"), button) && SaveLives())
            {
                returnToMenu();
                Destroy(gameObject);
                return;
            }
            GUI.Label(new Rect(430, 750, 500, 40), saveIssue ?? "", muted);
            GUI.enabled = startingCultivator != null && saveReadable;
            if (GUI.Button(new Rect(950, 746, 220, 42), "开始轮回", button))
            {
                preparing = false;
                NewRound();
                if (board == null) InitializeBoard();
                if (!UsesPrefabHud) OpenMapHud?.Invoke(this);
            }
            GUI.enabled = true;
        }

        private void DrawHeader()
        {
            var world = simulation.State;
            GUI.Label(new Rect(24, 14, 850, 34), board == null ? "修仙轮回 · 世界时间线调试" : TimelineText("Timeline_UI_1"), title);
            GUI.Label(new Rect(25, 54, 1030, 28),
                $"第 {world.Year} 年（0=元年）   /   仪式截止 {world.Ritual.DeadlineYear} 年   /   已重置 {world.Ritual.ResetCount} 次   /   {StatusText()}", text);
            GUI.Label(new Rect(1080, 18, 200, 26), "六边形大地图 · 331个节点", muted);
            if (returnToMenu != null && GUI.Button(new Rect(1284, 14, 131, 30), TimelineText("Timeline_UI_61"), button) && SaveLives())
            {
                autoObserve = false;
                returnToMenu();
                Destroy(gameObject);
            }
            if (GUI.Button(new Rect(1080, 48, 335, 30), "查看等级经验 / 属性增量 / 资质", button)) showGrowth = true;
        }

        private Vector2 HexPosition(NodeState node) => new Vector2(1.7320508f * (node.Q + node.R * .5f), 1.5f * node.R) * hexSize;

        private bool showBoardDetails;
        private bool boardPointerDown;

        private void DrawBoard(float scale)
        {
            var viewport = new Rect(20, 138, showBoardDetails ? 850 : 1400, 588);
            float offsetX = (Screen.width - 1440 * scale) * .5f;
            float offsetY = (Screen.height - 900 * scale) * .5f;
            board.SetViewport(new Rect(0, 0, Screen.width, Screen.height));
            if (GUI.Button(new Rect(1240, 102, 180, 30), showBoardDetails ? "收起世界详情" : "角色 / 路线 / 日志", button))
                showBoardDetails = !showBoardDetails;
            GUI.Box(new Rect(20, 94, 850, 40), GUIContent.none, card);
            GUI.Label(new Rect(36, 103, 390, 26), "蓝棋：本世　金棋：无名客　紫棋：前世", muted);
            if (GUI.Button(new Rect(438, 102, 66, 28), "缩小", button)) board.Zoom(1.22f);
            if (GUI.Button(new Rect(510, 102, 66, 28), "放大", button)) board.Zoom(.82f);
            if (GUI.Button(new Rect(582, 102, 76, 28), TimelineText("Timeline_UI_12"), button)) board.Overview();
            if (GUI.Button(new Rect(664, 102, 86, 28), TimelineText("Timeline_UI_85"), button)) FocusBoardActor(1);
            if (GUI.Button(new Rect(756, 102, 96, 28), TimelineText("Timeline_UI_79"), button)) FocusBoardActor(1000);

            var e = Event.current;
            board.HoveredNode = null;
            if (showGrowth || !viewport.Contains(e.mousePosition)
                || (!showBoardDetails && new Rect(1040, 145, 380, 410).Contains(e.mousePosition)))
            {
                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    boardPointerDown = false;
                    mapDragging = false;
                }
                return;
            }

            var screen = new Vector2(offsetX + e.mousePosition.x * scale,
                Screen.height - offsetY - e.mousePosition.y * scale);
            int? node = board.Pick(screen);
            if (!mapDragging && node.HasValue)
            {
                board.HoveredNode = node;
                var world = simulation.State;
                var location = world.Nodes[node.Value];
                var player = world.Actors[world.PlayerActorId];
                bool reachable = player.NodeId.HasValue && world.Nodes[player.NodeId.Value].Links.Contains(location.Id);
                string type = locations.TryGetValue(location.Id, out var detail) ? detail.Name
                    : world.Ritual.NodeId == location.Id ? TimelineText("Timeline_UI_91")
                    : location.MonsterHealth > 0 ? "怪物" : location.Items.Count > 0 ? "宝藏" : "普通地点";
                string travel = reachable ? "移动 1 年" : "当前无法直接抵达";
                var occupants = world.Actors.Values.Where(actor => actor.IsAlive).OrderBy(actor => actor.Id)
                    .Where(actor =>
                    {
                        var point = ActorMapProjection.Position(world, actor);
                        return !point.IsMoving && point.Q == location.Q && point.R == location.R;
                    }).Select(actor => ActorName(actor.Id)).ToArray();
                float height = 60 + 22 * ((occupants.Length + 2) / 3);
                var tooltip = new Rect(Mathf.Min(e.mousePosition.x + 14, viewport.xMax - 246),
                    Mathf.Min(e.mousePosition.y + 18, viewport.yMax - height), 246, height);
                GUI.Box(tooltip, GUIContent.none, card);
                GUI.Label(new Rect(tooltip.x + 10, tooltip.y + 6, 226, 50),
                    $"{type} ({location.Q},{location.R})\n灵气 {location.CultivationPerYear}  ·  {travel}", text);
                for (int i = 0; i < occupants.Length; i += 3)
                    GUI.Label(new Rect(tooltip.x + 10, tooltip.y + 50 + i / 3 * 22, 226, 22),
                        string.Join("  ", occupants.Skip(i).Take(3)), small);
            }
            if (e.type == EventType.ScrollWheel)
            {
                board.Zoom(Mathf.Pow(1.1f, e.delta.y));
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0)
            {
                boardPointerDown = true;
                mouseDown = e.mousePosition;
                mapDragging = false;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && boardPointerDown)
            {
                if ((e.mousePosition - mouseDown).sqrMagnitude > 25) mapDragging = true;
                if (mapDragging) board.Pan(e.delta * scale);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (node.HasValue)
                {
                    selectedNode = node.Value;
                    board.Sync(simulation.State, selectedNode);
                }
                e.Use();
            }
        }

        private void FocusBoardActor(int actorId) => board.FocusActor(actorId);

        private void FocusNode(int id)
        {
            mapPan = -HexPosition(simulation.State.Nodes[id]);
        }

        private void DrawMap()
        {
            GUI.Box(new Rect(20, 94, 850, 468), GUIContent.none, card);
            GUI.Label(new Rect(36, 103, 390, 26), "六向相邻 · 越接近中心，灵气越浓", muted);
            if (GUI.Button(new Rect(438, 102, 66, 28), "缩小", button)) hexSize = Mathf.Max(8, hexSize / 1.3f);
            if (GUI.Button(new Rect(510, 102, 66, 28), "放大", button)) hexSize = Mathf.Min(52, hexSize * 1.3f);
            if (GUI.Button(new Rect(582, 102, 76, 28), TimelineText("Timeline_UI_12"), button)) { hexSize = 12; mapPan = Vector2.zero; }
            if (GUI.Button(new Rect(664, 102, 86, 28), TimelineText("Timeline_UI_85"), button))
            {
                var actor = simulation.State.Actors[1];
                hexSize = 28;
                FocusNode(actor.NodeId ?? actor.TravelOriginNodeId ?? HexTimelineDemoFactory.PlayerStart);
            }
            if (GUI.Button(new Rect(756, 102, 96, 28), TimelineText("Timeline_UI_79"), button))
            {
                var actor = simulation.State.Actors[1000];
                hexSize = 28;
                FocusNode(actor.NodeId ?? actor.TravelOriginNodeId ?? HexWorldMap.Id(-10, 0));
            }
            var viewport = new Rect(36, 138, 815, 412);
            var e = Event.current;
            Vector2 localMouse = e.mousePosition - viewport.position;
            if (!showGrowth && viewport.Contains(e.mousePosition))
            {
                if (e.type == EventType.ScrollWheel)
                {
                    float old = hexSize;
                    hexSize = Mathf.Clamp(hexSize * Mathf.Pow(1.12f, -e.delta.y), 8, 52);
                    Vector2 anchor = localMouse - viewport.size * .5f;
                    mapPan = anchor + (mapPan - anchor) * (hexSize / old);
                    e.Use();
                }
                if (e.type == EventType.MouseDown && e.button == 0) { mouseDown = localMouse; mapDragging = false; }
                if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    if ((localMouse - mouseDown).sqrMagnitude > 25) mapDragging = true;
                    if (mapDragging) mapPan += e.delta;
                    e.Use();
                }
                if (e.type == EventType.MouseUp && e.button == 0 && !mapDragging && GUIUtility.hotControl == 0)
                {
                    Vector2 point = localMouse - viewport.size * .5f - mapPan;
                    var hit = simulation.State.Nodes.Values.FirstOrDefault(n => InsideHex(point - HexPosition(n), hexSize));
                    if (hit != null) selectedNode = hit.Id;
                    e.Use();
                }
            }
            GUI.BeginGroup(viewport);
            Vector2 origin = viewport.size * .5f + mapPan;
            var route = simulation.Chosen.Route;
            for (int i = 1; i < route.Count; i++)
                DrawRoute(origin + HexPosition(simulation.State.Nodes[route[i - 1]]), origin + HexPosition(simulation.State.Nodes[route[i]]));
            var player = simulation.State.Actors[1];
            var neighbours = player.NodeId.HasValue ? simulation.State.Nodes[player.NodeId.Value].Links : null;
            var occupants = simulation.State.Actors.Values.Where(a => a.IsAlive && a.NodeId.HasValue).GroupBy(a => a.NodeId.Value).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var node in simulation.State.Nodes.Values)
            {
                Vector2 center = origin + HexPosition(node);
                if (center.x < -hexSize || center.x > viewport.width + hexSize || center.y < -hexSize || center.y > viewport.height + hexSize) continue;
                occupants.TryGetValue(node.Id, out var actors);
                bool hasPlayer = actors != null && actors.Any(a => a.Id == 1);
                bool hasChosen = actors != null && actors.Any(a => a.Id == 1000);
                Color fill = hasPlayer ? new Color(.12f, .46f, .78f) : hasChosen ? new Color(.75f, .53f, .13f) :
                    actors != null ? new Color(.48f, .25f, .6f) : node.Id == HexTimelineDemoFactory.FinalNode ? new Color(.6f, .15f, .18f) :
                    Color.Lerp(new Color(.09f, .17f, .2f), new Color(.15f, .43f, .34f), (node.CultivationPerYear - 1) / 10f);
                Color border = node.Id == selectedNode ? Color.white : neighbours != null && neighbours.Contains(node.Id) ? new Color(.3f, .85f, 1) : new Color(.25f, .35f, .38f);
                DrawHex(center, hexSize - .4f, border);
                DrawHex(center, hexSize - (node.Id == selectedNode ? 2.2f : 1.5f), fill);
                string marker = hasPlayer ? "我" : hasChosen ? "天" : actors != null ? "史" : node.Id == HexTimelineDemoFactory.FinalNode ? "终" : node.MonsterHealth > 0 ? "•" : node.Items.Count > 0 ? "+" : "";
                if (marker.Length > 0) GUI.Label(new Rect(center.x - hexSize, center.y - 11, hexSize * 2, 22), marker, hexText);
                else if (hexSize >= 27) GUI.Label(new Rect(center.x - hexSize, center.y - 11, hexSize * 2, 22), node.CultivationPerYear.ToString(), hexText);
            }
            DrawActorMarkers(viewport, origin);
            GUI.EndGroup();
        }

        private static bool InsideHex(Vector2 point, float size)
        {
            float x = Mathf.Abs(point.x) / (.8660254f * size), y = Mathf.Abs(point.y) / size;
            return x <= 1 && y <= 1 - .5f * x;
        }

        private void DrawActorMarkers(Rect viewport, Vector2 origin)
        {
            var occupied = new List<Rect>();
            foreach (var actor in simulation.State.Actors.Values.Where(a => a.IsAlive).OrderBy(a => a.Id))
            {
                var point = ActorMapProjection.Position(simulation.State, actor);
                Vector2 projected = origin + new Vector2(1.7320508f * ((float)point.Q + (float)point.R * .5f), 1.5f * (float)point.R) * hexSize;
                bool outside = projected.x < 25 || projected.x > viewport.width - 25 || projected.y < 12 || projected.y > viewport.height - 12;
                var rect = new Rect(Mathf.Clamp(projected.x - 25, 2, viewport.width - 52), Mathf.Clamp(projected.y - 10, 2, viewport.height - 22), 50, 20);
                // Distinct labels for co-located party members and actors on the same edge.
                if (occupied.Any(r => r.Overlaps(rect)))
                    for (int slot = 1; slot < 400; slot++)
                    {
                        int dx = (slot % 5 - 2) * 52, dy = (slot / 5 + 1) * 22;
                        var candidate = new Rect(Mathf.Clamp(projected.x - 25 + dx, 2, viewport.width - 52),
                            Mathf.Clamp(projected.y - 10 + (projected.y > viewport.height * .5f ? -dy : dy), 2, viewport.height - 22), 50, 20);
                        if (occupied.All(r => !r.Overlaps(candidate))) { rect = candidate; break; }
                    }
                occupied.Add(rect);
                Color old = GUI.color;
                GUI.color = actor.Id == 1 ? new Color(.1f, .45f, .8f) : actor.Id == 1000 ? new Color(.75f, .5f, .07f) : new Color(.48f, .22f, .64f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = old;
                string name = actor.Id == 1 ? TimelineText("Timeline_UI_85") : actor.Id == 1000 ? TimelineText("Timeline_UI_79") : "#" + actor.Id;
                if (GUI.Button(rect, name + (outside ? "外" : point.IsMoving ? "行" : ""), hexText))
                {
                    inspectedActorId = actor.Id;
                    if (board != null) board.Focus(point.Q, point.R);
                    mapPan = -(projected - origin);
                    showGrowth = true;
                }
            }
        }

        private string NextExperience(ActorState actor) => actor.Level >= cultivation.Levels.Count ? "满级" : cultivation.GetLevel(actor.Level).ExperienceToNext.ToString();

        private static string AttributeName(GrowthAttribute attribute) => attribute == GrowthAttribute.Health ? "生命" : TimelineText("Timeline_UI_40");

        private void DrawGrowthWindow(int windowId)
        {
            GUI.Label(new Rect(18, 12, 920, 30), "等级经验与属性增量 · 每个属性独立资质", title);
            if (GUI.Button(new Rect(1030, 12, 90, 30), TimelineText("Timeline_UI_14"), button)) showGrowth = false;
            var actor = simulation.State.Actors[inspectedActorId];
            GUI.Label(new Rect(18, 48, 1100, 24), $"{ActorName(actor.Id)} Lv{actor.Level}  生命{actor.Talents.Health.Grade} 攻击{actor.Talents.Attack.Grade}  当前经验 {actor.LevelExperience}/{NextExperience(actor)}", text);
            GUI.Label(new Rect(18, 78, 1100, 42), "第1级为出生属性，第2级起为升入该级的增量。每次升级独立取值，历史回放保持一致。\n单次增量允许少量重叠；本表保证同等级基础属性的高档累计下限高于低档累计上限。无名客吸收与物品加成另算。", small);
            GUI.Label(new Rect(18, 124, 1100, 26), "配置文件：Config/CultivationLevel.xlsx（导出后重新进入 Play）", muted);
            GUILayout.BeginArea(new Rect(18, 160, 1100, 550));
            growthScroll = GUILayout.BeginScrollView(growthScroll);
            GUILayout.BeginHorizontal();
            GUILayout.Label("等级 / 本级经验→下级", small, GUILayout.Width(200));
            GUILayout.Label("属性", small, GUILayout.Width(70));
            foreach (TalentGrade grade in Enum.GetValues(typeof(TalentGrade))) GUILayout.Label(grade.ToString(), text, GUILayout.Width(195));
            GUILayout.EndHorizontal();
            foreach (var level in cultivation.Levels)
            {
                GUILayout.Label($"Lv{level.Level}  ·  {(level.Level == 1 ? "出生值" : "升入本级时增加")}  ·  升下一级所需经验：{(level.ExperienceToNext == 0 ? "满级" : level.ExperienceToNext.ToString())}", muted);
                foreach (GrowthAttribute attribute in new[] { GrowthAttribute.Health, GrowthAttribute.Attack })
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("单次 / 累计基础范围", small, GUILayout.Width(200));
                    GUILayout.Label(AttributeName(attribute), text, GUILayout.Width(70));
                    foreach (TalentGrade grade in Enum.GetValues(typeof(TalentGrade)))
                        GUILayout.Label(level.Range(attribute, grade) + " / " + cultivation.CumulativeRange(level.Level, attribute, grade), small, GUILayout.Width(195));
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(12);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawHex(Vector2 center, float size, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - .8660254f * size, center.y - size, 1.7320508f * size, size * 2), hexTexture);
            GUI.color = old;
        }

        private static void DrawRoute(Vector2 start, Vector2 end)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = new Color(.95f, .7f, .2f);
            GUIUtility.RotateAroundPivot(Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - 1, Vector2.Distance(start, end), 2), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void DrawLocationDetails()
        {
            var world = simulation.State;
            var node = world.Nodes[selectedNode];
            bool configured = locations.TryGetValue(node.Id, out var row);
            bool final = world.Ritual.NodeId == node.Id;
            string name = configured ? row.Name : final ? TimelineText("Timeline_UI_88") : node.MonsterHealth > 0 ? TimelineText("Timeline_UI_89") : "荒野";
            string kind = final ? TimelineText("Timeline_UI_91") : node.MonsterHealth > 0 ? TimelineText("Timeline_UI_93")
                : configured && row.MonsterHealth > 0 ? "战斗关卡 · 已清除"
                : node.Items.Count > 0 ? TimelineText("Timeline_UI_96") : node.CultivationPerYear >= 8 ? "修炼地点" : TimelineText("Timeline_UI_98");
            var player = world.Actors[world.PlayerActorId];
            bool current = player.NodeId == node.Id;
            bool reachable = player.NodeId.HasValue && world.Nodes[player.NodeId.Value].Links.Contains(node.Id);

            GUI.Box(new Rect(1040, 145, 380, 410), GUIContent.none, card);
            GUI.Label(new Rect(1058, 160, 344, 36), name, title);
            GUI.Label(new Rect(1058, 203, 344, 28), $"{kind}  ·  ({node.Q},{node.R})", text);
            GUI.Label(new Rect(1058, 240, 344, 28), $"修炼收益   每年 {node.CultivationPerYear} 经验", text);
            GUI.Label(new Rect(1058, 276, 344, 52), node.MonsterHealth > 0
                ? $"守关敌人   生命 {node.MonsterHealth} / 攻击 {node.MonsterAttack}\n抵达后自动战斗"
                : "守关敌人   当前无守怪", text);
            string loot = node.Items.Count == 0 ? "当前无可领取宝物"
                : string.Join("、", node.Items.Select(item => configured && row.LootItemId == item
                    ? row.LootName + $"（攻击 +{node.LootAttackBonus}）" : "遗物 #" + item));
            GUI.Label(new Rect(1058, 340, 344, 62), "宝物   " + loot, text);

            string hint = final ? $"仪式截止第 {world.Ritual.DeadlineYear} 年。击败守关者会重启仪式，挑战者承受反噬。"
                : configured && row.MonsterHealth > 0 ? "击败守怪后，本局该地点保持清空。后来抵达的前世会按当前状态行动。"
                : node.Items.Count > 0 ? "宝物由本局最先到达并存活的角色获得。"
                : "停留修炼会消耗时间，其他角色也会同步行动。";
            GUI.Label(new Rect(1058, 410, 344, 76), hint, muted);
            GUI.Label(new Rect(1058, 500, 344, 36),
                current ? "本世当前所在地点" : reachable ? "相邻地点 · 移动耗时 1 年" : "需要经相邻地点逐步前往", text);
        }

        private void DrawActions()
        {
            var world = simulation.State;
            var player = world.Actors[1];
            var node = world.Nodes[selectedNode];
            GUI.Box(new Rect(20, 572, 850, board == null ? 308 : 160), GUIContent.none, card);
            GUI.Label(new Rect(36, 582, 815, 26), $"{(locations.TryGetValue(node.Id, out var details) ? details.Name : "节点")} ({node.Q},{node.R})  |  灵气 {node.CultivationPerYear}  |  怪物生命 {node.MonsterHealth} / 攻击 {node.MonsterAttack}  |  宝物 {string.Join(",", node.Items.Select(item => details != null && details.LootItemId == item ? details.LootName + $"（攻+{node.LootAttackBonus}）" : item.ToString()))}", text);
            GUI.Label(new Rect(36, 614, 815, 34), $"本世 Lv{player.Level}  生命 {player.Health}/{player.MaxHealth} [{player.Talents.Health.Grade}]  攻击 {player.Attack} [{player.Talents.Attack.Grade}]  经验 {player.LevelExperience}/{NextExperience(player)}", text);
            GUI.enabled = !showGrowth && simulation.WaitingForPlayer && player.NodeId.HasValue && world.Nodes[player.NodeId.Value].Links.Contains(selectedNode);
            if (GUI.Button(new Rect(36, 654, 150, 36), "移动至 " + selectedNode + " · 1年", button)) Submit(WorldAction.Move(selectedNode));
            GUI.enabled = !showGrowth && simulation.WaitingForPlayer;
            if (GUI.Button(new Rect(196, 654, 140, 36), "修炼 1 年", button)) Submit(WorldAction.Cultivate(1));
            if (GUI.Button(new Rect(346, 654, 140, 36), "修炼 5 年", button)) Submit(WorldAction.Cultivate(5));
            if (GUI.Button(new Rect(496, 654, 140, 36), "等待 1 年", button)) Submit(WorldAction.Wait());
            GUI.enabled = !showGrowth && simulation.WaitingForPlayer && !simulation.CanJoinChosen(1);
            if (GUI.Button(new Rect(646, 654, 205, 36), "提前伪装 · 1年", button)) Submit(WorldAction.Disguise());
            GUI.enabled = !showGrowth && simulation.WaitingForPlayer && simulation.CanJoinChosen(1);
            if (GUI.Button(new Rect(36, 702, 200, 36), "与无名客同行 → 观察", button)) Submit(WorldAction.JoinChosen());
            GUI.enabled = !showGrowth && !simulation.WaitingForPlayer && !world.IsEnded;
            if (GUI.Button(new Rect(246, 702, 130, 36), TimelineText("Timeline_UI_58"), button)) { autoObserve = false; StartAdvancing(simulation.AdvanceOneYearRoutine()); }
            if (GUI.Button(new Rect(386, 702, 140, 36), autoObserve ? TimelineText("Timeline_UI_22") : TimelineText("Timeline_UI_21"), button)) autoObserve = !autoObserve;
            GUI.enabled = !showGrowth;
            if (GUI.Button(new Rect(536, 702, 105, 36), yearsPerSecond + " 年/秒", button)) yearsPerSecond = yearsPerSecond == 5 ? 20 : yearsPerSecond == 20 ? 1 : 5;
            if (returnToMenu == null)
            {
                if (GUI.Button(new Rect(651, 702, 200, 36), "按勾选路线开始新局", button)) { round++; NewRound(); }
            }
            else if (world.IsEnded && GUI.Button(new Rect(651, 702, 200, 36), "结算 · 下一世", button) && SaveLives())
            {
                round++;
                startingCultivator = null;
                foreach (var entry in library) entry.Selected = false;
                preparing = true;
                showBoardDetails = false;
            }
            if (board != null) return;
            GUI.Label(new Rect(36, 750, 815, 48), saveIssue ?? message, muted);
            string saveHint = returnToMenu == null ? "调试场景的记录保留到本次 Play 结束。" : "结算后的前世路线自动保存。";
            GUI.Label(new Rect(36, 800, 815, 65), "保留三条破仪路线，无名客第100年抵达中心。滚轮缩放、拖动平移。\n改命验证：勾选改命 A，元年从 (0,-10) 进入 (0,-9) 清怪并离开，解除其第5年的死因。\n右上角可查看经验和增量表。" + saveHint, small);
        }

        private void DrawActors()
        {
            GUI.Box(new Rect(886, 94, 534, 340), GUIContent.none, card);
            GUI.Label(new Rect(900, 103, 506, 24), "全部存活角色 · 点击定位 / 查看资质", text);
            int row = 0;
            foreach (var actor in simulation.State.Actors.Values.Where(a => a.IsAlive).OrderBy(a => a.Id))
            {
                var point = ActorMapProjection.Position(simulation.State, actor);
                string position = $"({point.Q:0.#},{point.R:0.#})" + (point.IsMoving ? " 移动中" : "") + (actor.LeaderId.HasValue ? " 队友" : "");
                string label = $"{ActorName(actor.Id)} Lv{actor.Level} 生{actor.Talents.Health.Grade} 攻{actor.Talents.Attack.Grade}  HP{actor.Health} ATK{actor.Attack}  {position}";
                if (GUI.Button(new Rect(900, 133 + row * 24, 506, 23), label, small))
                {
                    inspectedActorId = actor.Id;
                    if (board != null)
                    {
                        board.FocusActor(actor.Id);
                        showBoardDetails = false;
                        return;
                    }
                    hexSize = 28;
                    mapPan = -new Vector2(1.7320508f * ((float)point.Q + (float)point.R * .5f), 1.5f * (float)point.R) * hexSize;
                    showGrowth = true;
                }
                row++;
            }
            if (row == 0) GUI.Label(new Rect(900, 140, 506, 24), "当前没有存活角色。", muted);
        }

        private void DrawHistory()
        {
            GUI.Box(new Rect(886, 444, 534, 160), GUIContent.none, card);
            GUILayout.BeginArea(new Rect(900, 454, 506, 140));
            GUILayout.Label("下一局历史路线 · 最多选择10条", text);
            historyScroll = GUILayout.BeginScrollView(historyScroll);
            foreach (var entry in library)
            {
                GUI.enabled = !showGrowth && (entry.Selected || library.Count(e => e.Selected) < 10);
                entry.Selected = GUILayout.Toggle(entry.Selected, entry.Name + "  · 出生点 " + entry.Record.BirthNodeId, toggle);
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawLogs()
        {
            GUI.Box(new Rect(886, 614, 534, 266), GUIContent.none, card);
            GUILayout.BeginArea(new Rect(900, 624, 506, 246));
            GUILayout.Label("世界日志 / " + StatusText(), text);
            logScroll = GUILayout.BeginScrollView(logScroll);
            foreach (var entry in simulation.Events.Skip(Math.Max(0, simulation.Events.Count - 180)))
                GUILayout.Label(FormatEvent(entry), small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string StatusText()
        {
            switch (simulation.State.Outcome)
            {
                case RunOutcome.Cleared: return TimelineText("Timeline_UI_130");
                case RunOutcome.WorldDestroyed: return TimelineText("Timeline_UI_131");
                case RunOutcome.PlayerKilledChosen: return TimelineText("Timeline_UI_132");
                default: return simulation.WaitingForPlayer ? TimelineText("Timeline_UI_55") : TimelineText("Timeline_UI_133");
            }
        }

        private static string HistoryStatus(HistoryReplayStatus status)
        {
            switch (status)
            {
                case HistoryReplayStatus.FateChanged: return TimelineText("Timeline_UI_134");
                case HistoryReplayStatus.Dead: return TimelineText("Timeline_UI_135");
                case HistoryReplayStatus.RecordFinished: return TimelineText("Timeline_UI_117");
                case HistoryReplayStatus.RouteBlocked: return TimelineText("Timeline_UI_136");
                default: return TimelineText("Timeline_UI_137");
            }
        }

        private static string ActorName(int id) => id == 1 ? TimelineText("Timeline_UI_138") : id == 1000 ? TimelineText("Timeline_UI_79") : TimelineText("Timeline_UI_139") + id;

        private static string EventDetail(WorldEvent entry)
        {
            string detail = entry.Detail ?? "";
            switch (detail)
            {
                case "Battle": return TimelineText("Timeline_UI_DetailBattle");
                case "Lifespan": return TimelineText("Timeline_UI_DetailLifespan");
                case "Ritual backlash": return TimelineText("Timeline_UI_DetailBacklash");
                case "World destroyed": return TimelineText("Timeline_UI_DetailDestroyed");
                case "Chosen defeated final boss": return TimelineText("Timeline_UI_DetailChosenVictory");
                case "Player killed chosen": return TimelineText("Timeline_UI_DetailPlayerKilledChosen");
                case "Story delay": return TimelineText("Timeline_UI_DetailStoryDelay");
            }
            if ((entry.Kind == WorldEventKind.ActionStarted || entry.Kind == WorldEventKind.ActionCompleted)
                && Enum.TryParse<WorldActionKind>(detail, out var action) && Enum.IsDefined(typeof(WorldActionKind), action))
                return TimelineText("Timeline_UI_Detail" + action);
            if (entry.Kind == WorldEventKind.Breakthrough && detail.StartsWith("Level ", StringComparison.Ordinal))
                return TimelineText("Timeline_UI_DetailLevel", detail.Substring("Level ".Length));
            if (entry.Kind == WorldEventKind.RitualReset && detail.StartsWith("Deadline year ", StringComparison.Ordinal))
                return TimelineText("Timeline_UI_DetailDeadline", detail.Substring("Deadline year ".Length));
            if (entry.Kind == WorldEventKind.Absorbed && detail.StartsWith("Health +", StringComparison.Ordinal))
            {
                const string separator = ", attack +";
                int index = detail.IndexOf(separator, StringComparison.Ordinal);
                if (index >= 0)
                    return TimelineText("Timeline_UI_DetailAbsorbed",
                        detail.Substring("Health +".Length, index - "Health +".Length), detail.Substring(index + separator.Length));
            }
            return detail;
        }

        private static string FormatEvent(WorldEvent entry)
        {
            string action;
            string detail = EventDetail(entry);
            switch (entry.Kind)
            {
                case WorldEventKind.Breakthrough: action = TimelineText("Timeline_UI_140") + detail; break;
                case WorldEventKind.StoryResolved: action = TimelineText("Timeline_UI_141") + detail; break;
                case WorldEventKind.Arrived: action = TimelineText("Timeline_UI_EventArrived", entry.NodeId); break;
                case WorldEventKind.Battle: action = entry.OtherActorId.HasValue
                    ? TimelineText("Timeline_UI_EventDuel", ActorName(entry.OtherActorId.Value))
                    : TimelineText("Timeline_UI_EventBattle", entry.NodeId); break;
                case WorldEventKind.Died: action = TimelineText("Timeline_UI_142") + detail; break;
                case WorldEventKind.FateChanged: action = entry.OtherActorId is int clearer
                    ? TimelineText("Timeline_Linxi_FateRescued", ActorName(clearer))
                    : TimelineText("Timeline_UI_134"); break;
                case WorldEventKind.Joined: action = TimelineText("Timeline_UI_143"); break;
                case WorldEventKind.RitualReset: action = TimelineText("Timeline_UI_144") + detail; break;
                case WorldEventKind.RitualCompleted: action = TimelineText("Timeline_UI_145"); break;
                case WorldEventKind.WorldCleared: action = TimelineText("Timeline_UI_146"); break;
                case WorldEventKind.Absorbed: action = TimelineText("Timeline_UI_147") + detail; break;
                case WorldEventKind.Looted: action = TimelineText("Timeline_UI_EventLoot", entry.NodeId); break;
                case WorldEventKind.Cultivated: action = TimelineText("Timeline_UI_148"); break;
                case WorldEventKind.Disguised: action = TimelineText("Timeline_UI_149"); break;
                case WorldEventKind.RunEnded: action = TimelineText("Timeline_UI_150") + detail; break;
                case WorldEventKind.ActionCompleted: action = TimelineText("Timeline_UI_151") + detail; break;
                default: action = TimelineText("Timeline_UI_152") + detail; break;
            }
            return TimelineText("Timeline_UI_EventLine", entry.Year, ActorName(entry.ActorId), action);
        }

        private void OnDestroy()
        {
            Closed?.Invoke();
            if (font != null) Destroy(font);
            if (hexTexture != null) Destroy(hexTexture);
        }
    }
}
