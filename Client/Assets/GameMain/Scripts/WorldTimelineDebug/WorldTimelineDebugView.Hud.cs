using System;
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
        public event Action Closed;
        public bool UsesPrefabHud { get; set; }
        public bool IsPreparing => preparing;
        public bool IsBusy => advancing;
        public int SelectedNodeId => selectedNode;
        public NodeState SelectedLocation => simulation.State.Nodes[selectedNode];
        public string PlayerName => startingCultivator?.Name ?? TimelineText("Timeline_UI_86");
        public string WeaponName => startingCultivator?.Weapon ?? TimelineText("Timeline_UI_87");
        public string Notice => saveIssue ?? message;
        public bool AutoObserving => autoObserve;
        public int ObservationSpeed => yearsPerSecond;
        public string CurrentStatus => StatusText();

        public string LocationName(NodeState node)
        {
            if (simulation.Stories != null && simulation.Stories.TryGetDefinition(node.Id, out var story)) return story.Title;
            return locations.TryGetValue(node.Id, out var row) ? row.Name : node.Id == simulation.State.Ritual.NodeId
                ? TimelineText("Timeline_UI_88") : node.MonsterHealth > 0 ? TimelineText("Timeline_UI_89") : TimelineText("Timeline_UI_90");
        }

        public string LocationKind(NodeState node)
        {
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
            && (node.HasExplorationGuardian || (locations.TryGetValue(node.Id, out var row) && row.MonsterHealth > 0));

        public string LocationLoot(NodeState node)
        {
            locations.TryGetValue(node.Id, out var row);
            return node.Items.Count == 0 ? TimelineText("Timeline_UI_99") : string.Join("、", node.Items.Select(item =>
                (row != null && row.LootItemId == item ? row.LootName : TimelineText("Timeline_UI_RelicId", item))
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
                return TimelineText("Timeline_UI_Talents", actor.Talents.Health.Grade, actor.Talents.Attack.Grade, actor.Talents.Lifespan.Grade)
                    + TimelineText("Timeline_UI_StartingGear", WeaponName)
                    + TimelineText("Timeline_UI_CurrentAttack", actor.Attack, actor.CombatAttack)
                    + string.Join("\n", actor.Items.Where(id => simulation.Stories?.Config.Items.ContainsKey(id) == true)
                        .Select(id => simulation.Stories.Config.Items[id].Name + "：" + simulation.Stories.Config.Items[id].Description)) + "\n\n"
                    + string.Join("\n", cultivation.Levels.OrderBy(level => level.Level)
                        .Select(level => TimelineText("Timeline_UI_LevelExperience", level.Level, level.ExperienceToNext)));
            }
        }

        public string HistoryDetails => library.Count == 0 ? TimelineText("Timeline_UI_106")
            : string.Join("\n\n", library.Select(entry => (entry.Selected ? TimelineText("Timeline_UI_107") : TimelineText("Timeline_UI_108")) + entry.Name));
        public string LogDetails => simulation.Events.Count == 0 ? TimelineText("Timeline_UI_109")
            : string.Join("\n", simulation.Events.Reverse().Take(60).Select(FormatEvent));

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
