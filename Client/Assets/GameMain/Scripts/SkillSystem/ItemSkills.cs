using System;
using System.Collections.Generic;
using System.Linq;
using DataTable;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    internal static class ItemSkills
    {
        private static readonly Lazy<Dictionary<int, int[]>> additional = new Lazy<Dictionary<int, int[]>>(() =>
            Resources.Load<TextAsset>("AdditionalItemSkills").text.Split('\n').Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim().Split(',')).GroupBy(row => int.Parse(row[0]))
                .ToDictionary(group => group.Key, group => group.Select(row => int.Parse(row[1])).Distinct().ToArray()));

        internal static IEnumerable<int> All(DRItem item)
        {
            if (item.SkillID != 0) yield return item.SkillID;
            if (additional.Value.TryGetValue(item.Id, out var extra))
                foreach (int id in extra)
                    if (id != item.SkillID) yield return id;
        }

        internal static DRSkill Highest(DRItem item)
        {
            var table = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
            DRSkill best = null;
            foreach (int id in All(item))
            {
                var skill = table.GetDataRow(id);
                if (skill == null || (SkillType)skill.SkillType == SkillType.SpSkill) continue;
                // Normal = 1, auto = 2, passive = 4.
                if (best == null || skill.SkillType < best.SkillType) best = skill;
            }
            return best;
        }

        internal static string Label(SkillType type)
        {
            string key = type switch
            {
                SkillType.NormalSkill => "SkillType_Active",
                SkillType.NoAnimSkill => "SkillType_Auto",
                SkillType.PassiveSkill => "SkillType_Passive",
                _ => null
            };
            return key == null ? null : GameEntry.Localization.GetString(key);
        }

        internal static string ItemLabel(DRItem item)
        {
            var highest = Highest(item);
            if (highest != null) return Label((SkillType)highest.SkillType);
            return item.AttrAdd?.Count > 0 ? Label(SkillType.PassiveSkill) : null;
        }

        internal static string ArtifactLabel(int id)
        {
            if (WorldTimeline.ExpeditionEquipment.Definition(id)?.AutoSkillItem > 0) return Label(SkillType.NoAnimSkill);
            if (id == WorldTimeline.ExpeditionEquipment.OpeningBrush)
                return ItemLabel(GameEntry.DataTable.GetDataTable<DRItem>("Item")[9145]);
            return Label(SkillType.PassiveSkill);
        }
    }
}
