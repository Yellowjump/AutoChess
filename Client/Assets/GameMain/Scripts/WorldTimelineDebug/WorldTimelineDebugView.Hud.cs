using System;
using System.Linq;
using UnityEngine;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        public static Action<WorldTimelineDebugView> OpenMapHud { get; set; }
        public event Action Closed;
        public bool UsesPrefabHud { get; set; }
        public bool IsPreparing => preparing;
        public bool IsBusy => advancing;
        public int SelectedNodeId => selectedNode;
        public NodeState SelectedLocation => simulation.State.Nodes[selectedNode];
        public string PlayerName => startingCultivator?.Name ?? "本世修士";
        public string WeaponName => startingCultivator?.Weapon ?? "初始武器";
        public string Notice => saveIssue ?? message;
        public bool AutoObserving => autoObserve;
        public int ObservationSpeed => yearsPerSecond;
        public string CurrentStatus => StatusText();

        public string LocationName(NodeState node) => locations.TryGetValue(node.Id, out var row)
            ? row.Name : node.Id == simulation.State.Ritual.NodeId ? "最终祭坛"
            : node.MonsterHealth > 0 ? "妖兽驻地" : "山野";

        public string LocationKind(NodeState node)
        {
            if (node.Id == simulation.State.Ritual.NodeId) return "最终关卡";
            if (node.MonsterHealth > 0) return "战斗关卡";
            if (locations.TryGetValue(node.Id, out var row) && row.MonsterHealth > 0) return "守怪已清除";
            if (node.Items.Count > 0) return "遗宝关卡";
            return node.CultivationPerYear >= 8 ? "修炼福地" : "探索地点";
        }

        public string LocationLoot(NodeState node)
        {
            locations.TryGetValue(node.Id, out var row);
            return node.Items.Count == 0 ? "暂无可领取宝物" : string.Join("、", node.Items.Select(item =>
                row != null && row.LootItemId == item ? row.LootName + $"  攻击 +{node.LootAttackBonus}" : "遗物 " + item));
        }

        public bool CanMoveSelected => !advancing && simulation.WaitingForPlayer &&
            simulation.State.Actors[simulation.State.PlayerActorId].NodeId.HasValue &&
            simulation.State.Nodes[simulation.State.Actors[simulation.State.PlayerActorId].NodeId.Value].Links.Contains(selectedNode);

        public string LocationHint
        {
            get
            {
                var world = simulation.State;
                if (selectedNode == world.Ritual.NodeId) return "击败守关者可重启灭世仪式，挑战者将承受反噬。";
                if (SelectedLocation.MonsterHealth > 0) return "抵达后自动进入战斗。清除守怪可以改变后来者的命运。";
                if (SelectedLocation.Items.Count > 0) return "先到者取得宝物，后来者将面对取走宝物后的地点。";
                return "驻留修炼可以积累经验，所有角色的时间会一同流逝。";
            }
        }

        public string CultivationDetails
        {
            get
            {
                var actor = simulation.State.Actors[simulation.State.PlayerActorId];
                return $"生命资质 {actor.Talents.Health.Grade}    攻击资质 {actor.Talents.Attack.Grade}    寿元资质 {actor.Talents.Lifespan.Grade}\n"
                    + $"初始装备  {WeaponName}\n\n"
                    + string.Join("\n", cultivation.Levels.OrderBy(level => level.Level)
                        .Select(level => $"第 {level.Level} 层    突破所需经验 {level.ExperienceToNext}"));
            }
        }

        public string HistoryDetails => library.Count == 0 ? "尚无前世记录。完成本局后，路线会保存供下一局选择。"
            : string.Join("\n\n", library.Select(entry => (entry.Selected ? "本局登场  " : "未登场  ") + entry.Name));
        public string LogDetails => simulation.Events.Count == 0 ? "山河初定，等待你的第一步。"
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
