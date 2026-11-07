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

        private IEnumerator PresentBattleSettlement(BattleSettlement result)
        {
            if (IsRestoringCheckpoint || !UsesStoryHud) yield break;
            PendingBattleSettlement = result;
            try
            {
                while (PendingBattleSettlement == result && UsesStoryHud) yield return null;
            }
            finally { PendingBattleSettlement = null; }
        }

        public string BattleDropName(int nodeId, int itemId)
            => DescribeMapLoot(itemId, nodeId).Name;
        public event Action Closed;
        public bool UsesPrefabHud { get; set; }
        public bool IsPreparing => preparing;
        public bool IsBusy => advancing;
        public int SelectedNodeId => selectedNode;
        public NodeState SelectedLocation => simulation.State.Nodes[selectedNode];
        public string PlayerName => startingCultivator?.Name ?? TimelineText("Timeline_UI_86");
        public string Notice => saveIssue ?? (LightningNextStop ?? message);
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
            if (node.Id == SwordTrialNode && node.IsGuardianGroup) return TimelineText("Timeline_SwordTrial_Name");
            if (simulation.Stories != null && simulation.Stories.TryGetDefinition(node.Id, out var story)) return story.Title;
            return locations.TryGetValue(node.Id, out var row) ? row.Name : node.Id == simulation.State.Ritual.NodeId
                ? TimelineText("Timeline_UI_88") : node.MonsterHealth > 0 ? TimelineText("Timeline_UI_89") : TimelineText("Timeline_UI_90");
        }

        public string LocationKind(NodeState node)
        {
            if (node.IsMerchant) return TimelineText("Timeline_MerchantKind");
            if (node.IsGuardianGroup && node.GuardianCount > 0)
                return TimelineText("Timeline_SwordTrial_Guardians", node.GuardianCount);
            if (node.Id == simulation.State.Ritual.NodeId) return TimelineText("Timeline_UI_91");
            bool story = simulation.Stories != null && simulation.Stories.TryGetDefinition(node.Id, out _);
            if (node.MonsterHealth > 0) return story ? TimelineText("Timeline_UI_92") : TimelineText("Timeline_UI_93");
            if (story) return TimelineText("Timeline_UI_94");
            if (HasClearedGuardian(node)) return TimelineText("Timeline_UI_95");
            if (node.Items.Count > 0) return TimelineText("Timeline_UI_96");
            return node.IsCultivationSite || node.CultivationPerYear >= 8
                ? TimelineText("Timeline_UI_97") : TimelineText("Timeline_UI_98");
        }

        public bool HasClearedGuardian(NodeState node) => node.MonsterHealth <= 0
            && (node.HasExplorationGuardian || node.StoryRequiresGuardianDefeat ||
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

        public string LocationLoot(NodeState node)
        {
            return node.Items.Count == 0 ? TimelineText("Timeline_UI_99") : string.Join("、", node.Items.Select(item =>
                DescribeMapLoot(item, node.Id).Name
                + (node.LootAttackBonus > 0 ? TimelineText("Timeline_UI_LootAttack", node.LootAttackBonus) : "")));
        }

        public bool CanMoveSelected => !advancing && simulation.WaitingForPlayer &&
            simulation.State.Actors[simulation.State.PlayerActorId].NodeId.HasValue &&
            simulation.State.Nodes[simulation.State.Actors[simulation.State.PlayerActorId].NodeId.Value].Links.Contains(selectedNode);

        public string LocationHint
        {
            get
            {
                var world = simulation.State;
                if (SelectedLocation.IsMerchant) return TimelineText("Timeline_MerchantHint");
                if (selectedNode == world.Ritual.NodeId) return TimelineText("Timeline_UI_100");
                if (simulation.Stories != null && simulation.Stories.TryGetDefinition(selectedNode, out var story))
                    return story.Text + (SelectedLocation.MonsterHealth > 0 ? TimelineText("Timeline_UI_101") : TimelineText("Timeline_UI_102"));
                if (SelectedLocation.MonsterHealth > 0) return TimelineText("Timeline_UI_103");
                if (SelectedLocation.Items.Count > 0) return TimelineText("Timeline_UI_104");
                return TimelineText("Timeline_UI_105");
            }
        }

        public string CultivationDetails
        {
            get
            {
                var actor = simulation.State.Actors[simulation.State.PlayerActorId];
                return $"{TimelineText("Attr_MaxHp")} [{actor.Talents.Health.Grade}]    {TimelineText("Attr_ATK")} [{actor.Talents.Attack.Grade}]\n"
                    + TimelineText("Timeline_UI_CurrentAttack", actor.Attack, actor.CombatAttack)
                    + string.Join("\n", actor.Items.Where(id => simulation.Stories?.Config.Items.ContainsKey(id) == true)
                        .Select(id => simulation.Stories.Config.Items[id].Name + "：" + simulation.Stories.Config.Items[id].Description)) + "\n\n"
                    + string.Join("\n", cultivation.Levels.OrderBy(level => level.Level)
                        .Select(level => TimelineText("Timeline_UI_LevelExperience", level.Level, level.ExperienceToNext)));
            }
        }

        public string HistoryDetails => library.Count == 0 ? TimelineText("Timeline_UI_106")
            : string.Join("\n\n", library.Select(entry => (entry.Selected ? TimelineText("Timeline_UI_107") : TimelineText("Timeline_UI_108")) + entry.Name));
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
            board.Sync(simulation.State, selectedNode);
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
        public void FocusMapActor(int id) { if (!advancing) board?.FocusActor(id); }
        public void MoveSelected() { if (CanMoveSelected) Submit(WorldAction.Move(selectedNode)); }
        public void CultivateYears(int years) { if (!advancing && simulation.WaitingForPlayer) Submit(WorldAction.Cultivate(years)); }
        public void WaitYear() { if (!advancing && simulation.WaitingForPlayer) Submit(WorldAction.Wait()); }
        public void Disguise() { if (!advancing && simulation.WaitingForPlayer) Submit(WorldAction.Disguise()); }
        public void JoinChosen() { if (!advancing && simulation.WaitingForPlayer && simulation.CanJoinChosen(1)) Submit(WorldAction.JoinChosen()); }
        public void ObserveYear()
        {
            if (advancing || simulation.WaitingForPlayer || simulation.State.IsEnded) return;
            autoObserve = false;
            StartAdvancing(simulation.AdvanceOneYearRoutine());
        }
        public void ToggleObservation() { if (!advancing) autoObserve = !autoObserve; }
        public void CycleObservationSpeed() { yearsPerSecond = yearsPerSecond == 5 ? 20 : yearsPerSecond == 20 ? 1 : 5; }

        public void PrepareNextLife()
        {
            if (advancing || !simulation.State.IsEnded || !SaveLives()) return;
            round++;
            startingCultivator = null;
            foreach (var entry in library) entry.Selected = false;
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
