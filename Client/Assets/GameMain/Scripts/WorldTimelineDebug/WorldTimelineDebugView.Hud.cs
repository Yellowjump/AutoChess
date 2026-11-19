using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        public static System.Func<string, string> ResolveLanguageKey { private get; set; }

        public static string TimelineText(string key, params object[] arguments)
        {
            string text = (ResolveLanguageKey?.Invoke(key) ?? key).Replace("\\n", "\n");
            return arguments.Length == 0 ? text : string.Format(text, arguments);
        }
        public static Action<WorldTimelineDebugView> OpenMapHud { get; set; }
        public static Action<WorldTimelineDebugView> OpenStoryHud { get; set; }
        public bool UsesStoryHud { get; set; }
        public BattleSettlement PendingBattleSettlement { get; private set; }

        public void ContinueBattleSettlement() => PendingBattleSettlement = null;

        public (string Title, string Body, string Art)[] EndingPages { get; private set; }
        public void CloseEnding() => EndingPages = null;

        private void ShowEnding()
        {
            var world = simulation.State;
            if (!UsesStoryHud || world.Outcome != RunOutcome.Cleared) return;
            EndingPages = CreateEndingPages(
                world.ContractRevoked.HasValue
                    ? simulation.Events.Any(entry => entry.Kind == WorldEventKind.ContractRevoked && entry.Detail == "Guardian disabled")
                    : (bool?)null,
                world.NorthRelayRestored.HasValue
                    ? simulation.Events.Any(entry => entry.Kind == WorldEventKind.NorthRelaySpringUsed)
                    : (bool?)null,
                world.SluiceOpening.HasValue ? world.SluiceDrainsBoss : (bool?)null);
        }

        public static (string Title, string Body, string Art)[] CreateEndingPages(
            bool? guardianDisabled, bool? springUsed, bool? sluiceDrainsBoss)
        {
            var pages = new System.Collections.Generic.List<(string Title, string Body, string Art)>();
            void Add(string key, string art) => pages.Add((TimelineText("Timeline_Ending_" + key + "Title"),
                TimelineText("Timeline_Ending_" + key + "Body"), art));

            Add("Stele", "Stele");
            if (guardianDisabled.HasValue) Add(guardianDisabled.Value ? "Deed" : "DeedsKept", "Deed");
            if (springUsed.HasValue) Add(springUsed.Value ? "Spring" : "Names", "Spring");
            if (sluiceDrainsBoss.HasValue) Add(sluiceDrainsBoss.Value ? "Sluice" : "Channel", "Sluice");
            bool complete = guardianDisabled.HasValue && springUsed.HasValue && sluiceDrainsBoss.HasValue;
            Add(complete ? "Book" : "Road", complete ? "Book" : "Road");
            return pages.ToArray();
        }

        private IEnumerator PresentBattleSettlement(BattleSettlement result)
        {
            if(!IsRestoringCheckpoint && UsesStoryHud)
            {
                PendingBattleSettlement = result;
                try
                {
                    while (PendingBattleSettlement == result && UsesStoryHud) yield return null;
                }
                finally { PendingBattleSettlement = null; }
            }
            yield return PresentTeamReward(result);
        }

        public string BattleDropName(int nodeId, int itemId)
            => DescribeMapLoot(itemId, nodeId).Name;
        public event Action Closed;
        public bool UsesPrefabHud { get; set; }
        public bool IsPreparing => preparing;
        public bool IsBusy => advancing;
        public int SelectedNodeId => selectedNode;
        public NodeState SelectedLocation => simulation.State.Nodes[selectedNode];
        public string PlayerName => Expedition?.LeaderRole >= 0 ? CompanionName(Expedition.LeaderRole).Split('·')[0].Trim() : OriginalPlayerName;
        public string OriginalPlayerName => startingCultivator?.Name ?? TimelineText("Timeline_UI_86");
        public (string Text, int? NodeId) MapNotice
        {
            get
            {
                var notice = MapGuide;
                var world = simulation?.State;
                if (world == null || advancing || world.IsEnded || !simulation.WaitingForPlayer || saveIssue != null) return notice;
                var explored = simulation.Events.LastOrDefault(entry => entry.Kind == WorldEventKind.Explored &&
                    entry.ActorId == world.PlayerActorId && entry.Year == world.Year);
                return explored == null ? notice : ("新地点历练 +" + explored.Detail + "　·　" + notice.Text, notice.NodeId);
            }
        }
        private (string Text, int? NodeId) MapGuide
        {
            get
            {
                string notice = saveIssue;
                if (notice != null) return (notice, null);
                var world = simulation?.State;
                if (world?.Outcome == RunOutcome.WorldDestroyed)
                    return (TimelineText("Timeline_Guide_AfterRitual", world.Ritual.DeadlineYear), null);
                if (world?.Outcome == RunOutcome.PlayerKilledChosen)
                    return (TimelineText("Timeline_Guide_AfterKillingChosen"), null);
                if (world?.Outcome == RunOutcome.Running && !world.Actors[world.PlayerActorId].IsAlive)
                    return (TimelineText("Timeline_Guide_AfterDeath"), null);
                notice = LocalRecruitmentNotice();
                if (notice != null) return (notice, null);
                if (PendingVanguard is int firstBattle)
                    return (Expedition.IsFallen(0) || Expedition.LeaderRole >= 0 ? "前往近战守关。胜利后可招募阿翎。" : "岩舟已入队 · 前往近战守关，盾卫放前排、主角放后排。胜利后可招募阿翎。", firstBattle);
                notice = OpeningNotice;
                if (notice != null) return (notice, null);
                if (simulation?.WaitingForPlayer == true && simulation.CanJoinChosen(simulation.State.PlayerActorId))
                {
                    var player = simulation.State.Actors[simulation.State.PlayerActorId];
                    return (TimelineText("Timeline_ChosenEncounter_Notice"), player.NodeId);
                }
                var saltGuide = SaltChannelGuide();
                if (saltGuide.NodeId.HasValue) return saltGuide;
                notice = LightningNextStop;
                if (sluiceCampaignActive && simulation != null)
                {
                      var guide = PersonalStoryGuide();
                    return !guide.NodeId.HasValue && notice != null ? (notice, null) : guide;
                }
                return notice == null ? RelayGuide() : (notice, null);
            }
        }
        public string Notice => MapNotice.Text;

        private (string Text, int? NodeId) SaltChannelGuide()
        {
            if (simulation?.Stories == null || simulation.State.PlayerControlEnded || simulation.State.IsEnded ||
                !simulation.Stories.TryGetDefinition(SaltChannel.Ditch, out _) ||
                simulation.State.Actors[simulation.State.PlayerActorId].NodeId is not int current ||
                simulation.Stories.ClaimedItems.Contains(SaltChannel.RewardItemId)) return (null, null);
            var world = simulation.State;
            if (current == SaltChannel.Spring || current == SaltChannel.Kiln)
                return StoryWaypoint(current, SaltChannel.Ditch, "Timeline_Salt_Guide_Ditch");
            if (current == SaltChannel.Ditch)
                return (TimelineText("Timeline_Salt_Guide_Beach"), SaltChannel.Beach);
            if (current == SaltChannel.Beach && world.Nodes[SaltChannel.Beach].MonsterHealth == 0)
                return (TimelineText("Timeline_Salt_Guide_Drain"), SaltChannel.Drain);
            if (current == SaltChannel.Drain)
                return (TimelineText(world.SaltChannelDrained ? "Timeline_Salt_Guide_Dry" : "Timeline_Salt_Guide_Beast"), SaltChannel.Head);
            return (null, null);
        }
        private string LightningNextStop
        {
            get
            {
                var world = simulation?.State;
                if (world == null || world.IsEnded || world.PlayerControlEnded || simulation.Stories == null) return null;
                var actor = world.Actors[world.PlayerActorId];
                if (!actor.IsAlive || actor.Items.Contains(9153)) return null;
                int optionId = actor.Items.Contains(9152) ? 602 : actor.Items.Contains(9151) ? 702 : 0;
                var stories = simulation.Stories;
                if (!stories.Config.Options.TryGetValue(optionId, out var option) ||
                    stories.ClaimedItems.Contains(option.ItemId) ||
                    !stories.Config.Events.TryGetValue(option.EventId, out var destination) ||
                    !world.Nodes.TryGetValue(destination.NodeId, out var node) ||
                    node.StoryTrialItemId != option.ItemId) return null;
                return TimelineText("Timeline_LightningNextStop", destination.Title, node.Q, node.R);
            }
        }
        public bool AutoObserving => autoObserve;
        public int ObservationSpeed => yearsPerSecond;
        public string CurrentStatus => StatusText();
        public bool TryGetLearnedTechnique(int id, out TechniqueDefinition definition, out TechniqueProgress progress)
        {
            definition = null;
            progress = null;
            var state = simulation?.State;
            return state != null && state.Actors.TryGetValue(state.PlayerActorId, out var actor) &&
                actor.Techniques.TryGetValue(id, out progress) &&
                scenarioData?.Techniques?.TryGetValue(id, out definition) == true;
        }

        public string LocationName(NodeState node)
        {
            if (node.IsExpeditionVanguard) return "近战守关";
            if (openingMap && node.Id == OpeningGate) return TimelineText("Timeline_Opening_Gate");
            if (contractStoryActive && node.Id == TwinGolemGate) return TimelineText("Timeline_QiStory_Pass");
            if (node.Id == simulation.State.NorthRelaySpringNodeId) return TimelineText("Timeline_NorthRelay_Spring");
            if (simulation.State.NorthRelaySpringNodeId.HasValue && node.Id == HexWorldMap.Id(-2, 0))
                return TimelineText("Timeline_NorthRelay_WreckBeastPass");
            if (node.Id == SwordTrialNode && node.IsGuardianGroup) return TimelineText("Timeline_SwordTrial_Name");
            if (TryGetPlayerStory(node.Id, out var story)) return story.Title;
            return locations.TryGetValue(node.Id, out var row) ? row.Name : node.Id == simulation.State.Ritual.NodeId
                ? TimelineText("Timeline_UI_88") : node.MonsterHealth > 0 ? TimelineText("Timeline_UI_89") : TimelineText("Timeline_UI_90");
        }

        private bool TryGetPlayerStory(int nodeId, out StoryDefinition story)
        {
            story = null;
            if (simulation.Stories?.TryGetDefinition(nodeId, out story) != true) return false;
            return CanSeeStory(story, simulation.State.Actors[simulation.State.PlayerActorId]);
        }

        public string LocationKind(NodeState node)
        {
            if (node.IsExpeditionVanguard) return node.MonsterHealth > 0 ? "首战 · 近战守关" : "首战 · 已通关";
            if (node.CompanionStageRole is int role)
                return Expedition.IsFallen(role) ? "同伴关卡 · 已阵亡" : Expedition.HasRecruited(role) ? "同伴关卡 · 已招募" : "同伴关卡";
            var kind = WorldTimelineMapMarker.Classify(node, TryGetPlayerStory(node.Id, out _),
                simulation.State.Ritual?.NodeId == node.Id, HasClearedGuardian(node));
            return node.IsGuardianGroup && (kind is MapMarkerKind.Battle or MapMarkerKind.BattleStory)
                ? TimelineText("Timeline_SwordTrial_Guardians", node.GuardianCount)
                : TimelineText(WorldTimelineMapMarker.LabelKey(kind));
        }

        public bool HasClearedGuardian(NodeState node) => node.MonsterHealth <= 0
            && (node.HasExplorationGuardian || node.StoryRequiresGuardianDefeat ||
                node.GuardianDefeat.HasValue ||
                (locations.TryGetValue(node.Id, out var row) && row.MonsterHealth > 0));

        public static string TechniqueName(int id) => id == 1 ? TimelineText("Timeline_Technique_SwordArt")
            : TimelineText("Timeline_Technique_Number", id);

        public static string DetailFlavor(int skillId, int itemId = 0)
        {
            if (itemId != 0)
            {
                string key = $"Timeline_Flavor_Item_{itemId}";
                string text = TimelineText(key);
                if (text != key) return text;
            }
            if (skillId == 0) return "";
            string skillKey = $"Timeline_Flavor_Skill_{skillId}";
            string skillText = TimelineText(skillKey);
            return skillText == skillKey ? "" : skillText;
        }

        public static string TechniqueSkillName(int id) => id switch
        {
            1 => TimelineText("Timeline_Technique_Slash"),
            1001 => TimelineText("Timeline_Technique_LongReach"),
            8 => TimelineText("ItemName_FlySwordAnim"),
            _ => TimelineText("Timeline_Technique_SkillNumber", id)
        };

        public string MapRewardDetails(NodeState node)
        {
            if (Expedition?.ProgressionEnabled != true) return LocationLoot(node);
            if (node.FirstClear.HasValue) return ExplorationReward(node);
            var lines = new System.Collections.Generic.List<string>();
            if (node.MonsterHealth > 0)
            {
                lines.Add($"守军历练 +{Expedition.VictoryExperience(node.Id,simulation.Cultivation)} · 灵石 +{node.VictorySpiritStones}");
                var choices = Expedition.Equipment.PreviewArtifactChoices(node.Id);
                if (choices.Count > 0) lines.Add("三选一  " + string.Join("、",choices.Select(i => ExpeditionEquipment.Definition(i).Name)));
            }
            if (node.Items.Count > 0) lines.Add(LocationLoot(node));
            if (lines.Count == 0) return "尚未首通";
            return string.Join("\n",lines);
        }

        public string MapActorLocation(ActorState actor)
        {
            var world = simulation.State;
            if (actor.HasJoinedPlayer)
            {
                for (int role = ExpeditionParty.StageRoles; role < Expedition.RoleCount; role++)
                    if (Expedition.PastBirth(role) == actor.BirthNodeId && Expedition.IsFallen(role)) return "入队后阵亡";
                return "已加入当世队伍";
            }
            if (!actor.IsAlive) return "已阵亡";
            var traveller = actor.LeaderId is int leader ? world.Actors[leader] : actor;
            bool travelling = traveller.CurrentAction?.Kind == WorldActionKind.Move;
            int? node = travelling ? traveller.CurrentAction.TargetNodeId : traveller.NodeId;
            if (!node.HasValue) return "行进中";
            var place = world.Nodes[node.Value];
            string location = $"{LocationName(place)} ({place.Q}, {place.R})";
            return travelling ? $"前往{location} · 余{traveller.RemainingYears}年" : location;
        }

        public string LocationLoot(NodeState node)
        {
            return node.Items.Count == 0 ? TimelineText("Timeline_UI_99") : string.Join("、", node.Items.Select(item =>
                DescribeMapLoot(item, node.Id).Name
                + (node.LootAttackBonus > 0 ? TimelineText("Timeline_UI_LootAttack", node.LootAttackBonus) : "")));
        }

        public string LocationArrivals(NodeState node)
        {
            var arrivals = ActorMapProjection.NextArrivals(simulation, node.Id);
            var world = simulation.State;
            var present = simulation.Histories.Select(h => world.Actors[h.ActorId])
                .Where(a => a.OnWorldMap && a.NodeId == node.Id)
                .Select(a => ClearOwner(a.Id) + " · 已在此地");
            var lines = present.Concat(arrivals.Select(entry =>
            {
                var actor = world.Actors[entry.ActorId];
                string name = actor.Id == world.PlayerActorId ? PlayerName
                    : actor.Id == world.ChosenActorId ? TimelineText("Timeline_UI_79")
                    : roster.FirstOrDefault(character => character.Birth == actor.BirthNodeId)?.Name
                        ?? TimelineText("Timeline_UI_PastLife", actor.Id);
                return TimelineText(entry.Years == 1 ? "Timeline_Arrival_NextYear" : "Timeline_Arrival_Years",
                    name, entry.Years);
            })).ToArray();
            return lines.Length == 0 ? TimelineText("Timeline_Arrival_None") : string.Join("\n",lines);
        }

        public bool CanMoveSelected => !advancing && simulation.WaitingForPlayer &&
            simulation.State.Actors[simulation.State.PlayerActorId].NodeId.HasValue &&
            simulation.State.Nodes[simulation.State.Actors[simulation.State.PlayerActorId].NodeId.Value].Links.Contains(selectedNode);

        public string LocationHint
        {
            get
            {
                var world = simulation.State;
                if (SelectedLocation.IsExpeditionVanguard) return SelectedLocation.MonsterHealth > 0
                    ? "两名近战守军从正面逼近。让岩舟站前排接敌，主角留后排输出。胜利可满足阿翎的招募条件。"
                    : "近战守关已通关。前往阿翎的同伴关卡，招募远程输出。";
                if (SelectedLocation.IsMerchant) return Expedition?.Equipment.Modern==true?
                    "行商出售通用道具，也可消耗灵石升级本命法器和道具。售价与升级费用见商铺。":TimelineText("Timeline_MerchantHint");
                if (selectedNode == world.Ritual.NodeId)
                    return TimelineText(world.SluiceDrainsBoss && world.SluiceOpening.HasValue
                        ? "Timeline_Sluice_DrainedBossHint" : "Timeline_UI_100");
                if (selectedNode == world.NorthRelaySpringNodeId)
                    return TimelineText(world.NorthRelayRestored.HasValue ? "Timeline_NorthRelay_SpringFlowing" : "Timeline_NorthRelay_SpringDry");
                if (TryGetPlayerStory(selectedNode, out var story))
                {
                    string text = story.Text + TimelineText(SelectedLocation.MonsterHealth > 0 ? "Timeline_UI_101" : "Timeline_UI_102");
                    if (Expedition?.Equipment.Modern != true || story.IsMerchant) return text;
                    if (SelectedLocation.FirstClear is { } clear && clear.ActorId != world.PlayerActorId)
                        return text + "\n此处首通归" + ClearOwner(clear.ActorId) + "。";
                    return text + (SelectedLocation.EventRewardTaken || Expedition.Equipment.HasVisitedEvent(selectedNode)
                        ? "\n此处的灵石与淬炼机缘已领取。" : "\n首次完成事件：灵石 +12，可选一件法器或道具免费升阶。");
                }
                if (SelectedLocation.MonsterHealth > 0) return TimelineText("Timeline_UI_103");
                if (SelectedLocation.Items.Count > 0) return TimelineText("Timeline_UI_104");
                return "探索新地点、招募队友，再挑战守关敌人。原地等待只推进时间，不获得经验。";
            }
        }

        public string CultivationDetails
        {
            get
            {
                var actor = simulation.State.Actors[simulation.State.PlayerActorId];
                return MapGrowthRules + "\n\n" + $"{TimelineText("Attr_MaxHp")} [{actor.Talents.Health.Grade}]    {TimelineText("Attr_ATK")} [{actor.Talents.Attack.Grade}]\n"
                    + TimelineText("Timeline_UI_CurrentAttack", actor.Attack, actor.CombatAttack)
                    + string.Join("\n", actor.Items.Where(id => simulation.Stories?.Config.Items.ContainsKey(id) == true)
                        .Select(id => simulation.Stories.Config.Items[id].Name + "：" + simulation.Stories.Config.Items[id].Description)) + "\n\n"
                    + string.Join("\n", cultivation.Levels.OrderBy(level => level.Level)
                        .Select(level => TimelineText("Timeline_UI_LevelExperience", level.Level, level.ExperienceToNext)));
            }
        }

        public string[] InactiveHistoryNames() => library.Where(entry => !simulation.Histories.Any(h =>
            h.Record.BirthNodeId == entry.Record.BirthNodeId)).Select(entry => entry.Name).ToArray();

        public string HistoryDetails => library.Count == 0 ? TimelineText("Timeline_UI_106")
            : string.Join("\n\n", library.Select(entry => TimelineText(simulation.Histories.Any(history =>
                history.Record.BirthNodeId == entry.Record.BirthNodeId) ? "Timeline_UI_107" : "Timeline_UI_108") + entry.Name));
        public string LogDetails
        {
            get
            {
                string log = simulation.Events.Count == 0 ? TimelineText("Timeline_UI_109")
                    : string.Join("\n", simulation.Events.Reverse().Take(60).Select(FormatEvent));
                string next = LightningNextStop;
                return next == null ? log : next + "\n\n" + log;
            }
        }

        public bool SelectAtScreen(Vector2 screen)
        {
            if (board == null || advancing) return false;
            int? id = board.Pick(screen);
            if (!id.HasValue) return false;
            selectedNode = id.Value;
            board.SelectNode(selectedNode);
            return true;
        }

        public void HoverAtScreen(Vector2 screen)
        {
            if (board != null) board.HoveredNode = advancing ? null : board.Pick(screen);
        }
        public void ClearMapHover() { if (board != null) board.HoveredNode = null; }
        public void PanMap(Vector2 delta) { if (!advancing) board?.Pan(new Vector2(delta.x, -delta.y)); }
        public void ZoomMap(float factor) { if (!advancing) board?.Zoom(factor); }
        public void OverviewMap() { if (!advancing) board?.Overview(); }
        public MapMarkerKind? MapMarkerFilter
        {
            get => board?.MarkerFilter;
            set { if (board != null) board.MarkerFilter = value; }
        }
        public void FocusMapActor(int id) { if (!advancing) board?.FocusActor(id); }
        public void MoveSelected() { if (CanMoveSelected) Submit(WorldAction.Move(selectedNode)); }
        public bool CanRest => !OpeningActive && !IsBusy && simulation.WaitingForPlayer &&
            Expedition?.AttritionEnabled == true && Expedition.NeedsRest;
        public void RestYear() { if (CanRest) Submit(WorldAction.Rest()); }
        public void WaitYear() { if (!advancing && simulation.WaitingForPlayer) Submit(WorldAction.Wait()); }
        public void Disguise() { if (!advancing && simulation.WaitingForPlayer) Submit(WorldAction.Disguise()); }
        public void JoinChosen() { if (!advancing && simulation.WaitingForPlayer && simulation.CanJoinChosen(1)) Submit(WorldAction.JoinChosen()); }
        public bool CanObserve => simulation != null && !preparing && !OpeningActive &&
            !simulation.WaitingForPlayer && !simulation.State.IsEnded;

        public void ObserveYear()
        {
            if (!CanObserve || advancing) return;
            autoObserve = false;
            StartAdvancing(simulation.AdvanceOneYearRoutine());
        }
        public void ToggleObservation()
        {
            if (autoObserve) autoObserve = false;
            else if (CanObserve && !advancing) autoObserve = true;
        }
        public void CycleObservationSpeed() { yearsPerSecond = yearsPerSecond == 5 ? 20 : yearsPerSecond == 20 ? 1 : 5; }

        public void PrepareNextLife()
        {
            if (preparing || advancing || simulation == null) return;
            if (!simulation.State.IsEnded)
            {
                if (!CanRecordPersonalLife || !simulation.RecordCompletedLife()) return;
                Execute(() => { });
            }
            if (!SaveLives() || !SaveCheckpoint()) return;
            round++;
            UnlockFinaleCultivator();
            startingCultivator = null;
            preparing = true;
            showBoardDetails = false;
            OpenPreparation?.Invoke(this);
        }

        public void ReturnToMenu()
        {
            if (advancing || !SaveLives() || (!preparing && !SaveCheckpoint())) return;
            autoObserve = false;
            returnToMenu?.Invoke();
            Destroy(gameObject);
        }
    }
}
