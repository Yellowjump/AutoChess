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
        private string SavePath => Path.Combine(Application.persistentDataPath, "WorldTimeline", "cultivator-lives.dat");
        public sealed class StartingCultivator
        {
            public string Name, Weapon;
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
        private string message = "选择相邻节点移动，或留在原地修炼。";
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
                    saveIssue = "前世存档读取失败：" + error.Message;
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
                message = "未找到可用的场景地形，已打开调试地图。";
            }
        }

        private void LoadScenario()
        {
            roster.AddRange(scenarioData.Cultivators);
            foreach (var location in scenarioData.Locations)
                locations.Add(HexWorldMap.Id(location.Q, location.R), location);
        }

        private void ConfigureLocations(WorldState world)
        {
            foreach (var pair in locations)
            {
                var node = world.Nodes[pair.Key];
                var row = pair.Value;
                node.CultivationPerYear = row.CultivationPerYear;
                node.MonsterHealth = row.MonsterHealth;
                node.MonsterAttack = row.MonsterAttack;
                node.Items.Clear();
                int loot = row.LootItemId;
                if (loot != 0) node.Items.Add(loot);
                node.LootAttackBonus = row.LootAttackBonus;
            }
            foreach (var cultivator in roster) world.Nodes[cultivator.Birth].MonsterHealth = 0;
        }

        private void AddLifeToLibrary(SavedLife entry)
        {
            var character = roster.FirstOrDefault(candidate => candidate.Birth == entry.Record.BirthNodeId);
            library.Add(new HistoryEntry
            {
                Name = (character?.Name ?? "轮回 " + entry.Round) + " · 第" + entry.Record.EndYear + "年"
                    + (entry.Record.EndedInDeath ? "身死" : "结束"),
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
                saveIssue = "前世存档写入失败，可点返回主菜单重试：" + error.Message;
                return false;
            }
        }

        private void NewRound(IEnumerable<LifeRecord> historyOverride = null)
        {
            battleResolver = returnToMenu == null ? null : BattleResolverFactory?.Invoke();
            simulation = HexTimelineDemoFactory.Create(playerNode: startingCultivator?.Birth,
                cultivation: cultivation, talentSeed: round, startingItem: startingCultivator?.Item ?? 0,
                weaponAttack: startingCultivator?.Attack ?? 0,
                configureLocations: startingCultivator == null ? (Action<WorldState>)null : ConfigureLocations,
                battle: battleResolver);
            inspectedActorId = 1;
            int id = 10;
            foreach (var record in historyOverride ?? library.Where(e => e.Selected).Take(10).Select(e => e.Record))
                simulation.AddHistory(record, id++);
            archived = false;
            autoObserve = false;
            selectedNode = simulation.State.Actors[1].NodeId.Value;
            hexSize = 12f;
            mapPan = Vector2.zero;
            logScroll = Vector2.zero;
            message = "本局已载入勾选路线。天选第100年抵达中心；初始仪式截止第35年。";
            showGrowth = false;
            if (board != null)
            {
                board.ClearPieces();
                board.Overview();
                board.Sync(simulation.State, selectedNode);
            }
            BeginCheckpoint();
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
                    else message = "前世存档已满（15条）；本局仍可观察。";
                }
                if (simulation.State.IsEnded) autoObserve = false;
            }
            catch (InvalidOperationException error)
            {
                autoObserve = false;
                message = "无法执行：" + error.Message;
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

        private IEnumerator AdvanceRoutine(IEnumerator routine, bool observeAfter, WorldAction action)
        {
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
                        message = "推进失败：" + failure.Message;
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
                Execute(() => { });
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
            // Only the opening character/route selection still uses the legacy debug GUI.
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
                    GUI.Label(new Rect(35, 20, 1000, 45), "战斗进行中 · 世界时间暂停", title);
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
                if (GUI.Button(rect, $"{(used ? "已结算" : startingCultivator == character ? "已选" : "选择")} · {character.Name} · {birth} · {character.Weapon}（攻+{character.Attack}）", button))
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

            if (GUI.Button(new Rect(260, 746, 160, 42), "返回主菜单", button) && SaveLives())
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
            GUI.Label(new Rect(24, 14, 850, 34), board == null ? "修仙轮回 · 世界时间线调试" : "修仙轮回", title);
            GUI.Label(new Rect(25, 54, 1030, 28),
                $"第 {world.Year} 年（0=元年）   /   仪式截止 {world.Ritual.DeadlineYear} 年   /   已重置 {world.Ritual.ResetCount} 次   /   {StatusText()}", text);
            GUI.Label(new Rect(1080, 18, 200, 26), "六边形大地图 · 331个节点", muted);
            if (returnToMenu != null && GUI.Button(new Rect(1284, 14, 131, 30), "返回主菜单", button) && SaveLives())
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
            GUI.Label(new Rect(36, 103, 390, 26), "蓝棋：本世　金棋：天选　紫棋：前世", muted);
            if (GUI.Button(new Rect(438, 102, 66, 28), "缩小", button)) board.Zoom(1.22f);
            if (GUI.Button(new Rect(510, 102, 66, 28), "放大", button)) board.Zoom(.82f);
            if (GUI.Button(new Rect(582, 102, 76, 28), "全图", button)) board.Overview();
            if (GUI.Button(new Rect(664, 102, 86, 28), "本世", button)) FocusBoardActor(1);
            if (GUI.Button(new Rect(756, 102, 96, 28), "天选", button)) FocusBoardActor(1000);

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
                    : world.Ritual.NodeId == location.Id ? "最终关卡"
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
            if (GUI.Button(new Rect(582, 102, 76, 28), "全图", button)) { hexSize = 12; mapPan = Vector2.zero; }
            if (GUI.Button(new Rect(664, 102, 86, 28), "本世", button))
            {
                var actor = simulation.State.Actors[1];
                hexSize = 28;
                FocusNode(actor.NodeId ?? actor.TravelOriginNodeId ?? HexTimelineDemoFactory.PlayerStart);
            }
            if (GUI.Button(new Rect(756, 102, 96, 28), "天选", button))
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
                string name = actor.Id == 1 ? "本世" : actor.Id == 1000 ? "天选" : "#" + actor.Id;
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

        private static string AttributeName(GrowthAttribute attribute) => attribute == GrowthAttribute.Health ? "生命" : attribute == GrowthAttribute.Attack ? "攻击" : "寿元";

        private void DrawGrowthWindow(int windowId)
        {
            GUI.Label(new Rect(18, 12, 920, 30), "等级经验与属性增量 · 每个属性独立资质", title);
            if (GUI.Button(new Rect(1030, 12, 90, 30), "关闭", button)) showGrowth = false;
            var actor = simulation.State.Actors[inspectedActorId];
            GUI.Label(new Rect(18, 48, 1100, 24), $"{ActorName(actor.Id)} Lv{actor.Level}  生命{actor.Talents.Health.Grade} 攻击{actor.Talents.Attack.Grade} 寿元{actor.Talents.Lifespan.Grade}  当前经验 {actor.LevelExperience}/{NextExperience(actor)}", text);
            GUI.Label(new Rect(18, 78, 1100, 42), "第1级为出生属性，第2级起为升入该级的增量。每次升级独立取值，历史回放保持一致。\n单次增量允许少量重叠；本表保证同等级基础属性的高档累计下限高于低档累计上限。天选吸收与物品加成另算。", small);
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
                foreach (GrowthAttribute attribute in Enum.GetValues(typeof(GrowthAttribute)))
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
            string name = configured ? row.Name : final ? "最终祭坛" : node.MonsterHealth > 0 ? "妖兽驻地" : "荒野";
            string kind = final ? "最终关卡" : node.MonsterHealth > 0 ? "战斗关卡"
                : configured && row.MonsterHealth > 0 ? "战斗关卡 · 已清除"
                : node.Items.Count > 0 ? "遗宝关卡" : node.CultivationPerYear >= 8 ? "修炼地点" : "探索地点";
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
            GUI.Label(new Rect(36, 614, 815, 34), $"本世 Lv{player.Level}  生命 {player.Health}/{player.MaxHealth} [{player.Talents.Health.Grade}]  攻击 {player.Attack} [{player.Talents.Attack.Grade}]  寿元 {player.Age}/{player.Lifespan} [{player.Talents.Lifespan.Grade}]  经验 {player.LevelExperience}/{NextExperience(player)}", text);
            GUI.enabled = !showGrowth && simulation.WaitingForPlayer && player.NodeId.HasValue && world.Nodes[player.NodeId.Value].Links.Contains(selectedNode);
            if (GUI.Button(new Rect(36, 654, 150, 36), "移动至 " + selectedNode + " · 1年", button)) Submit(WorldAction.Move(selectedNode));
            GUI.enabled = !showGrowth && simulation.WaitingForPlayer;
            if (GUI.Button(new Rect(196, 654, 140, 36), "修炼 1 年", button)) Submit(WorldAction.Cultivate(1));
            if (GUI.Button(new Rect(346, 654, 140, 36), "修炼 5 年", button)) Submit(WorldAction.Cultivate(5));
            if (GUI.Button(new Rect(496, 654, 140, 36), "等待 1 年", button)) Submit(WorldAction.Wait());
            GUI.enabled = !showGrowth && simulation.WaitingForPlayer && !simulation.CanJoinChosen(1);
            if (GUI.Button(new Rect(646, 654, 205, 36), "提前伪装 · 1年", button)) Submit(WorldAction.Disguise());
            GUI.enabled = !showGrowth && simulation.WaitingForPlayer && simulation.CanJoinChosen(1);
            if (GUI.Button(new Rect(36, 702, 200, 36), "加入天选 → 观察", button)) Submit(WorldAction.JoinChosen());
            GUI.enabled = !showGrowth && !simulation.WaitingForPlayer && !world.IsEnded;
            if (GUI.Button(new Rect(246, 702, 130, 36), "观察下一年", button)) { autoObserve = false; StartAdvancing(simulation.AdvanceOneYearRoutine()); }
            if (GUI.Button(new Rect(386, 702, 140, 36), autoObserve ? "暂停观察" : "加速观察", button)) autoObserve = !autoObserve;
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
            GUI.Label(new Rect(36, 800, 815, 65), "保留三条破仪路线，天选第100年抵达中心。滚轮缩放、拖动平移。\n改命验证：勾选改命 A，元年从 (0,-10) 进入 (0,-9) 清怪并离开，解除其第5年的死因。\n右上角可查看经验和增量表。" + saveHint, small);
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
                string label = $"{ActorName(actor.Id)} Lv{actor.Level} 生{actor.Talents.Health.Grade} 攻{actor.Talents.Attack.Grade} 寿{actor.Talents.Lifespan.Grade}  HP{actor.Health} ATK{actor.Attack}  {position}";
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
                case RunOutcome.Cleared: return "通关：天选击败最终 Boss";
                case RunOutcome.WorldDestroyed: return "失败：仪式完成，世界毁灭";
                case RunOutcome.PlayerKilledChosen: return "结束：本世角色杀死天选";
                default: return simulation.WaitingForPlayer ? "等待选择 · 世界暂停" : "观察中";
            }
        }

        private static string HistoryStatus(HistoryReplayStatus status)
        {
            switch (status)
            {
                case HistoryReplayStatus.FateChanged: return "已改命";
                case HistoryReplayStatus.Dead: return "死亡";
                case HistoryReplayStatus.RecordFinished: return "记录结束";
                case HistoryReplayStatus.RouteBlocked: return "道路阻断";
                default: return "历史回放";
            }
        }

        private static string ActorName(int id) => id == 1 ? "本世#1" : id == 1000 ? "天选" : "历史#" + id;

        private static string FormatEvent(WorldEvent entry)
        {
            string action;
            switch (entry.Kind)
            {
                case WorldEventKind.Breakthrough: action = "突破升级 · " + entry.Detail; break;
                case WorldEventKind.Arrived: action = "抵达节点 " + entry.NodeId; break;
                case WorldEventKind.Battle: action = entry.OtherActorId.HasValue ? "与 " + ActorName(entry.OtherActorId.Value) + " 战斗" : "挑战节点 " + entry.NodeId + " 的敌人"; break;
                case WorldEventKind.Died: action = "死亡 · " + entry.Detail; break;
                case WorldEventKind.Joined: action = "加入天选队伍"; break;
                case WorldEventKind.RitualReset: action = "击败最终 Boss，仪式重置 · " + entry.Detail; break;
                case WorldEventKind.RitualCompleted: action = "灭世仪式完成"; break;
                case WorldEventKind.WorldCleared: action = "终结最终 Boss，世界通关"; break;
                case WorldEventKind.Absorbed: action = "吸收敌人属性 · " + entry.Detail; break;
                case WorldEventKind.Looted: action = "取得节点 " + entry.NodeId + " 的宝物"; break;
                case WorldEventKind.Cultivated: action = "修炼积累经验"; break;
                case WorldEventKind.Disguised: action = "伪装关卡敌人"; break;
                case WorldEventKind.RunEnded: action = "本局结算 · " + entry.Detail; break;
                case WorldEventKind.ActionCompleted: action = "行动完成 · " + entry.Detail; break;
                default: action = "开始行动 · " + entry.Detail; break;
            }
            return "[" + entry.Year + "年] " + ActorName(entry.ActorId) + " " + action;
        }

        private void OnDestroy()
        {
            Closed?.Invoke();
            if (font != null) Destroy(font);
            if (hexTexture != null) Destroy(hexTexture);
        }
    }
}
