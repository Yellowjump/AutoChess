using System;
using System.Linq;
using DataTable;
using SkillSystem;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

// The Game Framework data-table boundary; simulation and map code receive typed configuration.
public static class WorldTimelineTableConfig
{
    internal static string Localize(string value)
    {
        if (string.IsNullOrEmpty(value) || !GameEntry.Localization.HasRawString(value)) return value;
        return GameEntry.Localization.GetString(value);
    }

    public static WorldTimelineDebugView.ScenarioData Create()
    {
        // Archive validation reads item levels before the first simulation is created.
        ExpeditionEquipment.DefinitionTableLoader=()=>UnityEngine.Resources.Load<UnityEngine.TextAsset>("ExpeditionItems").text;
        WorldTimelineDebugView.ResolveLanguageKey = Localize;
        var growth = GameEntry.DataTable.GetDataTable<DRCultivationLevel>("CultivationLevel");
        var cultivators = GameEntry.DataTable.GetDataTable<DRStartingCultivators>("StartingCultivators");
        var locations = GameEntry.DataTable.GetDataTable<DROuterLocations>("OuterLocations");
        var chosenRoute = GameEntry.DataTable.GetDataTable<DRChosenRoute>("ChosenRoute");
        var stories = GameEntry.DataTable.GetDataTable<DRStoryEvent>("StoryEvent");
        var options = GameEntry.DataTable.GetDataTable<DRStoryOption>("StoryOption");
        var items = GameEntry.DataTable.GetDataTable<DRStoryItem>("StoryItem");
        var offers = GameEntry.DataTable.GetDataTable<DRMerchantOffer>("MerchantOffer");
        var techniques = GameEntry.DataTable.GetDataTable<DRTechniqueStage>("TechniqueStage");
        var skills = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
        if (growth == null || cultivators == null || locations == null || chosenRoute == null || stories == null ||
            options == null || items == null || offers == null || techniques == null || skills == null)
            throw new InvalidOperationException("轮回配置表尚未完成预加载。");
        var merchants = offers.GetAllDataRows().GroupBy(row => row.MerchantId).ToArray();
        foreach (var merchant in merchants)
            if (merchant.Key <= 0 || merchant.Any(row => row.Q != merchant.First().Q || row.R != merchant.First().R ||
                row.LegacyPrice < 0 || row.ItemId <= 0 || !items.HasDataRow(row.ItemId)) ||
                merchant.Select(row => row.ItemId).Distinct().Count() != merchant.Count() ||
                HexWorldMap.Distance(merchant.First().Q, merchant.First().R) > HexWorldMap.Radius)
                throw new InvalidOperationException($"商人 {merchant.Key} 的商品配置无效。");
        return new WorldTimelineDebugView.ScenarioData
        {
            ChosenRoute = chosenRoute.GetAllDataRows().OrderBy(row => row.Id).Select(row =>
                new ChosenRouteStage
                {
                    Q = row.Q, R = row.R, TravelYears = row.TravelYears, GuardianCount = row.GuardianCount,
                    GuardianHealth = row.GuardianHealth, GuardianAttack = row.GuardianAttack,
                    GuardianAppearance = row.GuardianAppearance, RitualPeriod = row.RitualPeriod
                }).ToArray(),
            Techniques = techniques.GetAllDataRows().GroupBy(row => row.TechniqueId).ToDictionary(
                group => group.Key, group => new TechniqueDefinition(group.Key, group.OrderBy(row => row.Rank)
                    .Select((row, index) =>
                    {
                        if (row.Rank != index + 1 || !skills.HasDataRow(row.SkillId) ||
                            (SkillType)skills[row.SkillId].SkillType != SkillType.NormalSkill ||
                            !Enum.TryParse(row.Requirement, out TechniqueRequirement requirement) ||
                            requirement.ToString() != row.Requirement ||
                            (requirement == TechniqueRequirement.Item && !items.HasDataRow(row.Value)))
                            throw new InvalidOperationException($"功法进阶表第 {row.Id} 条配置无效。");
                        return new TechniqueStage(row.SkillId, requirement, row.Value);
                    }))),
            Stories = new StoryConfig(stories.GetAllDataRows().Select(row => new StoryDefinition {
                Id = row.Id, NodeId = HexWorldMap.Id(row.Q, row.R), Title = Localize(row.Title), Text = Localize(row.Text),
                Background = row.Background, Repeatable = row.Repeatable != 0 }).Concat(merchants.Select(group =>
                    new StoryDefinition
                    {
                        Id = group.Key, NodeId = HexWorldMap.Id(group.First().Q, group.First().R),
                        Title = Localize($"Timeline_Merchant_{group.Key}_Name"),
                        Text = Localize($"Timeline_Merchant_{group.Key}_Intro"),
                        Repeatable = true, IsMerchant = true
                    })),
                options.GetAllDataRows().Select(row => new StoryOption { Id = row.Id, EventId = row.EventId,
                    Text = Localize(row.Text), Result = Localize(row.Result), Condition = (StoryCondition)Enum.Parse(typeof(StoryCondition), row.Condition),
                    Value = row.Value, Flag = row.Flag, Hint = Localize(row.Hint), Weight = row.Weight, ItemId = row.ItemId,
                    CostItemId = row.CostItemId, HealthCost = row.HealthCost, Heal = row.Heal, Experience = row.Experience,
                    TimeYears = row.TimeYears, SetFlag = row.SetFlag })
                    .Concat(offers.GetAllDataRows().Select(row => new StoryOption
                    {
                        Id = row.Id, EventId = row.MerchantId, ItemId = row.ItemId, LegacyPrice = row.LegacyPrice,
                        Text = Localize(items[row.ItemId].Name), Result = "", Weight = 0
                    }))
                    .Concat(merchants.Select(group => new StoryOption
                    {
                        Id = checked(group.Key * 100), EventId = group.Key, Text = Localize("Timeline_MerchantLeave"), Weight = 1
                    })),
                items.GetAllDataRows().Select(row => new StoryItem { Id = row.Id, Name = Localize(row.Name),
                    Description = Localize(row.Description), AttackBonus = row.AttackBonus, HealthBonus = row.HealthBonus,
                    CombatAttackPercent = row.CombatAttackPercent }), Localize),
            Cultivation = CultivationConfig.FromRows(growth.GetAllDataRows().OrderBy(row => row.Id).Select(row =>
                new CultivationGrowthRow(row.Level, row.ExperienceToNext,
                    (GrowthAttribute)Enum.Parse(typeof(GrowthAttribute), row.Attribute),
                    new AttributeRange(row.SMin, row.SMax), new AttributeRange(row.AMin, row.AMax),
                    new AttributeRange(row.BMin, row.BMax), new AttributeRange(row.CMin, row.CMax)))),
            Cultivators = cultivators.GetAllDataRows().OrderBy(row => row.Id).Select(row =>
                new WorldTimelineDebugView.StartingCultivator
                {
                    Name = Localize(row.Name), Birth = HexWorldMap.Id(row.Q, row.R), Item = row.ItemId,
                    Attack = row.AttackBonus,
                    Technique = row.ItemId == WorldTimelineDebugView.QiMoStartingItem ? Localize(row.WeaponName) : ""
                }).ToArray(),
            Locations = locations.GetAllDataRows().OrderBy(row => row.Id).Select(row =>
                new WorldTimelineDebugView.LocationDefinition
                {
                    ConfigId = row.Id, Q = row.Q, R = row.R, Name = Localize(row.Name), CultivationPerYear = row.CultivationPerYear,
                    MonsterHealth = row.MonsterHealth, MonsterAttack = row.MonsterAttack,
                    LootItemId = row.LootItemId, LootAttackBonus = row.LootAttackBonus, LootName = Localize(row.LootName)
                }).ToArray()
        };
    }
}
