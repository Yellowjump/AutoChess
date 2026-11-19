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
        public Func<IReadOnlyList<int>, Action<int>, IEnumerator> SuccessorPresenter { get; set; }
        private readonly List<CheckpointAction> replaySuccessors = new List<CheckpointAction>();

        public Func<int,Action<bool>,IEnumerator> PastInvitationPresenter { get; set; }
        private readonly Queue<CheckpointAction> replayInvitations = new Queue<CheckpointAction>();
        private int? fallbackInvitation;
        private bool? fallbackInvitationChoice;

        public IEnumerable<(int Definition,int Level)> InvitationBag(int id)
        {
            var gear = simulation.State.ExpeditionFor(id).Equipment;
            return gear.Instances.Where(i => i.Owner != 0).Select(i => (i.DefinitionId,i.Level)).Concat(gear.DisbandedInnates());
        }

        private IEnumerator PresentPastInvitation(int id,Action<bool> decide)
        {
            if (IsRestoringCheckpoint)
            {
                if (replayInvitations.Count == 0) throw new InvalidOperationException("缺少前世相遇记录。");
                var step = replayInvitations.Dequeue();
                if (step.years != simulation.State.Year || step.target != id) throw new InvalidOperationException("前世相遇无法重演。");
                decide(step.accepted);
                yield break;
            }
            bool? choice = null;
            if (PastInvitationPresenter != null)
                yield return PastInvitationPresenter(id,value => choice = value);
            else
            {
                fallbackInvitation = id;
                fallbackInvitationChoice = null;
                try { while (!fallbackInvitationChoice.HasValue) yield return null; choice = fallbackInvitationChoice; }
                finally { fallbackInvitation = null; }
            }
            if (!choice.HasValue) throw new InvalidOperationException("缺少相遇选择。");
            checkpoint?.actions.Add(new CheckpointAction(CheckpointAction.PastInvitationChoice,id,simulation.State.Year)
                { accepted = choice.Value });
            decide(choice.Value);
        }

        private IReadOnlyList<int> fallbackCandidates;
        private int fallbackChoice;
        private IEnumerator PresentSuccessor(IReadOnlyList<int> candidates, Action<int> choose)
        {
            if (IsRestoringCheckpoint)
            {
                var step = replaySuccessors.Single(s => s.atBattle == expeditionBattleNumber);
                if (!candidates.Contains(step.target)) throw new InvalidOperationException("接任队长无法重演。");
                choose(step.target);
                replaySuccessors.Remove(step);
                yield break;
            }
            int role = -1;
            if (SuccessorPresenter != null) yield return SuccessorPresenter(candidates, selected => role = selected);
            else
            {
                fallbackChoice = -1;
                fallbackCandidates = candidates;
                try { while (fallbackChoice < 0) yield return null; role = fallbackChoice; }
                finally { fallbackCandidates = null; }
            }
            if (!candidates.Contains(role)) throw new InvalidOperationException("无效的队长选择。");
            checkpoint?.actions.Add(new CheckpointAction(CheckpointAction.SelectSuccessor, role, 0) { atBattle = expeditionBattleNumber });
            choose(role);
        }

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
        internal bool AddPastLeaderToRoster(int actorId) => !advancing && ChangeExpedition(CheckpointAction.AddPastLeader, actorId);
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
            if (kind == CheckpointAction.EnableAttrition) { Expedition.EnableAttrition(); return true; }
            if (kind == CheckpointAction.EnableMapProgression) { EnableMapProgression(); return true; }
            if (kind == CheckpointAction.EnableCompanionStages) { EnableCompanionStages(); return true; }
            if (kind == CheckpointAction.EnableVanguard) { EnableVanguard(); return true; }
            if(kind==CheckpointAction.EnableTeamEquipment){Expedition.Equipment.Enable();return true;}
            if(kind==CheckpointAction.EnablePastInvitations){simulation.EnablePastInvitations();return true;}
            if(kind==CheckpointAction.EnableHistoricalGrowth){simulation.EnableHistoricalGrowth();return true;}
            if(kind==CheckpointAction.EnableSharedClears){simulation.EnableSharedClears();return true;}
            if(kind==CheckpointAction.EnableRainTassel){simulation.State.EnableRainTassel();return true;}
            if(kind==CheckpointAction.EnableRelayClasp){simulation.State.EnableRelayClasp();return true;}
            if(kind==CheckpointAction.EnableReprisalSeal){simulation.State.EnableReprisalSeal();return true;}
            if(kind==CheckpointAction.EnableHeartLamp){simulation.State.EnableHeartLamp();return true;}
            if(kind==CheckpointAction.EnableTinderPouch){simulation.State.EnableTinderPouch();return true;}
            if(kind==CheckpointAction.EnableMeridianSpike){simulation.State.EnableMeridianSpike();return true;}
            if(kind==CheckpointAction.EnableThunderBell){simulation.State.EnableThunderBell();return true;}
            if(kind==CheckpointAction.EnableFalterMirror){simulation.State.EnableFalterMirror();return true;}
            if(kind==CheckpointAction.EnableLastStandCharm){simulation.State.EnableLastStandCharm();return true;}
            if(kind==CheckpointAction.EnableAfflictionSeal){simulation.State.EnableAfflictionSeal();return true;}
            if(kind==CheckpointAction.EnableSupportArtifacts){simulation.State.EnableSupportArtifacts();return true;}
            if(kind==CheckpointAction.EnableCriticalArtifacts){simulation.State.EnableCriticalArtifacts();return true;}
            if(kind==CheckpointAction.EnableQuakeSpike){simulation.State.EnableQuakeSpike();return true;}
            if(kind==CheckpointAction.EnableSwordTraining){simulation.State.EnableSwordTraining();return true;}
            if(kind==CheckpointAction.EnableSignatureArtifacts){Expedition.EnableSignatureArtifacts();return true;}
            if(kind==CheckpointAction.EnableArtifactLinks){Expedition.Equipment.EnableLinks();return Expedition.Equipment.LinksEnabled;}
            if(kind==CheckpointAction.EnableArtifacts){Expedition.Equipment.EnableArtifacts();return true;}
            return kind switch
            {
                CheckpointAction.AddPastLeader => simulation.TryAddPastLeaderToRoster(target),
                CheckpointAction.RecruitCompanion => Expedition.Recruit(target),
                CheckpointAction.PlaceCompanion => Expedition.Place(target, cell),
                CheckpointAction.BenchCompanion => Expedition.Bench(target),
                CheckpointAction.EquipTeamItem => Expedition.Equipment.Equip(target,cell),
                CheckpointAction.UnequipTeamItem => Expedition.Equipment.Unequip(target,cell),
                CheckpointAction.UseTeamItem => Expedition.Equipment.Use(target,cell),
                CheckpointAction.ClaimTeamReward => Expedition.Equipment.ClaimReward(target),
                CheckpointAction.EquipArtifact => Expedition.Equipment.EquipInstance(target,cell/3,cell%3),
                CheckpointAction.EquipArtifactSixSlots => Expedition.Equipment.EquipInstance(target,cell/6,cell%6),
                CheckpointAction.TransferArtifact => Expedition.Equipment.EquipInstance(target,cell/6,cell%6,true),
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
                if (Expedition.IsFallen(0)) return "岩舟已阵亡。请按现有同伴的位置调整阵型。";
                int shield = Expedition.CellOf(Expedition.FieldId(0));
                if (Expedition.LeaderRole == 0) return "岩舟已接任队长。可将他放在前排，保护后排同伴。";
                int leader = Expedition.CellOf(simulation.State.PlayerActorId);
                if (shield < 0) return "岩舟尚未上阵。先选择岩舟，再点前排空位。";
                if (shield >= 3) return "岩舟在后排，主角容易先接敌。建议将岩舟移到前排中央。";
                if (leader < 3) return "主角也在前排，可能分担敌人的攻击。可退到后排，由岩舟先接敌。";
                return "盾卫在前、主角在后：岩舟先接敌，利用护盾和牵制保护主角。";
            }
        }

        public string MapGrowthRules => "关卡首通由当世与前世队伍争夺。首通探索历练为升级所需的20%，击败守军另得至少50%，全队保留战后气血，战死者永久离队。\n往返已探索地点、原地等待不提供经验。队友随主角成长。";

        public string ClearOwner(int id)
        {
            if (id == simulation.State.PlayerActorId) return "当世队伍";
            if (id == simulation.State.ChosenActorId) return "天命人";
            var actor = simulation.State.Actors[id];
            return "前世" + (roster.FirstOrDefault(row => row.Birth == actor.BirthNodeId)?.Name ?? "队伍");
        }

        public string ExplorationReward(NodeState node)
        {
            if (Expedition?.ProgressionEnabled != true) return "";
            var clear = node.FirstClear;
            if (clear.HasValue)
                return $"第{clear.Value.Year}年 · {ClearOwner(clear.Value.ActorId)}首通";
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
            ExpeditionParty.SandSite => "玄砂 · 镇砂杵", ExpeditionParty.TideSite => "闻潮 · 听潮剑",
            >= ExpeditionParty.StageRoles when Expedition.NamedSite(role) >= 0 => CompanionName(Expedition.NamedSite(role)),
            0 => "岩舟 · 盾卫", 1 => "陆离 · 剑客", 2 => "阿翎 · 弓手", 3 => "桑宁 · 医者",
            >= ExpeditionParty.StageRoles => PastName(Expedition, role) + " · 前世主角", _ => OriginalPlayerName
        };

        public string PastName(ExpeditionParty party, int role) =>
            role < party.RoleCount ? roster.FirstOrDefault(row => row.Birth == party.PastBirth(role))?.Name ?? "前世修士" : "前世修士";

        public int PastPortraitIndex(ExpeditionParty party, int role) =>
            role < ExpeditionParty.StageRoles || role >= party.RoleCount ? -1 :
            roster.FindIndex(row => row.Birth == party.PastBirth(role));

        public string RecruitmentAt(int nodeId)
        {
            if (Expedition == null || simulation.State.Nodes[nodeId].CompanionStageRole is not int role) return "";
            if (Expedition.IsFallen(role)) return "此同伴已阵亡，本世无法再次招募。";
            return Expedition.HasRecruited(role) ? "同伴已招募，可在战前选择上阵、拖动布阵。"
                : "抵达此地并满足条件后，在本关卡招募同伴。";
        }

        public string RecruitmentConditions(int role)
        {
            if (role == ExpeditionParty.SandSite || role == ExpeditionParty.TideSite)
                return (role == ExpeditionParty.SandSite ? "上阵替队友承担15%盾后伤害" : "上阵将溢出治疗化为护盾") + "\n抵达此地，清除守军后可邀请";
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
