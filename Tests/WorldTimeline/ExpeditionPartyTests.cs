using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WorldTimeline;

internal static partial class WorldSimulationTests
{
    private static void RunExpeditionTests()
    {
        Run("Innate artifacts are fixed and six universal slots accept independent copies",()=>
        {
            var sim=ExpeditionFixture();var party=sim.State.Expedition;party.Recruit(0);var gear=party.Equipment;gear.EnableArtifacts();
            int shield=party.CompanionId(0);var leader=sim.State.Actors[1];int originalHp=leader.Health;
            Check(gear.Innate(1).Id==1 && gear.Innate(shield).Id==3 && !gear.Buy(1) && !gear.Equip(shield,1),"Fixed artifacts cannot enter ordinary slots or shop stock");
            leader.AwardLegacySpiritStones(200);
            for(int i=1;i<ExpeditionEquipment.CommonSlots;i++)Check(gear.Buy(5),"Multiple copies of one item can be purchased");
            var robes=gear.Instances.Where(i=>i.DefinitionId==5).ToArray();
            for(int s=0;s<ExpeditionEquipment.CommonSlots;s++)Check(gear.EquipInstance(shield,robes[s].Id,s),"All six slots accept the same item type on any role");
            Check(!gear.EquipInstance(shield,gear.Instances.First(i=>i.Owner<0).Id,6),"Seventh ordinary slot does not exist");
            Check(!gear.EquipInstance(1,robes[0].Id,0),"One instance cannot be worn by two actors");
            Check(gear.Upgrade(robes[0].Id,true)&&robes[0].Level==2&&robes[1].Level==1,"Upgrading a copy leaves identical copies unchanged");
            Check(gear.Unequip(shield,0)&&gear.EquipInstance(1,robes[0].Id,2)&&robes[0].Level==2&&leader.Health==originalHp,"Transfers retain level and do not heal");
            Check(gear.Innate(shield).Id==3&&gear.InnateLevel(shield)==1,"Removing ordinary gear cannot remove the fixed artifact");
        });
        Run("Table prices, upgrade caps and event rewards reject duplicate or unpaid operations",()=>
        {
            var sim=ExpeditionFixture();var gear=sim.State.Expedition.Equipment;gear.EnableArtifacts();
            Check(!gear.Buy(5)&&!gear.Upgrade(-1),"No purchase or paid upgrade succeeds without funds");
            Check(gear.VisitEvent(2)&&!gear.VisitEvent(2)&&gear.SpiritStones==12,"Event pays currency once per node");
            int fee=ExpeditionEquipment.Definition(1).UpgradePrice(1);
            Check(gear.Upgrade(-1)&&gear.SpiritStones==12-fee&&gear.InnateLevel(1)==2,"Upgrade charges exactly its table price");
            while(gear.CanUpgrade(-1))Check(gear.Upgrade(-1,true),"Upgrade within cap");
            Check(!gear.Upgrade(-1,true)&&gear.InnateLevel(1)==ExpeditionEquipment.Definition(1).MaxLevel,"Innate level cap rejects further upgrades");
            sim.State.Actors[1].AwardLegacySpiritStones(100);long before=gear.SpiritStones;
            Check(gear.Buy(6)&&gear.SpiritStones==before-ExpeditionEquipment.Definition(6).Price,"Purchase charges table price");
            var item=gear.Instances.Last();while(gear.CanUpgrade(item.Id))gear.Upgrade(item.Id,true);
            before=gear.SpiritStones;Check(!gear.Upgrade(item.Id)&&gear.SpiritStones==before,"A capped item never charges currency");
            gear.OfferReward(1);Check(gear.PendingRewards.All(id=>!ExpeditionEquipment.Definition(id).Innate),"Battle rewards never replace innate artifacts");
        });
        Run("Upgraded loadouts and event claims survive binary history actor remapping",()=>
        {
            var sim=ExpeditionFixture();var party=sim.State.Expedition;party.Recruit(0);var gear=party.Equipment;gear.EnableArtifacts();
            gear.VisitEvent(2);gear.Upgrade(-2,true);var robe=gear.Instances.First();gear.Upgrade(robe.Id,true);gear.EquipInstance(party.CompanionId(0),robe.Id,5);
            gear.EquipInstance(party.LeaderId,gear.Instances.Last().Id,3);
            gear.EquipInstance(party.CompanionId(0),gear.Instances[1].Id,0);
            var life=WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            var replay=ExpeditionFixture();replay.AddHistory(life,10);var history=replay.State.ExpeditionFor(10);
            Check(history.Equipment.Modern&&history.Equipment.InnateLevel(history.CompanionId(0))==2&&history.Equipment.Worn(history.CompanionId(0),5).Level==2&&
                history.Equipment.Worn(10,3)!=null&&history.Equipment.Worn(history.CompanionId(0),0)!=null,
                "Sixth slot, cross-role loadouts and both fixed and ordinary levels survive archive");
            Check(history.Equipment.HasVisitedEvent(2)&&history.CreateCompanion(0,1).MaxHealth==party.CreateCompanion(0,1).MaxHealth,"Event claim and upgraded combat stats survive remapping");
            var malformed=gear.Snapshot();malformed[3]=999;
            bool rejected=false;try{ExpeditionEquipment.ValidateSnapshot(malformed);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Invalid innate levels cannot enter save data");
        });
        Run("Map battles pay spirit stones and modern merchants route into equipment services",()=>
        {
            var sim=ExpeditionFixture(new RecordedPartyResolver());var gear=sim.State.Expedition.Equipment;gear.EnableArtifacts();
            sim.ConfigureStories(new StoryConfig(new[]{new StoryDefinition{Id=1,NodeId=2,IsMerchant=true,Repeatable=true},
                new StoryDefinition{Id=2,NodeId=3}},new[]{new StoryOption{Id=1,EventId=1,Text="离开"},
                new StoryOption{Id=2,EventId=2,Text="继续"}},Array.Empty<StoryItem>()));
            int merchants=0,events=0;sim.Stories.ExpeditionService=Service;
            sim.State.Nodes[2].MonsterHealth=10;sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(gear.SpiritStones>=8&&merchants==1,"Victory pays currency and merchant opens its equipment service");
            long before=gear.SpiritStones;sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(events==1&&gear.SpiritStones==before+12,"Resolved map event opens its upgrade service and pays currency");
            IEnumerator Service(StoryEncounter c)
            {if(c.Definition.IsMerchant)merchants++;else{events++;gear.VisitEvent(c.Definition.NodeId);}yield break;}
        });
        Run("Equipment is exclusive, profession checked and removable without healing", () =>
        {
            var sim=ExpeditionFixture();var party=sim.State.Expedition;party.Recruit(0);party.Equipment.Enable();
            var gear=party.Equipment;var player=sim.State.Actors[1];
            int shield=party.CompanionId(0), baseShield=party.CreateCompanion(0,1).Attack, originalAttack=player.Attack;
            Check(gear.Equip(1,1) && player.Attack==originalAttack+8,"A carried sword changes only its wearer");
            Check(party.CreateCompanion(0,1).Attack==baseShield,"Leader gear cannot multiply into every companion");
            Check(!gear.Equip(shield,1) && gear.Count(1)==0,"Wrong profession cannot duplicate or steal equipped gear");
            Check(gear.Equip(shield,3) && party.CreateCompanion(0,1).MaxHealth==280,"Shield applies to its companion");
            Check(gear.Unequip(shield,0) && gear.Count(3)==1 && !gear.Unequip(shield,0),"Unequip returns one copy exactly once");
            int health=player.Health;
            Check(gear.Equip(1,5) && gear.Unequip(1,1) && player.Health==health,"Armor cycling does not heal");
            gear.OfferReward(2);gear.ClaimReward(1);
            int max=player.MaxHealth, robes=gear.Count(5);
            Check(gear.Equip(1,5) && gear.Equip(1,6) && gear.Count(5)==robes &&
                player.MaxHealth==max+110 && player.Health==health,"Armor replacement returns the previous copy and applies only the new bonus");
            Check(gear.Unequip(1,1) && player.MaxHealth==max,"Replacement armor can be removed");
            Check(gear.Use(shield,9) && gear.Count(9)==1 && party.CreateCompanion(0,1).MaxHealth==200,"Consumables permanently improve the selected companion");
        });
        Run("Three rewards are stable, distinct and exactly one can be claimed", () =>
        {
            var party=ExpeditionFixture().State.Expedition;party.Equipment.Enable();var gear=party.Equipment;
            var choices=gear.OfferReward(2).ToArray();int count=gear.Count(choices[1]);
            Check(choices.Length==3 && choices.Distinct().Count()==3 && choices.SequenceEqual(gear.OfferReward(999)),"Pending choices cannot be rerolled");
            Check(!gear.ClaimReward(-1) && gear.ClaimReward(1),"A valid option is required");
            Check(gear.Count(choices[1])==count+1 && gear.RewardsTaken==1 && !gear.ClaimReward(2),"Only the selected reward is granted once");
        });
        Run("Personal equipment and enhancements survive binary history remapping", () =>
        {
            var source=ExpeditionFixture();var party=source.State.Expedition;party.Recruit(0);party.Equipment.Enable();
            party.Equipment.Equip(1,1);party.Equipment.Equip(party.CompanionId(0),3);party.Equipment.Use(party.CompanionId(0),9);
            var life=WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,source.RecordPlayerLife())})).Single().Record;
            var replay=ExpeditionFixture();replay.AddHistory(life,10);var history=replay.State.ExpeditionFor(10);
            Check(history.Equipment.Equipped(10,0)==1 && history.Equipment.Equipped(history.CompanionId(0),0)==3,"Historical actor IDs retain the role's loadout");
            Check(history.CreateCompanion(0,1).MaxHealth==party.CreateCompanion(0,1).MaxHealth &&
                replay.State.Actors[10].Attack==source.State.Actors[1].Attack,"Archived equipment has the same combat effect");
        });
        Run("Archived expedition restores recruitment, battle positions and independent companion identities", () =>
        {
            var native = new RecordedPartyResolver();
            var source = ExpeditionFixture(native);
            var party = source.State.Expedition;
            party.EnableProgression(); party.Recruit(0);
            source.SubmitPlayerAction(WorldAction.Move(2)); party.Recruit(2);
            source.State.Nodes[3].MonsterHealth = 50;
            source.SetBattlePreparationPresenter(_ => Arrange());
            source.SubmitPlayerAction(WorldAction.Move(3));
            var life = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[] { new SavedLife(1, source.RecordPlayerLife()) })).Single().Record;
            Check(life.Expedition.Any(r => r.Battle == 1 && r.NodeId == 3 && r.Cells[0] == 0), "Battle formation survives binary archive");
            int leaderHealth = source.State.Actors[1].Health;
            var replayNative = new RecordedPartyResolver();
            var replay = ExpeditionFixture(replayNative);
            replay.State.Actors[1].NodeId = 5;
            replay.State.Nodes[3].MonsterHealth = 50;
            replay.AddHistory(life, 10);
            var history = replay.State.ExpeditionFor(10);
            Check(history.HasRecruited(0) && !history.HasRecruited(2), "Later recruitment is not granted at birth");
            replay.SetBattlePreparationPresenter(_ => throw new Exception("Historical battles must never ask for input"));
            replay.SubmitPlayerAction(WorldAction.Wait(2));
            Check(history.HasRecruited(2) && history.DeployedCount == 3 && replayNative.LastCount == 3, "Historical team automatically enters real encounter");
            Check(history.Cells[0] == history.CompanionId(0) && history.Cells[5] == history.CompanionId(2) &&
                history.CellOf(10) == 4, "Recorded roles remap to historical identities and exact cells");
            Check(history.CompanionId(0) != party.CompanionId(0) && !replay.State.Expedition.Owns(history.CompanionId(0)), "Current team cannot control the historical shield");
            Check(replay.State.Actors[10].Health == leaderHealth && replay.State.Nodes[3].MonsterHealth == 0,
                "History receives the same map growth and battle settlement");
            IEnumerator Arrange() { party.Place(party.CompanionId(0), 0); party.Place(party.CompanionId(2), 5); yield break; }
        });
        Run("Changed history recalculates combat instead of restoring a recorded win", () =>
        {
            var source = ExpeditionFixture(new RecordedPartyResolver()); source.State.Expedition.Recruit(0);
            source.State.Nodes[2].MonsterHealth = 10; source.SubmitPlayerAction(WorldAction.Move(2));
            var life = source.RecordPlayerLife();
            var replay = ExpeditionFixture(new RecordedPartyResolver { Lose = true });
            replay.State.Actors[1].NodeId = 5; replay.State.Nodes[2].MonsterHealth = 999;
            replay.AddHistory(life, 10); replay.SubmitPlayerAction(WorldAction.Wait());
            Check(!replay.State.Actors[10].IsAlive && replay.State.Nodes[2].MonsterHealth == 999,
                "The same historical formation can lose against the new world's enemies");
        });
        Run("Historical battle uses its formation before later edits in the same year", () =>
        {
            var source = ExpeditionFixture(new RecordedPartyResolver());
            source.State.Expedition.Recruit(0);
            source.State.Nodes[2].MonsterHealth = 10;
            source.SubmitPlayerAction(WorldAction.Move(2));
            source.State.Expedition.Bench(source.State.Expedition.CompanionId(0));
            var resolver = new RecordedPartyResolver();
            var replay = ExpeditionFixture(resolver);
            replay.State.Actors[1].NodeId = 5; replay.State.Nodes[2].MonsterHealth = 10;
            replay.AddHistory(source.RecordPlayerLife(), 10);
            replay.SubmitPlayerAction(WorldAction.Wait());
            Check(resolver.LastCount == 2, "Post-battle bench edit must not remove the shield from the archived battle");
        });
        Run("Battle preparation bench edits are reflected in fallback damage settlement", () =>
        {
            var sim = ExpeditionFixture();
            sim.State.Expedition.Recruit(0); sim.State.Nodes[2].MonsterHealth = 1;
            sim.SetBattlePreparationPresenter(_ => Bench());
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(sim.State.Actors[1].Health == 100, "A first-strike victory cannot take damage from a benched companion's removed health");
            IEnumerator Bench() { sim.State.Expedition.Bench(sim.State.Expedition.CompanionId(0)); yield break; }
        });
        Run("Invalid archived formations reject duplicate members and more than three deployed", () =>
        {
            bool rejected = false;
            try { new RecordedExpedition(1, 1, 0, false, false, new[] { 0, 1, 2 }, new[] { 0, 1, 2, -1, -2, -2 }); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Deployment limits are validated on archive input");
        });
        Run("First tactical encounter preserves existing nodes and activates deterministically once", () =>
        {
            var sim = ExpeditionFixture();
            for (int id = 6; id <= 8; id++)
            {
                sim.State.Nodes.Add(id, new NodeState(id));
                sim.State.Nodes[id - 1].Links.Add(id); sim.State.Nodes[id].Links.Add(id - 1);
            }
            var party = sim.State.Expedition;
            party.EnableProgression(); party.EnableCompanionStages();
            sim.State.Nodes[7].GuardianDefeat = (99, 0);
            party.EnableVanguard(new[] { 6 });
            Check(sim.State.Nodes[2].MonsterHealth == 0, "The safe road to recruit the shield stays open");
            Check(party.VanguardEnabled && party.VanguardNodeId == 8, "Uses a reachable unused site; preserves stories and cleared encounters");
            var node = sim.State.Nodes[8];
            Check(node.IsExpeditionVanguard && node.GuardianCount == 2 && node.MonsterHealth == 200 && node.MonsterAttack == 16,
                "First encounter has two close-range guardians with explicit stats");
            node.MonsterHealth = 0;
            party.EnableVanguard();
            Check(node.MonsterHealth == 0 && party.VanguardNodeId == 8, "Activation cannot respawn a cleared encounter");
        });
        Run("Companion stages gate recruitment by exploration, personal victories and arrival", () =>
        {
            var sim = ExpeditionFixture();
            var party = sim.State.Expedition;
            party.EnableProgression(); party.EnableCompanionStages();
            Check(party.RecruitmentSites.Count == 4 && party.RecruitmentSites.Values.Distinct().Count() == 4 &&
                !party.RecruitmentSites.Values.Contains(1), "Four distinct forward stages, none at spawn");
            Check(party.RecruitmentSites.All(pair => sim.State.Nodes[pair.Value].CompanionStageRole == pair.Key), "Nodes carry explicit companion type");
            Check(!party.Recruit(0), "No immediate or remote recruitment");
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(!party.RecruitmentConditionMet(0), "One newly explored node is insufficient");
            sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(party.Recruit(0) && !party.Recruit(0), "Second new node unlocks shield at its stage, once");
            sim.SubmitPlayerAction(WorldAction.Move(4));
            Check(!party.Recruit(2), "Arriving alone does not unlock the archer");
            sim.State.Nodes[2].GuardianDefeat = (99, sim.State.Year);
            Check(!party.Recruit(2), "Another actor's victory cannot satisfy a personal condition");
            sim.State.Nodes[2].GuardianDefeat = (1, sim.State.Year);
            Check(party.Recruit(2) && party.DeployedCount == 3, "A personal victory unlocks the archer");
            int swordSite = party.RecruitmentSites[1];
            sim.State.Actors[1].NodeId = swordSite;
            Check(!party.Recruit(1), "Sword requires three victories");
            sim.State.Nodes[3].GuardianDefeat = (1, sim.State.Year);
            sim.State.Nodes[4].GuardianDefeat = (1, sim.State.Year);
            sim.State.Nodes[swordSite].MonsterHealth = 1;
            Check(!party.Recruit(1), "Recruitment still requires a safe site");
            sim.State.Nodes[swordSite].MonsterHealth = 0;
            Check(party.Recruit(1) && party.DeployedCount == 3 && party.CellOf(ExpeditionParty.FirstCompanionId + 1) < 0,
                "Full team recruits into the bench");
        });
        Run("Stage activation preserves legacy recruitment and deterministically excludes story sites", () =>
        {
            var first = ExpeditionFixture(); var replay = ExpeditionFixture();
            foreach (var sim in new[] { first, replay })
            {
                var party = sim.State.Expedition;
                Check(party.Recruit(0), "Replay legacy immediate recruitment before activation");
                party.EnableProgression(new[] { 1, 2, 3 });
                int[] formation = party.Cells.ToArray();
                party.EnableCompanionStages(new[] { 5 });
                Check(party.HasRecruited(0) && party.Cells.SequenceEqual(formation), "Migrated ownership and formation retained");
                Check(!party.RecruitmentSites.Values.Contains(5), "Existing story remains its own node type");
                var sites = party.RecruitmentSites.ToArray();
                party.EnableCompanionStages(new[] { 2, 3, 4 });
                Check(party.RecruitmentSites.SequenceEqual(sites), "Repeated activation does not move sites");
            }
            Check(first.State.Expedition.RecruitmentSites.SequenceEqual(replay.State.Expedition.RecruitmentSites), "Identical replay yields identical stages");
        });
        Run("Exploration rewards only new cleared destinations and waiting cannot farm growth", () =>
        {
            var sim = ExpeditionFixture(growth: GrowthConfig());
            var party = sim.State.Expedition;
            party.EnableProgression();
            var player = sim.State.Actors[1];
            bool rejected = false;
            try { sim.SubmitPlayerAction(WorldAction.Cultivate(5)); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && sim.State.Year == 0 && player.CurrentAction == null, "Reject cultivation before recording or advancing time");
            sim.SubmitPlayerAction(WorldAction.Wait(5));
            Check(player.Experience == 0, "Waiting gives no experience");
            int reward = party.ExplorationExperience(sim.Cultivation);
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(player.Experience == reward && party.HasExplored(2), "First arrival grants advertised experience");
            sim.SubmitPlayerAction(WorldAction.Move(1));
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(player.Experience == reward && sim.Events.Count(e => e.Kind == WorldEventKind.Explored) == 1,
                "Neither birth node nor revisiting a new node can be farmed");
        });
        Run("Victories scale with the current level and restore health in the settlement", () =>
        {
            var sim = ExpeditionFixture(new ExpeditionTestResolver(), GrowthConfig(), 5);
            var party = sim.State.Expedition;
            party.EnableProgression(); party.Recruit(0);
            sim.State.Nodes[2].MonsterHealth = 50;
            var player = sim.State.Actors[1];
            int expected = party.VictoryExperience(2, sim.Cultivation);
            Check(expected >= sim.Cultivation.GetLevel(5).ExperienceToNext / 2, "Late victories remain meaningful");
            BattleSettlement settlement = null;
            sim.SetBattleSettlementPresenter(result => Capture(result));
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(settlement.Experience == expected && settlement.RecoveredHealth == player.MaxHealth / 4 &&
                settlement.Health == 1 + settlement.RecoveredHealth, "Settlement includes growth and post-victory recovery");
            Check(player.Experience == expected + party.ExplorationExperience(sim.Cultivation), "Cleared new node also grants exploration once");
            IEnumerator Capture(BattleSettlement result) { settlement = result; yield break; }
        });
        Run("Failed encounters do not grant exploration, levels or victory recovery", () =>
        {
            var sim = ExpeditionFixture(growth: GrowthConfig());
            sim.State.Expedition.EnableProgression();
            sim.State.Nodes[2].MonsterHealth = 1000000;
            sim.State.Nodes[2].MonsterAttack = 1000000;
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(!sim.State.Actors[1].IsAlive && sim.State.Actors[1].Experience == 0 && !sim.State.Expedition.HasExplored(2),
                "Defeat cannot pay progression rewards or revive player");
        });
        Run("Legacy cultivation replays before map progression begins and prior routes stay claimed", () =>
        {
            var sim = ExpeditionFixture(growth: GrowthConfig());
            sim.State.Nodes[1].CultivationPerYear = 2;
            sim.SubmitPlayerAction(WorldAction.Cultivate(1));
            sim.SubmitPlayerAction(WorldAction.Move(2));
            long legacyXp = sim.State.Actors[1].Experience;
            sim.State.Expedition.EnableProgression(new[] { 1, 2 });
            sim.SubmitPlayerAction(WorldAction.Move(1));
            Check(legacyXp == 2 && sim.State.Actors[1].Experience == legacyXp, "Past rewards retained; old route cannot pay again");
            sim.State.Expedition.EnableProgression(new[] { 3 });
            Check(!sim.State.Expedition.HasExplored(3), "Repeated activation cannot alter progression");
        });
        Run("Map recruits require arrival, clear ground and unique ownership", () =>
        {
            var sim = ExpeditionFixture();
            var party = sim.State.Expedition;
            Check(party.DeployedCount == 1 && party.CanRecruit(0), "Start solo beside first recruit");
            Check(!party.Recruit(2), "Cannot recruit remotely");
            Check(party.Recruit(0) && !party.Recruit(0) && party.DeployedCount == 2, "Recruit once and auto-deploy");
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(party.Recruit(2) && party.DeployedCount == 3, "Moving reaches the next recruit");
            sim.State.Nodes[3].MonsterHealth = 99;
            sim.State.Actors[1].NodeId = 3;
            Check(!party.Recruit(3), "Must clear the recruitment site");
            sim.State.Nodes[3].MonsterHealth = 0;
            Check(party.Recruit(3) && party.DeployedCount == 3, "Full team stores new recruit on bench");
            int healer = ExpeditionParty.FirstCompanionId + 3;
            Check(!party.Place(healer, party.CellOf(1)), "Cannot replace the leader");
            Check(party.Place(healer, party.CellOf(ExpeditionParty.FirstCompanionId + 2)), "Replace another companion");
            Check(!party.Bench(1) && !party.Place(12345, 1), "Leader and ownership restrictions");
            Check(party.Place(1, 0) && party.Cells.Where(id => id != 0).Distinct().Count() == 3, "Moving the leader preserves the team");
        });
        Run("Map battle uses confirmed roster and surviving companions rescue the leader", () =>
        {
            var resolver = new ExpeditionTestResolver();
            var sim = ExpeditionFixture(resolver);
            sim.State.Expedition.Recruit(0);
            sim.State.Nodes[2].MonsterHealth = 50;
            bool prepared = false;
            sim.SetBattlePreparationPresenter(_ => Prepare());
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(prepared && resolver.Count == 2, "Native party adapter receives recruited companion after preparation");
            Check(sim.State.Actors[1].Health == 1 && sim.State.Nodes[2].MonsterHealth == 0 && sim.WaitingForPlayer,
                "Companion victory clears the map node and preserves the player's life");
            Check(sim.State.Actors.Count == 1, "Companions do not become independent historical actors");
            var fresh = sim.State.Expedition.CreateCombatants(2)[1];
            Check(fresh.Health == fresh.MaxHealth, "Next encounter restores companion health");
            IEnumerator Prepare() { prepared = true; Check(sim.State.Expedition.Place(1, 0), "Can adjust before fight"); yield break; }
        });
        Run("Recruitment and formation commands replay without changing time or recruitment sites", () =>
        {
            var first = ExpeditionFixture();
            var replay = ExpeditionFixture();
            foreach (var sim in new[] { first, replay })
            {
                Check(sim.State.Expedition.Recruit(0), "First recruitment");
                sim.SubmitPlayerAction(WorldAction.Move(2));
                Check(sim.State.Expedition.Recruit(2), "Road recruitment");
                sim.State.Expedition.Place(1, 0);
                sim.State.Expedition.Place(ExpeditionParty.FirstCompanionId, 2);
            }
            Check(first.State.Expedition.Cells.SequenceEqual(replay.State.Expedition.Cells) && first.State.Year == 1 &&
                first.State.Expedition.RecruitmentSites.SequenceEqual(replay.State.Expedition.RecruitmentSites), "Deterministic map and formation");
        });
    }

    private static WorldSimulation ExpeditionFixture(IWorldBattleResolver resolver = null, CultivationConfig growth = null, int level = 1)
    {
        var world = new WorldState(1);
        for (int i = 1; i <= 5; i++)
        {
            world.Nodes.Add(i, new NodeState(i));
            if (i > 1) { world.Nodes[i - 1].Links.Add(i); world.Nodes[i].Links.Add(i - 1); }
        }
        world.Actors.Add(1, new ActorState(1, 1, 100, 20));
        if (growth != null) CultivationSystem.Initialize(world.Actors[1], growth, TalentProfile.Generate(1), level);
        world.EnableExpedition();
        return new WorldSimulation(world, resolver, growth);
    }

    private sealed class ExpeditionTestResolver : IWorldPartyBattleResolver
    {
        public int Count;
        public BattleResult Resolve(int a, int b, int c, int d) => throw new InvalidOperationException();
        public int[] ResolveParty(WorldState world, int node, IReadOnlyList<ActorState> first, IReadOnlyList<ActorState> second)
        {
            Count = first.Count;
            var result = new int[first.Count + second.Count];
            result[1] = first[1].Health / 2;
            return result;
        }
    }

    private sealed class RecordedPartyResolver : IWorldPartyBattleResolver
    {
        public int LastCount;
        public bool Lose;
        public BattleResult Resolve(int a, int b, int c, int d) => throw new InvalidOperationException();
        public int[] ResolveParty(WorldState world, int node, IReadOnlyList<ActorState> first, IReadOnlyList<ActorState> second)
        {
            LastCount = first.Count;
            return first.Select(a => Lose ? 0 : a.Health).Concat(second.Select(a => Lose ? a.Health : 0)).ToArray();
        }
    }
}
