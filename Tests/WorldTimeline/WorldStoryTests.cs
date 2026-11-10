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
        Run("Old version one lives remain readable", () =>
        {
            var source = Create(); source.SubmitPlayerAction(WorldAction.Wait());
            var bytes = WorldTimelineSaveStore.Encode(new[] { new SavedLife(1, source.RecordPlayerLife()) });
            // V2 has no guardian death marker; V1 also has no empty story-choice count.
            foreach (int version in new[] { 2, 1 })
            {
                Array.Resize(ref bytes, bytes.Length - (version == 2 ? 1 : 4));
                bytes[4] = (byte)version;
                var record = WorldTimelineSaveStore.Decode(bytes).Single().Record;
                Check(record.StoryChoices.Count == 0 && record.EndYear == 1, "Backward-compatible legacy reader");
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
