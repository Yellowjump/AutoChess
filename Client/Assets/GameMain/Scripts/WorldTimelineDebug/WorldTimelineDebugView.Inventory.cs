using System.Collections.Generic;
using System.Linq;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        public enum EquipmentKind { All, Weapon, Relic, Art }
        public sealed class EquipmentEntry
        {
            public int Id, Count, Icon;
            public string Name, Description, Origin;
            public EquipmentKind Kind;
        }
        public bool InventoryOpen { get; set; }
        public int InventoryPortraitIndex => roster.IndexOf(startingCultivator);
        public bool PauseInventoryObservation() { bool was = autoObserve; autoObserve = false; return was; }
        public void ResumeInventoryObservation(bool enabled)
        { if (enabled && simulation != null && !preparing && !simulation.State.IsEnded && !simulation.WaitingForPlayer) autoObserve = true; }
        public IReadOnlyList<EquipmentEntry> ReadEquipment()
        {
            var result = new List<EquipmentEntry>();
            if (simulation == null) return result;
            foreach (var group in simulation.State.Actors[simulation.State.PlayerActorId].Items.GroupBy(id => id))
            {
                int id = group.Key;
                var entry = new EquipmentEntry { Id = id, Count = group.Count(), Name = TimelineText("Timeline_UI_RelicNumber", id),
                    Kind = EquipmentKind.Relic, Icon = 13, Description = TimelineText("Timeline_UI_110"), Origin = TimelineText("Timeline_UI_72") };
                int characterIndex = roster.FindIndex(character => character.Item == id);
                if (characterIndex >= 0)
                {
                    var character = roster[characterIndex]; entry.Name = character.Weapon; entry.Kind = EquipmentKind.Weapon;
                    entry.Icon = characterIndex; entry.Origin = TimelineText("Timeline_UI_16");
                    entry.Description = character.Attack == 0 ? TimelineText("Timeline_UI_111") : TimelineText("Timeline_UI_ItemStartingAttack", character.Attack);
                }
                else if (scenarioData?.Stories != null && scenarioData.Stories.Items.TryGetValue(id, out var story))
                {
                    entry.Name = story.Name; entry.Description = story.Description; entry.Origin = TimelineText("Timeline_UI_112");
                    entry.Kind = story.CombatAttackPercent > 0 ? EquipmentKind.Art : story.AttackBonus > 0 ? EquipmentKind.Weapon : EquipmentKind.Relic;
                    entry.Icon = entry.Kind == EquipmentKind.Art ? 12 : story.HealthBonus > 0 ? 10 : 11;
                }
                else
                {
                    var location = locations.Values.FirstOrDefault(row => row.LootItemId == id);
                    if (location != null)
                    {
                        entry.Name = location.LootName; entry.Description = location.LootAttackBonus == 0 ? TimelineText("Timeline_UI_113") : TimelineText("Timeline_UI_ItemAttack", location.LootAttackBonus);
                        entry.Origin = location.Name + TimelineText("Timeline_UI_114");
                    }
                }
                result.Add(entry);
            }
            return result;
        }
    }
}
