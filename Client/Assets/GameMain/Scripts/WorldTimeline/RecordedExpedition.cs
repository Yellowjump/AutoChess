using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    // Role-based cells survive a different actor ID in the next life: -2 empty, -1 leader, 0..3 companions.
    public sealed class RecordedExpedition
    {
        public int Year { get; }
        public int NodeId { get; }
        public int Battle { get; }
        public bool Progression { get; }
        public bool CloseTaunt { get; }
        public IReadOnlyList<int> Recruited { get; }
        public IReadOnlyList<int> Cells { get; }
        public IReadOnlyList<int> Equipment { get; }
        public RecordedExpedition(int year, int nodeId, int battle, bool progression, bool closeTaunt,
            IEnumerable<int> recruited, IEnumerable<int> cells, IEnumerable<int> equipment = null)
        {
            var roles = recruited.ToArray(); var positions = cells.ToArray();
            if (year < 0 || battle < 0 || roles.Any(r => r < 0 || r > 3) || roles.Distinct().Count() != roles.Length ||
                positions.Length != 6 || positions.Count(r => r == -1) != 1 || positions.Count(r => r != -2) > 3 ||
                positions.Any(r => r < -2 || r > 3 || r >= 0 && !roles.Contains(r)) ||
                positions.Where(r => r != -2).Distinct().Count() != positions.Count(r => r != -2))
                throw new ArgumentException("Invalid recorded expedition.");
            Year = year; NodeId = nodeId; Battle = battle; Progression = progression; CloseTaunt = closeTaunt;
            Recruited = Array.AsReadOnly(roles); Cells = Array.AsReadOnly(positions);
            var loadout=(equipment??Enumerable.Empty<int>()).ToArray();
            ExpeditionEquipment.ValidateSnapshot(loadout);
            Equipment=Array.AsReadOnly(loadout);
        }
    }
}
