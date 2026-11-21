using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WorldTimeline;

internal static partial class WorldSimulationTests
{
    private static StoryConfig StoryFixture(StoryCondition condition = StoryCondition.Always, int value = 0,
        int delay = 0, bool repeat = true) => new StoryConfig(
        new[] { new StoryDefinition { Id = 1, NodeId = 3, Title = "石门", Text = "试炼", Repeatable = repeat } },
        new[] { new StoryOption { Id = 11, EventId = 1, Text = "取剑", Result = "成功", Condition = condition,
            Value = value, Hint = "不足", ItemId = 91, Weight = 100, TimeYears = delay, SetFlag = "opened" },
            new StoryOption { Id = 12, EventId = 1, Text = "离开", Result = "未取", Weight = 1 } },
        new[] { new StoryItem { Id = 91, Name = "残剑", Description = "攻击提升", AttackBonus = 12, CombatAttackPercent = 15 } });
    private static void RunStoryTests()
    {
        Run("Ashridge lends kindling before Rekindle reopens the kiln without another first clear", () =>
        {
            var world = new WorldState(1);
            foreach (var pair in HexWorldMap.Create()) world.Nodes.Add(pair.Key, pair.Value);
            int mentor = HexWorldMap.Id(-8, 5);
            var kiln = world.Nodes[NodeState.PracticeKilnId];
            kiln.SetPracticeKiln();
            var player = new ActorState(1, HexWorldMap.Id(-8, 4), 500, 100);
            world.Actors.Add(1, player);
            var sim = new WorldSimulation(world);
            sim.ConfigureStories(new StoryConfig(new[] { new StoryDefinition { Id = 26, NodeId = mentor } },
                new[] { new StoryOption { Id = 2602, EventId = 26, ItemId = 9135, TimeYears = 1 },
                    new StoryOption { Id = 2603, EventId = 26, ItemId = 9131 }, new StoryOption { Id = 2604, EventId = 26 } },
                new[] { new StoryItem { Id = 9131 }, new StoryItem { Id = 9135 } }),
                replayPlayerChoices: new[] { new RecordedStoryChoice(26, 1, 2603), new RecordedStoryChoice(26, 2, 2602) });
            sim.Stories.AllowOptionsOnRevisit(2602);
            sim.SubmitPlayerAction(WorldAction.Move(mentor));
            sim.SubmitPlayerAction(WorldAction.Move(kiln.Id));
            var first = kiln.FirstClear;
            Check(player.Items.Contains(9131) && kiln.GuardianCount == 0 && first.HasValue, "Kindling trial cleared");
            sim.SubmitPlayerAction(WorldAction.Move(mentor));
            Check(player.Items.Contains(9135) && kiln.GuardianCount == 3 && kiln.FirstClear == first,
                "The lesson restores all three targets and preserves the first clear");
        });
        Run("Salt channel replay drains the shared arena and awards its art once", () =>
        {
            const string clue = "salt_channel_clue";
            var unlock = new StoryOption { Id = 4801, EventId = 48, Condition = StoryCondition.FlagSetAndUnset,
                Flag = clue, SetFlag = SaltChannel.DrainedFlag };
            var config = new StoryConfig(
                new[]
                {
                    new StoryDefinition { Id = 46, NodeId = SaltChannel.Ditch },
                    new StoryDefinition { Id = 48, NodeId = SaltChannel.Drain, Repeatable = true },
                    new StoryDefinition { Id = 49, NodeId = SaltChannel.Head, Repeatable = true }
                },
                new[]
                {
                    new StoryOption { Id = 4601, EventId = 46, SetFlag = clue }, unlock,
                    new StoryOption { Id = 4802, EventId = 48, Condition = StoryCondition.FlagSet, Flag = SaltChannel.DrainedFlag },
                    new StoryOption { Id = 4803, EventId = 48 },
                    new StoryOption { Id = 4901, EventId = 49, ItemId = SaltChannel.RewardItemId },
                    new StoryOption { Id = 4902, EventId = 49 }
                },
                new[] { new StoryItem { Id = SaltChannel.RewardItemId } });

            WorldSimulation Setup(int playerStart)
            {
                var state = new WorldState(1);
                foreach (var pair in HexWorldMap.Create()) state.Nodes.Add(pair.Key, pair.Value);
                foreach (var node in state.Nodes.Values) { node.MonsterHealth = 0; node.Items.Clear(); }
                state.Nodes[SaltChannel.Beach].SetGuardianGroup(2, 48, 7);
                state.Nodes[SaltChannel.Head].MonsterHealth = 185;
                state.Nodes[SaltChannel.Head].MonsterAttack = 16;
                state.Actors.Add(1, new ActorState(1, playerStart, 500, 100, 100));
                var sim = new WorldSimulation(state);
                sim.ConfigureStories(config);
                return sim;
            }

            var history = Setup(SaltChannel.Spring);
            Check(history.Stories.UnavailableReason(history.State.Actors[1], unlock) != null,
                "The spillway requires the tally");
            history.SubmitPlayerAction(WorldAction.Move(SaltChannel.Ditch));
            history.SubmitPlayerAction(WorldAction.Move(SaltChannel.Beach));
            history.SubmitPlayerAction(WorldAction.Move(SaltChannel.Drain));
            Check(history.State.SaltChannelDrained && history.RecordPlayerLife().StoryChoices.Any(c => c.OptionId == 4801),
                "Opening the spillway records the choice and drains the world");

            var replay = Setup(HexWorldMap.Id(5, -6));
            replay.AddHistory(history.RecordPlayerLife(), 10);
            replay.SubmitPlayerAction(WorldAction.Wait(3));
            Check(replay.State.SaltChannelDrained && replay.State.Nodes[SaltChannel.Head].MonsterHealth == 185,
                "The archived character drains the spillway while the full-health boss still guards the channel");
            replay.SubmitPlayerAction(WorldAction.Move(SaltChannel.Head));
            Check(replay.State.Actors[1].Items.Contains(SaltChannel.RewardItemId) &&
                replay.Stories.ClaimedItems.Contains(SaltChannel.RewardItemId), "Defeating the beast grants the passive art");
            replay.SubmitPlayerAction(WorldAction.Move(HexWorldMap.Id(5, -6)));
            replay.SubmitPlayerAction(WorldAction.Move(SaltChannel.Head));
            Check(replay.RecordPlayerLife().StoryChoices.Count(c => c.OptionId == 4901) == 1,
                "Returning after taking the reward does not award it again");

            var bypass = Setup(HexWorldMap.Id(5, -6));
            bypass.SubmitPlayerAction(WorldAction.Move(SaltChannel.Head));
            Check(!bypass.State.SaltChannelDrained && bypass.State.Actors[1].Items.Contains(SaltChannel.RewardItemId),
                "The direct route still grants the same reward");
        });
        Run("Replayed story choice weakens the guardian for later arrivals", () =>
        {
            const string flag = "gorge_braced";
            var brace = new StoryOption
            {
                Id = 11, EventId = 1, Condition = StoryCondition.FlagUnset, Flag = flag,
                SetFlag = flag, HealthCost = 12, Hint = "卸石桩不可用"
            };
            var config = new StoryConfig(
                new[] { new StoryDefinition { Id = 1, NodeId = 3 } },
                new[] { brace, new StoryOption { Id = 12, EventId = 1 } },
                Array.Empty<StoryItem>());

            WorldSimulation Setup(IEnumerable<RecordedStoryChoice> choices = null)
            {
                var sim = Create();
                sim.State.Actors.Remove(2);
                sim.State.Nodes[2].MonsterHealth = 20;
                sim.State.Nodes[2].MonsterAttack = 15;
                sim.ConfigureStories(config, replayPlayerChoices: choices);
                sim.Stories.WeakenGuardianOnFlag(flag, 2, 7);
                return sim;
            }

            var source = Setup(new[] { new RecordedStoryChoice(1, 1, 11) });
            source.SubmitPlayerAction(WorldAction.Move(3));
            Check(source.State.Actors[1].Health == 88 && source.State.Nodes[2].MonsterAttack == 8,
                "The choice charges HP once and changes the guardian's shared attack");
            Check(source.Stories.UnavailableReason(source.State.Actors[1], brace) == brace.Hint,
                "The same brace cannot be set twice");

            var replay = Setup();
            replay.AddHistory(source.RecordPlayerLife(), 10);
            replay.SubmitPlayerAction(WorldAction.Move(2, 2));
            Check(replay.State.Actors[1].Health == 92, "The later player fights the weaker guardian");
            Check(replay.State.Actors[10].Health == 88, "Only the actor setting the brace pays the cost");

            var cleared = Setup();
            cleared.State.Nodes[2].MonsterHealth = 0;
            Check(cleared.Stories.UnavailableReason(cleared.State.Actors[1], brace) == brace.Hint,
                "Clearing the pass disables the repair choice");
        });
        Run("Merchant purchases survive save replay and recheck each historical purchase", () =>
        {
            var config = new StoryConfig(
                new[] { new StoryDefinition { Id = 11, NodeId = 3, Repeatable = true, IsMerchant = true } },
                new[]
                {
                    new StoryOption { Id = 1100, EventId = 11 },
                    new StoryOption { Id = 1101, EventId = 11, ItemId = 9131, LegacyPrice = 8, Weight = 0 },
                    new StoryOption { Id = 1102, EventId = 11, ItemId = 9161, LegacyPrice = 6, Weight = 0 }
                },
                new[] { new StoryItem { Id = 9131 }, new StoryItem { Id = 9161, HealthBonus = 60 } });
            var source = Create();
            source.State.LegacyTradeThroughYear = 1;
            source.ConfigureStories(config, interactivePlayer: true);
            var player = source.State.Actors[1];
            player.AwardLegacySpiritStones(14);
            using var visit = FlattenRoutine(source.SubmitPlayerActionRoutine(WorldAction.Move(3)));
            Check(visit.MoveNext() && source.Stories.Choose(1101), "Player can buy the first item");
            Check(visit.MoveNext() && player.LegacySpiritStones == 6 &&
                !source.Stories.Choose(1101) && source.Stories.Choose(1102), "Stock updates during the same visit");
            Check(visit.MoveNext() && source.Stories.Choose(1100) && !visit.MoveNext(), "Leaving resumes the map action");
            Check(player.LegacySpiritStones == 0 && player.MaxHealth == 160, "The second purchase applies its item effect");
            var archived = WorldTimelineSaveStore.Decode(
                WorldTimelineSaveStore.Encode(new[] { new SavedLife(1, source.RecordPlayerLife()) })).Single().Record;
            Check(archived.StoryChoices.Select(choice => choice.OptionId).SequenceEqual(new[] { 1101, 1102, 1100 }) &&
                archived.StoryChoices.All(choice => choice.Occurrence == 1), "The whole visit is recorded in order");

            var replay = Create();
            replay.State.LegacyTradeThroughYear = 1;
            replay.ConfigureStories(config);
            replay.AddHistory(archived, 10);
            replay.State.Actors[10].AwardLegacySpiritStones(6);
            replay.SubmitPlayerAction(WorldAction.Wait());
            Check(replay.State.Actors[10].Items.SequenceEqual(new[] { 9161 }) &&
                replay.State.Actors[10].LegacySpiritStones == 0 && !replay.Stories.ClaimedItems.Contains(9131),
                "An unaffordable purchase is skipped without skipping later recorded purchases");

            var fresh = Create();
            fresh.ConfigureStories(config);
            fresh.SubmitPlayerAction(WorldAction.Move(3));
            Check(fresh.State.Actors[1].Items.Count == 0,
                "A visit without recorded choices does not buy at random");

            var gift = Create();
            gift.ConfigureStories(config, interactivePlayer: true);
            using var encounter = FlattenRoutine(gift.SubmitPlayerActionRoutine(WorldAction.Move(3)));
            Check(encounter.MoveNext() && gift.Stories.Choose(1102) && !encounter.MoveNext(),
                "An empty wallet can choose a gift and close the encounter");
            Check(gift.State.Actors[1].Items.SequenceEqual(new[] { 9161 }), "The chosen gift is granted");
            gift.SubmitPlayerAction(WorldAction.Move(1));
            gift.SubmitPlayerAction(WorldAction.Move(3));
            Check(gift.State.Actors[1].Items.Count == 1, "A revisit cannot claim a second gift");

            var giftReplay = Create();
            giftReplay.ConfigureStories(config);
            giftReplay.AddHistory(archived, 10);
            giftReplay.Stories.ClaimedItems.Add(9131);
            giftReplay.SubmitPlayerAction(WorldAction.Wait());
            Check(giftReplay.State.Actors[10].Items.Count == 0 && !giftReplay.Stories.ClaimedItems.Contains(9161),
                "A taken gift does not cause a historical actor to take a different item");
        });
        Run("Lightning route allows gated revisits and battles with newly acquired skills", () =>
        {
            var world = new WorldState(1);
            for (int i = 1; i <= 4; i++) world.Nodes.Add(i, new NodeState(i));
            foreach (var node in world.Nodes.Values) node.Links.UnionWith(world.Nodes.Keys.Where(id => id != node.Id));
            var player = new ActorState(1, 1, 200, 30);
            world.Actors.Add(1, player);
            var encounters = new StoryTrialResolver();
            var sim = new WorldSimulation(world, encounters);
            int[] items = { 9151, 9152, 9153 };
            var events = items.Select((_, i) => new StoryDefinition { Id = i + 1, NodeId = i + 2 }).ToArray();
            var options = items.SelectMany((id, i) => new[]
            {
                new StoryOption { Id = 10 + i, EventId = i + 1, ItemId = id,
                    Condition = i == 0 ? StoryCondition.Always : StoryCondition.HasItem,
                    Value = i == 0 ? 0 : items[i - 1] },
                new StoryOption { Id = 20 + i, EventId = i + 1 }
            });
            sim.ConfigureStories(new StoryConfig(events, options, items.Select(id => new StoryItem { Id = id })),
                replayPlayerChoices: new[] { new RecordedStoryChoice(1, 1, 10) });
            sim.Stories.AllowOptionsOnRevisit(11, 12);
            for (int i = 0; i < items.Length; i++)
            {
                world.Nodes[i + 2].StoryTrialItemId = items[i];
                world.Nodes[i + 2].StoryTrialStage = i + 1;
            }
            foreach (int node in new[] { 3, 4, 3 }) sim.SubmitPlayerAction(WorldAction.Move(node));
            Check(encounters.Count == 0 && player.Items.Count == 0 &&
                sim.RecordPlayerLife().StoryChoices.Count == 2, "Early revisits do not replay ordinary dialogue");
            foreach (int node in new[] { 2, 3, 4, 3, 4 }) sim.SubmitPlayerAction(WorldAction.Move(node));
            Check(encounters.Count == 3 && player.Items.SequenceEqual(items), "Each trial runs once after its reward is acquired");
        });
        Run("Historical story conditions use current attributes", () =>
        {
            var source=Create(health:501);source.ConfigureStories(StoryFixture(StoryCondition.MaxHealthGreater,500),
                replayPlayerChoices:new[]{new RecordedStoryChoice(1,1,11)});
            source.SubmitPlayerAction(WorldAction.Move(3));
            var replay=Create();replay.ConfigureStories(StoryFixture(StoryCondition.MaxHealthGreater,500));
            replay.AddHistory(source.RecordPlayerLife(),10);replay.State.Actors[10].MaxHealth=500;
            replay.SubmitPlayerAction(WorldAction.Wait());
            Check(!replay.State.Actors[10].Items.Contains(91),"Recorded success cannot bypass current threshold");
        });
        Run("Item consumption checks health after removing its bonus", () =>
        {
            var item=new StoryItem{Id=91,Name="玉佩",HealthBonus=80};
            var option=new StoryOption{Id=11,EventId=1,CostItemId=91,HealthCost=15};
            var cfg=new StoryConfig(new[]{new StoryDefinition{Id=1,NodeId=3}},new[]{option,new StoryOption{Id=12,EventId=1}},new[]{item});
            var sim=Create();sim.ConfigureStories(cfg);var actor=sim.State.Actors[1];actor.Items.Add(91);actor.Health=20;
            Check(sim.Stories.UnavailableReason(actor,option)!=null,"A nonlethal cost cannot kill after unequipping a health item");
        });
        Run("Story strict threshold and interactive world pause", () =>
        {
            var sim = Create(health: 500); sim.ConfigureStories(StoryFixture(StoryCondition.MaxHealthGreater, 500), interactivePlayer: true);
            StoryDrain(sim, sim.SubmitPlayerActionRoutine(WorldAction.Move(3)), 12, true);
            Check(sim.State.Year == 1 && !sim.State.Actors[1].Items.Contains(91), "500 is not greater than 500");
            Check(sim.RecordPlayerLife().StoryChoices.Single().OptionId == 12, "Only the choice is recorded");
        });
        Run("Story rewards are unique and re-entry cannot farm", () =>
        {
            var sim = Create(health: 501); sim.ConfigureStories(StoryFixture(StoryCondition.MaxHealthGreater, 500),
                replayPlayerChoices: new[] { new RecordedStoryChoice(1, 1, 11) });
            sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(sim.State.Actors[1].Attack == 22 && sim.State.Actors[1].CombatAttack == 25, "Item and passive affect battle attack");
            sim.SubmitPlayerAction(WorldAction.Move(1)); sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(sim.State.Actors[1].Items.Count(x => x == 91) == 1 && sim.State.Actors[1].Attack == 22, "No duplicate reward");
        });
        Run("Story choice persists through version two save", () =>
        {
            var source = Create(); source.ConfigureStories(StoryFixture(), replayPlayerChoices: new[] { new RecordedStoryChoice(1, 1, 12) });
            source.SubmitPlayerAction(WorldAction.Move(3));
            var encoded = WorldTimelineSaveStore.Encode(new[] { new SavedLife(1, source.RecordPlayerLife()) });
            var restored = WorldTimelineSaveStore.Decode(encoded).Single().Record;
            Check(restored.StoryChoices.Single().OptionId == 12, "Save round trip retains deliberate no-reward branch");
            var replay = Create(); replay.ConfigureStories(StoryFixture()); replay.AddHistory(restored, 44);
            replay.SubmitPlayerAction(WorldAction.Wait());
            Check(!replay.State.Actors[44].Items.Contains(91) && replay.Events.Any(e => e.ActorId == 44 && e.Kind == WorldEventKind.StoryResolved), "Historical choice overrides random preference");
        });
        Run("Remembered rewards re-evaluate current world claims", () =>
        {
            var source = Create(); source.ConfigureStories(StoryFixture(), replayPlayerChoices: new[] { new RecordedStoryChoice(1, 1, 11) });
            source.SubmitPlayerAction(WorldAction.Move(3, 3));
            var replay = Create(attack: 100); replay.ConfigureStories(StoryFixture(),
                replayPlayerChoices: new[] { new RecordedStoryChoice(1, 1, 11) });
            replay.AddHistory(source.RecordPlayerLife(), 10); replay.SubmitPlayerAction(WorldAction.Move(3));
            // The player takes the object before the recorded arrival; later choices cannot restore it.
            replay.SubmitPlayerAction(WorldAction.Move(1)); replay.SubmitPlayerAction(WorldAction.Wait());
            Check(replay.Stories.ClaimedItems.Count == 1 && !replay.State.Actors[10].Items.Contains(91), "Current-run treasure state wins over recorded outcome");
        });
        Run("Automatic branch independent of history instance ID", () =>
        {
            var source = Create(); source.SubmitPlayerAction(WorldAction.Move(3)); var life = source.RecordPlayerLife();
            string Outcome(int id)
            {
                var sim = Create(); sim.ConfigureStories(StoryFixture(), 73); sim.AddHistory(life, id);
                sim.SubmitPlayerAction(WorldAction.Wait());
                return sim.Events.Single(e => e.Kind == WorldEventKind.StoryResolved).Detail;
            }
            Check(Outcome(10) == Outcome(99), "No selection-order RNG dependency");
        });
        Run("Story delay consumes whole years and resets global flag", () =>
        {
            var sim = Create(); sim.ConfigureStories(StoryFixture(delay: 2), replayPlayerChoices: new[] { new RecordedStoryChoice(1, 1, 11) });
            sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(sim.State.Year == 3 && sim.Stories.Flags.Contains("opened"), "Travel plus two actual annual steps");
            var next = Create(); next.ConfigureStories(StoryFixture());
            Check(!next.Stories.Flags.Contains("opened") && next.Stories.ClaimedItems.Count == 0, "Each run starts a fresh world");
        });
        Run("Older life records remain readable", () =>
        {
            var source = Create(); source.SubmitPlayerAction(WorldAction.Wait());
            var bytes = WorldTimelineSaveStore.Encode(new[] { new SavedLife(1, source.RecordPlayerLife()) });
            // Optional story milestones, guardian death and choices are removed newest first.
            foreach (int version in new[] { 7, 6, 5, 4, 3, 2, 1 })
            {
                Array.Resize(ref bytes, bytes.Length - (version == 1 || version == 7 ? 4 : 1));
                bytes[4] = (byte)version;
                var record = WorldTimelineSaveStore.Decode(bytes).Single().Record;
                Check(record.StoryChoices.Count == 0 && record.EndYear == 1 &&
                    !record.RitualBreakYear.HasValue && !record.SluiceOpeningYear.HasValue &&
                    !record.NorthRelayRestoredYear.HasValue, "Backward-compatible legacy reader");
            }
        });
        Run("Interactive and checkpoint replay produce same story effects", () =>
        {
            var interactive = Create(); interactive.ConfigureStories(StoryFixture(delay: 1), 91, true);
            StoryDrain(interactive, interactive.SubmitPlayerActionRoutine(WorldAction.Move(3)), 11, false);
            var replay = Create(); replay.ConfigureStories(StoryFixture(delay: 1), 91, false, interactive.RecordPlayerLife().StoryChoices);
            replay.SubmitPlayerAction(WorldAction.Move(3));
            Check(Signature(interactive) == Signature(replay), "Identical annual events, choices and state on restore");
        });
        Run("Sluice opening records the living cultivator and takes effect when replayed", () =>
        {
            var original = Create(attack: 100);
            original.ConfigureStories(StoryFixture(), replayPlayerChoices: new[] { new RecordedStoryChoice(1, 1, 11) });
            original.Stories.ConfigureSluiceOpening(11);
            Check(!original.RecordCompletedLife(), "Cannot archive before opening the sluice");
            original.SubmitPlayerAction(WorldAction.Move(3));
            Check(original.State.SluiceOpening == (1, 1) && original.WaitingForPlayer, "Opening preserves player control");
            original.SubmitPlayerAction(WorldAction.Wait());
            Check(original.RecordCompletedLife() && original.State.Outcome == RunOutcome.LifeRecorded,
                "Player can end their life record after continuing to explore");
            var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(
                new[] { new SavedLife(1, original.RecordPlayerLife()) })).Single().Record;
            Check(record.SluiceOpeningYear == 1 && record.EndYear == 2 && !record.EndedInDeath, "Archive preserves both dates and survival");

            var replay = Create(attack: 100);
            replay.ConfigureStories(StoryFixture());
            replay.Stories.ConfigureSluiceOpening(11);
            replay.AddHistory(record, 10);
            Check(replay.State.SluiceOpening == null, "A recorded opening does not affect the new world before replay");
            replay.SubmitPlayerAction(WorldAction.Wait());
            Check(replay.State.SluiceOpening == (10, 1) && !replay.RecordCompletedLife(),
                "History opens at the recorded time, without ending the current player's life");
        });
        Run("The sluice exposes the boss after replay and keeps the ward drained after a reset", () =>
        {
            WorldSimulation Setup(bool drainsBoss)
            {
                var sim = CreateRitual(110, 50, bossHealth: 650, bossAttack: 65);
                sim.ConfigureStories(StoryFixture(), replayPlayerChoices: new[] { new RecordedStoryChoice(1, 1, 11) });
                sim.Stories.ConfigureSluiceOpening(11, drainsBoss);
                return sim;
            }

            var source = Setup(true);
            source.SubmitPlayerAction(WorldAction.Move(3));
            Check(source.State.Nodes[4].MonsterHealth == 227, "Opening drains the ward from the final encounter");
            var replay = Setup(true);
            replay.AddHistory(source.RecordPlayerLife(), 10);
            Check(replay.State.Nodes[4].MonsterHealth == 650, "An archive does not drain the ward in advance");
            replay.SubmitPlayerAction(WorldAction.Wait());
            Check(replay.State.Nodes[4].MonsterHealth == 227, "The recorded opening drains the ward when replayed");
            replay.SubmitPlayerAction(WorldAction.Move(4));
            Check(replay.State.Ritual.ResetCount == 1 && replay.State.Nodes[4].MonsterHealth == 227,
                "A ritual reset rebuilds only the exposed stele");

            var damaged = Setup(true);
            damaged.State.Nodes[4].MonsterHealth = 50;
            damaged.SubmitPlayerAction(WorldAction.Move(3));
            Check(damaged.State.Nodes[4].MonsterHealth == 50, "Opening never heals an already damaged stele");
            var legacy = Setup(false);
            legacy.SubmitPlayerAction(WorldAction.Move(3));
            Check(legacy.State.SluiceOpening.HasValue && legacy.State.Nodes[4].MonsterHealth == 650,
                "Legacy runs still record the sluice without changing the health pool");
        });
        Run("Restoring the north array weakens the boss only after the replayed deed", () =>
        {
            WorldSimulation Setup(IEnumerable<RecordedStoryChoice> choices = null)
            {
                var sim = CreateRitual(110, 50, bossAttack: 65);
                sim.ConfigureStories(StoryFixture(), replayPlayerChoices: choices);
                sim.Stories.ConfigureNorthRelayRestoration(11);
                return sim;
            }

            var original = Setup(new[] { new RecordedStoryChoice(1, 1, 11) });
            original.SubmitPlayerAction(WorldAction.Wait(6));
            original.SubmitPlayerAction(WorldAction.Move(3));
            Check(original.State.NorthRelayRestored == (1, 7) && original.State.Nodes[4].MonsterAttack == 52,
                "Quest completion changes the actual final encounter attack");
            Check(original.RecordCompletedLife(), "The surviving player can archive their completed route");
            var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(
                new[] { new SavedLife(1, original.RecordPlayerLife()) })).Single().Record;
            Check(record.NorthRelayRestoredYear == 7 && !record.EndedInDeath, "Restoration year survives archive roundtrip");

            var replay = Setup();
            replay.AddHistory(record, 10);
            replay.SubmitPlayerAction(WorldAction.Wait(6));
            Check(!replay.State.NorthRelayRestored.HasValue && replay.State.Nodes[4].MonsterAttack == 65,
                "A forecast has no combat effect");
            replay.SubmitPlayerAction(WorldAction.Wait());
            Check(replay.State.NorthRelayRestored == (10, 7) && replay.State.Nodes[4].MonsterAttack == 52 &&
                !replay.RecordCompletedLife(), "History restores the array without ending the current life");
            replay.SubmitPlayerAction(WorldAction.Move(4));
            Check(replay.State.Ritual.ResetCount == 1 && replay.State.Nodes[4].MonsterAttack == 52,
                "A ritual reset still uses the weakened boss attack");
        });
        Run("The restored spring heals the chosen only on arrival", () =>
        {
            WorldSimulation Setup()
            {
                var world = new WorldState(1);
                for (int i = 1; i <= 5; i++) world.Nodes.Add(i, new NodeState(i));
                world.Nodes[1].Links.UnionWith(new[] { 2, 3 });
                world.Nodes[2].Links.Add(1);
                world.Nodes[3].Links.Add(4);
                world.Nodes[4].Links.Add(5);
                world.Actors.Add(1, new ActorState(1, 1, 100, 100) { Health = 50 });
                var sim = new WorldSimulation(world);
                sim.ConfigureChosen(new ActorState(1000, 3, 100, 100) { Health = 20 },
                    new[] { 3, 4, 5 }, yearsPerStep: 3);
                sim.ConfigureFinalRitual(100, 20, 100);
                var story = new StoryConfig(
                    new[] { new StoryDefinition { Id = 1, NodeId = 2 } },
                    new[] { new StoryOption { Id = 11, EventId = 1 } }, Array.Empty<StoryItem>());
                sim.ConfigureStories(story);
                sim.Stories.ConfigureNorthRelayRestoration(11, 4);
                return sim;
            }

            var early = Setup();
            early.SubmitPlayerAction(WorldAction.Move(2));
            Check(early.State.Actors[1].Health == 50 && early.State.Actors[1000].Health == 20,
                "Restoration itself does not heal either character");
            early.SubmitPlayerAction(WorldAction.Wait(2));
            Check(early.State.Actors[1000].Health == 100 &&
                early.Events.Single(entry => entry.Kind == WorldEventKind.NorthRelaySpringUsed).Detail == "80",
                "The chosen heals on arriving at the restored spring");

            var late = Setup();
            late.SubmitPlayerAction(WorldAction.Wait(3));
            late.SubmitPlayerAction(WorldAction.Move(2));
            Check(late.State.NorthRelayRestored.HasValue && late.State.Actors[1000].Health == 20 &&
                !late.Events.Any(entry => entry.Kind == WorldEventKind.NorthRelaySpringUsed),
                "Passing the dry spring grants no retroactive healing");
        });
        Run("Unrecorded revisits skip player-only story choices", () =>
        {
            var config = new StoryConfig(
                new[] { new StoryDefinition { Id = 1, NodeId = 3 } },
                new[] { new StoryOption { Id = 11, EventId = 1, Weight = 0, SetFlag = "restored" },
                    new StoryOption { Id = 12, EventId = 1 } }, Array.Empty<StoryItem>());
            var sim = Create();
            sim.ConfigureStories(config);
            sim.Stories.AllowOptionsOnRevisit(11);
            sim.SubmitPlayerAction(WorldAction.Move(3));
            sim.SubmitPlayerAction(WorldAction.Move(1));
            sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(!sim.Stories.Flags.Contains("restored") && sim.RecordPlayerLife().StoryChoices.Count == 1,
                "An unrecorded revisit cannot commit the player's special choice or interrupt the timeline");
        });
        Run("Revoked contract removes exactly one living guard when replayed", () =>
        {
            WorldSimulation Setup()
            {
                var sim = Create(attack: 100);
                sim.State.Nodes[2].SetGuardianGroup(2, 210, 42);
                sim.ConfigureStories(StoryFixture(), replayPlayerChoices: new[] { new RecordedStoryChoice(1, 1, 11) });
                sim.Stories.ConfigureContractRevocation(11, 2);
                return sim;
            }
            var source = Setup();
            source.SubmitPlayerAction(WorldAction.Move(3, 2));
            Check(source.State.ContractRevoked == (1, 2) && source.State.Nodes[2].GuardianCount == 1 &&
                source.State.Nodes[2].MonsterHealth == 210 && source.RecordCompletedLife(), "Revocation leaves a single guard and permits living archive");
            var record = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(
                new[] { new SavedLife(1, source.RecordPlayerLife()) })).Single().Record;
            Check(record.ContractRevokedYear == 2 && !record.EndedInDeath, "Revocation survives archive roundtrip");
            var replay = Setup();
            replay.AddHistory(record, 10);
            replay.SubmitPlayerAction(WorldAction.Wait());
            Check(replay.State.Nodes[2].GuardianCount == 2, "Forecast leaves both guards in place");
            replay.SubmitPlayerAction(WorldAction.Wait());
            Check(replay.State.ContractRevoked == (10, 2) && replay.State.Nodes[2].GuardianCount == 1 &&
                !replay.RecordCompletedLife(), "Past life applies the effect without ending the current life");
            var cleared = Setup();
            cleared.State.Nodes[2].SetGuardianFormation(42, Array.Empty<int>());
            cleared.SubmitPlayerAction(WorldAction.Move(3));
            Check(cleared.State.ContractRevoked.HasValue && cleared.State.Nodes[2].GuardianCount == 0,
                "Revoking a contract never repopulates a cleared gate");
        });
    }

    private sealed class StoryTrialResolver : IWorldPartyBattleResolver
    {
        public int Count;
        public BattleResult Resolve(int firstHealth, int firstAttack, int secondHealth, int secondAttack)
            => throw new InvalidOperationException("Trial must provide a formation.");

        public int[] ResolveParty(WorldState world, int nodeId, IReadOnlyList<ActorState> first,
            IReadOnlyList<ActorState> second)
        {
            Count++;
            Check(second.Count == Count && first[0].Items.Contains(9150 + Count),
                "One, two, and three enemies appear with the new skill already equipped");
            if (Count == 2) Check(second[0].Health < second[1].Health, "The relay trial has a weaker first target");
            var result = new int[1 + second.Count];
            result[0] = first[0].Health;
            return result;
        }
    }
    private static void StoryDrain(WorldSimulation sim, IEnumerator action, int choice, bool checkBlocked)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(action);
        int loops = 0;
        while (stack.Count > 0)
        {
            if (++loops > 10000) throw new Exception("Story loop did not finish");
            var current = stack.Peek();
            if (!current.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
            if (sim.Stories.PendingChoice != null)
            {
                int year = sim.State.Year;
                for (int i = 0; i < 4; i++) { Check(current.MoveNext(), "Story must keep waiting"); Check(sim.State.Year == year, "No world time while choice is open"); }
                if (checkBlocked) Check(!sim.Stories.Choose(11), "Unavailable branch rejected without mutation");
                Check(sim.Stories.Choose(choice), "Accept valid choice");
                Check(!sim.Stories.Choose(choice), "Reject double-click");
            }
            else if (sim.Stories.PendingResult != null) sim.Stories.Continue();
            else throw new Exception("Unexpected asynchronous yield");
        }
    }
}
