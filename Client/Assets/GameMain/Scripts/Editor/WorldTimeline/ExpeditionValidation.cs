using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;
using Object = UnityEngine.Object;

// Exercises the actual main-flow UI, combat, checkpoint replay and old-save opt-in in an isolated save folder.
public static class ExpeditionValidation
{
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static WorldTimelineDebugView session;
    private static MainTitlePanelCtrl title;
    private static string output, previousSaveDirectory;
    private static double deadline;
    private static int battles;
    private static int purchases,paidUpgrades,freeUpgrades;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/World Timeline/Validate Artifact Archive Loading")]
    public static void ValidateColdArtifactArchive()
    {
        if(!Application.isPlaying)return;
        string result=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Tests/WorldTimeline/obj/Expedition/ArtifactArchive.txt"));
        try
        {
            var state=new WorldState(1);state.Nodes.Add(1,new NodeState(1));state.Actors.Add(1,new ActorState(1,1,100,20));state.EnableExpedition();
            WorldTimelineTableConfig.Create();var simulation=new WorldSimulation(state);state.Expedition.Equipment.EnableArtifacts();
            state.Expedition.Equipment.Upgrade(-1,true);
            byte[] data=WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,simulation.RecordPlayerLife())});
            // Simulate opening the application before any run has populated the definition cache.
            typeof(ExpeditionEquipment).GetField("definitions",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
            ExpeditionEquipment.DefinitionTableLoader=null;WorldTimelineTableConfig.Create();
            var record=WorldTimelineSaveStore.Decode(data).Single().Record;
            Require(record.Expedition.Last().Equipment.SequenceEqual(state.Expedition.Equipment.Snapshot()),"A cold application loads upgraded historical artifacts before starting a new run");
            File.WriteAllText(result,"PASS: artifact table initializes before archive decoding on a cold application.");
            Debug.Log("ARTIFACT ARCHIVE VALIDATION PASSED");
        }
        catch(Exception error){File.WriteAllText(result,error.ToString());Debug.LogException(error);}
    }

    [MenuItem("Tools/World Timeline/Validate Map Expedition %#&F10")]
    public static void Run()
    {
        title = Object.FindFirstObjectByType<MainTitlePanelCtrl>();
        if (!Application.isPlaying || routines.Count != 0 || title is not { Visible: true })
        { Debug.LogWarning("请运行 bootstrap 并留在主菜单，再验证地图队伍。"); return; }
        output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Tests/WorldTimeline/obj/Expedition"));
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "Result.txt"), "RUNNING");
        previousSaveDirectory = WorldTimelineDebugView.EditorSaveDirectory;
        WorldTimelineDebugView.EditorSaveDirectory = Path.Combine(output, "Saves-" + Guid.NewGuid().ToString("N"));
        deadline = EditorApplication.timeSinceStartup + 480;
        battles = 0;
        purchases=paidUpgrades=freeUpgrades=0;
        routines.Push(Validate());
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Expedition validation stopped or timed out.");
            for (int budget = 0; budget < 64 && routines.Count > 0; budget++)
            {
                var routine = routines.Peek();
                if (!routine.MoveNext()) { (routines.Pop() as IDisposable)?.Dispose(); continue; }
                if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                break;
            }
            if (routines.Count == 0) Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static IEnumerator Validate()
    {
        WorldTimelineDebugView.OpenMapHud = view => GameEntry.UI.OpenUIForm(UICtrlName.WorldTimelinePanel, "top", userData: view);
        WorldTimelineDebugView.OpenStoryHud = view => GameEntry.UI.OpenUIForm(UICtrlName.WorldTimelineStory, "tips", userData: view);
        WorldTimelineDebugView.ScenarioFactory = WorldTimelineTableConfig.Create;
        WorldTimelineDebugView.BattleResolverFactory = () => new WorldTimelineBattleResolver();
        title.Visible = false;
        session = WorldTimelineDebugView.OpenFromMenu(() => { });
        Require(session.SelectPreparationCharacter(0) && session.BeginPreparedRun(), "Start main campaign");
        while (!session.UsesPrefabHud || !session.UsesStoryHud) yield return null;
        yield return Settle();
        Capture("Map.png");
        yield return Settle();
        var initialHud = Object.FindFirstObjectByType<WorldTimelinePanelCtrl>();
        Require(!initialHud.GetComponentsInChildren<Button>().Any(button => button.GetComponentsInChildren<TMPro.TMP_Text>()
            .Any(label => label.text.Contains("修炼"))), "No cultivation action in the main map");
        Require(session.Expedition.ProgressionEnabled, "New campaign enables map growth");

        // Recreate an actual old action log: cultivation must replay before the new rules opt in.
        typeof(ExpeditionParty).GetProperty("ProgressionEnabled").SetValue(session.Expedition, false);
        typeof(WorldTimelineDebugView).GetMethod("Submit", Hidden).Invoke(session, new object[] { WorldAction.Cultivate(1) });
        while (session.IsBusy) yield return null;
        long legacyExperience = session.Simulation.State.Actors[session.Simulation.State.PlayerActorId].Experience;
        session.ReturnToMenu();
        yield return null;
        string path = Path.Combine(WorldTimelineDebugView.EditorSaveDirectory, "current-run.json");
        var checkpointType = typeof(WorldTimelineDebugView).GetNestedType("Checkpoint", BindingFlags.NonPublic);
        object checkpoint = JsonUtility.FromJson(File.ReadAllText(path), checkpointType);
        var actions = (IList)checkpointType.GetField("actions").GetValue(checkpoint);
        for (int i = actions.Count - 1; i >= 0; i--)
            if ((int)actions[i].GetType().GetField("kind").GetValue(actions[i]) is -7 or -8 or -9) actions.RemoveAt(i);
        checkpointType.GetField("version").SetValue(checkpoint, 43);
        File.WriteAllText(path, JsonUtility.ToJson(checkpoint));
        session = WorldTimelineDebugView.OpenFromMenu(() => { });
        Require(session.ResumeUnfinishedRun() && session.Expedition?.ProgressionEnabled == true &&
            session.Simulation.State.Actors[session.Simulation.State.PlayerActorId].Experience == legacyExperience,
            "Old cultivation checkpoint migrates without changing prior growth");
        while (!session.UsesPrefabHud || !session.UsesStoryHud) yield return null;
        yield return Settle();

        var hud = Object.FindFirstObjectByType<WorldTimelinePanelCtrl>();
        typeof(WorldTimelinePanelCtrl).GetMethod("OpenPopup", Hidden).Invoke(hud, new object[] { 3 });
        yield return Settle(); Capture("Growth.png"); yield return Settle();
        typeof(WorldTimelinePanelCtrl).GetMethod("ClosePopup", Hidden).Invoke(hud, null);
        yield return Settle();
        var world = session.Simulation.State;
        Require(session.Expedition.CompanionStagesEnabled && session.Expedition.RecruitmentSites.Count == 4,
            "Four companion stages appear in the actual campaign");
        var locator = hud.GetComponentsInChildren<Button>().Single(button => button.name == "同伴关卡");
        locator.onClick.Invoke();
        yield return Settle();
        var recruitButton = hud.GetComponentsInChildren<Button>().Single(button => button.name == "关卡招募");
        Require(!session.Expedition.HasRecruited(0) && !recruitButton.interactable &&
            session.LocationKind(session.SelectedLocation) == "同伴关卡", "Locator opens a locked map stage without granting a teammate");
        Capture("LockedStage.png"); yield return Settle();
        int shieldNode = session.Expedition.RecruitmentSites[0];
        yield return Travel(shieldNode);
        Require(session.Expedition.RecruitmentConditionMet(0), "First stage route provides the required two explorations");
        yield return RecruitAtStage(hud, 0);
        Require(session.Expedition.DeployedCount == 2, "First stage reward deploys the shield");
        hud.OpenEquipment();
        yield return Settle();
        var equipment=Object.FindFirstObjectByType<ExpeditionEquipmentPanel>();
        Require(equipment!=null && session.InventoryOpen,"Map inventory opens the multi-character equipment UI");
        void EquipClick(string name)=>equipment.transform.Find(name).GetComponent<Button>().onClick.Invoke();
        EquipClick("EquipMember1");yield return Settle();
        EquipClick("BagItem3");yield return Settle();
        EquipClick("EquipOrUse");yield return Settle();
        int shieldId=session.Expedition.CompanionId(0);
        Require(session.Expedition.Equipment.Equipped(shieldId,0)==8 && session.Expedition.Equipment.Innate(shieldId).Id==3,"Any role equips a universal item beside its fixed artifact");
        EquipClick("EquipOrUse");yield return Settle();
        Require(session.Expedition.Equipment.Equipped(shieldId,0)==0 && session.Expedition.Equipment.Count(8)==1,"Unequip returns the same item instance");
        EquipClick("BagItem3");yield return Settle();EquipClick("EquipOrUse");yield return Settle();
        EquipClick("EquippedSlot1");yield return Settle();EquipClick("BagItem1");yield return Settle();EquipClick("EquipOrUse");yield return Settle();
        Require(session.Expedition.Equipment.Equipped(shieldId,1)==5 && session.Expedition.Equipment.InnateLevel(shieldId)>=1,"Ordinary slots mix without weapon or profession restrictions");
        EquipClick("InnateArtifact");yield return Settle();
        Require(!equipment.transform.Find("EquipOrUse").GetComponent<Button>().interactable,"Fixed artifact cannot be removed or replaced in the UI");
        Require(equipment.transform.Find("LegacyInventory")==null,"Retired inventory has no player-facing entry");
        var innate=session.Expedition.Equipment.Innate(shieldId);int innateLevel=session.Expedition.Equipment.InnateLevel(shieldId);
        Require(equipment.transform.Find("SelectedItemNextLevel").GetComponent<TMPro.TMP_Text>().text.Contains(innate.Effect(innateLevel+1)) &&
            equipment.transform.Find("SelectedItemUpgradeCost").GetComponent<TMPro.TMP_Text>().text.Contains(innate.UpgradePrice(innateLevel).ToString()),
            "Backpack directly previews the innate artifact's next stats and upgrade cost");
        Require(Enumerable.Range(0,6).All(s=>equipment.transform.Find("EquippedSlot"+s)!=null),"All six ordinary equipment slots are visible");
        EquipClick("EquippedSlot5");yield return Settle();EquipClick("BagItem2");yield return Settle();EquipClick("EquipOrUse");yield return Settle();
        Require(session.Expedition.Equipment.Worn(shieldId,5)?.DefinitionId==7,"Sixth slot equips through actual backpack UI");
        EquipClick("InnateArtifact");yield return Settle();
        Capture("TeamEquipment.png");yield return Settle();EquipClick("Close");yield return Settle();
        Require(!session.InventoryOpen && hud.CanInteractWithMap,"Closing backpack restores map interaction");
        Require(session.PendingVanguard.HasValue, "Recruitment points to the first tactical encounter");
        int vanguard = session.PendingVanguard.Value;
        locator.onClick.Invoke();
        Require(session.SelectedNodeId == vanguard, "Map guidance locates the shield's first encounter");
        yield return Travel(vanguard);
        Require(session.Expedition.RecruitmentConditionMet(2), "Real two-person victory satisfies archer condition");
        int recruitNode = session.Expedition.RecruitmentSites[2];
        yield return Travel(recruitNode);
        yield return RecruitAtStage(hud, 2);
        Require(session.Expedition.DeployedCount == 3, "Recruit archer at its stage after victory");
        Require(world.Actors[world.PlayerActorId].Experience > legacyExperience && session.Expedition.HasExplored(recruitNode),
            "Map advancement grants exploration experience");

        yield return FightNearestGuard();
        Require(battles > 0 && world.Actors[world.PlayerActorId].IsAlive, "Fight and clear a main-flow encounter");
        Require(world.Actors[world.PlayerActorId].LastBattleOutput.Count(row => row.Allied) == 3, "All three team members participated");
        var storyNodes=session.Simulation.Stories.Config.Events.Values;
        var nextEvent=storyNodes.Where(s=>!s.IsMerchant&&!session.Expedition.Equipment.HasVisitedEvent(s.NodeId))
            .OrderBy(s=>WorldPathfinder.FindRoute(world,world.Actors[world.PlayerActorId].NodeId.Value,new[]{s.NodeId}).Count).First();
        yield return Travel(nextEvent.NodeId);
        var nextShop=storyNodes.Where(s=>s.IsMerchant).OrderBy(s=>WorldPathfinder.FindRoute(world,
            world.Actors[world.PlayerActorId].NodeId.Value,new[]{s.NodeId}).Count).First();
        yield return Travel(nextShop.NodeId);
        Require(purchases>0 && paidUpgrades>0 && freeUpgrades>0,"Map merchant purchase, paid upgrade and event free upgrade all complete through actual UI");
        int[] formation = session.Expedition.Cells.ToArray();
        var sites = session.Expedition.RecruitmentSites.ToArray();
        int year = world.Year, hp = world.Actors[world.PlayerActorId].Health;
        long experience = world.Actors[world.PlayerActorId].Experience;
        int[] equipmentState=session.Expedition.Equipment.Snapshot();
        session.ReturnToMenu();
        yield return null;
        checkpoint=JsonUtility.FromJson(File.ReadAllText(path),checkpointType);
        actions=(IList)checkpointType.GetField("actions").GetValue(checkpoint);
        bool legacyItemCommand=false;
        foreach(object step in actions)
        {
            var type=step.GetType();int kind=(int)type.GetField("kind").GetValue(step),packed=(int)type.GetField("years").GetValue(step);
            if(kind!=-20||packed%6>=3)continue;
            type.GetField("kind").SetValue(step,-16);type.GetField("years").SetValue(step,packed/6*3+packed%6);
            legacyItemCommand=true;break;
        }
        Require(legacyItemCommand,"Checkpoint contains an old three-slot command alongside new six-slot commands");
        File.WriteAllText(path,JsonUtility.ToJson(checkpoint));
        session = WorldTimelineDebugView.OpenFromMenu(() => { });
        Require(session.ResumeUnfinishedRun(), "Replay checkpoint after real team battle");
        while (!session.UsesPrefabHud) yield return null;
        Require(session.Expedition.Cells.SequenceEqual(formation) && session.Expedition.HasRecruited(0) &&
            session.Expedition.HasRecruited(2) && session.Simulation.State.Year == year &&
            session.Simulation.State.Actors[session.Simulation.State.PlayerActorId].Health == hp &&
            session.Simulation.State.Actors[session.Simulation.State.PlayerActorId].Experience == experience &&
            session.Expedition.HasExplored(recruitNode) && session.Expedition.CompanionStagesEnabled &&
            session.Expedition.VanguardNodeId == vanguard && session.Simulation.State.Nodes[vanguard].MonsterHealth == 0 &&
            session.Expedition.RecruitmentSites.SequenceEqual(sites) && session.Expedition.RecruitmentConditionMet(2),
            "Recruitment, battle edits, progress and HP survive reload");
        Require(session.Expedition.Equipment.Snapshot().SequenceEqual(equipmentState),"Fixed artifacts, independent item levels, purchases and rewards survive checkpoint replay");
        session.ReturnToMenu();
        yield return null;
        session = null;
        ValidateHistoricalCombat();
    }

    private static void ValidateHistoricalCombat()
    {
        var world = new WorldState(1);
        world.Nodes.Add(1, new NodeState(1));
        world.Actors.Add(1, new ActorState(1, 1, 100, 20));
        world.EnableExpedition();
        var simulation = new WorldSimulation(world);
        Require(world.Expedition.Recruit(0), "Historical fixture recruits its shield");
        world.Expedition.Place(world.Expedition.CompanionId(0), 2);
        var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[] { new SavedLife(1, simulation.RecordPlayerLife()) })).Single().Record;
        simulation.AddHistory(record, 10); simulation.AddHistory(record, 11);
        var first = world.ExpeditionFor(10); var second = world.ExpeditionFor(11);
        Require(first.Cells[2] == first.CompanionId(0) && second.Cells[2] == second.CompanionId(0) &&
            first.CompanionId(0) != second.CompanionId(0), "Two archived squads retain positions and independent member IDs");
        int[] health = new WorldTimelineBattleResolver().ResolveParty(world, 1, first.CreateCombatants(1), second.CreateCombatants(1));
        Require(health.Length == 4 && (health.Take(2).Any(hp => hp > 0) != health.Skip(2).Any(hp => hp > 0)),
            "Two historical squads complete real native combat without player formation input");
    }

    private static IEnumerator Travel(int destination)
    {
        var world = session.Simulation.State;
        int current = world.Actors[world.PlayerActorId].NodeId.Value;
        if (current == destination) yield break;
        var route = WorldPathfinder.FindRoute(world, current, new[] { destination }, node => node.MonsterHealth == 0);
        if (route.Count == 0) route = WorldPathfinder.FindRoute(world, current, new[] { destination });
        Require(route.Count > 0, "Recruitment stage is reachable");
        foreach (int step in route) yield return Move(step);
    }

    private static void ValidateVanguardPositions(int nodeId)
    {
        var world = session.Simulation.State;
        var player = world.Actors[world.PlayerActorId];
        var party = session.Expedition;
        var node = world.Nodes[nodeId];
        var resolver = new WorldTimelineBattleResolver();
        int shieldId = ExpeditionParty.FirstCompanionId;
        party.Place(player.Id, 4); party.Place(shieldId, 1);
        var front = Fight();
        var frontReport = player.LastBattleOutput.ToArray();
        party.Place(player.Id, 1); // Swap: leader in front, shield behind.
        var rear = Fight();
        var rearReport = player.LastBattleOutput.ToArray();
        party.Place(player.Id, 4);
        File.WriteAllText(Path.Combine(output, "VanguardComparison.txt"),
            $"Shield front: leader HP {front[0]}, shield HP {front[1]}, leader damage taken {frontReport[0].DamageTaken}, shield damage taken {frontReport[1].DamageTaken}\n" +
            $"Shield rear: leader HP {rear[0]}, shield HP {rear[1]}, leader damage taken {rearReport[0].DamageTaken}, shield damage taken {rearReport[1].DamageTaken}\n");
        Require(front.Take(2).Any(hp => hp > 0) && front.Skip(2).All(hp => hp == 0), "Shield-front formation wins the first encounter");
        Require(front[0] > rear[0] && frontReport[1].DamageTaken > frontReport[0].DamageTaken,
            "Forward shield measurably protects the leader compared with reversed positions");

        int[] Fight()
        {
            var leader = new ActorState(player.Id, player.BirthNodeId, player.MaxHealth, player.Attack);
            leader.Items.AddRange(player.Items);
            var allies = new[] { leader, party.CreateCompanion(0, nodeId) };
            var enemies = Enumerable.Range(0, node.GuardianCount)
                .Select(index => new ActorState(-nodeId - index, nodeId, node.MonsterHealth / node.GuardianCount, node.MonsterAttack)).ToArray();
            return resolver.ResolveParty(world, nodeId, allies, enemies);
        }
    }

    private static IEnumerator RecruitAtStage(WorldTimelinePanelCtrl hud, int role)
    {
        session.FocusRecruitmentSite(role);
        typeof(WorldTimelinePanelCtrl).GetMethod("ShowLocationDetails", Hidden).Invoke(hud, new object[] { true });
        yield return Settle(); Capture("Location.png"); yield return Settle();
        var button = hud.GetComponentsInChildren<Button>().Single(candidate => candidate.name == "关卡招募");
        Require(button.interactable && !session.Expedition.HasRecruited(role), "Recruit button enables only after arrival and conditions");
        button.onClick.Invoke();
        yield return Settle(); Capture("Recruitment.png"); yield return Settle();
        Require(session.Expedition.HasRecruited(role) && !button.interactable && !session.RecruitCompanion(role), "Stage reward cannot be claimed twice");
        hud.CloseLocationDetails();
    }

    private static IEnumerator FightNearestGuard()
    {
        var world = session.Simulation.State;
        int current = world.Actors[world.PlayerActorId].NodeId.Value;
        var route = world.Nodes.Values.Where(node => node.MonsterHealth > 0 && world.Ritual?.NodeId != node.Id)
            .Select(node => new { Node = node, Path = WorldPathfinder.FindRoute(world, current, new[] { node.Id },
                candidate => candidate.MonsterHealth == 0 || candidate.Id == node.Id) })
            .Where(entry => entry.Path.Count > 0).OrderBy(entry => entry.Path.Count).ThenBy(entry => entry.Node.MonsterHealth)
            .First().Path;
        foreach (int step in route) yield return Move(step);
    }

    private static IEnumerator Move(int node)
    {
        typeof(WorldTimelineDebugView).GetField("selectedNode", Hidden).SetValue(session, node);
        Require(session.CanMoveSelected, "Target is a reachable map step");
        session.MoveSelected();
        bool capturedBattle = false;
        StoryEncounter answeredChoice = null;
        while (session.IsBusy)
        {
            var panel = Object.FindFirstObjectByType<ExpeditionBattleFormation>();
            if (session.PreparingExpeditionBattle && panel != null)
            {
                // Change the actual native UI during a pending movement, then verify replay later.
                yield return Settle();
                var style = Resources.Load<ExpeditionFormationStyle>("ExpeditionFormationStyle");
                Require(style != null && style.PaperTexture != null && style.Portraits.Length == 11 && style.Portraits.All(sprite => sprite != null),
                    "Paper skin and all character portraits load through player-build resource references");
                foreach(string asset in new[]{"Header","DetailsPaper","RosterPaper","Confirm","Bench","SelectedCorners","ShieldPortrait",
                    "LeaderPortrait","ShieldCardPortrait","ArcherPortrait","SwordsmanPortrait","HealerPortrait","Deployed","Reserve","Archive","RoleStamp","AbilityWash"})
                    Require(Resources.Load<Sprite>("FormationReference/"+asset)!=null,"Extracted reference asset loads: "+asset);
                Require(style.Landscape != null && style.PaperFrame != null && Resources.Load<Shader>("FormationPaper").isSupported,
                    "Paper feathering, landscape margins and sliced button frames load");
                foreach(var frame in panel.GetComponentsInChildren<ExpeditionInkFrame>())
                    Require(frame.canvasRenderer != null && frame.rectTransform.rect.width > 0 && frame.rectTransform.rect.height > 0,
                        "Card and confirm outlines have a renderer and nonempty bounds");
                var motes = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Single(p => p.name == "SelectionMotes");
                Require(GameObject.Find("布阵 · 青玉阵纹") != null && Resources.Load<Shader>("FormationInk").isSupported,
                    "Ground formation marks use a supported shader");
                int targetCell = session.Simulation.State.Nodes[node].IsExpeditionVanguard ? 1 : 2;
                var from = panel.transform.Find("Piece" + ExpeditionParty.FirstCompanionId).gameObject;
                var to = (RectTransform)panel.transform.Find("FormationCell" + targetCell);
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, from.transform.position) };
                ExecuteEvents.Execute(from, pointer, ExecuteEvents.beginDragHandler);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, to.position);
                ExecuteEvents.Execute(from, pointer, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(from, pointer, ExecuteEvents.endDragHandler);
                Require(session.Expedition.CellOf(ExpeditionParty.FirstCompanionId) == targetCell, "Dragging an actual piece changes its battlefield cell");
                var opponent = panel.GetComponentsInChildren<Button>().First(button => button.name.StartsWith("Opponent"));
                opponent.onClick.Invoke();
                Require(!motes.isPlaying, "Selecting an enemy hides the current squad selection effect");
                int[] beforeEnemyEdit = session.Expedition.Cells.ToArray();
                Require(!panel.PlaceSelected(0) && session.Expedition.Cells.SequenceEqual(beforeEnemyEdit), "Enemy formation is read-only");
                Click(panel, "Member1");
                Click(panel, "Bench");
                Require(!motes.isPlaying && session.Expedition.CellOf(ExpeditionParty.FirstCompanionId) < 0,
                    "Benching a selected companion removes its selection effect");
                var deploymentButton=panel.transform.Find("Bench").GetComponent<Button>();
                Require(deploymentButton.interactable && deploymentButton.GetComponentInChildren<TMPro.TMP_Text>().text=="上阵",
                    "Benched companion can immediately deploy using the right-side button");
                yield return Settle();Capture("BenchedFormation.png");yield return Settle();
                Click(panel,"Bench");
                Require(session.Expedition.CellOf(ExpeditionParty.FirstCompanionId)==targetCell && motes.isPlaying,
                    "Right-side button restores the previous cell and selection effect");
                Require(deploymentButton.GetComponentInChildren<TMPro.TMP_Text>().text=="下阵候补","Deployed companion's button changes back to bench");
                Click(panel,"Bench");
                Click(panel, "FormationCell" + targetCell);
                Require(HotGameEntry.HeroManager.IsTimelineBattle && !HotGameEntry.HeroManager.InBattle, "Real arena is visible but combat is paused during formation");
                int planningYear = session.Simulation.State.Year;
                int planningHp = session.Simulation.State.Actors[session.Simulation.State.PlayerActorId].Health;
                yield return Settle(1);
                Require(motes.isPlaying && motes.particleCount > 0, "Selected deployed companion emits restrained jade particles");
                Require(session.Simulation.State.Year == planningYear && session.Simulation.State.Actors[session.Simulation.State.PlayerActorId].Health == planningHp,
                    "Planning neither advances time nor deals damage");
                yield return Settle();
                Capture(session.Simulation.State.Nodes[node].IsExpeditionVanguard ? "VanguardFormation.png" : "Formation.png");
                yield return Settle();
                var detailPortrait = panel.transform.Find("SelectedPortrait").GetComponent<Image>();
                foreach(string name in new[]{"DetailLeader","DetailShield","DetailArcher","DetailSwordsman","DetailHealer"})
                    Require(Resources.Load<Sprite>("FormationReference/"+name)!=null,"Full character illustration loads: "+name);
                Click(panel,"Member0");
                Require(detailPortrait.sprite.name=="DetailLeader","Leader details show a full illustration");
                yield return Settle();
                Capture("LeaderFormation.png");
                yield return Settle();
                if(session.Expedition.Owns(ExpeditionParty.FirstCompanionId+2))
                {
                    Click(panel,"Member3");
                    Require(detailPortrait.sprite.name=="DetailArcher","Archer details show a full illustration");
                    yield return Settle();
                    Capture("ArcherFormation.png");
                    yield return Settle();
                }
                Click(panel, "Confirm");
                battles++;
            }
            if (HotGameEntry.HeroManager.IsTimelineBattle && !HotGameEntry.HeroManager.IsTimelineSimulation && !capturedBattle)
            {
                yield return Settle(2);
                Capture("Battle.png"); capturedBattle = true;
            }
            if (session.PendingBattleSettlement != null)
            {
                if (!session.PendingBattleSettlement.IsTreasure)
                {
                    Require(session.PendingBattleSettlement.Victory, "Team wins the selected map encounter");
                    Require(session.PendingBattleSettlement.Experience > 0, "Victory shows its growth reward");
                    yield return Settle(); Capture(session.Simulation.State.Nodes[node].IsExpeditionVanguard
                        ? "VanguardSettlement.png" : "Settlement.png"); yield return Settle();
                }
                session.ContinueBattleSettlement();
            }
            var reward=Object.FindFirstObjectByType<ExpeditionRewardPanel>();
            if(reward!=null)
            {
                yield return Settle();
                Require(session.Expedition.Equipment.PendingRewards.Count==3,"Victory presents exactly three choices");
                var claim=reward.transform.Find("ClaimReward").GetComponent<Button>();
                Require(!claim.interactable,"A reward must be selected before claiming");
                int item=session.Expedition.Equipment.PendingRewards[1], count=session.Expedition.Equipment.Count(item);
                reward.transform.Find("RewardChoice1").GetComponent<Button>().onClick.Invoke();
                yield return Settle();Capture("RewardChoices.png");yield return Settle();
                claim.onClick.Invoke();
                Require(session.Expedition.Equipment.Count(item)==count+1 && !session.ClaimTeamReward(0),"Exactly one selected reward is granted");
                yield return Settle();
            }
            var workshop=Object.FindFirstObjectByType<ExpeditionWorkshopPanel>();
            if(workshop!=null && session.EquipmentServiceOpen)
            {
                yield return Settle();
                var gear=session.Expedition.Equipment;bool merchant=session.EquipmentServiceMerchant;
                if(merchant)
                {
                    var buy=workshop.transform.Find("WorkshopConfirm").GetComponent<Button>();
                    if(buy.interactable)
                    {
                        int before=gear.Count(5);long stones=gear.SpiritStones;buy.onClick.Invoke();yield return Settle();
                        Require(gear.Count(5)==before+1 && gear.SpiritStones==stones-ExpeditionEquipment.Definition(5).Price,"Shop reads purchase price from the table");purchases++;
                    }
                    Capture("MerchantShop.png");yield return Settle();
                    workshop.transform.Find("UpgradeTab").GetComponent<Button>().onClick.Invoke();yield return Settle();
                }
                int key=Enumerable.Range(-5,5).Concat(gear.Instances.Select(i=>i.Id)).Where(gear.CanUpgrade)
                    .Where(k=>!merchant || gear.SpiritStones>=gear.UpgradeDefinition(k).UpgradePrice(gear.UpgradeLevel(k)))
                    .OrderBy(k=>gear.UpgradeDefinition(k).UpgradePrice(gear.UpgradeLevel(k))).FirstOrDefault();
                if(key!=0)
                {
                    // Page through actual upgrade cards to find the chosen independent instance.
                    while(workshop.GetComponentsInChildren<Button>().All(b=>b.name!="WorkshopItem"+key))
                    {workshop.transform.Find("WorkshopNext").GetComponent<Button>().onClick.Invoke();yield return Settle();}
                    workshop.GetComponentsInChildren<Button>().Single(b=>b.name=="WorkshopItem"+key).onClick.Invoke();yield return Settle();
                    int oldLevel=gear.UpgradeLevel(key);long stones=gear.SpiritStones;
                    int fee=gear.UpgradeDefinition(key).UpgradePrice(oldLevel);
                    var upgrade=workshop.transform.Find("WorkshopConfirm").GetComponent<Button>();Require(upgrade.interactable,"Selected upgrade is enabled");
                    Capture(merchant?"MerchantUpgrade.png":"EventUpgrade.png");yield return Settle();upgrade.onClick.Invoke();yield return Settle();
                    Require(gear.UpgradeLevel(key)==oldLevel+1 && gear.SpiritStones==stones-(merchant?fee:0),"Upgrade applies one level and charges the displayed price");
                    if(merchant)paidUpgrades++;else{freeUpgrades++;Require(!session.UpgradeArtifact(key),"Event grants exactly one free upgrade");}
                }
                workshop.transform.Find("LeaveWorkshop").GetComponent<Button>().onClick.Invoke();yield return Settle();
            }
            var stories = session.Simulation.Stories;
            if (stories?.PendingChoice is { } choice && !ReferenceEquals(choice, answeredChoice))
            {
                var available = choice.Options.Where(option => stories.UnavailableReason(choice.Actor, option) == null).ToArray();
                var option = available.FirstOrDefault(option => option.Condition == StoryCondition.Always && option.ItemId == 0)
                    ?? available.First();
                Require(stories.Choose(option.Id), "Resolve map story");
                answeredChoice = choice;
            }
            stories?.Continue();
            yield return null;
        }
        Require(session.Simulation.WaitingForPlayer, "Return to actionable map after settlement");
        Require(GameObject.Find("布阵 · 青玉阵纹") == null, "Formation VFX are released when combat starts");
    }

    private static void Click(Component panel, string name)
    {
        var button = panel.GetComponentsInChildren<Button>().Single(button => button.name == name);
        Require(button.interactable, name + " is enabled"); button.onClick.Invoke();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static IEnumerator Settle(double seconds = .4)
    {
        double until = EditorApplication.timeSinceStartup + seconds;
        int frame = Time.frameCount + 4;
        while (EditorApplication.timeSinceStartup < until || Time.frameCount < frame) yield return null;
    }
    private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(output, name));
    private static void Finish(string error)
    {
        EditorApplication.update -= Tick;
        while (routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose();
        if (session != null) Object.Destroy(session.gameObject);
        WorldTimelineDebugView.EditorSaveDirectory = previousSaveDirectory;
        if (title != null) title.Visible = true;
        File.WriteAllText(Path.Combine(output, "Result.txt"), error ?? "PASS: right-side bench/deploy toggle, six equipment slots, backpack upgrade preview, retired inventory entry removed, mixed old/new checkpoint commands, merchants, events and native team battles.");
        if (error == null) Debug.Log("EXPEDITION_VALIDATION_PASSED"); else Debug.LogError(error);
    }
}
