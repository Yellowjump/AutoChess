using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public enum StoryCondition { Always, MaxHealthGreater, HasItem, FlagSet, FlagUnset }
    public sealed class StoryDefinition
    {
        public int Id, NodeId;
        public string Title, Text, Background;
        public bool Repeatable;
        public bool IsMerchant;
    }
    public sealed class StoryOption
    {
        public int Id, EventId, Value, Weight = 1, ItemId, CostItemId, HealthCost, Heal, Experience, TimeYears;
        public StoryCondition Condition;
        public int LegacyPrice;
        public string Flag, SetFlag, Text, Result, Hint;
    }
    public sealed class StoryItem
    {
        public int Id, AttackBonus, HealthBonus, CombatAttackPercent;
        public string Name, Description;
    }
    public sealed class StoryConfig
    {
        public readonly Dictionary<int, StoryDefinition> Events;
        public readonly Dictionary<int, StoryOption> Options;
        public readonly Dictionary<int, StoryItem> Items;
        private readonly Func<string, string> localize;

        public StoryConfig(IEnumerable<StoryDefinition> events, IEnumerable<StoryOption> options, IEnumerable<StoryItem> items,
            Func<string, string> localize = null)
        {
            this.localize = localize;
            Events = events.ToDictionary(x => x.Id); Options = options.ToDictionary(x => x.Id); Items = items.ToDictionary(x => x.Id);
            foreach (var item in Items.Values)
                if (item.Id < 1 || item.AttackBonus < 0 || item.HealthBonus < 0 || item.CombatAttackPercent < 0)
                    throw new ArgumentException("Invalid story item " + item.Id);
            if (Events.Values.Select(x => x.NodeId).Distinct().Count() != Events.Count) throw new ArgumentException("Duplicate story nodes.");
            foreach (var row in Options.Values)
                if (row.Id < 1 || !Enum.IsDefined(typeof(StoryCondition), row.Condition) || !Events.ContainsKey(row.EventId)
                    || row.Weight < 0 || row.HealthCost < 0 || row.TimeYears < 0 || row.Experience < 0 || row.Heal < 0 || row.LegacyPrice < 0
                    || row.ItemId != 0 && !Items.ContainsKey(row.ItemId) || row.CostItemId != 0 && !Items.ContainsKey(row.CostItemId))
                    throw new ArgumentException("Invalid story option " + row.Id);
            foreach (var row in Events.Values)
                if ((!row.IsMerchant && Options.Values.Count(o => o.EventId == row.Id) > 4) || !Options.Values.Any(o => o.EventId == row.Id && o.Condition == StoryCondition.Always && o.ItemId == 0
                    && o.CostItemId == 0 && o.HealthCost == 0 && o.LegacyPrice == 0 && o.Weight > 0))
                    throw new ArgumentException("Story needs an unconditional fallback: " + row.Id);
        }

        internal string Format(string key, string fallback, params object[] arguments)
        {
            string template = localize?.Invoke(key);
            if (string.IsNullOrEmpty(template) || template == key) template = fallback;
            return arguments.Length == 0 ? template : string.Format(template, arguments);
        }

        public StoryConfig WithoutEvents(params int[] eventIds) => new StoryConfig(
            Events.Values.Where(row => !eventIds.Contains(row.Id)),
            Options.Values.Where(row => !eventIds.Contains(row.EventId)), Items.Values, localize);

        public StoryConfig WithoutOptions(params int[] optionIds) => new StoryConfig(
            Events.Values, Options.Values.Where(row => !optionIds.Contains(row.Id)), Items.Values, localize);
    }
    public sealed class RecordedStoryChoice
    {
        public int EventId { get; }
        public int Occurrence { get; }
        public int OptionId { get; }
        public RecordedStoryChoice(int eventId, int occurrence, int optionId)
        {
            if (eventId < 1 || occurrence < 1 || optionId < 1) throw new ArgumentOutOfRangeException();
            EventId = eventId; Occurrence = occurrence; OptionId = optionId;
        }
    }
    public sealed class StoryEncounter
    {
        public StoryDefinition Definition { get; internal set; }
        public ActorState Actor { get; internal set; }
        public int Occurrence { get; internal set; }
        public IReadOnlyList<StoryOption> Options { get; internal set; }
    }
    public sealed class StoryResolution
    {
        public StoryEncounter Encounter { get; internal set; }
        public StoryOption Option { get; internal set; }
        public string Text { get; internal set; }
    }

    public sealed class WorldStorySystem
    {
        public StoryConfig Config { get; }
        public bool InteractivePlayer { get; set; }
        public StoryEncounter PendingChoice { get; private set; }
        public StoryResolution PendingResult { get; private set; }
        public HashSet<string> Flags { get; } = new HashSet<string>();
        public HashSet<int> ClaimedItems { get; } = new HashSet<int>();
        private readonly WorldState world;
        private readonly CultivationConfig growth;
        private readonly int seed;
        private readonly Action<WorldEvent> emit;
        private readonly Action<RecordedStoryChoice> record;
        private readonly Func<int, int, int, IEnumerable<RecordedStoryChoice>> historicalChoices;
        private readonly Dictionary<int, StoryDefinition> byNode;
        private readonly Dictionary<long, int> visits = new Dictionary<long, int>();
        private readonly HashSet<(int ActorId, int MerchantId)> merchantClaims = new HashSet<(int, int)>();
        private readonly RecordedStoryChoice[] playerReplay;
        private int? decision;

        internal WorldStorySystem(WorldState world, StoryConfig config, CultivationConfig growth, int seed,
            Action<WorldEvent> emit, Action<RecordedStoryChoice> record, Func<int, int, int, IEnumerable<RecordedStoryChoice>> historicalChoices,
            IEnumerable<RecordedStoryChoice> playerReplay)
        {
            this.world = world; Config = config; this.growth = growth; this.seed = seed;
            this.emit = emit; this.record = record; this.historicalChoices = historicalChoices;
            byNode = config.Events.Values.ToDictionary(x => x.NodeId);
            this.playerReplay = playerReplay?.ToArray() ?? Array.Empty<RecordedStoryChoice>();
        }
        public bool TryGetDefinition(int nodeId, out StoryDefinition definition) => byNode.TryGetValue(nodeId, out definition);
        private readonly HashSet<int> revisitOptions = new HashSet<int>();
        public void AllowOptionsOnRevisit(params int[] optionIds) => revisitOptions.UnionWith(optionIds);
        public string UnavailableReason(ActorState actor, StoryOption option)
        {
            if (option.ItemId != 0 && ClaimedItems.Contains(option.ItemId)) return Config.Format("Timeline_UI_StoryClaimed", "遗物已被先行者取走");
            if (world.Year <= world.LegacyTradeThroughYear && option.LegacyPrice > actor.LegacySpiritStones)
                return Config.Format("Timeline_MerchantInsufficient", "灵石不足");
            if (option.CostItemId != 0 && !actor.Items.Contains(option.CostItemId))
                return Config.Format("Timeline_UI_StoryMissingItem", "缺少 {0}", Config.Items[option.CostItemId].Name);
            int healthAfterItem = option.CostItemId == 0 ? actor.Health : Math.Max(1, actor.Health - Config.Items[option.CostItemId].HealthBonus);
            if (healthAfterItem <= option.HealthCost) return Config.Format("Timeline_UI_StoryLowHealth", "当前气血不足");
            bool allowed;
            switch (option.Condition)
            {
                case StoryCondition.MaxHealthGreater: allowed = actor.MaxHealth > option.Value; break;
                case StoryCondition.HasItem: allowed = actor.Items.Contains(option.Value); break;
                case StoryCondition.FlagSet: allowed = Flags.Contains(option.Flag); break;
                case StoryCondition.FlagUnset: allowed = !Flags.Contains(option.Flag); break;
                default: allowed = true; break;
            }
            return allowed ? null : string.IsNullOrEmpty(option.Hint)
                ? Config.Format("Timeline_UI_StoryConditionUnmet", "当前条件不满足") : option.Hint;
        }
        public bool Choose(int optionId)
        {
            if (PendingChoice == null || decision.HasValue) return false;
            var option = PendingChoice.Options.FirstOrDefault(x => x.Id == optionId);
            if (option == null || UnavailableReason(PendingChoice.Actor, option) != null) return false;
            decision = optionId; return true;
        }
        public void Continue() { PendingResult = null; }

        internal IEnumerator ResolveRoutine(ActorState actor, int nodeId)
        {
            if (!actor.IsAlive || actor.LeaderId.HasValue || actor.HasJoinedChosen || world.IsEnded || !byNode.TryGetValue(nodeId, out var definition)) yield break;
            long key = ((long)(uint)actor.Id << 32) | (uint)definition.Id;
            visits.TryGetValue(key, out int occurrence);
            var options = Config.Options.Values.Where(x => x.EventId == definition.Id);
            if (occurrence > 0 && !definition.Repeatable)
                options = options.Where(option => revisitOptions.Contains(option.Id) && UnavailableReason(actor, option) == null);
            var availableOptions = options.OrderBy(x => x.Id).ToArray();
            if (availableOptions.Length == 0) yield break;
            occurrence++; visits[key] = occurrence;
            var context = new StoryEncounter { Definition = definition, Actor = actor, Occurrence = occurrence,
                Options = availableOptions };
            bool interactive = actor.Id == world.PlayerActorId && InteractivePlayer && !world.PlayerControlEnded;
            var remembered = actor.Id == world.PlayerActorId
                ? playerReplay.Where(x => x.EventId == definition.Id && x.Occurrence == occurrence)
                : historicalChoices(actor.Id, definition.Id, occurrence);
            if (definition.IsMerchant)
            {
                yield return ResolveMerchant(context, interactive, remembered);
                yield break;
            }
            StoryOption chosen = null;
            if (interactive)
            {
                decision = null; PendingChoice = context;
                while (!decision.HasValue) yield return null;
                chosen = context.Options.Single(x => x.Id == decision.Value);
                PendingChoice = null;
            }
            else
            {
                int? optionId = remembered?.FirstOrDefault()?.OptionId;
                chosen = context.Options.FirstOrDefault(x => x.Id == optionId && UnavailableReason(actor, x) == null);
                if (chosen == null) chosen = Automatic(context);
            }
            var result = Apply(context, chosen);
            if (actor.Id == world.PlayerActorId) record(new RecordedStoryChoice(definition.Id, occurrence, chosen.Id));
            emit(new WorldEvent(world.Year, WorldEventKind.StoryResolved, actor.Id, nodeId,
                definition.Title + " · " + chosen.Text + "：" + result.Text));
            if (interactive)
            {
                PendingResult = result;
                while (PendingResult != null) yield return null;
            }
        }

        private IEnumerator ResolveMerchant(StoryEncounter context, bool interactive, IEnumerable<RecordedStoryChoice> remembered)
        {
            var actor = context.Actor;
            var claim = (actor.Id, context.Definition.Id);
            bool legacyTrade = world.Year <= world.LegacyTradeThroughYear;
            if (!legacyTrade && merchantClaims.Contains(claim)) yield break;
            if (!interactive)
            {
                foreach (var choice in remembered ?? Enumerable.Empty<RecordedStoryChoice>())
                {
                    var option = context.Options.FirstOrDefault(x => x.Id == choice.OptionId);
                    if (option == null) continue;
                    if (option.ItemId == 0) break;
                    if (UnavailableReason(actor, option) == null) CommitPurchase(option);
                    if (!legacyTrade) break;
                }
                yield break;
            }
            while (true)
            {
                decision = null;
                PendingChoice = context;
                while (!decision.HasValue) yield return null;
                var option = context.Options.Single(x => x.Id == decision.Value);
                if (option.ItemId == 0)
                {
                    record(new RecordedStoryChoice(context.Definition.Id, context.Occurrence, option.Id));
                    PendingChoice = null;
                    yield break;
                }
                CommitPurchase(option);
                if (!legacyTrade)
                {
                    PendingChoice = null;
                    yield break;
                }
            }

            void CommitPurchase(StoryOption option)
            {
                var result = Apply(context, option);
                merchantClaims.Add(claim);
                if (actor.Id == world.PlayerActorId)
                    record(new RecordedStoryChoice(context.Definition.Id, context.Occurrence, option.Id));
                emit(new WorldEvent(world.Year, WorldEventKind.StoryResolved, actor.Id, context.Definition.NodeId,
                    context.Definition.Title + " · " + result.Text.Trim()));
            }
        }

        private StoryOption Automatic(StoryEncounter context)
        {
            var available = context.Options.Where(x => x.Weight > 0 && UnavailableReason(context.Actor, x) == null).ToArray();
            long total = available.Sum(x => (long)x.Weight);
            if (total == 0) throw new InvalidOperationException("Story has no available fallback.");
            // No runtime string hashes, Unity RNG, frame counters or history instance IDs.
            uint hash = unchecked((uint)seed * 747796405u ^ (uint)context.Actor.BirthNodeId * 2891336453u
                ^ (uint)context.Definition.Id * 277803737u ^ (uint)context.Occurrence * 1597334677u);
            hash ^= hash >> 16; hash *= 2246822519u; hash ^= hash >> 13;
            long roll = hash % total;
            foreach (var option in available) { if (roll < option.Weight) return option; roll -= option.Weight; }
            return available[available.Length - 1];
        }
        private StoryResolution Apply(StoryEncounter context, StoryOption option)
        {
            var actor = context.Actor;
            if (UnavailableReason(actor, option) != null) throw new InvalidOperationException("Story conditions changed before resolution.");
            if (world.Year <= world.LegacyTradeThroughYear && !actor.TrySpendLegacySpiritStones(option.LegacyPrice))
                throw new InvalidOperationException("Story purchase could not be paid.");
            var rewards = new List<string>();
            if (option.CostItemId != 0)
            {
                actor.Items.Remove(option.CostItemId); ApplyItem(actor, Config.Items[option.CostItemId], -1);
                rewards.Add(Config.Format("Timeline_UI_StorySurrender", "交出 {0}", Config.Items[option.CostItemId].Name));
            }
            actor.Health -= option.HealthCost;
            if (option.HealthCost > 0) rewards.Add(Config.Format("Timeline_UI_StoryLostHealth", "气血 -{0}", option.HealthCost));
            if (option.ItemId != 0)
            {
                var item = Config.Items[option.ItemId]; ClaimedItems.Add(item.Id); actor.Items.Add(item.Id); ApplyItem(actor, item, 1);
                actor.CheckTechniqueItems();
                rewards.Add(Config.Format("Timeline_UI_StoryObtained", "获得 {0}：{1}", item.Name, item.Description));
            }
            if (option.Heal > 0)
            {
                int amount = Math.Min(option.Heal, actor.MaxHealth - actor.Health);
                actor.Health += amount;
                rewards.Add(Config.Format("Timeline_UI_StoryHealed", "恢复气血 {0}", amount));
            }
            if (option.Experience > 0)
            {
                CultivationSystem.Gain(actor, option.Experience, growth, world.Year, emit);
                rewards.Add(Config.Format("Timeline_UI_StoryExperience", "经验 +{0}", option.Experience));
            }
            if (!string.IsNullOrEmpty(option.SetFlag)) Flags.Add(option.SetFlag);
            actor.StoryDelayYears = checked(actor.StoryDelayYears + option.TimeYears);
            if (option.TimeYears > 0) rewards.Add(Config.Format("Timeline_UI_StoryStay", "驻留 {0} 年", option.TimeYears));
            return new StoryResolution { Encounter = context, Option = option,
                Text = option.Result + (rewards.Count == 0 ? "" : "\n\n" + string.Join("\n", rewards)) };
        }
        private static void ApplyItem(ActorState actor, StoryItem item, int sign)
        {
            actor.Attack = checked(actor.Attack + item.AttackBonus * sign);
            actor.MaxHealth = checked(actor.MaxHealth + item.HealthBonus * sign);
            actor.Health = Math.Min(actor.MaxHealth, Math.Max(1, checked(actor.Health + item.HealthBonus * sign)));
            actor.CombatAttackPercent = checked(actor.CombatAttackPercent + item.CombatAttackPercent * sign);
        }
    }
}
