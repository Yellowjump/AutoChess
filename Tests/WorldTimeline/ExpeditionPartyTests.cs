using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WorldTimeline;

internal static partial class WorldSimulationTests
{
    private static void RunExpeditionTests()
    {
        RunCampaignRoutes();
        foreach (bool accept in new[] {true,false})
        {
            Run(accept ? "Map invitation merges exactly one protagonist, current assets and wounded stage survivors" :
                "Declining a historical invitation leaves both inventories separate and resolves the encounter", () =>
            {
                var sim = ExpeditionFixture(new RecordedPartyResolver(),birth:3);
                var world = sim.State;
                var player = world.Expedition;
                player.EnableAttrition(); player.Equipment.EnableArtifacts();
                world.PlayableBirths.UnionWith(new[]{1,3});
                sim.EnablePastInvitations();
                var source = ExpeditionFixture();
                source.SubmitPlayerAction(WorldAction.Move(2));
                var history = sim.AddHistory(source.RecordPlayerLife(),10);
                var former = world.ExpeditionFor(10);
                var gear = former.Equipment;
                former.Recruit(0);
                var worn = gear.Instances.First(i=>i.DefinitionId==5);
                gear.EquipInstance(10,worn.Id,4); gear.Upgrade(worn.Id,true);
                var allyGear = gear.Instances.First(i=>i.DefinitionId==7);
                gear.EquipInstance(former.CompanionId(0),allyGear.Id,2);
                gear.Upgrade(allyGear.Id,true);
                former.SetCompanionHealth(0,19);
                world.Actors[10].Health=31;
                world.Actors[10].AwardLegacySpiritStones(57);
                int encounters=0;
                IEnumerator Choose(int id,Action<bool> choose)
                {
                    Check(id==10 && world.Actors[id].NodeId==2 && player.RoleCount==4 &&
                        player.Equipment.SpiritStones==0,"Meeting precedes asset transfer and combat");
                    encounters++; choose(accept); yield break;
                }
                sim.SetPastInvitationPresenter(Choose);
                sim.SubmitPlayerAction(WorldAction.Move(2));
                Check(encounters==1,"One choice per meeting");
                if (!accept)
                {
                    Check(player.RoleCount==4 && player.Equipment.Instances.Count==3 && !world.Actors[10].IsAlive &&
                        world.Actors[10].LegacySpiritStones==57,"Decline follows the original actor battle");
                    return;
                }
                int member=player.CompanionId(4);
                Check(history.Status==HistoryReplayStatus.JoinedPlayer && !world.Actors[10].OnWorldMap &&
                    world.ExpeditionFor(10)==null && player.RoleCount==5 && player.CompanionHealth(4)==31 &&
                    player.Equipment.Worn(member,4)?.Level==2 && !player.HasRecruited(0),"Only former protagonist enters the formation");
                Check(player.Equipment.SpiritStones==57 && gear.SpiritStones==0 && gear.Instances.Count==0 &&
                    player.Equipment.Instances.Count==7 && player.Equipment.Instances.Any(item=>item.DefinitionId==7 && item.Level==2 && item.Owner<0) &&
                    player.Equipment.Instances.Any(item=>item.DefinitionId==3 && item.Owner<0),
                    "Captain loadout, bag, ally worn equipment, innate and stones transferred exactly once");
                Check(world.ReturnedStageHealth(0)==19 && world.StageOwner(0)==null,"Living shield returns to its stage wounded");
                sim.SubmitPlayerAction(WorldAction.Move(3));
                Check(player.Recruit(0) && player.CompanionHealth(0)==19 && player.Equipment.InnateLevel(player.CompanionId(0))==0 &&
                    world.ReturnedStageHealth(0)==null,"Recruiting the returned member cannot recreate transferred gear");
                sim.SubmitPlayerAction(WorldAction.Move(2));
                var frozen = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
                Check(encounters==1 && player.Equipment.SpiritStones==57 && player.Equipment.Instances.Count==7 &&
                    frozen.Expedition.Last().Equipment.SequenceEqual(player.Equipment.Snapshot()),
                    "Passing through the meeting site again grants nothing and the merged loadout serializes");
            });
        }
        Run("A past captain joins reserves when deployed slots are full and a fallen stage stays lost", () =>
        {
            var sim = ExpeditionFixture(birth:3);
            var world = sim.State;
            var party = world.Expedition;
            party.EnableAttrition(); party.Equipment.EnableArtifacts();
            sim.EnablePastInvitations();
            world.PlayableBirths.UnionWith(new[]{5,3});
            var source = ExpeditionFixture(birth:5);
            source.SubmitPlayerAction(WorldAction.Wait(4));
            source.SubmitPlayerAction(WorldAction.Move(4)); source.SubmitPlayerAction(WorldAction.Move(3));
            sim.AddHistory(source.RecordPlayerLife(),10);
            var former = world.ExpeditionFor(10);
            Check(former.Recruit(0),"Former team recruits the shield at birth");
            former.SetCompanionHealth(0,0);
            int site2 = party.RecruitmentSites[2], site3 = party.RecruitmentSites[3];
            foreach (int node in WorldPathfinder.FindRoute(world,3,new[]{site2})) sim.SubmitPlayerAction(WorldAction.Move(node));
            Check(party.Recruit(2),"Deploy archer");
            foreach (int node in WorldPathfinder.FindRoute(world,site2,new[]{site3})) sim.SubmitPlayerAction(WorldAction.Move(node));
            Check(party.Recruit(3),"Deploy healer");
            IEnumerator Accept(int id,Action<bool> choose) { choose(true); yield break; }
            sim.SetPastInvitationPresenter(Accept);
            foreach (int node in WorldPathfinder.FindRoute(world,site3,new[]{3})) sim.SubmitPlayerAction(WorldAction.Move(node));
            if (world.Year < 6) sim.SubmitPlayerAction(WorldAction.Wait(6-world.Year));
            Check(party.HasRecruited(4) && party.CellOf(party.CompanionId(4)) < 0 &&
                party.DeployedCount==3 && world.ReturnedStageHealth(0)==0 && !party.CanRecruit(0),
                "Past hero is a real reserve and disbanding does not revive the shield");
        });
        Run("Historical planning keeps actual wounds, rest, succession and cleared equipment through archive replay", () =>
        {
            var source = ExpeditionFixture(new RecordedPartyResolver());
            var original = source.State.Expedition;
            original.EnableAttrition(); original.Equipment.EnableArtifacts(); original.Recruit(0);
            source.EnableSharedClears();
            source.State.Nodes[2].MonsterHealth = source.State.Nodes[3].MonsterHealth = 5;
            source.SubmitPlayerAction(WorldAction.Move(2));
            original.Equipment.EquipInstance(1,original.Equipment.Instances.Last().Id,2);
            source.SubmitPlayerAction(WorldAction.Rest());
            source.SubmitPlayerAction(WorldAction.Move(3));
            original.RecordFormation();
            var archive = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(
                new[] {new SavedLife(1,source.RecordPlayerLife())})).Single().Record;

            var sim = ExpeditionFixture(new AttritionResolver(),birth:5);
            sim.EnableHistoricalGrowth();
            sim.State.Nodes[2].MonsterHealth = sim.State.Nodes[3].MonsterHealth = 5;
            sim.AddHistory(archive,10);
            var party = sim.State.ExpeditionFor(10);
            var actor = sim.State.Actors[10];
            sim.SubmitPlayerAction(WorldAction.Wait(1));
            Check(actor.Health == 60 && party.CompanionHealth(0) == 20 && party.Equipment.RewardsTaken == 1 &&
                party.Equipment.Worn(10,2) != null, "Archived full-health edits retain current wounds and equip an earned drop");
            int rested = 20 + party.CompanionMaxHealth(0)/4;
            sim.SubmitPlayerAction(WorldAction.Wait(1));
            Check(party.CompanionHealth(0) == rested, "The recorded year of rest heals the actual injury");
            long xp = actor.Experience, stones = actor.LegacySpiritStones;
            sim.SubmitPlayerAction(WorldAction.Wait(1));
            Check(party.IsFallen(-1) && party.LeaderRole == 0 && actor.Health == 8 &&
                actor.Experience > xp && actor.LegacySpiritStones > stones && party.Equipment.RewardsTaken == 2,
                "Recorded healthy protagonist cannot replace the real successor or erase current rewards");
        });
        Run("Third and eighth year historical growth depends on real progress, including after save decoding", () =>
        {
            var growth = GrowthConfig();
            var source = ExpeditionFixture(growth:growth);
            source.SubmitPlayerAction(WorldAction.Cultivate(3));
            source.SubmitPlayerAction(WorldAction.Move(2));
            source.SubmitPlayerAction(WorldAction.Move(3));
            source.SubmitPlayerAction(WorldAction.Wait(3));
            var bytes = WorldTimelineSaveStore.Encode(new[] {new SavedLife(1,source.RecordPlayerLife())});
            foreach (bool preempted in new[] {false,true})
            {
                var sim = ExpeditionFixture(growth:growth,birth:5);
                sim.EnableHistoricalGrowth();
                sim.AddHistory(WorldTimelineSaveStore.Decode(bytes).Single().Record,10);
                foreach (int site in new[] {2,3})
                {
                    if (preempted) sim.State.Nodes[site].ClaimClear(1,1,true);
                    else sim.State.Nodes[site].MonsterHealth = 1;
                }
                var actor = sim.State.Actors[10];
                sim.SubmitPlayerAction(WorldAction.Wait(3));
                Check(actor.Level == 1 && actor.Experience == 0 && actor.LegacySpiritStones == 0,
                    "Three archived cultivation years preserve the itinerary without farming experience");
                sim.SubmitPlayerAction(WorldAction.Wait(5));
                Check(sim.State.Year == 8 && actor.NodeId == 3 && (actor.Level > 1) == !preempted &&
                    (actor.LegacySpiritStones > 0) == !preempted &&
                    sim.State.ExpeditionFor(10).Equipment.RewardsTaken == (preempted ? 0 : 2),
                    "Later meeting reflects only the two sites that were actually available");
            }
        });
        Run("Historical merchant plans spend only current funds and an obsolete reward cannot enter the bag", () =>
        {
            var source = ExpeditionFixture(new RecordedPartyResolver());
            var gear = source.State.Expedition.Equipment;
            gear.EnableArtifacts(); source.EnableSharedClears();
            source.State.Nodes[2].MonsterHealth = 1;
            source.SubmitPlayerAction(WorldAction.Move(2));
            source.State.Nodes[3].IsMerchant = true;
            source.SubmitPlayerAction(WorldAction.Move(3));
            source.State.Actors[1].AwardLegacySpiritStones(200);
            Check(gear.Buy(6) && gear.Upgrade(gear.Instances.Last().Id), "Record a purchase and paid upgrade");
            var record = source.RecordPlayerLife();
            foreach (bool wins in new[] {true,false})
            {
                var sim = ExpeditionFixture(birth:5);
                sim.EnableHistoricalGrowth(); sim.AddHistory(record,10);
                var site = sim.State.Nodes[2];
                if (wins) { site.MonsterHealth = 1; site.CultivationPerYear = 100; }
                else site.ClaimClear(1,0,true);
                sim.State.Nodes[3].IsMerchant = true;
                sim.SubmitPlayerAction(WorldAction.Wait(2));
                var current = sim.State.ExpeditionFor(10).Equipment;
                int spend = ExpeditionEquipment.Definition(6).Price + ExpeditionEquipment.Definition(6).UpgradePrice(1);
                Check(current.RewardsTaken == (wins ? 1 : 0) && current.Instances.Count == (wins ? 5 : 3) &&
                    current.SpiritStones == (wins ? 200-spend : 0) && (!wins || current.Instances.Last().Level == 2),
                    "Both reward and merchant activity depend on the simulated journey");
            }
        });
        foreach (bool historyFirst in new[] { true, false })
        {
            Run(historyFirst ? "Historical first clear survives a later player visit" :
                "Player first clear removes the obsolete award from historical equipment replay", () =>
            {
                var source = ExpeditionFixture();
                source.State.Expedition.EnableProgression();
                source.State.Expedition.Equipment.EnableArtifacts();
                source.EnableSharedClears();
                source.State.Nodes[2].MonsterHealth = 5;
                if (!historyFirst) source.SubmitPlayerAction(WorldAction.Wait(1));
                source.SubmitPlayerAction(WorldAction.Move(2));
                source.SubmitPlayerAction(WorldAction.Move(1));
                var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(
                    new[] { new SavedLife(1,source.RecordPlayerLife()) })).Single().Record;

                var sim = ExpeditionFixture(birth:3);
                var state = sim.State;
                var node = state.Nodes[2];
                node.MonsterHealth = 5;
                node.Items.Add(1234);
                state.Expedition.EnableProgression();
                state.Expedition.Equipment.EnableArtifacts();
                sim.AddHistory(record,10);
                sim.EnableSharedClears();
                sim.SubmitPlayerAction(historyFirst ? WorldAction.Wait(1) : WorldAction.Move(2));
                sim.SubmitPlayerAction(historyFirst ? WorldAction.Move(2) : WorldAction.Move(3));
                sim.SubmitPlayerAction(WorldAction.Wait(1));
                int owner = historyFirst ? 10 : 1, other = historyFirst ? 1 : 10;
                Check(node.FirstClear == (owner,1) && node.FirstArtifact > 0 &&
                    state.Actors[owner].Items.Contains(1234) && !state.Actors[other].Items.Contains(1234),
                    "First clearer owns the site and its loot");
                Check(sim.Events.Count(e => e.Kind == WorldEventKind.Explored && e.NodeId == 2) == 1 &&
                    state.Actors[owner].LegacySpiritStones > state.Actors[other].LegacySpiritStones &&
                    state.ExpeditionFor(owner).Equipment.RewardsTaken == 1 && state.ExpeditionFor(other).Equipment.RewardsTaken == 0 &&
                    state.ExpeditionFor(owner).Equipment.Instances.Count == 4 && state.ExpeditionFor(other).Equipment.Instances.Count == 3,
                    "Only one exploration, stone payout and real equipment instance after archived formations replay");
                Check(state.ExpeditionFor(other).Equipment.OfferReward(2).Count == 0 &&
                    state.ExpeditionFor(owner).Equipment.OfferReward(2).Count == 0,"Neither team can reopen the award");
            });
        }
        Run("A simultaneous arrival settles the duel before awarding the surviving party the guardian clear", () =>
        {
            var old = ExpeditionFixture();
            old.SubmitPlayerAction(WorldAction.Move(2));
            var sim = ExpeditionFixture(birth:3);
            sim.State.Actors[1].Health = 1;
            sim.State.Nodes[2].MonsterHealth = 1;
            sim.AddHistory(old.RecordPlayerLife(),10);
            sim.EnableSharedClears();
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(sim.State.Nodes[2].FirstClear == (10,1) && sim.State.ExpeditionFor(10).Equipment.RewardsTaken == 1,
                "Earlier actor ID cannot claim before the winning party defeats the guard");
        });
        Run("A failed historical attempt leaves the first clear and treasure available to the player", () =>
        {
            var old = ExpeditionFixture();
            old.SubmitPlayerAction(WorldAction.Move(2));
            var sim = ExpeditionFixture(birth:3);
            var state = sim.State;
            var node = state.Nodes[2];
            node.MonsterHealth = 150;
            node.MonsterAttack = 110;
            node.Items.Add(701);
            state.Expedition.EnableProgression();
            state.Expedition.Equipment.EnableArtifacts();
            sim.AddHistory(old.RecordPlayerLife(),10);
            sim.EnableSharedClears();
            sim.SubmitPlayerAction(WorldAction.Wait(1));
            Check(!node.FirstClear.HasValue && node.MonsterHealth > 0 && node.Items.Contains(701),
                "Arriving and losing does not reserve a reward");
            state.Actors[1].Attack = 1000;
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(node.FirstClear == (1,2) && state.Actors[1].Items.Contains(701) &&
                state.Expedition.Equipment.RewardsTaken == 1, "Next party can finish the guard and receive the full clear");
        });
        Run("Artifact links use worn effects, ignore duplicate triggers and preserve old combat snapshots", () =>
        {
            var sim = ExpeditionFixture();
            var party = sim.State.Expedition;
            var gear = party.Equipment;
            gear.EnableArtifacts(); party.Recruit(0);
            var legacy = party.Records.Last();
            gear.EnableLinks();
            Check(gear.BattleTriggers(1) == ArtifactTrigger.Pursuit, "Bag items never grant a combat trigger");
            gear.VisitEvent(0); gear.VisitEvent(1); gear.Buy(8);
            foreach(var copy in gear.Instances.Where(i=>i.DefinitionId==8).ToArray())
                gear.EquipInstance(1,copy.Id,gear.FirstFreeSlot(1));
            Check(gear.BattleTriggers(1) == ArtifactTrigger.Pursuit, "Duplicate pursuit grants one trigger");
            gear.EquipInstance(party.CompanionId(0),gear.Instances.First(i=>i.DefinitionId==5).Id,0);
            var archive = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            var replay = ExpeditionFixture(); replay.AddHistory(archive,10);
            var historic = replay.State.ExpeditionFor(10);
            Check(historic.Equipment.BattleTriggers(historic.CompanionId(0)) == (ArtifactTrigger.ShieldOpening | ArtifactTrigger.Recovery),
                "Historical member retains the shield and healing combination");
            historic.Restore(legacy);
            Check(!historic.Equipment.LinksEnabled && historic.Equipment.BattleTriggers(10)==ArtifactTrigger.None,
                "Pre-activation history replays without new combat effects");
        });
        Run("Only distinct playable past protagonists retain their own growth, skills and attire as teammates", () =>
        {
            var sim = ExpeditionFixture();
            var party = sim.State.Expedition;
            party.EnableAttrition(); party.Equipment.EnableArtifacts(); party.Recruit(0);
            var world = sim.State;
            var sourceWorld = new WorldState(1);
            sourceWorld.Nodes.Add(2, new NodeState(2));
            sourceWorld.Actors.Add(1, new ActorState(1, 2, 372, 46));
            sourceWorld.EnableExpedition();
            var oldSim = new WorldSimulation(sourceWorld);
            var gear = sourceWorld.Expedition.Equipment;
            gear.EnableArtifacts();
            var robe = gear.Instances.First(item => item.DefinitionId == 5);
            gear.EquipInstance(1, robe.Id, 4); gear.Upgrade(robe.Id, true);
            sim.AddHistory(oldSim.RecordPlayerLife(), 10);
            var old = world.Actors[10];
            old.Level = 6; old.Experience = 905; old.LevelExperience = 39; old.Health = 17;
            old.Items.Add(8109);
            old.LearnTechnique(new TechniqueDefinition(1, new[] { new TechniqueStage(80),
                new TechniqueStage(81, TechniqueRequirement.BattleExperience, 10) }));
            old.SettleTechniqueBattle(1, 1, 4, 0);
            Check(!sim.TryAddPastLeaderToRoster(10), "A history outside the selection roster cannot join");
            world.PlayableBirths.UnionWith(new[]{1,2,3});
            sim.AddHistory(sim.RecordPlayerLife(), 11);
            Check(!sim.TryAddPastLeaderToRoster(11) && sim.TryAddPastLeaderToRoster(10) &&
                !sim.TryAddPastLeaderToRoster(10), "Original identity and duplicates are excluded");
            int role = ExpeditionParty.StageRoles, id = party.CompanionId(role);
            var member = party.CreateCombatants(1).Single(actor => actor.Id == id);
            Check(member.BirthNodeId == 2 && member.Health == 17 && member.MaxHealth == old.MaxHealth &&
                member.CombatAttack == old.CombatAttack && member.Level == 6 && member.LevelExperience == 39 &&
                member.Experience == 905 && member.Techniques[1].Progress == 4 && member.Items.Contains(8109) &&
                party.Equipment.Worn(id, 4)?.Level == 2, "Current stats and equipped instance preserved without doubling bonuses");
            Check(party.Bench(id), "Past leader can be benched");
            sim.SubmitPlayerAction(WorldAction.Rest());
            Check(party.CompanionHealth(role) == 17 + member.MaxHealth/4 && party.Place(id, 3),
                "Reserve recovers and can deploy again");
            var archive = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single();
            var replay = ExpeditionFixture(); replay.AddHistory(archive.Record, 10);
            var restored = replay.State.ExpeditionFor(10);
            restored.Restore(archive.Record.Expedition.Last());
            var fighter = restored.CreateCombatants(1).Single(a => a.Id == restored.CompanionId(role));
            Check(fighter.Level == 6 && fighter.Health == party.CompanionHealth(role) && fighter.Techniques[1].Progress == 4 &&
                restored.Equipment.Worn(fighter.Id,4)?.Level == 2 && fighter.Id != id,
                "Archived roster remaps combat IDs and preserves wounds, skills and equipped levels");
            party.RemoveFallen(-1, 1); party.Promote(role);
            Check(world.Actors[1].Level == 6 && world.Actors[1].Techniques[1].Progress == 4 &&
                world.Actors[1].Health == fighter.Health && party.Equipment.Worn(1,4)?.Level == 2,
                "A past protagonist succeeds with their own cultivation and equipped gear");
            party.RemoveFallen(role,1);
            Check(party.Equipment.Instances.Any(item => item.Owner < 0 && item.DefinitionId == 5 && item.Level == 2),
                "The dead captain returns the upgraded item to the bag");
        });
        Run("One year of rest heals living deployed and reserve members and leaves casualties dead", () =>
        {
            var sim = ExpeditionFixture();
            var party = sim.State.Expedition;
            var captain = sim.State.Actors[1];
            party.EnableAttrition();
            party.Recruit(0);
            sim.SubmitPlayerAction(WorldAction.Move(2)); party.Recruit(2);
            sim.SubmitPlayerAction(WorldAction.Move(3)); party.Recruit(3);
            party.Bench(party.CompanionId(2));
            captain.Health = captain.MaxHealth - 1;
            party.SetCompanionHealth(0, 4);
            party.SetCompanionHealth(2, 2);
            party.SetCompanionHealth(3, 0);
            int year = sim.State.Year;
            sim.SubmitPlayerAction(WorldAction.Wait());
            Check(party.CompanionHealth(2) == 2 && captain.Health == captain.MaxHealth - 1, "Ordinary waiting preserves wounds");
            sim.SubmitPlayerAction(WorldAction.Rest());
            Check(sim.State.Year == year + 2 && captain.NodeId == 3 && captain.Health == captain.MaxHealth &&
                party.CompanionHealth(0) == 4 + party.CompanionMaxHealth(0) / 4 &&
                party.CompanionHealth(2) == 2 + party.CompanionMaxHealth(2) / 4 &&
                party.CompanionHealth(3) == 0 && party.IsFallen(3), "Rest heals at year end, caps HP and skips the fallen");
            var life = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[] { new SavedLife(1, sim.RecordPlayerLife()) })).Single().Record;
            Check(life.Actions.Last().Action.Kind == WorldActionKind.Rest && life.Actions.Last().Action.Years == 1,
                "Life archive distinguishes one year of rest from waiting");
            for (int i = 0; i < 5; i++) sim.SubmitPlayerAction(WorldAction.Rest());
            Check(!party.NeedsRest && party.IsFallen(3), "Full parties and fallen members never consume another rest from the HUD");
        });
        Run("Persistent wounds, permanent losses and a chosen reserve captain survive successive battles", () =>
        {
            var resolver = new AttritionResolver();
            var sim = ExpeditionFixture(resolver);
            var party = sim.State.Expedition;
            var gear = party.Equipment;
            party.EnableAttrition(); gear.EnableArtifacts(); party.Recruit(0);
            sim.SubmitPlayerAction(WorldAction.Move(2)); party.Recruit(2);
            sim.SubmitPlayerAction(WorldAction.Move(3)); party.Recruit(3);
            int shield = party.CompanionId(0), archer = party.CompanionId(2);
            var relic = gear.Instances.First();
            Check(gear.EquipInstance(archer, relic.Id, 5) && gear.Upgrade(relic.Id, true) &&
                gear.Upgrade(gear.InnateKey(archer), true), "Equip upgraded items before casualty");
            int choices = 0;
            sim.SetSuccessorPresenter((roles, choose) => Pick(roles, choose));
            IEnumerator Pick(IReadOnlyList<int> roles, Action<int> choose)
            {
                Check(roles.SequenceEqual(new[] { 0, 3 }), "Both the wounded fighter and living reserve can lead");
                choices++; choose(3); yield break;
            }
            BattleSettlement report = null;
            sim.SetBattleSettlementPresenter(result => Capture(result));
            IEnumerator Capture(BattleSettlement result) { report = result; yield break; }
            void Fight(int destination)
            {
                sim.State.Nodes[destination].MonsterHealth = 50;
                sim.SubmitPlayerAction(WorldAction.Move(destination));
            }
            Fight(4);
            Check(party.CompanionHealth(0) == 20 && party.IsFallen(2) && !party.Owns(archer) &&
                party.CellOf(archer) < 0 && relic.Owner == -1 && relic.Slot == -1 && relic.Level == 2 &&
                gear.Instances.Any(item => item.Owner == -1 && item.DefinitionId == 2 && item.Level == 2),
                "Fallen archer permanently leaves, returning equipped instance and upgraded innate artifact");
            int reserveHp = party.CompanionHealth(3);
            Fight(3);
            Check(resolver.StartHealth.SequenceEqual(new[] { 60, 20 }) && choices == 1 && party.LeaderRole == 3 &&
                sim.State.Actors[1].Health == reserveHp && party.CompanionHealth(0) == 8 && party.CellOf(shield) >= 0 &&
                report.SuccessorRole == 3 && party.IsFallen(-1) && gear.Innate(1).Id == 4 && sim.WaitingForPlayer,
                "Wounds persist and selected reserve assumes command with her own health and innate artifact");
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(!party.CanRecruit(2), "Returning to the dead companion's site cannot recruit again");
            party.RecordFormation();
            var archive = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[] {new SavedLife(1, sim.RecordPlayerLife())})).Single().Record;
            Check(archive.Expedition.Any(step => step.Successor == 3) &&
                archive.Expedition.Last().Vitality[0] == 3, "History preserves the succession choice and roster");
            Fight(1);
            Check(!sim.State.Actors[1].IsAlive && party.IsFallen(3) && party.IsFallen(0) && !sim.WaitingForPlayer && choices == 1,
                "With no living reserve the final wipe ends control");
        });
        Run("A surviving reserve can succeed after a lost battle without clearing the enemy", () =>
        {
            var sim = ExpeditionFixture(new RecordedPartyResolver { Lose = true });
            var party = sim.State.Expedition;
            party.EnableAttrition(); party.Equipment.EnableArtifacts(); party.Recruit(0);
            party.Bench(party.CompanionId(0));
            sim.State.Nodes[2].MonsterHealth = 40;
            sim.State.Nodes[2].Items.Add(99);
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(sim.WaitingForPlayer && party.LeaderRole == 0 && sim.State.Nodes[2].MonsterHealth == 40 &&
                sim.State.Nodes[2].Items.Contains(99), "Reserve continues on a defeat without victory loot");
        });
        Run("Innate artifacts are fixed and six universal slots accept independent copies",()=>
        {
            var sim=ExpeditionFixture();var party=sim.State.Expedition;party.Recruit(0);var gear=party.Equipment;gear.EnableArtifacts();
            int shield=party.CompanionId(0);var leader=sim.State.Actors[1];int originalHp=leader.Health;
            Check(gear.Innate(1).Id==1 && gear.Innate(shield).Id==3 && !gear.Buy(1) && !gear.Equip(shield,1),"Fixed artifacts cannot enter ordinary slots or shop stock");
            leader.AwardLegacySpiritStones(200);
            for(int i=1;i<ExpeditionEquipment.CommonSlots;i++)Check(gear.Buy(5),"Multiple copies of one item can be purchased");
            var robes=gear.Instances.Where(i=>i.DefinitionId==5).ToArray();
            for(int s=0;s<ExpeditionEquipment.CommonSlots;s++)Check(gear.EquipInstance(shield,robes[s].Id,s),"All six slots accept the same item type on any role");
            Check(gear.FirstFreeSlot(shield)==-1,"A full loadout has no automatic destination");
            Check(!gear.EquipInstance(shield,gear.Instances.First(i=>i.Owner<0).Id,6),"Seventh ordinary slot does not exist");
            Check(!gear.EquipInstance(1,robes[0].Id,0),"One instance cannot be worn by two actors");
            Check(gear.Upgrade(robes[0].Id,true)&&robes[0].Level==2&&robes[1].Level==1,"Upgrading a copy leaves identical copies unchanged");
            var displaced=gear.Instances.First(i=>i.Owner<0);
            Check(gear.EquipInstance(1,displaced.Id,2),"Destination can already have an item");
            Check(gear.EquipInstance(1,robes[0].Id,2,true)&&robes[0].Level==2&&leader.Health==originalHp,"Confirmed transfers retain level and do not heal");
            Check(gear.Worn(shield,0)==null&&gear.Worn(1,2)==robes[0]&&displaced.Owner==-1&&displaced.Slot==-1,"Transfer clears the source slot and returns the replaced instance to the bag");
            Check(gear.Unequip(shield,4)&&gear.FirstFreeSlot(shield)==0,"Several gaps resolve to the lowest slot index");
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
            int startingHealth = --player.Health;
            Check(expected >= sim.Cultivation.GetLevel(5).ExperienceToNext / 2, "Late victories remain meaningful");
            BattleSettlement settlement = null;
            sim.SetBattleSettlementPresenter(result => Capture(result));
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(settlement.Experience == expected && settlement.RecoveredHealth == player.MaxHealth / 4 &&
                settlement.Health == 1 + settlement.RecoveredHealth, "Settlement includes growth and post-victory recovery");
            var members = settlement.PartyAtBattleEnd;
            Check(members.Count == 2 && members.Single(m => m.ActorId == player.Id).StartHealth == startingHealth &&
                members.Single(m => m.ActorId == player.Id).Health == 0 &&
                members.Single(m => m.ActorId == party.CompanionId(0)).Health == party.CompanionMaxHealth(0) / 2,
                "Report preserves the individual combat result before rescue and recovery");
            Check(party.CreateCombatants(2)[1].Health == party.CompanionMaxHealth(0), "Next battle restores the companion");
            Check(player.Experience == expected + party.ExplorationExperience(sim.Cultivation), "Cleared new node also grants exploration once");
            Check(party.Recruit(2), "Archer joins the next battle");
            sim.State.Nodes[3].MonsterHealth = 50;
            sim.SubmitPlayerAction(WorldAction.Move(3));
            int archer = party.CompanionId(2);
            Check(settlement.PartyAtBattleEnd.Single(m => m.ActorId == archer).Health == 0 &&
                party.CreateCombatants(3).Single(a => a.Id == archer).Health == party.CompanionMaxHealth(2),
                "A fallen companion remains deployed and starts the next fight at full health");
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

    private static WorldSimulation ExpeditionFixture(IWorldBattleResolver resolver = null, CultivationConfig growth = null, int level = 1, int birth = 1)
    {
        var world = new WorldState(1);
        for (int i = 1; i <= 5; i++)
        {
            world.Nodes.Add(i, new NodeState(i));
            if (i > 1) { world.Nodes[i - 1].Links.Add(i); world.Nodes[i].Links.Add(i - 1); }
        }
        world.Actors.Add(1, new ActorState(1, birth, 100, 20));
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

    private sealed class AttritionResolver : IWorldPartyBattleResolver
    {
        private int fight;
        public int[] StartHealth;
        public BattleResult Resolve(int a, int b, int c, int d) => throw new InvalidOperationException();
        public int[] ResolveParty(WorldState world, int node, IReadOnlyList<ActorState> first, IReadOnlyList<ActorState> second)
        {
            StartHealth = first.Select(a => a.Health).ToArray();
            int[] hp = new int[first.Count + second.Count];
            switch (++fight)
            {
                case 1: hp[0] = 60; hp[1] = 20; break;
                case 2: hp[1] = 8; break;
                default: hp[first.Count] = 1; break;
            }
            return hp;
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
