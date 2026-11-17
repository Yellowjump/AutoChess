using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    // A short, repeatable encounter. It owns no campaign actors, clock, progression or save data.
    public sealed class TacticalTrial
    {
        public const int PartySize = 3;
        public const int CellCount = 6;
        public const int Seed = 17031;
        private readonly int[] cells = { 0, -1, -1, 2, 1, -1 };
        public int Attempts { get; private set; }
        public IReadOnlyList<int> Cells => Array.AsReadOnly(cells);
        public int DeployedCount => cells.Count(id => id >= 0);
        public bool CanFight => DeployedCount == PartySize;

        // Front row: cells 0..2. Back row: cells 3..5. Clicking an occupied cell swaps units.
        public bool Place(int role, int cell)
        {
            if (role < 0 || role > 3 || cell < 0 || cell >= CellCount) return false;
            int old = Array.IndexOf(cells, role);
            if (old == cell) return true;
            if (old < 0 && cells[cell] < 0 && DeployedCount == PartySize) return false;
            if (old >= 0) cells[old] = cells[cell];
            cells[cell] = role;
            return true;
        }

        public void Bench(int role)
        {
            int cell = Array.IndexOf(cells, role);
            if (cell >= 0) cells[cell] = -1;
        }

        public int CellOf(int role) => Array.IndexOf(cells, role);

        public TrialDeployment BeginAttempt()
        {
            if (!CanFight) throw new InvalidOperationException("Deploy exactly three different cultivators.");
            Attempts++;
            return new TrialDeployment((int[])cells.Clone());
        }
    }

    public sealed class TrialDeployment
    {
        private readonly int[] cells;
        internal TrialDeployment(int[] cells) { this.cells = cells; }
        public int CellOf(int role) => Array.IndexOf(cells, role);
        public int[] Roles => cells.Where(role => role >= 0).ToArray();
        public ActorState[] CreateParty() => Roles.Select(role => new ActorState(role + 1, 0,
            Health(role), Attack(role))).ToArray();
        public static int Health(int role) => role switch { 0 => 1200, 1 => 480, 2 => 340, 3 => 390, _ => throw new ArgumentOutOfRangeException(nameof(role)) };
        public static int Attack(int role) => role switch { 0 => 35, 1 => 105, 2 => 90, 3 => 22, _ => throw new ArgumentOutOfRangeException(nameof(role)) };
    }
}
