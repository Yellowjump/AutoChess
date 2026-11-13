using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.Video;
using WorldTimelineDebug;
using Object = UnityEngine.Object;

public static class OpeningCinematicSmoke
{
    private static MainTitlePanelCtrl menu;
    private static WorldTimelineDebugView session;
    private static string output, previous;
    private static string checkpoint;
    private static int step;
    private static double deadline, next;

    [MenuItem("Tools/World Timeline/Validate Opening Cinematic %#F8")]
    public static void Start()
    {
        if (!EditorApplication.isPlaying || menu != null) return;
        menu = Object.FindFirstObjectByType<MainTitlePanelCtrl>();
        if (menu == null || !menu.Visible) return;
        output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Tests/WorldTimeline/obj/Opening/" +
            DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output);
        previous = WorldTimelineDebugView.EditorSaveDirectory;
        WorldTimelineDebugView.EditorSaveDirectory = Path.Combine(output, "Saves");
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Restore;
        step = 0;
        next = EditorApplication.timeSinceStartup;
        deadline = next + 85;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup >= deadline) throw new TimeoutException("Step " + step);
            var cg = Object.FindFirstObjectByType<OpeningCinematic>();
            switch (step)
            {
                case 0:
                    Click("_btnStart");
                    break;
                case 1:
                    if (cg == null) throw new InvalidOperationException("Fresh start did not open the cinematic");
                    var director = cg.GetComponent<PlayableDirector>();
                    if (director.time < 15.7) return;
                    Check(Math.Abs(director.duration - 32) < .01 && director.state == PlayState.Playing,
                        $"32-second Timeline plays (duration {director.duration:R}, state {director.state})");
                    var view = Object.FindFirstObjectByType<WorldTimelineDebugView>();
                    Check(view != null && view.Simulation == null && !WorldTimelineDebugView.HasUnfinishedRun,
                        "CG plays before simulation or save");
                    var painting = cg.GetComponentsInChildren<RawImage>().Single(image => image.name == "Painting");
                    var video = cg.GetComponentsInChildren<VideoPlayer>().Single();
                    Check(video.isPrepared && video.isPlaying && Math.Abs(video.time - director.time) < .3 &&
                        painting.texture == video.texture,
                        "Timeline displays the single picture master in sync through the chase");
                    var label = cg.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Dialogue");
                    Check(label.text.Contains("我还在这儿") || label.text.Contains("still here"), "Subtitle is evaluated");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "ForgottenChild.png"));
                    break;
                case 2:
                    if (cg != null) return;
                    session = Object.FindFirstObjectByType<WorldTimelineDebugView>();
                    if (Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>() == null) return;
                    Check(session.Simulation == null && session.SelectPreparationCharacter(0) &&
                        session.BeginPreparedRun() && session.Simulation.State.Year == 0,
                        "Natural end opens preparation and the new run begins at year zero");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "FirstChoice.png"));
                    break;
                case 3:
                    session.ReturnToMenu();
                    break;
                case 4:
                    if (!menu.Visible) return;
                    Check(WorldTimelineDebugView.HasUnfinishedRun, "Tutorial checkpoint exists");
                    Click("_btnContinue");
                    break;
                case 5:
                    if (Object.FindFirstObjectByType<WorldTimelinePanelCtrl>() == null) return;
                    Check(cg == null, "Continue bypasses the cinematic");
                    session = Object.FindFirstObjectByType<WorldTimelineDebugView>();
                    Check(session.Simulation.State.Year == 0, "Resume preserves the starting year");
                    session.ReturnToMenu();
                    break;
                case 6:
                    if (!menu.Visible) return;
                    checkpoint = File.ReadAllText(Path.Combine(output, "Saves/current-run.json"));
                    Click("_btnReplayOpening");
                    break;
                case 7:
                    Check(cg != null && Object.FindFirstObjectByType<WorldTimelineDebugView>() == null,
                        "Replay opens with an existing save and no session");
                    cg.GetComponentInChildren<Button>().onClick.Invoke();
                    break;
                case 8:
                    Check(cg == null && menu.Visible && WorldTimelineDebugView.HasUnfinishedRun &&
                        checkpoint == File.ReadAllText(Path.Combine(output, "Saves/current-run.json")),
                        "Replay returns to the title and preserves the checkpoint");
                    break;
                case 9:
                    if (!menu.Visible) return;
                    WorldTimelineDebugView.EditorSaveDirectory = Path.Combine(output, "SkipSaves");
                    Click("_btnStart");
                    break;
                case 10:
                    Check(cg != null, "Fresh save opens the cinematic for skip");
                    cg.GetComponentInChildren<Button>().onClick.Invoke();
                    break;
                case 11:
                    if (Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>() == null) return;
                    Check(cg == null && Object.FindFirstObjectByType<WorldTimelineDebugView>().Simulation == null,
                        "Skip opens preparation");
                    Finish("Passed natural completion, skip, resume, replay with a save, shot and subtitle.");
                    return;
            }
            step++;
            next = EditorApplication.timeSinceStartup + .5;
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void Click(string field) =>
        ((Button)new SerializedObject(menu).FindProperty(field).objectReferenceValue).onClick.Invoke();

    private static void Check(bool result, string message)
    {
        if (!result) throw new InvalidOperationException(message);
        Debug.Log("Opening CG: " + message);
    }

    private static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText(Path.Combine(output, "Result.txt"), result);
        Debug.Log("Opening CG: " + result);
        EditorApplication.isPlaying = false;
    }

    private static void Restore(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        WorldTimelineDebugView.EditorSaveDirectory = previous;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= Restore;
        menu = null;
    }
}
