using System.Collections.Generic;
using System.Linq;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        public enum EquipmentKind { All, Relic, Art }
        public sealed class EquipmentEntry
        {
            public int Id, Count, Icon;
            public int TechniqueId;
            public int SkillItemId;
            public bool IsStartingSkill;
            public long SelectionId => TechniqueId == 0 ? Id : (long)int.MaxValue + TechniqueId;
            public string Name, Description, Origin;
            public EquipmentKind Kind;
        }
        public bool InventoryOpen { get; set; }
        public int InventoryPortraitIndex => roster.IndexOf(startingCultivator);
        public bool PauseInventoryObservation() { bool was = autoObserve; autoObserve = false; return was; }
        public void ResumeInventoryObservation(bool enabled)
        { if (enabled && simulation != null && !preparing && !simulation.State.IsEnded && !simulation.WaitingForPlayer) autoObserve = true; }

        public EquipmentEntry DescribeMapLoot(int id, int? sourceNode = null)
        {
            var entry = new EquipmentEntry { Id = id, Kind = EquipmentKind.Relic, Icon = 13,
                Name = TimelineText("Timeline_UI_RelicNumber", id), Description = TimelineText("Timeline_UI_110") };
            var stories = simulation?.Stories?.Config ?? scenarioData?.Stories;
            if (stories != null && stories.Items.TryGetValue(id, out var story))
            {
                entry.Name = story.Name;
                entry.Description = story.Description;
                entry.Origin = TimelineText("Timeline_UI_112");
                entry.Icon = story.HealthBonus > 0 ? 10 : 11;
                return entry;
            }
            var location = sourceNode.HasValue && locations.TryGetValue(sourceNode.Value, out var row) && row.LootItemId == id
                ? row : locations.Values.FirstOrDefault(candidate => candidate.LootItemId == id);
            if (location != null)
            {
                entry.Name = location.LootName;
                entry.Description = location.LootAttackBonus == 0 ? TimelineText("Timeline_UI_113")
                    : TimelineText("Timeline_UI_ItemAttack", location.LootAttackBonus);
                entry.Origin = location.Name + TimelineText("Timeline_UI_114");
            }
            else if (id > 0 && simulation?.State.Nodes.TryGetValue(id, out var node) == true)
            {
                int kind = id % 3;
                entry.Name = TimelineText("Timeline_ExplorerRelic_" + kind);
                entry.Icon = 16 + kind;
                entry.Description = TimelineText("Timeline_ExplorerRelic_Description_" + kind) + "\n" +
                    (node.LootAttackBonus > 0 ? TimelineText("Timeline_UI_ItemAttack", node.LootAttackBonus)
                        : TimelineText("Timeline_ExplorerRelic_Keepsake"));
                entry.Origin = TimelineText("Timeline_ExplorerRelic_Origin", node.Q, node.R);
            }
            return entry;
        }

        public IReadOnlyList<EquipmentEntry> ReadEquipment()
        {
            var result = new List<EquipmentEntry>();
            if (simulation == null) return result;
            var actor = simulation.State.Actors[simulation.State.PlayerActorId];
            foreach (var group in actor.Items.GroupBy(id => id))
            {
                int id = group.Key;
                var entry = DescribeMapLoot(id);
                entry.Count = group.Count();
                if (roster.Any(character => character.Item == id))
                {
                    entry.IsStartingSkill = true;
                    entry.Origin = TimelineText("Timeline_UI_16");
                    entry.Description = "";
                }
                result.Add(entry);
            }
            foreach (var art in actor.Techniques.OrderBy(entry => entry.Key))
                result.Add(new EquipmentEntry
                {
                    TechniqueId = art.Key, Name = TechniqueName(art.Key), Icon = 12, Count = 1,
                    Kind = EquipmentKind.Art, Description = TimelineText("Timeline_Technique_Stage",
                        art.Value.Rank, TechniqueSkillName(art.Value.SkillId))
                });
            return result;
        }
    }
}
