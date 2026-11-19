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
        public IReadOnlyList<int> Vitality { get; }
        internal IReadOnlyList<ActorState> PastLeaders { get; }
        public int Successor { get; internal set; }
        public RecordedExpedition(int year, int nodeId, int battle, bool progression, bool closeTaunt,
            IEnumerable<int> recruited, IEnumerable<int> cells, IEnumerable<int> equipment = null, IEnumerable<int> vitality = null, int successor = -1, IEnumerable<ActorState> pastLeaders = null)
        {
            var members = (pastLeaders ?? Enumerable.Empty<ActorState>()).Select(actor => actor.CopyForParty(actor.Id)).ToArray();
            if (members.Length > ExpeditionParty.MaxPastLeaders || members.Select(a => a.BirthNodeId).Distinct().Count() != members.Length)
                throw new ArgumentException("Invalid past leader identities.");
            PastLeaders = Array.AsReadOnly(members);
            int count = ExpeditionParty.StageRoles + members.Length;
            var vitals = vitality?.ToArray() ?? Array.Empty<int>();
            if ((vitals.Length != 0 && (vitals.Length != 4 + count || vitals[0] < -1 || vitals[0] >= count ||
                vitals[1] < 0 || vitals[1] >= (1 << (count + 1)) || vitals.Skip(4).Any(h => h < -1))) || successor < -1 || successor >= count)
                throw new ArgumentException("Invalid expedition vitality.");
            Vitality = Array.AsReadOnly(vitals); Successor = successor;
            var roles = recruited.ToArray(); var positions = cells.ToArray();
            if (members.Length > 0 && (vitals.Length == 0 || Enumerable.Range(ExpeditionParty.StageRoles, members.Length).Except(roles).Any()) || year < 0 || battle < 0 || roles.Any(r => r < 0 || r >= count) || roles.Distinct().Count() != roles.Length ||
                positions.Length != 6 || positions.Count(r => r == -1) != 1 || positions.Count(r => r != -2) > 3 ||
                positions.Any(r => r < -2 || r >= count || r >= 0 && !roles.Contains(r)) ||
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
