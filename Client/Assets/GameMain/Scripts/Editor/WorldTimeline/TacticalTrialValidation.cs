using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TacticalTrialValidation
{
    private const string Pending = "TacticalTrialValidation.Pending";
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static string output;
    private static double deadline;
    private static bool running;

    static TacticalTrialValidation()
    {
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick;
    }

    // Launch with -batchmode -executeMethod TacticalTrialValidation.RunBatch (without -quit).
    public static void RunBatch()
    {
        SessionState.SetBool(Pending, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorSceneManager.OpenScene("Assets/GameMain/Scenes/bootstrap.unity");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/World Timeline/Validate Tactical Trial %#&F9")]
    public static void RunInEditor()
    {
        if (!EditorApplication.isPlaying || running || Object.FindFirstObjectByType<MainTitlePanelCtrl>() is not { Visible: true })
        { Debug.LogWarning("请运行 bootstrap 并留在主菜单，再验证山门试阵。"); return; }
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        { if (running) Finish("Play mode was stopped during validation."); return; }
        if (!running)
        {
            running = true;
            deadline = EditorApplication.timeSinceStartup + 240;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Tests/WorldTimeline/obj/TacticalTrial"));
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "Result.txt"), "RUNNING");
            routines.Push(Validate());
        }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Tactical trial validation timed out.");
            for (int budget = 0; budget < 64 && routines.Count > 0; budget++)
            {
                var routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); (routine as IDisposable)?.Dispose(); continue; }
                if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                break;
            }
            if (routines.Count == 0) Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static IEnumerator Validate()
    {
        MainTitlePanelCtrl title;
        while ((title = Object.FindFirstObjectByType<MainTitlePanelCtrl>()) == null || !title.Visible) yield return null;
        var trials = new List<string>();
        // These are exploratory balance cases using the actual combat engine, not a second approximation.
        var layouts = new[]
        {
            new[] { 0, -1, -1, 2, 1, -1 },
            new[] { -1, 1, 0, -1, -1, 2 },
            new[] { -1, 1, 0, -1, -1, 3 },
            new[] { 2, 1, -1, -1, -1, 0 },
            new[] { 0, 1, -1, 2, -1, -1 },
            new[] { -1, -1, 0, -1, 2, 3 },
            new[] { -1, 0, -1, 2, -1, 1 },
            new[] { -1, 0, -1, 1, -1, 2 },
            new[] { -1, -1, 0, -1, 2, 1 },
            new[] { -1, 0, 1, -1, -1, 2 },
            new[] { 1, 0, -1, 2, -1, -1 }
        };
        var outcomes = new List<TacticalTrialResult>();
        foreach (var layout in layouts)
        {
            var formation = new TacticalTrial();
            for (int role = 0; role < 4; role++) formation.Bench(role);
            for (int cell = 0; cell < layout.Length; cell++) if (layout[cell] >= 0) formation.Place(layout[cell], cell);
            TacticalTrialResult result = null;
            yield return TacticalTrialBattle.Run(formation.BeginAttempt(), false, value => result = value);
            if (result == null) throw new InvalidOperationException("No battle result");
            outcomes.Add(result);
            string line = $"[{string.Join(",", layout)}] won={result.Victory} seconds={result.Seconds:0.0} HP=[{string.Join(",", result.Health)}] damage=[{string.Join(",", result.Damage)}] heal={result.AlliedHealing}/{result.EnemyHealing} first={result.FirstFallenId}";
            trials.Add(line);
            Debug.Log("TACTICAL_TRIAL " + line);
        }
        File.WriteAllLines(Path.Combine(output, "Balance.txt"), trials);
        if (!outcomes.Any(result => result.Victory) || outcomes.All(result => result.Victory))
            throw new InvalidOperationException("Formation cases must include both a win and a loss. See Balance.txt.");
        var damageTeam = outcomes.Where(result => result.Roles.OrderBy(role => role).SequenceEqual(new[] { 0, 1, 2 })).ToArray();
        if (!damageTeam.Any(result => result.Victory) || damageTeam.All(result => result.Victory))
            throw new InvalidOperationException("The same three roles must be able to win or lose by changing formation alone.");
        if (!outcomes.Any(result => result.Roles.Contains(3) && result.Victory))
            throw new InvalidOperationException("The healer team must also have a viable formation.");
        // Repeat the opening layout to verify that a prior run did not leak equipment, state or RNG.
        TacticalTrialResult repeat = null;
        yield return TacticalTrialBattle.Run(new TacticalTrial().BeginAttempt(), false, result => repeat = result);
        if (!repeat.Health.SequenceEqual(outcomes[0].Health) || repeat.EnemyHealing != outcomes[0].EnemyHealing)
            throw new InvalidOperationException("A repeated layout produced different results.");

        title.OpenTacticalTrial();
        yield return null;
        var panel = Object.FindFirstObjectByType<TacticalTrialPanel>();
        if (panel == null || title.Visible) throw new InvalidOperationException("Trial menu entry failed.");
        var slot = panel.GetComponentsInChildren<Button>().Single(button => button.name == "Cell2");
        panel.SelectRole(0);
        slot.onClick.Invoke();
        if (panel.Formation.CellOf(0) != 2) throw new InvalidOperationException("Native formation button failed.");
        TacticalTrialResult expected = null;
        yield return TacticalTrialBattle.Run(panel.Formation.BeginAttempt(), false, result => expected = result);
        Canvas.ForceUpdateCanvases();
        yield return SettleFrames();
        Capture("Formation.png");
        yield return SettleFrames();
        panel.StartFight();
        double captureAt = EditorApplication.timeSinceStartup + 4;
        while (panel.IsFighting && EditorApplication.timeSinceStartup < captureAt) yield return null;
        Capture("Battle.png");
        yield return SettleFrames();
        panel.CancelFight();
        if (panel.IsFighting || HotGameEntry.HeroManager.IsTimelineBattle || HotGameEntry.HeroManager.QiziCSList.Count != 0)
            throw new InvalidOperationException("Retreat did not release the battle board.");
        // Retry immediately on the same screen, then verify natural completion and the result screen.
        panel.StartFight();
        while (panel.IsFighting) yield return null;
        if (panel.LastResult == null) throw new InvalidOperationException("Visible retry did not finish.");
        if (!panel.LastResult.Health.SequenceEqual(expected.Health) || panel.LastResult.Victory != expected.Victory)
            throw new InvalidOperationException("Visible combat differs from deterministic simulation.");
        Canvas.ForceUpdateCanvases();
        yield return SettleFrames();
        Capture("Result.png");
        yield return SettleFrames();
        panel.Close();
        yield return null;
        if (!title.Visible || Object.FindFirstObjectByType<TacticalTrialPanel>() != null)
            throw new InvalidOperationException("Closing trial did not restore the title.");
    }

    private static void Capture(string file)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        ScreenCapture.CaptureScreenshot(Path.Combine(output, file));
    }

    private static IEnumerator SettleFrames()
    {
        double until = EditorApplication.timeSinceStartup + .35;
        while (EditorApplication.timeSinceStartup < until) yield return null;
    }

    private static void Finish(string error)
    {
        while (routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose();
        SessionState.SetBool(Pending, false);
        running = false;
        EditorApplication.update -= Tick;
        if (output != null) File.WriteAllText(Path.Combine(output, "Result.txt"), error ?? "PASS: formation, real combat balance, repeatability, retreat, retry and menu return.");
        if (error != null) Debug.LogError(error); else Debug.Log("TACTICAL_TRIAL_VALIDATION_PASSED");
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
    }
}
