using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WorldTimeline;

internal static partial class WorldSimulationTests
{
    private static IEnumerable<string[]> CampaignRows(string file) => File.ReadLines(Path.Combine(AppContext.BaseDirectory,file))
        .Where(line => line.StartsWith("\t")).Select(line => line.Split('\t'));

    private static WorldSimulation RouteCampaign(int birth)
    {
        var route = CampaignRows("ChosenRoute.txt").Select(row => new ChosenRouteStage {
            Q=int.Parse(row[2]), R=int.Parse(row[3]), TravelYears=int.Parse(row[4]), GuardianCount=int.Parse(row[5]),
            GuardianHealth=int.Parse(row[6]), GuardianAttack=int.Parse(row[7]), GuardianAppearance=row[8], RitualPeriod=int.Parse(row[9])
        }).ToArray();
        var sim = HexTimelineDemoFactory.Create(playerNode:birth, cultivation:GrowthConfig(), talentSeed:1,
            startingItem:birth==HexWorldMap.Id(-10,2)?8111:8110, weaponAttack:birth==HexWorldMap.Id(-10,2)?8:14,
            chosenRoute:route, initialRitualDeadline:110, configureLocations:world =>
            {
                foreach (var row in CampaignRows("OuterLocations.txt"))
                {
                    var n = world.Nodes[HexWorldMap.Id(int.Parse(row[2]),int.Parse(row[3]))];
                    n.CultivationPerYear=int.Parse(row[5]); n.MonsterHealth=int.Parse(row[6]); n.MonsterAttack=int.Parse(row[7]);
                    n.Items.Clear();
                    int loot=int.Parse(row[8]); if (loot!=0) n.Items.Add(loot);
                    n.LootAttackBonus=int.Parse(row[9]);
                }
            });
        sim.State.EnableExpedition();
        var p = sim.State.Expedition;
        p.EnableAttrition(); p.EnableProgression(); p.Equipment.EnableArtifacts(); p.Equipment.EnableLinks();
        sim.EnablePastInvitations();
        return sim;
    }

    private static void RunCampaignRoutes()
    {
        Run("Signature artifacts follow recruits and successors through a saved formation", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableCompanionStages();
            party.EnableSignatureArtifacts();
            party.EnableSignatureArtifacts();
            Check(party.RecruitmentSites.Count == 6 && party.RecruitmentSites.Values.Distinct().Count() == 6,"Six unique map sites");
            Check(gear.Innate(1).Id == 14 && gear.BattleTriggers(1).HasFlag(ArtifactTrigger.Bamboo),"Qinghuang's innate");
            foreach (int site in new[]{ExpeditionParty.SandSite,ExpeditionParty.TideSite})
            {
                int location = party.RecruitmentSites[site];
                world.Actors[1].NodeId = location;
                world.Nodes[location].MonsterHealth = 0;
                Check(party.Recruit(site) && !party.Recruit(site),"Recruit exactly once at own site");
            }
            int sand = party.SiteMember(ExpeditionParty.SandSite), tide = party.SiteMember(ExpeditionParty.TideSite);
            Check(gear.Innate(party.FieldId(sand)).Id == 15 && gear.Innate(party.FieldId(tide)).Id == 16,"Named companions receive the correct innate");
            party.RemoveFallen(-1,1);
            party.Promote(sand);
            var sword = gear.Instances.Single(i => i.DefinitionId == 14);
            Check(gear.EquipInstance(1,sword.Id,0) && gear.BattleTriggers(1) == ArtifactTrigger.Sand,"Successor retains own aura and dropped sword gives stats only");
            party.RecordFormation();
            var archive = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            var snapshot = archive.Expedition.Last();
            var restored = RouteCampaign(HexWorldMap.Id(0,-10)).State.Expedition;
            restored.EnableSignatureArtifacts(); restored.Restore(snapshot);
            Check(restored.HasRecruited(ExpeditionParty.TideSite) && restored.Equipment.Innate(1).Id == 15,"Binary round trip preserves roles and identities");
        });
        Run("Mountain Ring and Spring Pendant obey per-battle thresholds and healing delay", () =>
        {
            var state = new ArtifactVitality();
            var effects = ArtifactTrigger.SpringPendant | ArtifactTrigger.MountainRing;
            state.OnRemedy(10,0);
            Check(state.TakeRepeat(3)==0,"Pendant waits for low health");
            Check(state.LoseHealth(effects,100,90,100,0)==0 && state.LoseHealth(effects,90,84,100,0)==8,"Small hits accumulate across threshold");
            Check(state.LoseHealth(effects,84,49,100,0)==8 && state.LoseHealth(effects,49,30,100,0)==0,"Ring only shields twice");
            state.OnRemedy(12,4); state.OnRemedy(30,5);
            Check(state.TakeRepeat(5.99f)==0 && state.TakeRepeat(6)==12 && state.TakeRepeat(7)==0,"First remedy repeats once after two seconds");
            state.OnRemedy(12,9);
            Check(state.TakeRepeat(20)==0,"Further healing cannot rearm pendant");
        });
        Run("An equipped sword case matures on two guardian first clears and carries its charge into a new party", () =>
        {
            var sim = ExpeditionFixture(new RecordedPartyResolver());
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            gear.EnableArtifacts(); gear.EnableLinks(); party.EnableAttrition();
            party.EnableSignatureArtifacts(); world.EnableSwordTraining(); sim.EnablePastInvitations();
            int id = ExpeditionEquipment.GrowingSwordCase;
            world.Actors[1].AwardLegacySpiritStones(100);
            Check(gear.Buy(id) && gear.Buy(id),"Obtain distinct cases");
            var worn = gear.Instances.First(i => i.DefinitionId == id);
            var spare = gear.Instances.Last();
            gear.EquipInstance(1,worn.Id,0);
            foreach (int node in new[]{2,4}) world.Nodes[node].MonsterHealth = 5;
            Check(gear.PreviewArtifactChoices(2).Contains(id),"New case appears in map reward preview");
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(worn.SwordClears == 1 && !gear.BattleTriggers(1).HasFlag(ArtifactTrigger.OpeningSword),"First victory charges without firing");
            sim.SubmitPlayerAction(WorldAction.Move(3)); sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(worn.SwordClears == 1,"Safe exploration and revisiting do not count");
            sim.SubmitPlayerAction(WorldAction.Move(3)); sim.SubmitPlayerAction(WorldAction.Move(4));
            Check(worn.SwordClears == 2 && spare.SwordClears == 0 && gear.BattleTriggers(1).HasFlag(ArtifactTrigger.OpeningSword),"Only the worn instance matures");
            var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            party.Restore(record.Expedition.Last());
            Check(gear.Instance(worn.Id).SwordClears == 2 && gear.Instance(spare.Id).SwordClears == 0,"Save and restore per-item progress");
            var other = ExpeditionFixture().State.Expedition.Equipment;
            other.EnableArtifacts();
            other.ImportLeaderLoadout(gear,4); other.MergeBags(gear);
            Check(other.Instances.Any(i => i.Owner == 5 && i.SwordClears == 2) &&
                other.Instances.Any(i => i.Owner < 0 && i.DefinitionId == id && i.SwordClears == 0),"Meeting preserves the captain's case and the spare");
        });
        Run("Echo Jade, Comrade Knot and Field Kit join the same first-clear pool and persist independently", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining(); world.EnableQuakeSpike(); world.EnableRainTassel();
            world.EnableRelayClasp(); world.EnableReprisalSeal(); world.EnableHeartLamp();
            world.EnableTinderPouch(); world.EnableMeridianSpike(); world.EnableThunderBell();
            world.EnableFalterMirror(); world.EnableLastStandCharm(); world.EnableAfflictionSeal();
            int[] ids = { ExpeditionEquipment.EchoJade, ExpeditionEquipment.ComradeKnot, ExpeditionEquipment.FieldKit };
            var guards = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            Check(ids.All(id => !gear.IsForSale(ExpeditionEquipment.Definition(id))) &&
                guards.All(n => !gear.PreviewArtifactChoices(n.Id).Intersect(ids).Any()), "Old reward rolls are preserved");
            world.EnableSupportArtifacts();
            for (int slot = 0; slot < ids.Length; slot++)
            {
                int id = ids[slot];
                var site = guards.First(n => gear.PreviewArtifactChoices(n.Id).Contains(id));
                var preview = gear.PreviewArtifactChoices(site.Id).ToArray();
                site.ClaimClear(1,1,true);
                Check(gear.IsForSale(ExpeditionEquipment.Definition(id)) && preview.Distinct().Count() == 3 &&
                    gear.OfferReward(site.Id).SequenceEqual(preview) && gear.ClaimReward(Array.IndexOf(preview,id)) &&
                    gear.EquipInstance(1,gear.Instances.Last().Id,slot) && gear.OfferReward(site.Id).Count == 0,
                    "Each new item can be selected once from its displayed choices");
            }
            var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            party.Restore(record.Expedition.Last());
            Check(gear.BattleTriggers(1).HasFlag(ArtifactTrigger.EchoJade | ArtifactTrigger.ComradeKnot | ArtifactTrigger.FieldKit),
                "All three worn effects survive an archive round trip");
        });
        Run("Critical artifacts enter first clear choices only after opt-in and retain their owners", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            world.EnableSupportArtifacts();
            int[] ids = { ExpeditionEquipment.HiddenEdgePearl, ExpeditionEquipment.JadeRendBlade, ExpeditionEquipment.BreachMirror };
            var nodes = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            Check(nodes.All(n => !gear.PreviewArtifactChoices(n.Id).Intersect(ids).Any()) &&
                ids.All(id => !gear.IsForSale(ExpeditionEquipment.Definition(id))), "Old campaigns keep previous offers");
            world.EnableCriticalArtifacts();
            for (int slot = 0; slot < ids.Length; slot++)
            {
                int id = ids[slot];
                var node = nodes.First(n => !n.FirstClear.HasValue && gear.PreviewArtifactChoices(n.Id).Contains(id));
                var preview = gear.PreviewArtifactChoices(node.Id).ToArray();
                node.ClaimClear(party.LeaderId, 1, true);
                Check(preview.Length == 3 && preview.Distinct().Count() == 3 && gear.OfferReward(node.Id).SequenceEqual(preview) &&
                    gear.IsForSale(ExpeditionEquipment.Definition(id)) && gear.ClaimReward(Array.IndexOf(preview,id)) &&
                    gear.EquipInstance(party.LeaderId,gear.Instances.Last().Id,slot) && gear.OfferReward(node.Id).Count == 0,
                    "Claim and equip exactly one first-clear item");
            }
            var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            party.Restore(record.Expedition.Last());
            Check(gear.BattleTriggers(party.LeaderId).HasFlag(ArtifactTrigger.HiddenEdgePearl | ArtifactTrigger.JadeRendBlade | ArtifactTrigger.BreachMirror),
                "Critical build survives archival and restore");
        });
        Run("Pearl rolls once per basic attack and preserves charge across active skills", () =>
        {
            var fighter = new CriticalFighter { Equipment = ArtifactTrigger.HiddenEdgePearl };
            var enemy = new CriticalFighter();
            int rolls = 0;
            int Miss() { rolls++; return 99; }
            bool Hit(CombatCast cast, Func<int> roll) => cast.IsCriticalHit(enemy,0,5,roll,out _);
            var basic = new CombatCast(fighter,true,null);
            Check(!Hit(basic,Miss) && !Hit(basic,Miss) && rolls == 1 && fighter.PearlBonus == 10, "Several impacts only bank one missed basic");
            Check(!Hit(new CombatCast(fighter,false,null),()=>14) && fighter.PearlBonus == 10, "Active cast ignores pearl stacks");
            Check(Hit(new CombatCast(fighter,true,null),()=>14) && fighter.PearlBonus == 0, "Next basic crit at 15 percent resets pearl");
            for (int i = 0; i < 10; i++) Check(!Hit(new CombatCast(fighter,true,null),Miss),"Roll of 99 misses until guaranteed");
            Check(Hit(new CombatCast(fighter,true,null),Miss) && fighter.PearlBonus == 0 &&
                CriticalFighter.DamagePercent(ArtifactTrigger.JadeRendBlade) == 190 && CriticalFighter.DamagePercent(0) == 150,
                "Capped charge guarantees a critical, blade adds 40 percentage points");
        });
        Run("Mirror forces one ally cast against one target, with real-time expiry and cooldown", () =>
        {
            var marker = new CriticalFighter { Equipment = ArtifactTrigger.BreachMirror };
            var secondMarker = new CriticalFighter { Equipment = ArtifactTrigger.BreachMirror };
            var ally = new CriticalFighter();
            var target = new CriticalFighter();
            var other = new CriticalFighter();
            Check(marker.TryMark(target,1) && !secondMarker.TryMark(target,1), "Only one mark fits on the target");
            bool Hit(CombatCast cast, CriticalFighter victim, float time, out bool opened) =>
                cast.IsCriticalHit(victim,time,5,()=>99,out opened);
            Check(!Hit(new CombatCast(marker,false,null),target,2,out _) &&
                !Hit(new CombatCast(ally,true,null),target,2,out _) && target.HasMark(2), "Owner and ally basics cannot consume the mark");
            var volley = new CombatCast(ally,false,null);
            Check(!Hit(volley,other,2,out _) && Hit(volley,target,2,out bool used) && used &&
                Hit(volley,target,5,out used) && !used && !Hit(volley,other,5,out _) &&
                !Hit(new CombatCast(ally,false,null),target,5,out _), "Delayed hits share the force only on their marked target");
            Check(!marker.TryMark(target,8.99f) && marker.TryMark(target,9) && !target.HasMark(12) &&
                !Hit(new CombatCast(ally,false,null),target,12,out _) && marker.MarksApplied == 2 && marker.MarksUsed == 1,
                "Eight-second cooldown and three-second expiry use combat time");
        });
        Run("Echo casts and same-column shields observe independent combat limits", () =>
        {
            var output = new ArtifactBattleOutput();
            output.ChargeEcho();
            Check(output.BeginEchoCast() == null, "One basic hit cannot prepare the jade");
            output.ChargeEcho(); output.ChargeEcho();
            var first = output.BeginEchoCast();
            Check(first != null && output.BeginEchoCast() == null, "Extra hits cannot bank a second cast");
            output.ChargeEcho(); output.ChargeEcho();
            var second = output.BeginEchoCast();
            Check(second.TakePower(31) == 24 && first.TakePower(30) == 24 &&
                first.TakePower(30) == 0 && second.TakePower(31) == 0, "Overlapping projectiles each resolve once per original cast");
            Check(output.TryKnotShield(100,51,100,200) == 0 && output.TryKnotShield(45,40,100,200) == 0 &&
                output.TryKnotShield(51,0,100,200) == 0, "No trigger above half, below an existing threshold or on a lethal hit");
            Check(output.TryKnotShield(51,50,100,200) == 24 && output.TryKnotShield(75,30,100,200) == 0,
                "Crossing half grants 12 percent of the supporter's max health, once");
            var world = ExpeditionFixture().State;
            var party = world.Expedition;
            var gear = party.Equipment;
            world.EnableSupportArtifacts(); gear.EnableArtifacts(); gear.EnableLinks(); party.Recruit(0);
            world.Actors[1].AwardLegacySpiritStones(100);
            int rear = party.CompanionId(0);
            Check(gear.Buy(ExpeditionEquipment.ComradeKnot) && gear.EquipInstance(rear,gear.Instances.Last().Id,0) &&
                party.Place(1,0) && party.Place(rear,3), "Place the supporter behind the leader");
            Check(party.KnotBearerFor(0) == rear && party.KnotBearerFor(1) == 0 && party.KnotBearerFor(3) == 0,
                "Shield only the directly aligned front cell");
            party.Place(rear,4);
            Check(party.KnotBearerFor(0) == 0 && party.KnotBearerFor(1) == rear, "Moving the knot changes its protected column");
        });
        Run("Field Kit settles a first clear once, using surviving wearers from the final formation", () =>
        {
            var sim = ExpeditionFixture(new RecordedPartyResolver());
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableAttrition(); gear.EnableArtifacts(); gear.EnableLinks(); world.EnableSupportArtifacts();
            sim.EnableSharedClears(); party.Recruit(0);
            var captain = world.Actors[1];
            captain.AwardLegacySpiritStones(100);
            int companion = party.CompanionId(0);
            void EquipKit(int actor) => Check(gear.Buy(ExpeditionEquipment.FieldKit) &&
                gear.EquipInstance(actor,gear.Instances.Last().Id,0), "Equip a real purchased kit");
            EquipKit(1); EquipKit(companion);
            party.SetCompanionHealth(0,10);
            int amount = party.CompanionMaxHealth(0) * 12 / 100;
            BattleSettlement report = null;
            IEnumerator Capture(BattleSettlement value) { report = value; yield break; }
            sim.SetBattleSettlementPresenter(Capture);
            void Fight(int node)
            {
                world.Nodes[node].MonsterHealth = 10;
                sim.SubmitPlayerAction(WorldAction.Move(node));
            }
            Fight(2);
            Check(report.WonFirstClear && report.FieldTreatment == (0,amount) && party.CompanionHealth(0) == 10 + amount,
                "Two deployed kits pay once and the health is retained");
            sim.SubmitPlayerAction(WorldAction.Move(1)); Fight(2);
            Check(!report.WonFirstClear && report.FieldTreatment.Amount == 0 && party.CompanionHealth(0) == 10 + amount,
                "Winning again cannot farm first-clear healing");
            party.Bench(companion);
            party.SetCompanionHealth(0,3);
            Fight(3);
            Check(report.FieldTreatment == (0,amount) && party.CompanionHealth(0) == 3 + amount, "A deployed kit may treat a wounded reserve");
            gear.Unequip(1,0);
            Fight(4);
            Check(report.FieldTreatment.Amount == 0 && party.CompanionHealth(0) == 3 + amount, "A benched kit cannot treat the party");
        });
        Run("A fallen Field Kit wearer cannot heal the new captain", () =>
        {
            var sim = ExpeditionFixture(new ExpeditionTestResolver());
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableAttrition(); gear.EnableArtifacts(); gear.EnableLinks(); world.EnableSupportArtifacts();
            party.Recruit(0);
            world.Actors[1].AwardLegacySpiritStones(100);
            Check(gear.Buy(ExpeditionEquipment.FieldKit) && gear.EquipInstance(1,gear.Instances.Last().Id,0), "Equip the doomed captain");
            BattleSettlement report = null;
            IEnumerator Capture(BattleSettlement value) { report = value; yield break; }
            sim.SetBattleSettlementPresenter(Capture);
            int expected = party.CompanionHealth(0) / 2;
            world.Nodes[2].MonsterHealth = 10;
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(report.WonFirstClear && party.LeaderRole == 0 && report.FieldTreatment.Amount == 0 && world.Actors[1].Health == expected,
                "Promotion cannot turn a dead wearer's kit into a surviving wearer's kit");
        });
        Run("Affliction Seal counts distinct pre-hit conditions and preserves its first-clear award", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining(); world.EnableQuakeSpike(); world.EnableRainTassel();
            world.EnableRelayClasp(); world.EnableReprisalSeal(); world.EnableHeartLamp();
            world.EnableTinderPouch(); world.EnableMeridianSpike(); world.EnableThunderBell();
            world.EnableFalterMirror(); world.EnableLastStandCharm();
            int id = ExpeditionEquipment.AfflictionSeal;
            var guards = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            var item = ExpeditionEquipment.Definition(id);
            Check(!gear.IsForSale(item) && guards.All(n => !gear.PreviewArtifactChoices(n.Id).Contains(id)), "Earlier checkpoints keep the previous pool");
            world.EnableAfflictionSeal();
            var site = guards.First(n => gear.PreviewArtifactChoices(n.Id).Contains(id));
            var preview = gear.PreviewArtifactChoices(site.Id).ToArray();
            site.ClaimClear(1,1,true);
            Check(gear.IsForSale(item) && preview.Distinct().Count() == 3 && gear.OfferReward(site.Id).SequenceEqual(preview) &&
                gear.ClaimReward(Array.IndexOf(preview,id)) && gear.EquipInstance(1,gear.Instances.Last().Id,0), "Preview, claim and equip the new seal");
            var archive = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            party.Restore(archive.Expedition.Last());
            Check(gear.BattleTriggers(1).HasFlag(ArtifactTrigger.AfflictionSeal) && gear.OfferReward(site.Id).Count == 0, "Round-trip preserves wearer and unique award");

            var output = new ArtifactBattleOutput();
            Check(output.AfflictionPower(31,0,false,false) == 0 && output.AfflictionPower(31,6,false,false) == 0 &&
                output.AfflictionPower(31,0,true,false) == 0 && output.AfflictionPower(31,0,false,true) == 0,
                "Neither one status nor several layers of one status can trigger the seal");
            Check(output.AfflictionPower(31,1,true,false) == 15 && output.AfflictionPower(31,5,false,true) == 15 &&
                output.AfflictionPower(31,0,true,true) == 15, "All three pairs grant half attack rounded down");
            Check(output.AfflictionPower(31,1,true,true) == 31 && output.AfflictionPower(31,6,true,true) == 31 &&
                output.DoubleAfflictionHits == 3 && output.TripleAfflictionHits == 2, "All three statuses grant full attack just once per hit");
        });
        Run("Last Stand Charm preserves three boosted hits after healing and resets each battle", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining(); world.EnableQuakeSpike(); world.EnableRainTassel();
            world.EnableRelayClasp(); world.EnableReprisalSeal(); world.EnableHeartLamp();
            world.EnableTinderPouch(); world.EnableMeridianSpike(); world.EnableThunderBell(); world.EnableFalterMirror();
            int id = ExpeditionEquipment.LastStandCharm;
            var guards = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            var item = ExpeditionEquipment.Definition(id);
            Check(!gear.IsForSale(item) && guards.All(n => !gear.PreviewArtifactChoices(n.Id).Contains(id)), "Earlier checkpoints keep their reward pool");
            world.EnableLastStandCharm();
            var site = guards.First(n => gear.PreviewArtifactChoices(n.Id).Contains(id));
            var preview = gear.PreviewArtifactChoices(site.Id).ToArray();
            site.ClaimClear(1,1,true);
            Check(gear.IsForSale(item) && preview.Distinct().Count() == 3 && gear.OfferReward(site.Id).SequenceEqual(preview) &&
                gear.ClaimReward(Array.IndexOf(preview,id)) && gear.EquipInstance(1,gear.Instances.Last().Id,0), "Claim and equip the first-clear charm");
            var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            party.Restore(record.Expedition.Last());
            Check(gear.BattleTriggers(1).HasFlag(ArtifactTrigger.LastStandCharm) && gear.OfferReward(site.Id).Count == 0,
                "Equipped charm persists and the reward stays claimed");

            var battle = new ArtifactBattleOutput();
            battle.CheckLastStandHealth(51,101);
            battle.CheckLastStandHealth(0,101);
            Check(!battle.LastStandActive && battle.TakeLastStandHit(30) == 0, "Above half and fallen wearers gain no charges");
            battle.CheckLastStandHealth(50,100);
            battle.CheckLastStandHealth(90,100);
            Check(battle.TakeLastStandHit(30) == 18 && battle.TakeLastStandHit(31) == 18,
                "Exactly half health arms the charm and healing preserves its charges");
            battle.CheckLastStandHealth(35,100);
            Check(battle.TakeLastStandHit(1) == 1 && battle.TakeLastStandHit(30) == 0 && battle.LastStandHits == 3,
                "Falling low again cannot replenish charges");
            var next = new ArtifactBattleOutput();
            Check(next.TakeLastStandHit(30) == 0, "Unspent charges never arrive in the next battle");
            next.CheckLastStandHealth(49,101);
            Check(next.TakeLastStandHit(30) == 18 && next.LastStandHits == 1, "A wounded entrant can activate anew");
        });
        Run("Falter Mirror primes weakness from the front and builds existing openings on its own cooldown", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining(); world.EnableQuakeSpike(); world.EnableRainTassel();
            world.EnableRelayClasp(); world.EnableReprisalSeal(); world.EnableHeartLamp();
            world.EnableTinderPouch(); world.EnableMeridianSpike(); world.EnableThunderBell();
            int id = ExpeditionEquipment.FalterMirror;
            var item = ExpeditionEquipment.Definition(id);
            var guards = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            Check(!gear.IsForSale(item) && guards.All(n => !gear.PreviewArtifactChoices(n.Id).Contains(id)), "Earlier runs keep their original pool");
            world.EnableFalterMirror();
            var site = guards.First(n => gear.PreviewArtifactChoices(n.Id).Contains(id));
            var preview = gear.PreviewArtifactChoices(site.Id).ToArray();
            site.ClaimClear(1,1,true);
            Check(gear.IsForSale(item) && preview.Distinct().Count() == 3 && gear.OfferReward(site.Id).SequenceEqual(preview) &&
                gear.ClaimReward(Array.IndexOf(preview,id)) && gear.EquipInstance(1,gear.Instances.Last().Id,0), "Select and equip the mirror");
            var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            party.Restore(record.Expedition.Last());
            Check(gear.BattleTriggers(1).HasFlag(ArtifactTrigger.FalterMirror) && gear.OfferReward(site.Id).Count == 0, "Worn mirror persists, reward stays claimed");

            var output = new ArtifactBattleOutput();
            int weakened = 0, openings = 0;
            void Hit(float time, bool front, bool weak) => output.ResolveFalterMirror(time,front,weak,() => weakened++, () =>
            {
                if (openings >= 3) return false;
                openings++;
                return true;
            });
            Hit(0,false,true);
            Check(weakened == 0 && openings == 0, "Back-row hits do not activate the mirror or spend its cooldown");
            Hit(0,true,false);
            Hit(2.99f,true,true);
            Check(weakened == 1 && openings == 0, "First hit weakens and rapid hits cannot add openings");
            Hit(3,true,true);
            Check(weakened == 2 && openings == 1, "Exactly three seconds later the still-weakened enemy gains an opening");
            Hit(8,true,false);
            Check(weakened == 3 && openings == 1, "An expired weakness must be applied again");
            Hit(11,true,true); Hit(14,true,true); Hit(17,true,true);
            Check(output.MirrorWeakens == 6 && output.MirrorOpenings == 3 && openings == 3, "At the opening cap only weakness and duration refresh");
            var ally = new ArtifactBattleOutput();
            ally.ResolveFalterMirror(0,true,true,() => weakened++,() => true);
            Check(ally.MirrorWeakens == 1 && ally.MirrorOpenings == 1, "A teammate's existing weakness works on the first hit");
        });
        Run("Thunder Bell borrows allied marks and keeps its three-hit rhythm", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining(); world.EnableQuakeSpike(); world.EnableRainTassel();
            world.EnableRelayClasp(); world.EnableReprisalSeal(); world.EnableHeartLamp();
            world.EnableTinderPouch(); world.EnableMeridianSpike();
            int id = ExpeditionEquipment.ThunderBell;
            var item = ExpeditionEquipment.Definition(id);
            var guardians = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            Check(!gear.IsForSale(item) && guardians.All(n => !gear.PreviewArtifactChoices(n.Id).Contains(id)),
                "Earlier checkpoints replay the former shop and first-clear pool");
            world.EnableThunderBell();
            var site = guardians.First(n => gear.PreviewArtifactChoices(n.Id).Contains(id));
            var preview = gear.PreviewArtifactChoices(site.Id).ToArray();
            site.ClaimClear(1,1,true);
            Check(gear.IsForSale(item) && preview.Distinct().Count() == 3 && gear.OfferReward(site.Id).SequenceEqual(preview) &&
                gear.ClaimReward(Array.IndexOf(preview,id)) && gear.EquipInstance(1,gear.Instances.Last().Id,0),
                "The same bell can be selected in the first-clear reward and equipped");
            var archive = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            party.Restore(archive.Expedition.Last());
            Check(gear.BattleTriggers(1).HasFlag(ArtifactTrigger.ThunderBell) && gear.OfferReward(site.Id).Count == 0,
                "The worn bell survives an archive round trip and the reward stays claimed");
            var battle = new ArtifactBattleOutput();
            var powers = new[]{false,false,false,true,true,true,false,false,false}
                .Select(marked => battle.ThunderBellPower(marked,31)).ToArray();
            Check(powers.SequenceEqual(new[]{0,0,18,18,18,18,0,0,18}),
                "Every third unassisted hit and every marked hit qualify once at 60 percent attack");
            Check(new ArtifactBattleOutput().ThunderBellPower(false,31) == 0 &&
                new ArtifactBattleOutput().ThunderBellPower(true,31) == 18, "Fresh combatants start with their own count");
        });
        Run("Meridian Spike collects only unpaid bleed ticks from the oldest two layers", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining(); world.EnableQuakeSpike(); world.EnableRainTassel();
            world.EnableRelayClasp(); world.EnableReprisalSeal(); world.EnableHeartLamp(); world.EnableTinderPouch();
            int id = ExpeditionEquipment.MeridianSpike;
            var guards = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            var item = ExpeditionEquipment.Definition(id);
            Check(!gear.IsForSale(item) && guards.All(n => !gear.PreviewArtifactChoices(n.Id).Contains(id)), "Legacy pool stays replayable");
            world.EnableMeridianSpike();
            var site = guards.First(n => gear.PreviewArtifactChoices(n.Id).Contains(id));
            var preview = gear.PreviewArtifactChoices(site.Id).ToArray();
            site.ClaimClear(1,1,true);
            Check(gear.IsForSale(item) && gear.OfferReward(site.Id).SequenceEqual(preview) &&
                gear.ClaimReward(Array.IndexOf(preview,id)) && gear.EquipInstance(1,gear.Instances.Last().Id,0), "Claim and equip the first-clear item");
            var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            party.Restore(record.Expedition.Last());
            Check(gear.BattleTriggers(1).HasFlag(ArtifactTrigger.MeridianSpike) && gear.OfferReward(site.Id).Count == 0,
                "Equipment persists without repeating the first-clear reward");

            var layers = new List<(int Paid, int Damage)> { (5,7), (2,3) };
            bool Consume(out long burst) => MeridianBleed.TryConsume(layers,6000,l => l.Paid,l => l.Damage,out burst);
            Check(!Consume(out long damage) && damage == 0 && layers.Count == 2, "Below threshold, leave the existing layers alone");
            layers.Add((1,9));
            Check(Consume(out damage) && damage == 19 && layers.SequenceEqual(new[]{(1,9)}),
                "Pay one remaining tick of seven and four of three, preserving the newest layer");
            Check(!Consume(out damage) && damage == 0, "Cashed-out ticks cannot pay again");
            layers.AddRange(new[]{(0,2),(0,4),(0,5)});
            Check(Consume(out damage) && damage == 57 && layers.SequenceEqual(new[]{(0,4),(0,5)}),
                "Extra layers remain in application order with their own snapshot damage");
        });
        Run("Tinder Pouch lights the first surviving target and feeds every third hit after that", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining(); world.EnableQuakeSpike(); world.EnableRainTassel();
            world.EnableRelayClasp(); world.EnableReprisalSeal(); world.EnableHeartLamp();
            int id = ExpeditionEquipment.TinderPouch;
            var guardians = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            var item = ExpeditionEquipment.Definition(id);
            Check(!gear.IsForSale(item) && guardians.All(n => !gear.PreviewArtifactChoices(n.Id).Contains(id)),
                "Older reward choices replay before the pouch becomes available");
            world.EnableTinderPouch();
            var site = guardians.First(n => gear.PreviewArtifactChoices(n.Id).Contains(id));
            var preview = gear.PreviewArtifactChoices(site.Id).ToArray();
            site.ClaimClear(1,1,true);
            Check(gear.IsForSale(item) && preview.Distinct().Count() == 3 && gear.OfferReward(site.Id).SequenceEqual(preview) &&
                gear.ClaimReward(Array.IndexOf(preview,id)) && gear.EquipInstance(1,gear.Instances.Last().Id,0),
                "Select the pouch from its preview and equip it");
            var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            party.Restore(record.Expedition.Last());
            Check(gear.BattleTriggers(1).HasFlag(ArtifactTrigger.TinderPouch) && gear.OfferReward(site.Id).Count == 0,
                "Saved equipment keeps the effect and a first-clear reward is only claimable once");
            var battle = new ArtifactBattleOutput();
            var layers = Enumerable.Range(0,8).Select(i => battle.TinderLayersOnBasicHit()).ToArray();
            Check(layers.SequenceEqual(new[]{2,0,0,1,0,0,1,0}) && new ArtifactBattleOutput().TinderLayersOnBasicHit() == 2,
                "Hits one, four and seven ignite and each wearer starts fresh each battle");
        });
        Run("Heart Lamp shares actual ally healing once with the lowest-health wounded teammate", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining(); world.EnableQuakeSpike(); world.EnableRainTassel();
            world.EnableRelayClasp(); world.EnableReprisalSeal();
            int id = ExpeditionEquipment.HeartLamp;
            var definition = ExpeditionEquipment.Definition(id);
            var guardians = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            Check(!gear.IsForSale(definition) && guardians.All(n => !gear.PreviewArtifactChoices(n.Id).Contains(id)),
                "Earlier runs keep their original shop and first-clear pool");
            world.EnableHeartLamp();
            var site = guardians.First(n => gear.PreviewArtifactChoices(n.Id).Contains(id));
            int slot = Array.IndexOf(gear.PreviewArtifactChoices(site.Id).ToArray(), id);
            site.ClaimClear(1,1,true);
            Check(gear.IsForSale(definition) && gear.OfferReward(site.Id)[slot] == id && gear.ClaimReward(slot) &&
                gear.EquipInstance(1,gear.Instances.Last().Id,0) && gear.BattleTriggers(1).HasFlag(ArtifactTrigger.HeartLamp),
                "The first-clear selection equips a working lamp on a regular wearer");
            var allies = new[] { ("primary",1,10,100), ("fallen",2,0,100), ("healer",3,9,20),
                ("guard",4,30,100), ("full",5,30,30) };
            var output = new ArtifactBattleOutput();
            int calls = 0;
            int Apply(string recipient,int amount)
            {
                calls++;
                Check(recipient == "guard", "Choose the lowest remaining HP ratio, skipping the primary and the fallen");
                return amount;
            }
            Check(output.ResolveHeartLamp("primary",21,allies,Apply) == "guard" && calls == 1 && output.LampHealing == 10,
                "A single transfer grants half the primary gain rounded down");
            output.ResolveHeartLamp("primary",4,allies,Apply);
            Check(calls == 2 && output.LampTransfers == 2 && output.LampHealing == 12, "A four-point gain transfers two");
            output.ResolveHeartLamp("primary",0,allies,Apply);
            output.ResolveHeartLamp("primary",1,allies,Apply);
            output.ResolveHeartLamp("primary",20,allies.Where(a => a.Item1 != "guard" && a.Item1 != "healer"),Apply);
            Check(calls == 2, "No transfer for pure overheal, an insufficient gain, or no other injured survivor");
            Check(output.ResolveHeartLamp("primary",20,new[]{("healer",3,99,100)}, (who,amount) => Math.Min(amount,1)) == "healer" &&
                output.LampHealing == 13 && output.LampTransfers == 3, "The wounded healer can receive it and only actual HP is credited");
        });
        Run("Reprisal Seal banks only lost HP until the next basic hit", () =>
        {
            var world = RouteCampaign(HexWorldMap.Id(10,-8)).State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining(); world.EnableQuakeSpike(); world.EnableRainTassel(); world.EnableRelayClasp();
            int id = ExpeditionEquipment.ReprisalSeal;
            var guardians = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            Check(!gear.IsForSale(ExpeditionEquipment.Definition(id)) &&
                guardians.All(n => !gear.PreviewArtifactChoices(n.Id).Contains(id)), "Old reward pool is preserved");
            world.EnableReprisalSeal();
            var site = guardians.First(n => gear.PreviewArtifactChoices(n.Id).Contains(id));
            int slot = Array.IndexOf(gear.PreviewArtifactChoices(site.Id).ToArray(), id);
            site.ClaimClear(1,1,true);
            Check(gear.IsForSale(ExpeditionEquipment.Definition(id)) && gear.OfferReward(site.Id)[slot] == id &&
                gear.ClaimReward(slot) && gear.EquipInstance(1,gear.Instances.Last().Id,0), "Select and equip the seal");
            var effects = gear.BattleTriggers(1);
            Check(effects.HasFlag(ArtifactTrigger.Sand | ArtifactTrigger.ReprisalSeal), "Seal coexists with the Sand innate");

            var vitality = new ArtifactVitality();
            const int attack = 30, maxHp = 200;
            vitality.LoseHealth(effects,200,200,maxHp,attack);
            Check(vitality.SpendSealCharge(attack) == 0, "Damage fully absorbed by a shield gives no charge");
            int shared = ExpeditionEquipment.SharedLoss(140,maxHp);
            vitality.LoseHealth(effects,200,200-shared,maxHp,attack);
            vitality.LoseHealth(effects,179,178,maxHp,attack);
            Check(vitality.SpendSealCharge(attack) == 11 && vitality.SpendSealCharge(attack) == 0,
                "Shared and direct losses pool before rounding and are spent once");
            vitality.LoseHealth(effects,178,99,maxHp,attack);
            Check(vitality.SpendSealCharge(60) == 30, "Excess charge is discarded at the current attack cap");
            vitality.LoseHealth(ArtifactTrigger.ReprisalSeal,99,79,maxHp,attack);
            Check(vitality.SpendSealCharge(5) == 5, "Any wearer can charge and a lower attack caps the payout");
            vitality.LoseHealth(effects,79,78,maxHp,attack);
            Check(vitality.SpendSealCharge(attack) == 0, "A basic hit clears an unpaired point of HP loss");
            vitality.LoseHealth(effects,78,77,maxHp,attack);
            Check(vitality.SpendSealCharge(attack) == 0, "That point does not carry to the next hit");
            vitality.LoseHealth(ArtifactTrigger.Sand,77,57,maxHp,attack);
            Check(vitality.SpendSealCharge(attack) == 0, "A wearer without the seal gains no charge");
            vitality.LoseHealth(effects,57,37,maxHp,attack);
            Check(new ArtifactVitality().SpendSealCharge(attack) == 0, "A new battle starts empty despite missing HP");
        });
        Run("Relay Clasp grants pursuit and passes one timed follow-up to another ally", () =>
        {
            var world = RouteCampaign(HexWorldMap.Id(0,-10)).State;
            var party = world.Expedition;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining(); world.EnableQuakeSpike(); world.EnableRainTassel();
            var gear = party.Equipment;
            int clasp = ExpeditionEquipment.RelayClasp;
            var guardians = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            Check(!gear.IsForSale(ExpeditionEquipment.Definition(clasp)) &&
                guardians.All(n => !gear.PreviewArtifactChoices(n.Id).Contains(clasp)), "Old pool stays replayable");
            world.EnableRelayClasp();
            var site = guardians.First(n => gear.PreviewArtifactChoices(n.Id).Contains(clasp));
            int slot = Array.IndexOf(gear.PreviewArtifactChoices(site.Id).ToArray(), clasp);
            site.ClaimClear(1,1,true);
            Check(gear.IsForSale(ExpeditionEquipment.Definition(clasp)) && gear.OfferReward(site.Id)[slot] == clasp &&
                gear.ClaimReward(slot) && gear.EquipInstance(1,gear.Instances.Last().Id,0), "Select and equip the clasp");
            var wearer = gear.BattleTriggers(1);
            Check(wearer.HasFlag(ArtifactTrigger.Pursuit | ArtifactTrigger.RelayClasp), "Clasp grants pursuit on its own");

            var mark = new PursuitMark();
            var plain = ArtifactTrigger.None;
            Check(mark.OnBasicHit(0,1,wearer) == PursuitHit.None, "An unmarked hit cannot start a relay");
            mark.Apply(0);
            Check(mark.OnBasicHit(1,1,wearer) == PursuitHit.Opening && PursuitMark.BonusDamage(20) == 10, "Wearer starts pursuit");
            Check(mark.OnBasicHit(1.5f,1,wearer) == PursuitHit.None, "Starter cannot take the follow-up");
            Check(mark.OnBasicHit(2.99f,2,plain) == PursuitHit.Relay && PursuitMark.BonusDamage(34) == 17, "Ally can follow with their own attack");
            Check(mark.OnBasicHit(2.99f,3,wearer) == PursuitHit.None, "One follow-up per mark");
            mark.Apply(3);
            mark.OnBasicHit(3,1,wearer);
            Check(mark.OnBasicHit(5,2,plain) == PursuitHit.None, "Two-second window expires");
            mark.Apply(6);
            mark.OnBasicHit(6,1,wearer);
            Check(mark.OnBasicHit(7,2,wearer) == PursuitHit.Relay && mark.OnBasicHit(7,3,plain) == PursuitHit.None,
                "A second wearer cannot chain another relay");
            mark.Apply(8);
            Check(mark.OnBasicHit(8,2,ArtifactTrigger.Pursuit) == PursuitHit.Opening && mark.OnBasicHit(9,1,wearer) == PursuitHit.None,
                "Ordinary pursuit consumes the mark as before");
        });
        Run("Rain Tassel reward replays, equips with Bamboo, and survives an archive round trip", () =>
        {
            var sim = RouteCampaign(HexWorldMap.Id(0,-10));
            var world = sim.State;
            var party = world.Expedition;
            var gear = party.Equipment;
            party.EnableSignatureArtifacts(); world.EnableSwordTraining(); world.EnableQuakeSpike();
            var guardians = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            int tassel = ExpeditionEquipment.RainTassel;
            Check(guardians.All(n => !gear.PreviewArtifactChoices(n.Id).Contains(tassel)), "Older checkpoints keep their original choices");
            world.EnableRainTassel();
            var site = guardians.First(n => gear.PreviewArtifactChoices(n.Id).Contains(tassel));
            var preview = gear.PreviewArtifactChoices(site.Id).ToArray();
            site.ClaimClear(1,1,true);
            Check(preview.Length == 3 && preview.Distinct().Count() == 3 && gear.OfferReward(site.Id).SequenceEqual(preview) &&
                gear.ClaimReward(Array.IndexOf(preview,tassel)), "Selectable first-clear reward matches preview");
            var item = gear.Instances.Last();
            gear.EquipInstance(1,item.Id,0);
            var expected = ArtifactTrigger.RainTassel | ArtifactTrigger.Bamboo;
            Check((gear.BattleTriggers(1) & expected) == expected, "Both sources deployed");
            var archive = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[]{new SavedLife(1,sim.RecordPlayerLife())})).Single().Record;
            party.Restore(archive.Expedition.Last());
            Check(gear.Worn(1,0)?.DefinitionId == tassel && (gear.BattleTriggers(1) & expected) == expected, "Tassel restores with its wearer");
            Check(gear.Unequip(1,0) && !gear.BattleTriggers(1).HasFlag(ArtifactTrigger.RainTassel) &&
                gear.OfferReward(site.Id).Count == 0, "Unequipping ends the effect and the reward stays claimed");
        });
        Run("Quake Spike joins the first-clear pool with matching preview and a single award", () =>
        {
            var world = RouteCampaign(HexWorldMap.Id(0,-10)).State;
            var party = world.Expedition;
            party.EnableSignatureArtifacts();
            world.EnableSwordTraining();
            var gear = party.Equipment;
            var guards = world.Nodes.Values.Where(n => n.MonsterHealth > 0).ToArray();
            int spike = ExpeditionEquipment.QuakeSpike;
            Check(!guards.Any(n => gear.PreviewArtifactChoices(n.Id).Contains(spike)),"Previous reward pool is replayable");
            world.EnableQuakeSpike();
            var node = guards.First(n => gear.PreviewArtifactChoices(n.Id).Contains(spike));
            var options = gear.PreviewArtifactChoices(node.Id).ToArray();
            Check(options.Distinct().Count() == 3 && options.All(i => !ExpeditionEquipment.Definition(i).Innate),"Three different ordinary items");
            node.ClaimClear(1,1,true);
            Check(gear.OfferReward(node.Id).SequenceEqual(options) && gear.ClaimReward(Array.IndexOf(options,spike)),"Claim the previewed spike");
            var item = gear.Instances.Last();
            Check(item.DefinitionId == spike && gear.EquipInstance(1,item.Id,0) && gear.BattleTriggers(1).HasFlag(ArtifactTrigger.Quake),"The wearer gains the break effect");
            Check(gear.OfferReward(node.Id).Count == 0 && gear.Unequip(1,0) && !gear.BattleTriggers(1).HasFlag(ArtifactTrigger.Quake),"No repeat payment or lingering effect");
        });
        Run("Quake Spike front and back rows resolve the same hit against the same line-up", () =>
        {
            // At the instant of impact the two melee enemies occupy the two adjacent hexes in row 3.
            // Compare the same column in player rows 2 and 0; a ranged enemy remains in row 5.
            (ArtifactBattleOutput report,int hp) Impact(int row)
            {
                const int hpBefore = 90, attack = 20, shield = 8;
                var report = new ArtifactBattleOutput();
                var enemyHp = new[]{15,40,50};
                var cells = new[]{(3,3),(4,3),(3,5)};
                double DistanceSquared(int x,int y)
                {
                    double dx = x + (y%2==0 ? .5 : 0) - 3.5;
                    return dx*dx + .75*(y-row)*(y-row);
                }
                var nearby = Enumerable.Range(0,cells.Length).Where(i => DistanceSquared(cells[i].Item1,cells[i].Item2) <= 1);
                const int rangedHit = 8;
                // The same archer breaks an identical shield in both positions; surviving adjacent enemies retaliate.
                report.ResolveQuake(nearby,attack,(i,power) =>
                {
                    int dealt = Math.Min(enemyHp[i],power);
                    enemyHp[i] -= dealt;
                    return dealt;
                });
                int retaliation = nearby.Where(i => i < 2 && enemyHp[i] > 0).Count()*16;
                return (report,hpBefore-(rangedHit-shield)-retaliation);
            }
            var front = Impact(2);
            var back = Impact(0);
            Check(front.report.Quakes == 1 && front.report.QuakeHits == 2 && front.report.QuakeDamage == 35 && front.hp == 74,"Front hits both melee units and caps damage at actual health");
            Check(back.report.Quakes == 1 && back.report.QuakeHits == 0 && back.report.QuakeDamage == 0 && back.hp == 90,"Rear break is reported even when empty");
        });
        Run("All nine items appear in first-clear choices and named innates stay out", () =>
        {
            var world = RouteCampaign(HexWorldMap.Id(0,-10)).State;
            var party = world.Expedition; var gear = party.Equipment;
            party.EnableSignatureArtifacts();
            var awarded = new HashSet<int>();
            foreach (var node in world.Nodes.Values.Where(n => n.MonsterHealth > 0))
            {
                var preview = gear.PreviewArtifactChoices(node.Id).ToArray();
                Check(preview.Length == 3 && preview.Distinct().Count() == 3 && preview.All(id => id >= 5 && id <= 13),"Three different ordinary items");
                node.ClaimClear(1,1,true);
                Check(gear.OfferReward(node.Id).SequenceEqual(preview),"Preview equals award");
                int index = Array.FindIndex(preview,id => !awarded.Contains(id));
                if (index < 0) index = 0;
                Check(gear.ClaimReward(index) && gear.Instances.Last().DefinitionId == preview[index] && gear.OfferReward(node.Id).Count == 0,"One award goes into shared inventory");
                awarded.Add(preview[index]);
                if (awarded.Count == 9) break;
            }
            Check(awarded.Count == 9,"All nine reachable");
            Check(ExpeditionEquipment.SharedLoss(100,80)==15 && ExpeditionEquipment.SharedLoss(100,4)==4 && ExpeditionEquipment.RingThreshold(101)==16,"Shield remainder, lethal sharing and ring rounding");
        });
        foreach (string strategy in new[]{"direct","detour","rendezvous"})
            Run("Campaign route reaches the world clear: "+strategy,() => RunCampaignRoute(strategy));
    }

    private static void RunCampaignRoute(string strategy)
    {
        int start = HexWorldMap.Id(-10,2), meeting = HexWorldMap.Id(-8,0), gate = HexWorldMap.Id(-2,0);
        var sim = RouteCampaign(start);
        sim.State.Expedition.EnableSignatureArtifacts();
        var w = sim.State;
        var player = w.Actors[1];
        var party = w.Expedition;
        var gear = party.Equipment;
        if (strategy == "direct")
        {
            foreach (var item in gear.Instances.ToArray())
                gear.EquipInstance(1,item.Id,gear.FirstFreeSlot(1));
            Check(gear.BattleTriggers(1).HasFlag(ArtifactTrigger.Pursuit) &&
                gear.BattleTriggers(1).HasFlag(ArtifactTrigger.Recovery),"Direct loadout actually equipped");
        }
        int steps = 0;
        void Travel(int to)
        {
            var route = WorldPathfinder.FindRoute(w,player.NodeId.Value,new[]{to});
            foreach (int step in route)
            {
                if (player.Health < player.MaxHealth && w.Nodes[step].MonsterHealth > 0)
                    while (player.Health < player.MaxHealth) sim.SubmitPlayerAction(WorldAction.Rest());
                var options = gear.PreviewArtifactChoices(step).ToArray();
                Check(gear.PendingRewards.Count==0,"Map previews do not open a reward choice");
                sim.SubmitPlayerAction(WorldAction.Move(step));
                steps++;
                Check(player.IsAlive,"Survive "+strategy+" at "+w.Nodes[step].Q+","+w.Nodes[step].R+" year "+w.Year);
                if (options.Length>0 && w.Nodes[step].FirstClear?.ActorId==1)
                    Check(w.Nodes[step].FirstArtifact==options[0],"Map preview matches actual guardian award");
                foreach (var drop in gear.Instances.Where(i=>i.Owner<0 && (strategy != "detour" || i.Id > 3)).ToArray())
                    if (gear.FirstFreeSlot(1)>=0) gear.EquipInstance(1,drop.Id,gear.FirstFreeSlot(1));
            }
        }

        if (strategy=="detour")
        {
            Travel(HexWorldMap.Id(-7,4));
            Check(player.Level>1,"Side exploration brings a breakthrough");
            Travel(meeting);
        }
        if (strategy=="rendezvous")
        {
            int oldBirth=HexWorldMap.Id(-10,5);
            var former = RouteCampaign(oldBirth);
            var path=WorldPathfinder.FindRoute(former.State,oldBirth,new[]{meeting});
            foreach (int step in path) former.SubmitPlayerAction(WorldAction.Move(step));
            w.PlayableBirths.UnionWith(new[]{start,oldBirth});
            sim.AddHistory(former.RecordPlayerLife(),10);
            IEnumerator Accept(int id,Action<bool> choose) { choose(true); yield break; }
            sim.SetPastInvitationPresenter(Accept);
            Travel(meeting);
            while (w.Year < path.Count && !w.Actors[10].HasJoinedPlayer) sim.SubmitPlayerAction(WorldAction.Wait(1));
            Check(w.Actors[10].HasJoinedPlayer && party.HasRecruited(4) && w.Nodes.Values.Any(n=>n.FirstClear?.ActorId==10),
                "Both teams clear their own sites before joining");
        }
        Travel(gate);
        Check(w.Nodes[gate].FirstClear?.ActorId==1,"The expedition clears the last guardian for the Chosen");
        int shortest = WorldPathfinder.FindRoute(w,start,new[]{gate}).Count;
        if (strategy=="direct") Check(steps==shortest,"Direct loadout uses only the shortest path");
        if (strategy=="detour") Check(steps>shortest && gear.RewardsTaken>0,"Side clears supply extra experience and actual equipment");
        int gateYear=w.Year, gateLevel=player.Level;
        while (!sim.CanJoinChosen(1) && !w.IsEnded && w.Actors[w.ChosenActorId.Value].IsAlive) sim.SubmitPlayerAction(WorldAction.Wait(1));
        Check(sim.CanJoinChosen(1),strategy+" year "+w.Year+" chosen health "+w.Actors[1000].Health+" deaths "+string.Join(",",sim.Events.Where(e=>e.Kind==WorldEventKind.Died).Select(e=>e.NodeId)));
        sim.SubmitPlayerAction(WorldAction.JoinChosen());
        while (!w.IsEnded && w.Year<110) sim.AdvanceOneYear();
        Check(w.Outcome==RunOutcome.Cleared && sim.Events.Count(e=>e.Kind==WorldEventKind.WorldCleared)==1,
            "Real ritual completion on the campaign route");
        Console.WriteLine($"  {strategy}: {steps} moves, gate year {gateYear}, level {gateLevel}, world cleared year {w.Year}");

    }
}
