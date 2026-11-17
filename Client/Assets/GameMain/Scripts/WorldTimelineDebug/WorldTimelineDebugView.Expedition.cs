using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        public Func<int, IEnumerator> BattleFormationPresenter { get; set; }
        public bool ExpeditionPanelOpen { get; set; }
        public bool PreparingExpeditionBattle { get; private set; }
        public ExpeditionParty Expedition => simulation?.State.Expedition;
        private int expeditionBattleNumber;
        private readonly List<CheckpointAction> replayFormations = new List<CheckpointAction>();

        private IEnumerator PresentExpeditionFormation(int nodeId)
        {
            if (Expedition == null) yield break;
            expeditionBattleNumber++;
            if (IsRestoringCheckpoint)
            {
                foreach (var step in replayFormations.Where(step => step.atBattle == expeditionBattleNumber).ToArray())
                {
                    if (!ApplyExpeditionCommand(step.kind, step.target, step.years))
                        throw new InvalidOperationException("战前阵型无法重演。");
                    replayFormations.Remove(step);
                }
                yield break;
            }
            if (BattleFormationPresenter == null) yield break;
            PreparingExpeditionBattle = true;
            try { yield return BattleFormationPresenter(nodeId); }
            finally { PreparingExpeditionBattle = false; }
        }

        public bool RecruitCompanion(int role) => ChangeExpedition(CheckpointAction.RecruitCompanion, role);
        public bool PlaceCompanion(int actorId, int cell) => ChangeExpedition(CheckpointAction.PlaceCompanion, actorId, cell);
        public bool BenchCompanion(int actorId) => ChangeExpedition(CheckpointAction.BenchCompanion, actorId);

        private bool ChangeExpedition(int kind, int target, int cell = 0)
        {
            if (Expedition == null || preparing || simulation.State.IsEnded || simulation.State.PlayerControlEnded ||
                (advancing ? !PreparingExpeditionBattle : !simulation.WaitingForPlayer)) return false;
            if (kind == CheckpointAction.RecruitCompanion && advancing) return false;
            CancelLanternForecast();
            if (!ApplyExpeditionCommand(kind, target, cell)) return false;
            if (checkpoint != null)
            {
                checkpoint.actions.Add(new CheckpointAction(kind, target, cell)
                    { atBattle = PreparingExpeditionBattle ? expeditionBattleNumber : 0 });
                // A battle's formation changes precede the movement command in the replay log.
                // Commit all of them together after settlement, never save a half-finished year.
                if (!advancing) SaveCheckpoint();
            }
            return true;
        }

        private bool ApplyExpeditionCommand(int kind, int target, int cell)
        {
            if (kind == CheckpointAction.EnableExpedition)
            { simulation.State.EnableExpedition(); return true; }
            if (Expedition == null) return false;
            if (kind == CheckpointAction.EnableMapProgression) { EnableMapProgression(); return true; }
            if (kind == CheckpointAction.EnableCompanionStages) { EnableCompanionStages(); return true; }
            if (kind == CheckpointAction.EnableVanguard) { EnableVanguard(); return true; }
            if(kind==CheckpointAction.EnableTeamEquipment){Expedition.Equipment.Enable();return true;}
            if(kind==CheckpointAction.EnableArtifacts){Expedition.Equipment.EnableArtifacts();return true;}
            return kind switch
            {
                CheckpointAction.RecruitCompanion => Expedition.Recruit(target),
                CheckpointAction.PlaceCompanion => Expedition.Place(target, cell),
                CheckpointAction.BenchCompanion => Expedition.Bench(target),
                CheckpointAction.EquipTeamItem => Expedition.Equipment.Equip(target,cell),
                CheckpointAction.UnequipTeamItem => Expedition.Equipment.Unequip(target,cell),
                CheckpointAction.UseTeamItem => Expedition.Equipment.Use(target,cell),
                CheckpointAction.ClaimTeamReward => Expedition.Equipment.ClaimReward(target),
                CheckpointAction.EquipArtifact => Expedition.Equipment.EquipInstance(target,cell/3,cell%3),
                CheckpointAction.EquipArtifactSixSlots => Expedition.Equipment.EquipInstance(target,cell/6,cell%6),
                CheckpointAction.BuyArtifact => EquipmentServiceOpen && EquipmentServiceMerchant && Expedition.Equipment.Buy(target),
                CheckpointAction.UpgradeArtifact => EquipmentServiceOpen && EquipmentServiceMerchant && Expedition.Equipment.Upgrade(target),
                CheckpointAction.EventUpgrade => ApplyEventUpgrade(target),
                _ => false
            };
        }

        private void EnableMapProgression() => Expedition.EnableProgression(
            simulation.Events.Where(entry => entry.Kind == WorldEventKind.Arrived &&
                entry.ActorId == simulation.State.PlayerActorId && entry.NodeId.HasValue).Select(entry => entry.NodeId.Value)
                .Append(simulation.RecordPlayerLife().BirthNodeId));

        private void EnableCompanionStages() => Expedition.EnableCompanionStages(
            simulation.State.Nodes.Keys.Where(id => simulation.Stories?.TryGetDefinition(id, out _) == true));

        private void EnableVanguard() => Expedition.EnableVanguard(
            simulation.State.Nodes.Keys.Where(id => simulation.Stories?.TryGetDefinition(id, out _) == true));

        public int? PendingVanguard => Expedition?.HasRecruited(0) == true && Expedition.VanguardNodeId is int id &&
            simulation.State.Nodes[id].MonsterHealth > 0 ? id : (int?)null;

        public bool FocusVanguard()
        {
            if (advancing || PendingVanguard is not int node) return false;
            selectedNode = node;
            if (board != null)
            {
                board.SelectNode(node);
                var location = simulation.State.Nodes[node];
                board.Focus(location.Q, location.R);
            }
            return true;
        }

        public string VanguardFormationHint
        {
            get
            {
                int shield = Expedition.CellOf(ExpeditionParty.FirstCompanionId);
                int leader = Expedition.CellOf(simulation.State.PlayerActorId);
                if (shield < 0) return "岩舟尚未上阵。先选择岩舟，再点前排空位。";
                if (shield >= 3) return "岩舟在后排，主角容易先接敌。建议将岩舟移到前排中央。";
                if (leader < 3) return "主角也在前排，可能分担敌人的攻击。可退到后排，由岩舟先接敌。";
                return "盾卫在前、主角在后：岩舟先接敌，利用护盾和牵制保护主角。";
            }
        }

        public string MapGrowthRules => "首次抵达新地点获得升级所需经验的20%；战斗胜利至少获得50%，并恢复25%最大气血。\n往返已探索地点、原地等待不提供经验。队友随主角成长。";

        public string ExplorationReward(NodeState node)
        {
            if (Expedition?.ProgressionEnabled != true) return "";
            return Expedition.HasExplored(node.Id) ? "已探索 · 不重复发放历练"
                : $"首次抵达并清场：历练 +{Expedition.ExplorationExperience(simulation.Cultivation)}";
        }

        public bool FocusRecruitmentSite(int role)
        {
            if (advancing || Expedition == null || !Expedition.RecruitmentSites.TryGetValue(role, out int node)) return false;
            selectedNode = node;
            if (board != null)
            {
                board.SelectNode(node);
                var location = simulation.State.Nodes[node];
                board.Focus(location.Q, location.R);
            }
            return true;
        }

        public string CompanionName(int role) => role switch
        {
            0 => "岩舟 · 盾卫", 1 => "陆离 · 剑客", 2 => "阿翎 · 弓手", 3 => "桑宁 · 医者", _ => PlayerName
        };

        public string RecruitmentAt(int nodeId)
        {
            if (Expedition == null || simulation.State.Nodes[nodeId].CompanionStageRole is not int role) return "";
            return Expedition.HasRecruited(role) ? "同伴已招募，可在战前选择上阵、拖动布阵。"
                : "抵达此地并满足条件后，在本关卡招募同伴。";
        }

        public string RecruitmentConditions(int role)
        {
            string requirement = Expedition.RecruitmentUsesVictories(role) ? "亲自击败守军" : "探索新地点";
            int target = Expedition.RecruitmentTarget(role);
            int progress = Math.Min(target, Expedition.RecruitmentProgress(role));
            int site = Expedition.RecruitmentSites[role];
            bool arrived = simulation.State.Actors[simulation.State.PlayerActorId].NodeId == site;
            return $"{requirement}  {progress}/{target}\n" + (arrived ? "已抵达此关卡" : "尚未抵达此关卡") +
                (simulation.State.Nodes[site].MonsterHealth > 0 ? " · 须清除守军" : "");
        }

        private string LocalRecruitmentNotice()
        {
            if (Expedition == null || !simulation.WaitingForPlayer) return null;
            var available = Expedition.RecruitmentSites.Where(pair => Expedition.CanRecruit(pair.Key)).Select(pair => pair.Key).ToArray();
            return available.Length == 0 ? null : CompanionName(available[0]) + "的招募条件已满足。点击当前地图节点，在同伴关卡中招募。";
        }
    }
}
