using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;
using Object = UnityEngine.Object;

public static class OpeningCinematicSmoke
{
    private static MainTitlePanelCtrl menu;
    private static WorldTimelineDebugView session;
    private static string output, previous;
    private static string checkpoint;
    private static SavedLife sampleLife;
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
        step = -3;
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
                case -3:
                    PreviewCinematic(ending: true);
                    break;
                case -2:
                    Check(cg != null && Object.FindFirstObjectByType<WorldTimelineDebugView>() == null,
                        "GM ending plays before the first run without creating a session");
                    var heading = cg.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Location");
                    Check(heading.text == "碑倒之后" || heading.text == "After the Stele Fell",
                        "GM ending resolves localized captions from a fresh title screen");
                    cg.GetComponentInChildren<Button>().onClick.Invoke();
                    break;
                case -1:
                    Check(cg == null && menu.Visible && !Directory.Exists(WorldTimelineDebugView.EditorSaveDirectory),
                        "Skipping the GM ending returns to the title without creating saves");
                    break;
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
                    if (Object.FindFirstObjectByType<WorldTimelinePanelCtrl>() == null) return;
                    Check(session.OpeningApproach && session.Simulation.State.Year == 0, "Natural end starts the opening map at year zero");
                    Check(session.Notice == WorldTimelineDebugView.TimelineText("Timeline_Opening_Continue"), "Opening shows the single road instruction");
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
                    Check(session.OpeningApproach && session.Simulation.State.Year == 0, "Resume preserves the tutorial");
                    Check(VisibleCount() == 2, "Only the birthplace and fork are visible");
                    session.SelectOpeningRoute(false);
                    break;
                case 6:
                    if (session.IsBusy) return;
                    Check(session.OpeningOfferRoutes && session.Simulation.State.Year == 1 && VisibleCount() == 7,
                        "The common road reveals both branches at the fork");
                    session.SelectOpeningRoute(true);
                    break;
                case 7:
                    if (session.IsBusy) return;
                    if (session.Simulation.State.Year < 5) { session.SelectOpeningRoute(true); return; }
                    Check(session.Simulation.State.Actors[session.Simulation.Chosen.ActorId].Items.Contains(9102),
                        "The Nameless takes the sword before the detour arrives");
                    session.SelectOpeningRoute(false);
                    Check(!session.OpeningActive && VisibleCount() == 9, "The gate opens two new exits");
                    session.ReturnToMenu();
                    break;
                case 8:
                    if (!menu.Visible) return;
                    Click("_btnContinue");
                    break;
                case 9:
                    if (Object.FindFirstObjectByType<WorldTimelinePanelCtrl>() == null) return;
                    session = Object.FindFirstObjectByType<WorldTimelineDebugView>();
                    Check(!session.OpeningActive && VisibleCount() == 9, "Resume retains discovery beyond the gate");
                    sampleLife = new SavedLife(1, session.Simulation.RecordPlayerLife());
                    session.ReturnToMenu();
                    break;
                case 10:
                    if (!menu.Visible) return;
                    checkpoint = File.ReadAllText(Path.Combine(output, "Saves/current-run.json"));
                    PreviewCinematic(ending: false);
                    break;
                case 11:
                    Check(cg != null && Object.FindFirstObjectByType<WorldTimelineDebugView>() == null,
                        "Replay opens with an existing save and no session");
                    cg.GetComponentInChildren<Button>().onClick.Invoke();
                    break;
                case 12:
                    Check(cg == null && menu.Visible && WorldTimelineDebugView.HasUnfinishedRun &&
                        checkpoint == File.ReadAllText(Path.Combine(output, "Saves/current-run.json")),
                        "Replay returns to the title and preserves the checkpoint");
                    break;
                case 13:
                    if (!menu.Visible) return;
                    WorldTimelineSaveStore.Save(Path.Combine(output, "Saves/cultivator-lives.dat"), new[] { sampleLife });
                    File.WriteAllText(Path.Combine(output, "Saves/unrelated.txt"), "preserve");
                    Click("_btnStart");
                    break;
                case 14:
                    if (FindConfirmation() == null) return;
                    Check(Object.FindFirstObjectByType<WorldTimelineDebugView>() == null &&
                        checkpoint == File.ReadAllText(Path.Combine(output, "Saves/current-run.json")),
                        "New game waits for confirmation before opening a session");
                    Click(FindConfirmation(), "_btnCancel");
                    break;
                case 15:
                    if (FindConfirmation() != null) return;
                    Check(WorldTimelineDebugView.HasUnfinishedRun &&
                        checkpoint == File.ReadAllText(Path.Combine(output, "Saves/current-run.json")) &&
                        FindButton(menu, "_btnContinue").gameObject.activeSelf, "Cancelling a new game preserves Continue and the checkpoint");
                    Click("_btnStart");
                    break;
                case 16:
                    if (FindConfirmation() == null) return;
                    Click(FindConfirmation(), "_btnConfirm");
                    break;
                case 17:
                    if (Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>() == null) return;
                    Check(checkpoint == File.ReadAllText(Path.Combine(output, "Saves/current-run.json")) &&
                        WorldTimelineSaveStore.Load(Path.Combine(output, "Saves/cultivator-lives.dat")).Count == 1,
                        "Confirming opens character selection and keeps the old save until a new life begins");
                    Object.FindFirstObjectByType<WorldTimelineDebugView>().ReturnToMenu();
                    break;
                case 18:
                    if (!menu.Visible) return;
                    ShowClearSaveConfirmation();
                    break;
                case 19:
                    if (FindConfirmation() == null) return;
                    Click(FindConfirmation(), "_btnCancel");
                    break;
                case 20:
                    if (FindConfirmation() != null) return;
                    Check(WorldTimelineDebugView.HasUnfinishedRun &&
                        checkpoint == File.ReadAllText(Path.Combine(output, "Saves/current-run.json")) &&
                        WorldTimelineSaveStore.Load(Path.Combine(output, "Saves/cultivator-lives.dat")).Count == 1 &&
                        FindButton(menu, "_btnContinue").gameObject.activeSelf, "Cancel preserves both saves and Continue");
                    ShowClearSaveConfirmation();
                    break;
                case 21:
                    if (FindConfirmation() == null) return;
                    Click(FindConfirmation(), "_btnConfirm");
                    break;
                case 22:
                    if (FindConfirmation() != null) return;
                    Check(!File.Exists(Path.Combine(output, "Saves/current-run.json")) &&
                        !File.Exists(Path.Combine(output, "Saves/cultivator-lives.dat")) &&
                        File.Exists(Path.Combine(output, "Saves/unrelated.txt")) &&
                        !FindButton(menu, "_btnContinue").gameObject.activeSelf, "Confirmed GM clears both saves and hides Continue");
                    Click("_btnStart");
                    break;
                case 23:
                    Check(cg != null, "Start after clearing both saves opens the cinematic");
                    cg.GetComponentInChildren<Button>().onClick.Invoke();
                    break;
                case 24:
                    if (Object.FindFirstObjectByType<WorldTimelinePanelCtrl>() == null) return;
                    session = Object.FindFirstObjectByType<WorldTimelineDebugView>();
                    Check(cg == null && session.OpeningApproach, "Skip starts the same map");
                    var ritual = session.Simulation.State.Ritual;
                    typeof(FinalRitualState).GetProperty(nameof(FinalRitualState.DeadlineYear)).SetValue(ritual, 1);
                    Click(Object.FindFirstObjectByType<WorldTimelinePanelCtrl>(), "move");
                    break;
                case 25:
                    if (session.IsBusy || session.Simulation.State.Outcome != RunOutcome.WorldDestroyed) return;
                    var panel = Object.FindFirstObjectByType<WorldTimelinePanelCtrl>();
                    var notice = (TMP_Text)new SerializedObject(panel).FindProperty("notice").objectReferenceValue;
                    Check(notice.text == WorldTimelineDebugView.TimelineText("Timeline_Guide_AfterRitual", 1) &&
                        FindButton(panel, "nextLife").interactable, "Ritual defeat shows guidance and enables Begin Next Life");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "AfterRitual.png"));
                    break;
                case 26:
                    Click(Object.FindFirstObjectByType<WorldTimelinePanelCtrl>(), "nextLife");
                    break;
                case 27:
                    if (Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>() == null) return;
                    Check(session.IsPreparing, "Begin Next Life opens character selection after defeat");
                    session.ReturnToMenu();
                    break;
                case 28:
                    if (!menu.Visible) return;
                    Check(FindButton(menu, "_btnQuit").isActiveAndEnabled, "Quit is available on the title screen");
                    EditorApplication.update -= Tick;
                    EditorApplication.playModeStateChanged += VerifyQuit;
                    Click("_btnQuit");
                    return;
            }
            step++;
            next = EditorApplication.timeSinceStartup + .5;
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static int VisibleCount()
    {
        var board = session.GetComponentInChildren<WorldTimelineBoard>();
        var revealed = typeof(WorldTimelineBoard).GetMethod("Revealed", BindingFlags.Instance | BindingFlags.NonPublic);
        return session.Simulation.State.Nodes.Keys.Count(id => (bool)revealed.Invoke(board, new object[] { id }));
    }

    private static void ShowClearSaveConfirmation() =>
        menu.SendMessage("ConfirmClearWorldTimelineSaves");

    private static void PreviewCinematic(bool ending)
    {
        var method = typeof(MainTitlePanelCtrl).GetMethod("TryPreviewCinematic",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check((bool)method.Invoke(menu, new object[] { ending }), "GM preview started");
    }

    private static ConfirmPanelCtrl FindConfirmation() =>
        GameEntry.UI.GetUIForm(UICtrlName.ConfirmPanel)?.Logic as ConfirmPanelCtrl;

    private static Button FindButton(Component view, string field) =>
        (Button)new SerializedObject(view).FindProperty(field).objectReferenceValue;

    private static void Click(Component view, string field) => FindButton(view, field).onClick.Invoke();

    private static void Click(string field) => Click(menu, field);

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

    private static void VerifyQuit(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= VerifyQuit;
        const string result = "Passed CG previews, first-life guidance, resume, new-game confirmation, GM save reset, ritual defeat guidance, and title Quit.";
        File.WriteAllText(Path.Combine(output, "Result.txt"), result);
        Debug.Log("Opening CG: " + result);
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
