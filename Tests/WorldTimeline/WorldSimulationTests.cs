using System;
using System.Linq;
using WorldTimeline;

internal static partial class WorldSimulationTests
{
    private static int passed;

    private static void Main()
    {
        RunTacticalTrialTests();
        RunExpeditionTests();
        RunStoryTests();
        Run("Cultivation advances everyone and pauses", () =>
        {
            var sim = Create();
            sim.AssignAction(2, WorldAction.Move(3, 2));
            Check(sim.State.Year == 0, "Assigning actions must not advance time");
            sim.SubmitPlayerAction(WorldAction.Cultivate(5));
            Check(sim.State.Year == 5 && sim.State.Actors[1].Experience == 10, "Five annual rewards");
            Check(sim.State.Actors[2].NodeId == 3 && sim.WaitingForPlayer, "Other actors advance, then pause");
        });
        Run("Simultaneous arrivals fight by ID despite insertion order", () =>
        {
            var sim = Create();
            sim.State.Nodes[3].Items.Add(99);
            sim.State.Actors.Add(3, new ActorState(3, 2, 100, 10));
            sim.AssignAction(3, WorldAction.Move(3, 10));
            sim.AssignAction(2, WorldAction.Move(3, 10));
            sim.SubmitPlayerAction(WorldAction.Move(3, 10));
            var battles = sim.Events.Where(e => e.Kind == WorldEventKind.Battle).ToArray();
            Check(battles.Length == 2 && battles[0].ActorId == 1 && battles[0].OtherActorId == 2 &&
                battles[1].ActorId == 1 && battles[1].OtherActorId == 3, "ID tournament order");
            Check(sim.State.Actors[3].Health == 90 && sim.State.Actors[3].Items.Single() == 99, "Damage persists; final winner loots");
        });
        Run("Arrival interrupts resident cultivation on death", () =>
        {
            var sim = Create(otherHealth: 200, otherAttack: 100);
            sim.AssignAction(2, WorldAction.Move(1, 2));
            sim.SubmitPlayerAction(WorldAction.Cultivate(5));
            Check(sim.State.Year == 2 && !sim.State.Actors[1].IsAlive, "Stop immediately on death");
            Check(sim.State.Actors[1].Experience == 2 && sim.State.Actors[1].CurrentAction == null, "No reward in death year");
            for (int i = 0; i < 4; i++) sim.AdvanceOneYear();
            Check(sim.State.Actors[1].Experience == 2, "No delayed reward");
        });
        Run("Current rules let actors arrive past their former lifespan", () =>
        {
            var sim = Create(lifespan: 2);
            sim.SubmitPlayerAction(WorldAction.Move(3, 2));
            Check(sim.State.Actors[1].IsAlive && sim.State.Actors[1].NodeId == 3, "Former lifespan no longer interrupts movement");
        });
        Run("Legacy checkpoint lifespan stops applying after the saved year", () =>
        {
            var sim = Create(lifespan: 2);
            sim.State.LegacyLifespanDeathThroughYear = 1;
            sim.SubmitPlayerAction(WorldAction.Move(3, 2));
            Check(sim.State.Actors[1].IsAlive && sim.State.Actors[1].NodeId == 3, "Continued years use current rules");
        });
        Run("Legacy replay preserves lifespan death before arrival", () =>
        {
            var sim = Create(lifespan: 2);
            sim.State.LegacyLifespanDeathThroughYear = 2;
            sim.SubmitPlayerAction(WorldAction.Move(3, 2));
            Check(!sim.State.Actors[1].IsAlive && sim.State.Actors[1].NodeId == null, "Death clears location");
            Check(!sim.Events.Any(e => e.Kind == WorldEventKind.Arrived && e.ActorId == 1), "No ghost arrival");
        });
        Run("Disconnected movement rejected without mutation", () =>
        {
            var sim = Create();
            try { sim.SubmitPlayerAction(WorldAction.Move(999)); throw new Exception("Accepted invalid move"); }
            catch (InvalidOperationException) { }
            Check(sim.State.Year == 0 && sim.State.Actors[1].NodeId == 1 && sim.WaitingForPlayer, "Invalid action leaves state intact");
        });
        Run("Travelling actor cannot be fought at departure", () =>
        {
            var sim = Create();
            sim.AssignAction(2, WorldAction.Move(1));
            sim.SubmitPlayerAction(WorldAction.Move(3, 3));
            Check(!sim.Events.Any(e => e.Kind == WorldEventKind.Battle), "No departure-node encounter");
        });
        Run("Node monster and unique treasure use current state", () =>
        {
            var sim = Create(attack: 100);
            sim.State.Nodes[3].MonsterHealth = 10;
            sim.State.Nodes[3].Items.Add(7);
            sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(sim.State.Nodes[3].MonsterHealth == 0 && sim.State.Actors[1].Items.Count == 1, "Clear and loot");
            sim.SubmitPlayerAction(WorldAction.Move(1));
            sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(sim.State.Actors[1].Items.Count == 1 && sim.Events.Count(e => e.Kind == WorldEventKind.Battle) == 1, "No duplicate monster or loot");
        });
        Run("Batch and annual observation give identical events", () =>
        {
            var batch = Create(); var annual = Create();
            batch.AssignAction(2, WorldAction.Move(3, 2));
            annual.AssignAction(2, WorldAction.Move(3, 2));
            batch.SubmitPlayerAction(WorldAction.Cultivate(5));
            annual.AssignAction(1, WorldAction.Cultivate(5));
            for (int i = 0; i < 5; i++) annual.AdvanceOneYear();
            Check(Signature(batch) == Signature(annual), "Same event order and state");
        });
        Run("Replay does not duplicate treasure taken earlier", () =>
        {
            var source = Create();
            source.State.Nodes[3].Items.Add(99);
            source.SubmitPlayerAction(WorldAction.Move(3, 3));
            source.SubmitPlayerAction(WorldAction.Cultivate(1));
            var record = source.RecordPlayerLife();
            var replay = Create(attack: 100);
            replay.State.Nodes[3].Items.Add(99);
            var history = replay.AddHistory(record, 10);
            replay.SubmitPlayerAction(WorldAction.Move(3));
            replay.SubmitPlayerAction(WorldAction.Move(1));
            replay.SubmitPlayerAction(WorldAction.Wait(2));
            Check(replay.State.Actors[1].Items.Single() == 99 && replay.State.Actors[10].Items.Count == 0, "No copied historical loot");
            Check(replay.State.Actors[10].Experience == 2 && history.Status == HistoryReplayStatus.RecordFinished, "Next action auto-starts");
            Check(record.Events.Any(e => e.Kind == WorldEventKind.Looted) && record.Actions.Count == 2, "Source record unchanged");
        });
        Run("Clearing historical death node changes fate", () =>
        {
            var source = Create();
            source.State.Nodes[3].MonsterHealth = 200;
            source.State.Nodes[3].MonsterAttack = 100;
            source.SubmitPlayerAction(WorldAction.Move(3, 3));
            var record = source.RecordPlayerLife();
            Check(record.EndedInDeath && record.EndYear == 3, "Death recorded");
            var replay = Create(attack: 1000);
            replay.State.Nodes[3].MonsterHealth = 200;
            replay.State.Nodes[3].MonsterAttack = 100;
            var saved = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[] { new SavedLife(1, record) }));
            var history = replay.AddHistory(saved[0].Record, 10);
            replay.SubmitPlayerAction(WorldAction.Move(3));
            replay.SubmitPlayerAction(WorldAction.Move(1));
            replay.SubmitPlayerAction(WorldAction.Wait());
            Check(replay.State.Actors[10].IsAlive && history.Status == HistoryReplayStatus.FateChanged, "Historical death not forced");
            Check(replay.Events.Count(e => e.Kind == WorldEventKind.FateChanged && e.ActorId == 10 &&
                e.OtherActorId == 1 && e.NodeId == 3 && e.Year == 3) == 1, "The log credits the guardian clearer after loading the life");
            Check(record.EndedInDeath && record.Events.Any(e => e.Kind == WorldEventKind.Died), "Original death preserved");
        });
        Run("Unchanged lethal node still kills replay", () =>
        {
            var source = Create();
            source.State.Nodes[3].MonsterHealth = 200;
            source.State.Nodes[3].MonsterAttack = 100;
            source.SubmitPlayerAction(WorldAction.Move(3, 3));
            var replay = Create();
            replay.State.Nodes[3].MonsterHealth = 200;
            replay.State.Nodes[3].MonsterAttack = 100;
            var history = replay.AddHistory(source.RecordPlayerLife(), 10);
            replay.SubmitPlayerAction(WorldAction.Wait(4));
            Check(history.Status == HistoryReplayStatus.Dead && !replay.State.Actors[10].IsAlive, "Actual combat still applies");
        });
        Run("Recording is a snapshot and preserves death year during observation", () =>
        {
            var sim = Create(lifespan: 2);
            sim.State.LegacyLifespanDeathThroughYear = 2;
            var early = sim.RecordPlayerLife();
            sim.SubmitPlayerAction(WorldAction.Cultivate(5));
            for (int i = 0; i < 3; i++) sim.AdvanceOneYear();
            var final = sim.RecordPlayerLife();
            Check(early.Actions.Count == 0 && !early.EndedInDeath, "Snapshot is independent");
            Check(final.Actions.Count == 1 && final.EndYear == 2 && final.EndedInDeath, "Observation doesn't extend recorded life");
        });
        Run("Replay missing connection blocks instead of teleporting", () =>
        {
            var source = Create();
            source.SubmitPlayerAction(WorldAction.Move(3));
            var replay = Create();
            replay.State.Nodes[1].Links.Remove(3);
            var history = replay.AddHistory(source.RecordPlayerLife(), 10);
            replay.SubmitPlayerAction(WorldAction.Wait(2));
            Check(history.Status == HistoryReplayStatus.RouteBlocked && replay.State.Actors[10].NodeId == 1, "Illegal route stays blocked");
        });
        Run("Chosen follows configured ten-year route and absorbs monster attributes", () =>
        {
            var sim = CreateWithChosen();
            sim.State.Nodes[3].MonsterHealth = 100;
            sim.State.Nodes[3].MonsterAttack = 10;
            sim.SubmitPlayerAction(WorldAction.Wait(10));
            Check(sim.State.Actors[20].NodeId == 3 && sim.State.Actors[20].Attack == 101 &&
                sim.State.Actors[20].MaxHealth == 210, "Arrival and percentage growth");
            sim.SubmitPlayerAction(WorldAction.Wait(10));
            Check(sim.State.Actors[20].NodeId == 4, "Next route step");
        });
        Run("Normal chosen encounter allows passing without combat", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 1);
            sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(sim.CanJoinChosen(1) && sim.WaitingForPlayer && !sim.Events.Any(e => e.Kind == WorldEventKind.Battle), "No forced battle");
            var meeting = sim.Events.Single(e => e.Kind == WorldEventKind.ChosenEncounter);
            Check(meeting.Year == 1 && meeting.NodeId == 3 && meeting.ActorId == 1 && meeting.OtherActorId == 20,
                "Both arrivals produce one player encounter at their shared node");
            sim.SubmitPlayerAction(WorldAction.Move(1));
            Check(sim.State.Actors[1].IsAlive && sim.State.Actors[1].NodeId == 1 &&
                sim.Events.Count(e => e.Kind == WorldEventKind.ChosenEncounter) == 1, "Can pass without another encounter");
        });
        Run("Crossing the Chosen on a road does not create a node encounter", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 2);
            sim.SubmitPlayerAction(WorldAction.Move(3));
            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(sim.State.Actors[1].NodeId == 2 && sim.State.Actors[20].NodeId == 3 &&
                !sim.Events.Any(e => e.Kind == WorldEventKind.ChosenEncounter), "Different arrivals do not count as a meeting");
        });
        Run("Player joins, loses control and contributes frozen combat stats", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 1);
            sim.SubmitPlayerAction(WorldAction.Move(3));
            sim.SubmitPlayerAction(WorldAction.JoinChosen());
            Check(sim.State.Year == 1 && !sim.WaitingForPlayer && sim.State.Actors[1].LeaderId == 20, "Immediate join without advancing year");
            sim.State.Nodes[4].MonsterHealth = 210;
            sim.State.Nodes[4].MonsterAttack = 1;
            sim.AdvanceOneYear();
            Check(sim.State.Actors[1].NodeId == 4 && sim.State.Actors[1].Attack == 10 && sim.State.Actors[1].Experience == 0, "Party follows without growth");
            Check(sim.State.Actors[1].Health == 99, "Combined attack kills in two turns; companion takes damage first");
            try { sim.SubmitPlayerAction(WorldAction.Wait()); throw new Exception("Joined player accepted input"); }
            catch (InvalidOperationException) { }
        });
        Run("Pre-arrival disguise feeds attributes to chosen", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 3);
            sim.SubmitPlayerAction(WorldAction.Move(3));
            sim.SubmitPlayerAction(WorldAction.Disguise());
            sim.SubmitPlayerAction(WorldAction.Wait());
            Check(!sim.State.Actors[1].IsAlive && sim.State.Actors[20].Attack == 101 && sim.State.Actors[20].MaxHealth == 210, "Disguised player defeated and absorbed");
            Check(!sim.Events.Any(e => e.Kind == WorldEventKind.ChosenEncounter), "An ambush skips the peaceful encounter event");
            Check(!sim.State.PlayerKilledChosen, "Sacrifice does not end run");
        });
        Run("Disguise forbidden once chosen is already present", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 1);
            sim.SubmitPlayerAction(WorldAction.Move(3));
            try { sim.SubmitPlayerAction(WorldAction.Disguise()); throw new Exception("Late ambush accepted"); }
            catch (InvalidOperationException) { }
            Check(sim.State.Year == 1 && sim.WaitingForPlayer, "Invalid ambush does not consume time");
        });
        Run("Player killing chosen ends and freezes the run", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 3, playerAttack: 1000, playerHealth: 1000);
            sim.SubmitPlayerAction(WorldAction.Move(3));
            sim.SubmitPlayerAction(WorldAction.Disguise());
            sim.SubmitPlayerAction(WorldAction.Wait(5));
            Check(sim.State.PlayerKilledChosen && sim.State.Year == 3, "Immediate end on chosen death by player");
            sim.AdvanceOneYear();
            Check(sim.State.Year == 3 && !sim.WaitingForPlayer, "No further world advancement");
        });
        Run("Changed history catches chosen and joins automatically", () =>
        {
            var record = CreateDeathRecord();
            var sim = CreateWithChosen(yearsPerStep: 4, playerAttack: 1000);
            sim.State.Nodes[3].MonsterHealth = 200;
            sim.State.Nodes[3].MonsterAttack = 100;
            var history = sim.AddHistory(record, 10);
            sim.SubmitPlayerAction(WorldAction.Move(3));
            sim.SubmitPlayerAction(WorldAction.Move(1));
            sim.SubmitPlayerAction(WorldAction.Wait(3));
            Check(history.Status == HistoryReplayStatus.FateChanged && sim.State.Actors[10].LeaderId == 20, "Saved history joins upon interception");
        });
        Run("Chosen death keeps player active and changed histories head to final", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 4, playerAttack: 1000, chosenLifespan: 1);
            sim.State.LegacyLifespanDeathThroughYear = 1;
            var history = sim.AddHistory(CreateDeathRecord(), 10);
            sim.SubmitPlayerAction(WorldAction.Move(3));
            sim.SubmitPlayerAction(WorldAction.Move(1));
            sim.SubmitPlayerAction(WorldAction.Wait(3));
            Check(!sim.State.Actors[20].IsAlive && sim.State.Actors[10].NodeId == 4, "Saved history proceeds to final node");
            Check(sim.WaitingForPlayer && !sim.State.PlayerKilledChosen, "Chosen death does not stop player's life");
        });
        Run("Joined player stays in observation when chosen dies in transit", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 1, chosenLifespan: 2);
            sim.State.LegacyLifespanDeathThroughYear = 2;
            sim.SubmitPlayerAction(WorldAction.Move(3));
            sim.SubmitPlayerAction(WorldAction.JoinChosen());
            sim.AdvanceOneYear();
            Check(sim.State.Actors[1].IsAlive && sim.State.Actors[1].NodeId == 3 && !sim.State.Actors[1].LeaderId.HasValue,
                "Surviving companion retains a valid location");
            Check(!sim.WaitingForPlayer && sim.State.PlayerControlEnded, "Observation is permanent after joining");
        });
        Run("Pathfinding uses connected steps with deterministic tie breaks", () =>
        {
            var sim = Create();
            sim.State.Nodes.Add(4, new NodeState(4));
            sim.State.Nodes[2].Links.Add(4);
            sim.State.Nodes[3].Links.Add(4);
            Check(WorldPathfinder.NextStep(sim.State, 1, new[] { 4 }) == 2, "Equal paths choose smaller node ID");
            sim.State.Nodes[1].Links.Clear();
            Check(!WorldPathfinder.NextStep(sim.State, 1, new[] { 4 }).HasValue, "Unreachable destinations do not teleport");
        });
        Run("Joining chosen is recorded and replayed as an interaction", () =>
        {
            var source = CreateWithChosen(yearsPerStep: 1);
            source.SubmitPlayerAction(WorldAction.Move(3));
            source.SubmitPlayerAction(WorldAction.JoinChosen());
            var record = source.RecordPlayerLife();
            var replay = CreateWithChosen(yearsPerStep: 1);
            replay.AddHistory(record, 10);
            replay.SubmitPlayerAction(WorldAction.Wait(2));
            Check(replay.State.Actors[10].LeaderId == 20 && replay.State.Actors[10].NodeId == 4,
                "Historical joining decision preserved");
        });
        Run("Historical companion resumes final-route travel after chosen death", () =>
        {
            var source = CreateWithChosen(yearsPerStep: 1);
            source.SubmitPlayerAction(WorldAction.Move(3));
            source.SubmitPlayerAction(WorldAction.JoinChosen());
            var replay = CreateWithChosen(yearsPerStep: 1, chosenLifespan: 2);
            replay.State.LegacyLifespanDeathThroughYear = 2;
            replay.AddHistory(source.RecordPlayerLife(), 10);
            replay.SubmitPlayerAction(WorldAction.Wait(4));
            Check(replay.State.Actors[10].IsAlive && replay.State.Actors[10].NodeId == 4 &&
                !replay.State.Actors[10].LeaderId.HasValue, "Former companion continues rather than stopping with record");
        });
        Run("Chosen losing to a monster does not end player control", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 1);
            sim.State.Nodes[3].MonsterHealth = 1000;
            sim.State.Nodes[3].MonsterAttack = 1000;
            sim.SubmitPlayerAction(WorldAction.Cultivate(3));
            Check(sim.State.Year == 3 && !sim.State.Actors[20].IsAlive && sim.State.Actors[1].Experience == 6 && sim.WaitingForPlayer,
                "Player continues after chosen combat death");
        });
        Run("Ritual expiry destroys world before annual rewards and freezes outcome", () =>
        {
            var sim = CreateRitual(period: 3, chosenStep: 10);
            sim.SubmitPlayerAction(WorldAction.Cultivate(10));
            Check(sim.State.Year == 3 && sim.State.Outcome == RunOutcome.WorldDestroyed, "Deadline ends world");
            Check(sim.State.Actors[1].Experience == 4 && sim.State.Actors.Values.All(a => !a.IsAlive), "No deadline-year rewards; all die");
            int eventCount = sim.Events.Count;
            sim.AdvanceOneYear();
            Check(sim.State.Year == 3 && sim.Events.Count == eventCount, "Terminal state is idempotent");
        });
        Run("Ordinary final victory resets ritual and kills only challenger", () =>
        {
            var sim = CreateRitual(period: 6, chosenStep: 3);
            sim.SubmitPlayerAction(WorldAction.Move(4));
            Check(!sim.State.Actors[1].IsAlive && sim.State.Actors[2].IsAlive && sim.State.Actors[20].IsAlive,
                "Backlash is local to challenger");
            Check(sim.State.Ritual.ResetCount == 1 && sim.State.Ritual.DeadlineYear == 7 &&
                sim.State.Nodes[4].MonsterHealth == 100, "Reset restores full boss without permanent weakening");
            for (int i = 0; i < 5; i++) sim.AdvanceOneYear();
            Check(sim.State.Outcome == RunOutcome.Cleared && sim.State.Year == 6, "Observation continues to chosen victory");
            var life = sim.RecordPlayerLife();
            Check(life.EndYear == 1 && life.RitualBreakYear == 1, "Observation preserves sacrifice year");
            var restored = WorldTimelineSaveStore.Decode(WorldTimelineSaveStore.Encode(new[] { new SavedLife(1, life) })).Single().Record;
            Check(restored.RitualBreakYear == 1, "Ritual break year remains available when selecting a saved history");
        });
        Run("Three historical sacrifices relay ritual until chosen arrives", () =>
        {
            var sim = CreateRitual(period: 6, chosenStep: 6);
            sim.AddHistory(FinalArrivalRecord(3), 10);
            sim.AddHistory(FinalArrivalRecord(7), 11);
            sim.AddHistory(FinalArrivalRecord(11), 12);
            sim.SubmitPlayerAction(WorldAction.Wait(20));
            Check(sim.State.Year == 12 && sim.State.Outcome == RunOutcome.Cleared, "Chosen survives original deadline");
            Check(sim.State.Ritual.ResetCount == 3 && sim.State.Ritual.DeadlineYear == 17, "Each sacrifice resets from its own year");
            Check(sim.Events.Count(e => e.Kind == WorldEventKind.RitualReset) == 3 &&
                sim.Events.Count(e => e.Kind == WorldEventKind.WorldCleared) == 1, "Single clear after three resets");
            Check(sim.State.Actors[1].IsAlive && !sim.WaitingForPlayer, "Clear ends even a surviving player's unfinished action");
        });
        Run("Arriving exactly on ritual deadline is too late", () =>
        {
            var sim = CreateRitual(period: 3, chosenStep: 10);
            sim.SubmitPlayerAction(WorldAction.Move(4, 3));
            Check(sim.State.Outcome == RunOutcome.WorldDestroyed && sim.State.Ritual.ResetCount == 0, "Expiry precedes same-year arrival");
            Check(!sim.Events.Any(e => e.Kind == WorldEventKind.Arrived), "No ghost final battle after destruction");
        });
        Run("Chosen arriving exactly at deadline cannot clear", () =>
        {
            var sim = CreateRitual(period: 4, chosenStep: 2);
            sim.SubmitPlayerAction(WorldAction.Wait(10));
            Check(sim.State.Outcome == RunOutcome.WorldDestroyed && !sim.Events.Any(e => e.Kind == WorldEventKind.WorldCleared),
                "Chosen uses same deadline rule");
        });
        Run("Losing to final boss does not reset ritual", () =>
        {
            var sim = CreateRitual(period: 10, chosenStep: 2, bossHealth: 1000, bossAttack: 1000);
            sim.SubmitPlayerAction(WorldAction.Cultivate(5));
            Check(!sim.State.Actors[20].IsAlive && sim.State.Ritual.ResetCount == 0 && sim.WaitingForPlayer,
                "Chosen defeat is not an immediate world ending");
            sim.SubmitPlayerAction(WorldAction.Wait(10));
            Check(sim.State.Outcome == RunOutcome.WorldDestroyed && sim.State.Year == 10, "Original deadline remains");
        });
        Run("Same-year final arrivals fight first and yield one reset", () =>
        {
            var sim = CreateRitual(period: 6, chosenStep: 10);
            sim.AddHistory(FinalArrivalRecord(3), 10);
            sim.AddHistory(FinalArrivalRecord(3), 11);
            sim.SubmitPlayerAction(WorldAction.Wait(3));
            Check(sim.State.Ritual.ResetCount == 1 && !sim.State.Actors[10].IsAlive && !sim.State.Actors[11].IsAlive,
                "One battle winner sacrifices; simultaneous arrivals do not stack resets");
        });
        Run("Player and chosen arriving together reset then clear freshly restored boss", () =>
        {
            var sim = CreateRitual(period: 10, chosenStep: 2);
            sim.SubmitPlayerAction(WorldAction.Move(4, 4));
            Check(sim.State.Outcome == RunOutcome.Cleared && sim.State.Ritual.ResetCount == 1 && !sim.State.Actors[1].IsAlive,
                "Ordinary challenger goes first, chosen fights restored boss");
        });
        Run("Century ritual relays through four sacrifices to year four hundred", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 200, chosenLifespan: 1000);
            sim.State.Nodes[1].Links.Add(4);
            sim.ConfigureFinalRitual(100, 10);
            sim.AddHistory(FinalArrivalRecord(90), 10);
            sim.AddHistory(FinalArrivalRecord(180), 11);
            sim.AddHistory(FinalArrivalRecord(270), 12);
            sim.AddHistory(FinalArrivalRecord(360), 13);
            // The player survives their former lifespan while the ritual relay continues.
            for (int i = 0; i < 500 && !sim.State.IsEnded; i++) sim.AdvanceOneYear();
            Check(sim.State.Year == 400 && sim.State.Outcome == RunOutcome.Cleared && sim.State.Ritual.ResetCount == 4,
                "Long timeline reaches chosen victory after four resets");
            Check(sim.State.Ritual.DeadlineYear == 460 && sim.State.Actors[1].IsAlive &&
                sim.RecordPlayerLife().EndYear == 400, "Old deadlines do not fire and lifespan no longer ends the player life");
        });
        Run("Unity demo fixture clears with all three relay histories", () =>
        {
            var sim = WorldTimelineDemoFactory.Create();
            sim.AddHistory(WorldTimelineDemoFactory.CreateRelayRecord(9), 10);
            sim.AddHistory(WorldTimelineDemoFactory.CreateRelayRecord(20), 11);
            sim.AddHistory(WorldTimelineDemoFactory.CreateRelayRecord(31), 12);
            sim.SubmitPlayerAction(WorldAction.Wait(50));
            Check(sim.State.Outcome == RunOutcome.Cleared && sim.State.Year == 40 && sim.State.Ritual.ResetCount == 3,
                "Playable fixture has a deterministic winning relay");
        });
        Run("Unity demo without relay histories destroys world at year twelve", () =>
        {
            var sim = WorldTimelineDemoFactory.Create();
            sim.SubmitPlayerAction(WorldAction.Wait(50));
            Check(sim.State.Outcome == RunOutcome.WorldDestroyed && sim.State.Year == 12, "No hidden assistance");
        });
        Run("Unity demo fate rescue follows documented movement steps", () =>
        {
            var sim = WorldTimelineDemoFactory.Create();
            var history = sim.AddHistory(WorldTimelineDemoFactory.CreateFateRecord(), 10);
            sim.SubmitPlayerAction(WorldAction.Move(19));
            sim.SubmitPlayerAction(WorldAction.Move(18));
            sim.SubmitPlayerAction(WorldAction.Move(17));
            sim.SubmitPlayerAction(WorldAction.Wait(7));
            Check(history.Status == HistoryReplayStatus.FateChanged && sim.State.Actors[10].LeaderId == WorldTimelineDemoFactory.ChosenId,
                "Early clearing saves history and joins chosen by year ten");
        });
        Run("Large hex map has 331 cells and six reciprocal neighbours", () =>
        {
            var nodes = HexWorldMap.Create();
            Check(nodes.Count == 331 && nodes[HexWorldMap.Id(0, 0)].Links.Count == 6, "Radius ten hex disk");
            foreach (var node in nodes.Values)
                foreach (int other in node.Links)
                {
                    Check(nodes[other].Links.Contains(node.Id), "Reciprocal links");
                    Check(HexWorldMap.Distance(node.Q - nodes[other].Q, node.R - nodes[other].R) == 1, "No diagonal shortcut");
                }
            Check(nodes.Values.Count(n => HexWorldMap.Distance(n.Q, n.R) == 10) == 60, "Sixty outer spawn candidates");
            Check(nodes.Values.All(n => n.CultivationPerYear == 11 - HexWorldMap.Distance(n.Q, n.R)), "Spirit gradient");
        });
        Run("Hex relay histories keep the world alive until year one hundred", () =>
        {
            var sim = HexTimelineDemoFactory.Create();
            sim.AddHistory(HexTimelineDemoFactory.CreateRelayRecord(30), 10);
            sim.AddHistory(HexTimelineDemoFactory.CreateRelayRecord(60), 11);
            sim.AddHistory(HexTimelineDemoFactory.CreateRelayRecord(90), 12);
            sim.SubmitPlayerAction(WorldAction.Wait(120));
            Check(sim.State.Outcome == RunOutcome.Cleared && sim.State.Year == 100 && sim.State.Ritual.ResetCount == 3,
                "Hex fixture preserves relay gameplay");
        });
        Run("Hex world expires without relays and supports early rescue", () =>
        {
            var empty = HexTimelineDemoFactory.Create();
            empty.SubmitPlayerAction(WorldAction.Wait(50));
            Check(empty.State.Year == 35 && empty.State.Outcome == RunOutcome.WorldDestroyed, "Hex expiry");
            var sim = HexTimelineDemoFactory.Create();
            var history = sim.AddHistory(HexTimelineDemoFactory.CreateFateRecord(), 10);
            sim.SubmitPlayerAction(WorldAction.Move(HexTimelineDemoFactory.FateNode));
            sim.SubmitPlayerAction(WorldAction.Move(HexTimelineDemoFactory.PlayerStart));
            sim.SubmitPlayerAction(WorldAction.Wait(3));
            Check(history.Status == HistoryReplayStatus.FateChanged && sim.State.Actors[10].IsAlive, "Hex rescue uses current node state");
        });
        Run("Growth CSV loads independent attributes and per-level experience", () =>
        {
            var config = GrowthConfig();
            Check(config.Levels.Count == 10 && config.GetLevel(1).ExperienceToNext == 6 && config.GetLevel(2).ExperienceToNext == 20, "Editable costs");
            Check(config.GetLevel(2).Range(GrowthAttribute.Attack, TalentGrade.S).Min == 17 && config.GetLevel(2).Range(GrowthAttribute.Attack, TalentGrade.A).Max == 18, "Overlapping upgrade increments");
            foreach (var level in config.Levels)
                foreach (GrowthAttribute attribute in Enum.GetValues(typeof(GrowthAttribute)))
                    for (int grade = 0; grade < 3; grade++)
                        Check(config.CumulativeRange(level.Level, attribute, (TalentGrade)grade).Min > config.CumulativeRange(level.Level, attribute, (TalentGrade)(grade + 1)).Max, "Cumulative higher-grade guarantee");
        });
        Run("Framework growth bytes build the same typed cultivation configuration", () =>
        {
            var rows = new System.Collections.Generic.List<CultivationGrowthRow>();
            using (var reader = new System.IO.BinaryReader(System.IO.File.OpenRead(
                System.IO.Path.Combine(AppContext.BaseDirectory, "CultivationLevel.bytes"))))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    int length = reader.Read7BitEncodedInt();
                    long end = reader.BaseStream.Position + length;
                    reader.Read7BitEncodedInt(); // Framework row ID.
                    int level = reader.Read7BitEncodedInt();
                    long experience = reader.Read7BitEncodedInt64();
                    var attribute = (GrowthAttribute)Enum.Parse(typeof(GrowthAttribute), reader.ReadString());
                    var ranges = new AttributeRange[4];
                    for (int i = 0; i < 4; i++) ranges[i] = new AttributeRange(reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt());
                    rows.Add(new CultivationGrowthRow(level, experience, attribute, ranges));
                    Check(reader.BaseStream.Position == end, "Framework row boundary matches binary schema");
                }
            }
            var actual = CultivationConfig.FromRows(rows);
            var expected = GrowthConfig();
            Check(actual.Levels.Count == expected.Levels.Count, "Same number of levels");
            foreach (var level in expected.Levels)
            {
                Check(actual.GetLevel(level.Level).ExperienceToNext == level.ExperienceToNext, "Same experience requirement");
                foreach (GrowthAttribute attribute in Enum.GetValues(typeof(GrowthAttribute)))
                    foreach (TalentGrade grade in Enum.GetValues(typeof(TalentGrade)))
                    {
                        var a = actual.GetLevel(level.Level).Range(attribute, grade);
                        var b = level.Range(attribute, grade);
                        Check(a.Min == b.Min && a.Max == b.Max, "Same per-attribute talent range");
                    }
            }
        });
        Run("Single S upgrade may rarely roll below A while cumulative S stays ahead", () =>
        {
            var config = GrowthConfig();
            int inversions = 0;
            for (int roll = 0; roll <= 10000; roll++)
            {
                int sGain = config.GetLevel(2).Range(GrowthAttribute.Attack, TalentGrade.S).SampleAtLevel(new AttributeTalent(TalentGrade.S, roll), 2);
                int aGain = config.GetLevel(2).Range(GrowthAttribute.Attack, TalentGrade.A).SampleAtLevel(new AttributeTalent(TalentGrade.A, 10000 - roll), 2);
                if (sGain < aGain) inversions++;
            }
            Check(inversions > 0 && inversions < 1000, "Rare single-upgrade reversal is actually possible");
        });
        Run("Cultivation spends experience, carries remainder and adds stats", () =>
        {
            var config = GrowthConfig();
            var actor = new ActorState(1, 1);
            CultivationSystem.Initialize(actor, config, TalentProfile.Generate(137));
            int oldHealth = actor.MaxHealth, oldAttack = actor.Attack, oldLifespan = actor.Lifespan;
            actor.Health -= 25;
            CultivationSystem.Gain(actor, 40, config, 1, _ => { });
            Check(actor.Level == 3 && actor.LevelExperience == 14 && actor.Experience == 40, "Two breakthroughs and remainder");
            Check(actor.MaxHealth > oldHealth && actor.Attack > oldAttack && actor.Lifespan > oldLifespan && actor.MaxHealth - actor.Health == 25, "Additive stats keep missing health");
            Check(config.CumulativeRange(3, GrowthAttribute.Attack, actor.Talents.Attack.Grade).Min <= actor.Attack &&
                actor.Attack <= config.CumulativeRange(3, GrowthAttribute.Attack, actor.Talents.Attack.Grade).Max, "Stats inside cumulative bounds");
        });
        Run("Growth rolls survive history replay under a different actor ID", () =>
        {
            var config = GrowthConfig();
            var source = HexTimelineDemoFactory.Create(cultivation: config, talentSeed: 137);
            source.SubmitPlayerAction(WorldAction.Cultivate(10));
            var record = source.RecordPlayerLife();
            var replay = HexTimelineDemoFactory.Create(cultivation: config, talentSeed: 2);
            replay.AddHistory(record, 10);
            replay.SubmitPlayerAction(WorldAction.Move(HexWorldMap.Id(1, -10)));
            replay.SubmitPlayerAction(WorldAction.Wait(9));
            var original = source.State.Actors[1]; var historical = replay.State.Actors[10];
            Check(historical.Level == 2 && historical.MaxHealth == original.MaxHealth && historical.Attack == original.Attack && historical.Lifespan == original.Lifespan, "Replay retains individual growth rolls");
        });
        Run("Companion growth remains frozen after joining", () =>
        {
            var config = GrowthConfig(); var actor = new ActorState(1, 1);
            CultivationSystem.Initialize(actor, config, TalentProfile.Generate(3));
            actor.HasJoinedChosen = true;
            int attack = actor.Attack;
            CultivationSystem.Gain(actor, 10000, config, 1, _ => { });
            Check(actor.Level == 1 && actor.Experience == 0 && actor.Attack == attack, "No companion upgrades");
        });
        Run("Configured hex playground still clears with preset relays", () =>
        {
            var config = GrowthConfig();
            var sim = HexTimelineDemoFactory.Create(cultivation: config);
            sim.AddHistory(HexTimelineDemoFactory.CreateRelayRecord(30, config), 10);
            sim.AddHistory(HexTimelineDemoFactory.CreateRelayRecord(60, config), 11);
            sim.AddHistory(HexTimelineDemoFactory.CreateRelayRecord(90, config), 12);
            sim.SubmitPlayerAction(WorldAction.Cultivate(120));
            for (int i = 0; i < 120 && !sim.State.IsEnded; i++) sim.AdvanceOneYear();
            Check(sim.State.Outcome == RunOutcome.Cleared && sim.State.Year == 100, "New birth stats preserve the playable relay");
            Check(HexTimelineDemoFactory.CreateFateRecord(config).EndedInDeath, "Configured weak history still has a real death");
        });
        Run("Table-driven Chosen route sets travel time, enemy formation, and ritual stats", () =>
        {
            int gate = HexWorldMap.Id(-1, 0);
            var sim = HexTimelineDemoFactory.Create(
                configureLocations: world => world.Nodes[gate].MonsterHealth = 999,
                chosenRoute: new[]
                {
                    new ChosenRouteStage { Q = -2 },
                    new ChosenRouteStage { Q = -1, TravelYears = 2, GuardianCount = 2,
                        GuardianHealth = 15, GuardianAttack = 5, GuardianAppearance = "GateGolem" },
                    new ChosenRouteStage { TravelYears = 5, GuardianCount = 1,
                        GuardianHealth = 35, GuardianAttack = 10, RitualPeriod = 20 }
                });
            var guardian = sim.State.Nodes[gate];
            Check(sim.Chosen.ArrivalYear == 7 && guardian.GuardianCount == 2 &&
                guardian.MonsterHealth == 30 && guardian.GuardianAppearance == "GateGolem", "Route overrides general location enemies");
            Check(sim.State.Ritual.DeadlineYear == 20 && sim.State.Ritual.BossHealth == 35 &&
                sim.State.Ritual.BossAttack == 10, "Ritual reads the final stage");
            sim.SubmitPlayerAction(WorldAction.Wait(2));
            Check(sim.State.Actors[1000].NodeId == gate && guardian.MonsterHealth == 0, "First leg takes two years");
            sim.SubmitPlayerAction(WorldAction.Wait(5));
            Check(sim.State.Outcome == RunOutcome.Cleared && sim.State.Year == 7, "Second leg takes five years");
        });
        Run("Living traveller keeps a map position throughout movement and terminal freeze", () =>
        {
            var sim = HexTimelineDemoFactory.Create();
            sim.AssignAction(1, WorldAction.Move(HexWorldMap.Id(1, -10), 10));
            for (int i = 0; i < 5; i++) sim.AdvanceOneYear();
            var point = ActorMapProjection.Position(sim.State, sim.State.Actors[1]);
            Check(point.IsMoving && Math.Abs(point.Q - .5) < .001 && point.R == -10, "Halfway traveller is visible");
            sim.State.Finish(RunOutcome.Cleared);
            var frozen = ActorMapProjection.Position(sim.State, sim.State.Actors[1]);
            Check(Math.Abs(frozen.Q - point.Q) < .001 && frozen.R == point.R, "Finishing does not lose living markers");
        });
        Run("All party members have distinct identities at leader's moving position", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 3);
            sim.State.Nodes[2].Q = 0; sim.State.Nodes[3].Q = 3;
            sim.State.Actors[1].LeaderId = 20;
            sim.AdvanceOneYear();
            var player = ActorMapProjection.Position(sim.State, sim.State.Actors[1]);
            var chosen = ActorMapProjection.Position(sim.State, sim.State.Actors[20]);
            Check(player.IsMoving && player.Q == chosen.Q && player.R == chosen.R, "Moving companions remain projectable");
        });
        Run("Location arrivals follow remaining travel, recorded waits and joined actors", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 3);
            var history = new LifeRecorder(new ActorState(10, 2));
            history.RecordAction(0, WorldAction.Wait(2));
            history.RecordAction(2, WorldAction.Move(3));
            sim.AddHistory(history.Snapshot(9), 10);
            var blocked = new LifeRecorder(new ActorState(11, 1));
            blocked.RecordAction(0, WorldAction.Move(4));
            blocked.RecordAction(1, WorldAction.Move(3));
            sim.AddHistory(blocked.Snapshot(9), 11);
            sim.State.Actors[1].NodeId = sim.State.Actors[20].NodeId;
            sim.AssignAction(1, WorldAction.JoinChosen());

            var arrival = ActorMapProjection.NextArrivals(sim, 3);
            Check(arrival.SequenceEqual(new[] { (1, 3), (10, 3), (20, 3) }),
                "Only actors with a valid route appear, sorted by time and ID");
            sim.AdvanceOneYear();
            Check(ActorMapProjection.NextArrivals(sim, 3).SequenceEqual(new[] { (1, 2), (10, 2), (20, 2) }),
                "The countdown uses live remaining time");
            sim.State.Actors[10].Die();
            Check(ActorMapProjection.NextArrivals(sim, 3).SequenceEqual(new[] { (1, 2), (20, 2) }),
                "A dead actor disappears from forecasts immediately");
            sim.AdvanceOneYear();
            sim.AdvanceOneYear();
            Check(ActorMapProjection.NextArrivals(sim, 3).Count == 0, "Occupants are no longer incoming");
        });
        Run("Shared actor routes retain their own progress and skip blocked legs", () =>
        {
            var sim = CreateWithChosen(yearsPerStep: 3);
            var history = new LifeRecorder(new ActorState(10, 2));
            history.RecordAction(0, WorldAction.Move(3));
            history.RecordAction(2, WorldAction.Move(4));
            sim.AddHistory(history.Snapshot(9), 10);
            var blocked = new LifeRecorder(new ActorState(11, 1));
            blocked.RecordAction(0, WorldAction.Move(4));
            blocked.RecordAction(1, WorldAction.Move(3));
            sim.AddHistory(blocked.Snapshot(9), 11);

            var before = ActorMapProjection.Routes(sim);
            Check(before[10][(2, 3)] == ActorMapProjection.LegProgress.Planned &&
                before[20][(2, 3)] == ActorMapProjection.LegProgress.Planned, "Both actors show their planned shared leg");
            Check(before[11].Count == 0 && !before.ContainsKey(1), "Blocked routes stop at the missing connection");
            sim.AdvanceOneYear();
            var after = ActorMapProjection.Routes(sim);
            Check(after[10][(2, 3)] == ActorMapProjection.LegProgress.Travelled &&
                after[20][(2, 3)] == ActorMapProjection.LegProgress.Planned, "A shared leg progresses independently for each actor");
        });
        Run("Outer nest can be cleared before a recorded death", () =>
        {
            int nest = HexTimelineDemoFactory.FateNode;
            int ruin = HexWorldMap.Id(1, -9);
            void Configure(WorldState world)
            {
                world.Nodes[nest].MonsterHealth = 220;
                world.Nodes[nest].MonsterAttack = 120;
                world.Nodes[ruin].MonsterHealth = 0;
                world.Nodes[ruin].Items.Clear();
                world.Nodes[ruin].Items.Add(8201);
                world.Nodes[ruin].LootAttackBonus = 18;
            }

            var first = HexTimelineDemoFactory.Create(health: 240, attack: 100, configureLocations: Configure);
            first.SubmitPlayerAction(WorldAction.Wait());
            first.SubmitPlayerAction(WorldAction.Move(nest));
            var record = first.RecordPlayerLife();
            Check(record.EndedInDeath && record.EndYear == 2, "First cultivator dies in the nest");

            var second = HexTimelineDemoFactory.Create(HexWorldMap.Id(1, -10), health: 100, attack: 30,
                startingItem: 8102, weaponAttack: 195, configureLocations: Configure);
            var history = second.AddHistory(record, 10);
            second.SubmitPlayerAction(WorldAction.Move(nest));
            Check(second.State.Nodes[nest].MonsterHealth == 0, "Second cultivator clears the nest in year one");
            second.SubmitPlayerAction(WorldAction.Move(ruin));
            Check(history.Status == HistoryReplayStatus.FateChanged && second.State.Actors[10].IsAlive,
                "The replay survives the cleared nest");
            Check(second.State.Actors[1].Items.Contains(8201) && second.State.Actors[1].Attack == 243,
                "The ruin awards its item and attack bonus");
        });
        Run("Archived life survives a save reload and can replay", () =>
        {
            var config = GrowthConfig();
            var simulation = HexTimelineDemoFactory.Create(cultivation: config, talentSeed: 137);
            simulation.SubmitPlayerAction(WorldAction.Cultivate(10));
            var original = simulation.RecordPlayerLife();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "timeline-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                WorldTimelineSaveStore.Save(path, new[] { new SavedLife(1, original) });
                WorldTimelineSaveStore.Save(path, new[] { new SavedLife(1, original), new SavedLife(2, original) });
                var saved = WorldTimelineSaveStore.Load(path);
                Check(saved.Count == 2 && saved[1].Round == 2, "Atomic replacement preserves both records");
                var loaded = saved[0].Record;
                Check(loaded.Talents.Attack.Roll == original.Talents.Attack.Roll && loaded.InitialHealth == original.InitialHealth,
                    "Birth attributes and talent rolls persist");
                var replay = HexTimelineDemoFactory.Create(cultivation: config);
                replay.AddHistory(loaded, 10);
                replay.SubmitPlayerAction(WorldAction.Wait(10));
                Check(replay.State.Actors[10].Level == 2, "Reloaded cultivation action replays");
            }
            finally { System.IO.File.Delete(path); }
        });
        Run("Resume history snapshot is independent of later archives", () =>
        {
            var original = Create(attack: 100);
            original.SubmitPlayerAction(WorldAction.Move(3));
            original.SubmitPlayerAction(WorldAction.Cultivate(2));
            byte[] snapshot = WorldTimelineSaveStore.Encode(new[] { new SavedLife(1, original.RecordPlayerLife()) });
            var decoded = WorldTimelineSaveStore.Decode(snapshot);
            Check(decoded.Count == 1 && decoded[0].Record.Actions.Count == 2, "Snapshot preserves initial history");
            original.SubmitPlayerAction(WorldAction.Wait());
            Check(decoded[0].Record.Actions.Count == 2, "Later actions cannot mutate snapshot");
            var replay = Create(attack: 100);
            foreach (var action in decoded[0].Record.Actions) replay.SubmitPlayerAction(action.Action);
            var expected = Create(attack: 100);
            expected.SubmitPlayerAction(WorldAction.Move(3));
            expected.SubmitPlayerAction(WorldAction.Cultivate(2));
            Check(Signature(replay) == Signature(expected), "Snapshot actions reproduce state and events");
            try { WorldTimelineSaveStore.Decode(snapshot.Concat(new byte[] { 1 }).ToArray()); throw new Exception("Accepted trailing data"); }
            catch (System.IO.InvalidDataException) { }
        });
        Run("Native party adapter preserves individual health and equipment", () =>
        {
            var world = new WorldState(1);
            world.Nodes.Add(1, new NodeState(1));
            world.Nodes.Add(2, new NodeState(2) { MonsterHealth = 100, MonsterAttack = 20 });
            world.Nodes[1].Links.Add(2);
            world.Nodes[2].Links.Add(1);
            var player = new ActorState(1, 1, 100, 10);
            player.Items.Add(8101);
            world.Actors.Add(1, player);
            var native = new PartyResolver();
            var sim = new WorldSimulation(world, native);
            sim.ConfigureChosen(new ActorState(20, 1, 200, 10), new[] { 1, 2 }, 1, absorptionPercent: 0);
            sim.SubmitPlayerAction(WorldAction.JoinChosen());
            sim.AdvanceOneYear();
            Check(native.Called && player.Health == 70 && world.Actors[20].Health == 190,
                "Companion and chosen retain their actual individual results");
            Check(world.Nodes[2].MonsterHealth == 0, "Monster result is applied");
        });
        Run("Coastal formation preserves ranged and melee slots across arrivals", () =>
        {
            var world = new WorldState(1);
            world.Nodes.Add(1, new NodeState(1));
            var reef = new NodeState(2);
            world.Nodes.Add(2, reef);
            world.Nodes[1].Links.Add(2);
            world.Nodes[2].Links.Add(1);
            reef.SetCoastalFormation(meleeHealth: 11, rangedHealth: 8, attack: 2);
            world.Actors.Add(1, new ActorState(1, 1, 100, 10));
            world.Actors.Add(2, new ActorState(2, 1, 100, 10));
            var resolver = new CoastalFormationResolver();
            var sim = new WorldSimulation(world, resolver);

            sim.SubmitPlayerAction(WorldAction.Move(2));
            Check(reef.GuardianCount == 2 && reef.MonsterHealth == 19, "Only two original defenders survive");
            world.Actors[2].CurrentAction = WorldAction.Move(2);
            world.Actors[2].RemainingYears = 1;
            sim.AdvanceOneYear();
            Check(resolver.Calls == 2 && reef.GuardianCount == 0 && reef.MonsterHealth == 0, "Reef clears on the second visit");
        });
        Run("Visible combat suspends annual settlement until its result returns", () =>
        {
            var world = new WorldState(1);
            world.Nodes.Add(1, new NodeState(1));
            world.Nodes.Add(2, new NodeState(2) { MonsterHealth = 100, MonsterAttack = 20 });
            world.Nodes[1].Links.Add(2);
            world.Nodes[2].Links.Add(1);
            world.Actors.Add(1, new ActorState(1, 1, 100, 10));
            var sim = new WorldSimulation(world, new DelayedBattleResolver());
            var pending = FlattenRoutine(sim.SubmitPlayerActionRoutine(WorldAction.Move(2)));
            Check(pending.MoveNext(), "Battle yields for its presentation");
            Check(world.Year == 1 && world.Actors[1].Health == 100 && world.Nodes[2].MonsterHealth == 100,
                "Arrival is registered but combat results are not applied early");
            Check(!pending.MoveNext(), "Action resumes and finishes after combat");
            Check(world.Year == 1 && world.Actors[1].Health == 40 && world.Nodes[2].MonsterHealth == 0
                && sim.WaitingForPlayer, "Results apply once without advancing another year");
        });
        Run("Arrival presentation sees every mover before rewards are awarded", () =>
        {
            var world = new WorldState(1);
            for (int id = 1; id <= 3; id++) world.Nodes.Add(id, new NodeState(id));
            world.Nodes[1].Links.Add(2);
            world.Nodes[3].Links.Add(2);
            world.Nodes[2].Items.Add(9102);
            var player = new ActorState(1, 1, 100, 10);
            var chosen = new ActorState(20, 3, 100, 10);
            world.Actors.Add(player.Id, player);
            var sim = new WorldSimulation(world);
            sim.ConfigureChosen(chosen, new[] { 3, 2 }, 1);
            System.Collections.IEnumerator Present()
            {
                Check(world.Year == 1 && player.NodeId == 2 && chosen.NodeId == 2 &&
                    world.Nodes[2].Items.Count == 1, "Both arrivals are staged before the claim");
                yield return null;
            }
            using var routine = FlattenRoutine(sim.SubmitPlayerActionRoutine(WorldAction.Move(2), Present));
            Check(routine.MoveNext() && !player.Items.Contains(9102), "Treasure waits for presentation");
            Check(!routine.MoveNext() && player.Items.Contains(9102) && !chosen.Items.Contains(9102),
                "Simultaneous arrival resolves the unique reward once");
        });
        Run("Battle settlement shows applied rewards and waits for confirmation", () =>
        {
            var world = new WorldState(1) { BattleRewardsFromYear = 1 };
            world.Nodes.Add(1, new NodeState(1));
            world.Nodes.Add(2, new NodeState(2) { MonsterHealth = 100, MonsterAttack = 20, CultivationPerYear = 4 });
            world.Nodes[1].Links.Add(2);
            world.Nodes[2].Links.Add(1);
            world.Nodes[2].Items.Add(9101);
            var player = new ActorState(1, 1, 100, 10);
            world.Actors.Add(1, player);
            var sim = new WorldSimulation(world, new DelayedBattleResolver());
            BattleSettlement summary = null;
            sim.SetBattleSettlementPresenter(Present);
            System.Collections.IEnumerator Present(BattleSettlement result)
            {
                summary = result;
                yield return null;
            }
            using var pending = FlattenRoutine(sim.SubmitPlayerActionRoutine(WorldAction.Move(2)));
            Check(pending.MoveNext() && summary == null && player.Experience == 0, "Rewards wait for combat");
            Check(pending.MoveNext() && summary.Victory && summary.Health == 40 && summary.Experience == 12 &&
                player.LegacySpiritStones == 0 &&
                summary.Items.Single() == 9101 && player.Items.Single() == 9101 && world.Nodes[2].Items.Count == 0,
                "Summary reflects rewards already applied to the player");
            var output = summary.CombatOutput.Single().Skills.Single();
            Check(output.SkillId == 9153 && output.Damage == 100 && output.Shield == 20 && output.Healing == 5,
                "Per-skill damage, shield and healing reach the battle report");
            Check(!pending.MoveNext() && sim.WaitingForPlayer && world.Year == 1 && player.Experience == 12 &&
                player.Items.Count == 1 && player.LegacySpiritStones == 0, "Confirmation resumes without applying rewards twice");
        });
        Run("A resumed life gains battle rewards only from its saved activation year", () =>
        {
            var sim = Create(health: 1000, attack: 100);
            sim.State.BattleRewardsFromYear = 2;
            foreach (int id in new[] { 1, 3 })
            {
                sim.State.Nodes[id].MonsterHealth = 20;
                sim.State.Nodes[id].MonsterAttack = 1;
            }
            var player = sim.State.Actors[1];
            sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(player.Experience == 0 && player.LegacySpiritStones == 0, "Replayed legacy battle keeps its original rewards");
            sim.SubmitPlayerAction(WorldAction.Move(1));
            Check(player.Experience == 6 && player.LegacySpiritStones == 0, "Next victory pays only experience");
        });
        Run("Opening a treasure pauses after the award and does not pay twice", () =>
        {
            var sim = Create(attack: 30);
            var node = sim.State.Nodes[3];
            node.Items.Add(node.Id);
            node.LootAttackBonus = 5;
            var player = sim.State.Actors[1];
            BattleSettlement shown = null;
            sim.SetBattleSettlementPresenter(Present);
            System.Collections.IEnumerator Present(BattleSettlement reward)
            {
                shown = reward;
                yield return null;
            }
            using var pending = FlattenRoutine(sim.SubmitPlayerActionRoutine(WorldAction.Move(3)));
            Check(pending.MoveNext() && shown.IsTreasure && shown.Items.Single() == node.Id &&
                player.Items.Single() == node.Id && player.Attack == 35 && node.Items.Count == 0,
                "Chest contains the item already added to the bag");
            Check(!pending.MoveNext(), "Confirm resumes the move");
            sim.SubmitPlayerAction(WorldAction.Move(1));
            sim.SubmitPlayerAction(WorldAction.Move(3));
            Check(player.Items.Count == 1 && player.Attack == 35, "A reopened route cannot farm the chest");
        });
        Console.WriteLine("Passed " + passed + " world timeline scenarios.");
    }

    private static System.Collections.Generic.IEnumerator<object> FlattenRoutine(System.Collections.IEnumerator routine)
    {
        using (routine as IDisposable)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is System.Collections.IEnumerator nested)
                {
                    using (var flat = FlattenRoutine(nested))
                        while (flat.MoveNext()) yield return flat.Current;
                }
                else yield return routine.Current;
            }
        }
    }

    private sealed class DelayedBattleResolver : IWorldAsyncPartyBattleResolver
    {
        public BattleResult Resolve(int firstHealth, int firstAttack, int secondHealth, int secondAttack) =>
            throw new Exception("Foreground combat must use the asynchronous entry point.");
        public int[] ResolveParty(WorldState world, int nodeId,
            System.Collections.Generic.IReadOnlyList<ActorState> first,
            System.Collections.Generic.IReadOnlyList<ActorState> second) =>
            throw new Exception("Foreground combat must await its result.");
        public System.Collections.IEnumerator ResolvePartyRoutine(WorldState world, int nodeId,
            System.Collections.Generic.IReadOnlyList<ActorState> first,
            System.Collections.Generic.IReadOnlyList<ActorState> second, Action<int[]> completed)
        {
            yield return null;
            first[0].LastBattleOutput = new[]
            {
                new ActorBattleOutput { ActorId = first[0].Id, Allied = true,
                    Skills = new[] { new SkillBattleOutput(9153, 9153, 100, 20, 5) } }
            };
            completed(new[] { 40, 0 });
        }
    }

    private sealed class CoastalFormationResolver : IWorldPartyBattleResolver
    {
        public int Calls { get; private set; }
        public BattleResult Resolve(int firstHealth, int firstAttack, int secondHealth, int secondAttack) =>
            throw new Exception("Coastal battles must receive individual guardians.");

        public int[] ResolveParty(WorldState world, int nodeId,
            System.Collections.Generic.IReadOnlyList<ActorState> first,
            System.Collections.Generic.IReadOnlyList<ActorState> second)
        {
            Calls++;
            if (Calls == 1)
            {
                Check(second.Count == 20 && second.Take(7).All(guard => guard.Health == 8 && guard.Attack == 1) &&
                    second.Skip(7).All(guard => guard.Health == 11 && guard.Attack == 2), "Initial ranks and stats");
                var health = new int[21];
                health[3] = second[2].Health;
                health[18] = second[17].Health;
                return health;
            }

            Check(second.Select(guard => -nodeId - guard.Id).SequenceEqual(new[] { 2, 17 }) &&
                second.Select(guard => guard.Attack).SequenceEqual(new[] { 1, 2 }), "Original slots and roles survive");
            return new[] { first[0].Health, 0, 0 };
        }
    }

    private sealed class PartyResolver : IWorldPartyBattleResolver
    {
        public bool Called;
        public BattleResult Resolve(int firstHealth, int firstAttack, int secondHealth, int secondAttack) =>
            throw new Exception("Native battle must receive full participants.");
        public int[] ResolveParty(WorldState world, int nodeId,
            System.Collections.Generic.IReadOnlyList<ActorState> first,
            System.Collections.Generic.IReadOnlyList<ActorState> second)
        {
            Called = true;
            Check(first.Count == 2 && first[0].Items.Contains(8101) && second.Count == 1 && nodeId == 2,
                "Full party, equipment and node reach the adapter");
            return new[] { 70, 190, 0 };
        }
    }

    private static CultivationConfig GrowthConfig() => CultivationConfig.ParseCsv(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "CultivationLevels.csv")));

    private static WorldSimulation Create(int lifespan = 100, int health = 100, int attack = 10,
        int otherHealth = 100, int otherAttack = 10)
    {
        var state = new WorldState(1);
        for (int i = 1; i <= 3; i++) state.Nodes.Add(i, new NodeState(i) { CultivationPerYear = 2 });
        state.Nodes[1].Links.UnionWith(new[] { 2, 3 });
        state.Nodes[2].Links.UnionWith(new[] { 1, 3 });
        state.Nodes[3].Links.UnionWith(new[] { 1, 2 });
        state.Actors.Add(2, new ActorState(2, 2, otherHealth, otherAttack));
        state.Actors.Add(1, new ActorState(1, 1, health, attack, lifespan));
        return new WorldSimulation(state);
    }

    private static WorldSimulation CreateRitual(int period, int chosenStep, int bossHealth = 100, int bossAttack = 10)
    {
        var sim = CreateWithChosen(yearsPerStep: chosenStep, playerAttack: 1000);
        sim.State.Nodes[1].Links.Add(4);
        sim.ConfigureFinalRitual(bossHealth, bossAttack, period);
        return sim;
    }

    private static LifeRecord FinalArrivalRecord(int arrivalYear)
    {
        var sim = Create(health: 1000, attack: 1000, lifespan: 1000);
        sim.State.Nodes.Add(4, new NodeState(4));
        sim.State.Nodes[1].Links.Add(4);
        sim.SubmitPlayerAction(WorldAction.Move(4, arrivalYear));
        return sim.RecordPlayerLife();
    }

    private static LifeRecord CreateDeathRecord()
    {
        var source = Create();
        source.State.Nodes[3].MonsterHealth = 200;
        source.State.Nodes[3].MonsterAttack = 100;
        source.SubmitPlayerAction(WorldAction.Move(3, 3));
        return source.RecordPlayerLife();
    }

    private static WorldSimulation CreateWithChosen(int yearsPerStep = 10, int playerAttack = 10, int chosenLifespan = 100, int playerHealth = 100)
    {
        var sim = Create(attack: playerAttack, health: playerHealth);
        sim.State.Nodes.Add(4, new NodeState(4));
        sim.State.Nodes[3].Links.Add(4);
        sim.State.Nodes[4].Links.Add(3);
        sim.ConfigureChosen(new ActorState(20, 2, 200, 100, chosenLifespan), new[] { 2, 3, 4 }, yearsPerStep);
        return sim;
    }

    private static string Signature(WorldSimulation sim) => sim.State.Year + ":" + sim.State.Actors[1].Experience + ":" +
        string.Join("|", sim.Events.Select(e => e.Year + ":" + e.Kind + ":" + e.ActorId + ":" + e.NodeId + ":" + e.OtherActorId + ":" + e.Detail));
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
}
