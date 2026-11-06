using System;
using System.Linq;
using DataTable;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

// The Game Framework data-table boundary; simulation and map code receive typed configuration.
public static class WorldTimelineTableConfig
{
    private static string Localize(string value)
    {
        if (string.IsNullOrEmpty(value) || !GameEntry.Localization.HasRawString(value)) return value;
        return GameEntry.Localization.GetString(value);
    }

    public static WorldTimelineDebugView.ScenarioData Create()
    {
        WorldTimelineDebugView.ResolveLanguageKey = Localize;
        var growth = GameEntry.DataTable.GetDataTable<DRCultivationLevel>("CultivationLevel");
        var cultivators = GameEntry.DataTable.GetDataTable<DRStartingCultivators>("StartingCultivators");
        var locations = GameEntry.DataTable.GetDataTable<DROuterLocations>("OuterLocations");
        var stories = GameEntry.DataTable.GetDataTable<DRStoryEvent>("StoryEvent");
        var options = GameEntry.DataTable.GetDataTable<DRStoryOption>("StoryOption");
        var items = GameEntry.DataTable.GetDataTable<DRStoryItem>("StoryItem");
        if (growth == null || cultivators == null || locations == null || stories == null || options == null || items == null)
            throw new InvalidOperationException("轮回配置表尚未完成预加载。");
        return new WorldTimelineDebugView.ScenarioData
        {
            Stories = new StoryConfig(stories.GetAllDataRows().Select(row => new StoryDefinition {
                Id = row.Id, NodeId = HexWorldMap.Id(row.Q, row.R), Title = Localize(row.Title), Text = Localize(row.Text),
                Background = row.Background, Repeatable = row.Repeatable != 0 }),
                options.GetAllDataRows().Select(row => new StoryOption { Id = row.Id, EventId = row.EventId,
                    Text = Localize(row.Text), Result = Localize(row.Result), Condition = (StoryCondition)Enum.Parse(typeof(StoryCondition), row.Condition),
                    Value = row.Value, Flag = row.Flag, Hint = Localize(row.Hint), Weight = row.Weight, ItemId = row.ItemId,
                    CostItemId = row.CostItemId, HealthCost = row.HealthCost, Heal = row.Heal, Experience = row.Experience,
                    TimeYears = row.TimeYears, SetFlag = row.SetFlag }),
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
                    Weapon = Localize(row.WeaponName), Attack = row.AttackBonus
                }).ToArray(),
            Locations = locations.GetAllDataRows().OrderBy(row => row.Id).Select(row =>
                new WorldTimelineDebugView.LocationDefinition
                {
                    Q = row.Q, R = row.R, Name = Localize(row.Name), CultivationPerYear = row.CultivationPerYear,
                    MonsterHealth = row.MonsterHealth, MonsterAttack = row.MonsterAttack,
                    LootItemId = row.LootItemId, LootAttackBonus = row.LootAttackBonus, LootName = Localize(row.LootName)
                }).ToArray()
        };
    }
}
