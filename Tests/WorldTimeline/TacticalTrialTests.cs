using System;
using System.Linq;
using WorldTimeline;

internal static partial class WorldSimulationTests
{
    private static void RunTacticalTrialTests()
    {
        Run("Trial deployment swaps, replaces and enforces party limit", () =>
        {
            var trial = new TacticalTrial();
            Check(trial.CanFight && trial.DeployedCount == 3, "Three initial units");
            Check(!trial.Place(3, 2), "A fourth unit cannot occupy an empty cell");
            Check(trial.Place(3, 4) && trial.CellOf(1) == -1, "Bench unit replaces deployed unit");
            Check(trial.Place(0, 3) && trial.CellOf(2) == 0, "Occupied cells swap without losing a unit");
            Check(trial.Cells.Where(id => id >= 0).Distinct().Count() == 3, "No duplicate roles");
            Check(!trial.Place(-1, 0) && !trial.Place(4, 0) && !trial.Place(0, 6), "Invalid inputs rejected");
        });
        Run("Trial retry preserves formation but creates fresh full-health actors", () =>
        {
            var trial = new TacticalTrial();
            var first = trial.BeginAttempt();
            var actors = first.CreateParty();
            actors[0].Health = 0;
            trial.Place(0, 5);
            Check(first.CellOf(0) == 0 && trial.CellOf(0) == 5, "Running formation is an immutable snapshot");
            var next = trial.BeginAttempt().CreateParty();
            Check(next.All(actor => actor.Health == actor.MaxHealth && actor.Health > 0), "Retry restores full health");
            Check(trial.Attempts == 2 && !ReferenceEquals(actors[0], next[0]), "Attempts use independent actors");
            trial.Bench(0);
            bool rejected = false;
            try { trial.BeginAttempt(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && trial.Attempts == 2, "Incomplete formations cannot start or consume an attempt");
        });
    }
}
