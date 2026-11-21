using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        public Func<IEnumerator> TeamRewardPresenter { get; set; }
        public Func<IEnumerator> EquipmentServicePresenter { get; set; }
        private readonly List<CheckpointAction> replayEquipmentServices=new List<CheckpointAction>();
        private int equipmentServiceNumber;
        public bool EquipmentServiceOpen { get; private set; }
        public bool EquipmentServiceMerchant { get; private set; }
        public bool EventUpgradeAvailable { get; private set; }
        public string EquipmentServiceTitle { get; private set; }
        private readonly List<CheckpointAction> replayTeamRewards=new List<CheckpointAction>();
        private readonly HashSet<int> teamRewardNodes=new HashSet<int>();
        public bool EquipTeamItem(int actor,int item)=>ChangeTeamEquipment(CheckpointAction.EquipTeamItem,actor,item);
        public bool UnequipTeamItem(int actor,int slot)=>ChangeTeamEquipment(CheckpointAction.UnequipTeamItem,actor,slot);
        public bool UseTeamItem(int actor,int item)=>ChangeTeamEquipment(CheckpointAction.UseTeamItem,actor,item);
        public bool EquipArtifact(int actor,int instance,int slot,bool transfer=false)=>slot>=0&&slot<ExpeditionEquipment.CommonSlots&&instance>0&&instance<=1000000&&
            ChangeTeamEquipment(transfer?CheckpointAction.TransferArtifact:CheckpointAction.EquipArtifactSixSlots,actor,instance*6+slot);
        public bool BuyArtifact(int definition)=>ChangeEquipmentService(CheckpointAction.BuyArtifact,definition);
        public bool UpgradeArtifact(int key)=>ChangeEquipmentService(EquipmentServiceMerchant?
            CheckpointAction.UpgradeArtifact:CheckpointAction.EventUpgrade,key);
        private bool ChangeEquipmentService(int kind,int target)
        {
            if(!EquipmentServiceOpen || !ApplyExpeditionCommand(kind,target,0))return false;
            RecordCollection();
            checkpoint?.actions.Add(new CheckpointAction(kind,target,0){atService=equipmentServiceNumber});
            return true;
        }
        private bool ApplyEventUpgrade(int key)
        {
            if(!EquipmentServiceOpen || EquipmentServiceMerchant || !EventUpgradeAvailable || !Expedition.Equipment.Upgrade(key,true))return false;
            EventUpgradeAvailable=false;return true;
        }
        public void CloseEquipmentService()=>EquipmentServiceOpen=false;
        private IEnumerator PresentEquipmentService(StoryEncounter encounter)
        {
            if(Expedition?.Equipment.Modern!=true || !encounter.Actor.IsAlive || simulation.State.PlayerControlEnded)yield break;
            bool merchant=encounter.Definition.IsMerchant;
            if(!merchant && !Expedition.Equipment.VisitEvent(encounter.Definition.NodeId))yield break;
            equipmentServiceNumber++;EquipmentServiceOpen=true;EquipmentServiceMerchant=merchant;
            EventUpgradeAvailable=!merchant;EquipmentServiceTitle=encounter.Definition.Title;
            try
            {
                if(IsRestoringCheckpoint)
                {
                    foreach(var step in replayEquipmentServices.Where(s=>s.atService==equipmentServiceNumber).ToArray())
                    {
                        if(!ApplyExpeditionCommand(step.kind,step.target,step.years))
                            throw new InvalidOperationException("商人购买或道具升级无法重演。");
                        replayEquipmentServices.Remove(step);
                    }
                }
                else if(EquipmentServicePresenter!=null)yield return EquipmentServicePresenter();
            }
            finally{EquipmentServiceOpen=false;EventUpgradeAvailable=false;}
        }
        private bool ChangeTeamEquipment(int kind,int actor,int item)
        {
            if(advancing || Expedition?.Equipment.Enabled!=true)return false;
            return ChangeExpedition(kind,actor,item);
        }
        public bool ClaimTeamReward(int option)
        {
            if(Expedition?.Equipment.Enabled!=true || !Expedition.Equipment.ClaimReward(option))return false;
            RecordCollection();
            checkpoint?.actions.Add(new CheckpointAction(CheckpointAction.ClaimTeamReward,option,0)
                {atBattle=expeditionBattleNumber});
            return true;
        }
        private IEnumerator PresentTeamReward(BattleSettlement result)
        {
            if(result.IsTreasure || !result.Victory || Expedition?.Equipment.Enabled!=true ||
                !simulation.State.Actors[Expedition.LeaderId].IsAlive)yield break;
            if (simulation.State.SharedClears)
            {
                if (!result.WonFirstClear || Expedition.Equipment.PendingRewards.Count==0) yield break;
            }
            else
            {
            var defeat=simulation.State.Nodes[result.NodeId].GuardianDefeat;
            if(!defeat.HasValue || defeat.Value.ActorId!=Expedition.LeaderId ||
                defeat.Value.Year!=simulation.State.Year || !teamRewardNodes.Add(result.NodeId))yield break;
            Expedition.Equipment.OfferReward(result.NodeId);
            }
            if(IsRestoringCheckpoint)
            {
                var step=replayTeamRewards.SingleOrDefault(s=>s.atBattle==expeditionBattleNumber);
                if(step==null || !Expedition.Equipment.ClaimReward(step.target))
                    throw new InvalidOperationException("战后奖励选择无法重演。");
                replayTeamRewards.Remove(step);
                yield break;
            }
            if(TeamRewardPresenter!=null)yield return TeamRewardPresenter();
            else while(Expedition.Equipment.PendingRewards.Count>0)yield return null;
        }
    }
}
