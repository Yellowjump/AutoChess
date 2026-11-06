using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WorldTimeline;
using WorldTimelineDebug;
using Object = UnityEngine.Object;

public static class WorldTimelineInkMapValidation
{
    [Serializable] private sealed class Report { public bool passed; public string error; public List<string> checks = new List<string>(); }
    private static Report report;
    private static string output, previousDirectory;
    private static int stage, originalMask;
    private static double next, deadline;
    private static WorldTimelineDebugView session;
    private static bool preview;
    private static bool capturePending;
    [MenuItem("Tools/World Timeline/Preview Ink Map Sample")]
    public static void Preview() { Validate(); preview = true; }
    [MenuItem("Tools/World Timeline/Validate Ink Map Sample")]
    public static void Validate()
    {
        if (!EditorApplication.isPlaying || report != null || Object.FindFirstObjectByType<WorldTimelineDebugView>() != null)
            throw new InvalidOperationException("请运行 bootstrap 并停在主菜单。");
        output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../Tests/WorldTimeline/obj/InkMap/"+DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output);
        previousDirectory = WorldTimelineDebugView.EditorSaveDirectory;
        WorldTimelineDebugView.EditorSaveDirectory = Path.Combine(output,"Saves");
        report = new Report(); preview = false; stage = 0; next = EditorApplication.timeSinceStartup; deadline = next + 300;
        originalMask = Camera.main.cullingMask;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += RestoreDirectory;
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish("Play 已停止"); return; }
        if (EditorApplication.isPaused) return;
        if (capturePending) return;
        if (EditorApplication.timeSinceStartup < next) return;
        if (EditorApplication.timeSinceStartup > deadline) { Finish("阶段超时 " + stage); return; }
        try
        {
            switch(stage)
            {
                case 0:
                    var menu = Object.FindFirstObjectByType<MainTitlePanelCtrl>();
                    if (menu == null || !menu.Available) return;
                    Click(menu,"_btnStart"); break;
                case 1:
                    var prep = Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>();
                    if (prep?.Session == null) return;
                    session = prep.Session; session.SelectPreparationCharacter(0); Click(prep,"begin"); break;
                case 2:
                    if (session.Simulation == null || Object.FindFirstObjectByType<WorldTimelinePanelCtrl>() == null) return;
                    var ink = Object.FindFirstObjectByType<WorldTimelineInkMap>();
                    if (ink != null && !ink.IsApplied) return;
                    Check(ink != null && ink.IsApplied,"地图视角使用独立图层，原场景不穿插");
                    Check(session.Simulation.State.Nodes.Count == 331,"331个原节点完整保留");
                    Check(ink.GetComponentsInChildren<WorldTimelineRegion>(true).Length == WorldTimelineInkMapAuthoring.Definitions.Length,
                        "扩展区域均为独立预制体");
                    Check(Camera.main.cullingMask == (1 << LayerMask.NameToLayer(WorldTimelineInkMap.LayerName)),"地图相机仅渲染水墨表现");
                    var brain = Camera.main.GetComponent<Cinemachine.CinemachineBrain>();
                    var live = brain != null ? brain.ActiveVirtualCamera : null;
                    Check(live != null && live.Name == "轮回地图虚拟相机", "Brain持续接管轮回地图虚拟相机");
                    Check((Camera.main.cullingMask & (1 << live.VirtualCameraGameObject.layer)) != 0,
                        "地图Mask包含虚拟相机图层，Cinemachine不会将其排除");
                    var layout = ink.GetComponent<WorldTimelineMapLayout>();
                    Check(layout.FlatSurface && layout.InkPalette,"平面投影与淡墨配色启用");
                    var sceneCamera = Camera.main;
                    int mapMask = sceneCamera.cullingMask;
                    var mapFlags = sceneCamera.clearFlags;
                    var scenery = ink.GetComponentsInChildren<MeshRenderer>(true);
                    Check(scenery.Length > 0 && scenery.All(r => !r.forceRenderingOff && r.sharedMaterial.FindPass("InkScenery") >= 0),
                        "宣纸和水墨区块使用已启用的普通MeshRenderer");
                    for (int i=0;i<100;i++) WorldTimelineBoard.ForRenderingCamera(sceneCamera);
                    Check(sceneCamera.cullingMask == mapMask && sceneCamera.clearFlags == mapFlags,
                        "重复查询渲染相机不修改Mask和背景类型");
                    using (WorldTimelineBoard.SuspendMapPresentation())
                    {
                        Check(sceneCamera.cullingMask == originalMask && WorldTimelineBoard.ForRenderingCamera(sceneCamera) == null,
                            "可视战斗暂停地图并恢复场景Mask");
                        Check(scenery.All(r => r.forceRenderingOff),"可视战斗隐藏普通地图Renderer");
                        using (WorldTimelineBoard.SuspendMapPresentation()) { }
                        Check(sceneCamera.cullingMask == originalMask,"嵌套暂停不会提前恢复地图Mask");
                    }
                    Check(sceneCamera.cullingMask == mapMask && sceneCamera.clearFlags == mapFlags && ink.IsApplied,
                        "可视战斗结束后一次性恢复地图设置");
                    var board = Object.FindFirstObjectByType<WorldTimelineBoard>();
                    board.RestoreView(layout.Origin + new Vector3(-1.73205f * 4.1f,0,1.5f * 8.2f) * layout.NodeSpacing, .34f);
                    break;
                case 3:
                    File.WriteAllText(Path.Combine(output,"Rendering.txt"), string.Join("\n",Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                        .Select(c => c.name + " mask=" + c.cullingMask + " enabled="+c.enabled+" depth="+c.depth+" flags="+c.clearFlags+" pos="+c.transform.position))
                        + "\nOverlayDrawCount=" + Object.FindFirstObjectByType<WorldTimelineBoard>().OverlayDrawCount);
                    capturePending = true;
                    session.StartCoroutine(CaptureFrame("InkMapGameplay.png", true));
                    break;
                case 4:
                    var map = Object.FindFirstObjectByType<WorldTimelineBoard>();
                    map.Focus(-1,-9);
                    break;
                case 5:
                    var currentBoard = Object.FindFirstObjectByType<WorldTimelineBoard>();
                    Check(session.SelectAtScreen(currentBoard.ProjectNode(HexWorldMap.Id(-1,-9))),"平面节点支持原地图点击选择");
                    capturePending = true;
                    session.StartCoroutine(CaptureFrame("InkMapOuter.png", false));
                    break;
                case 6:
                    session.ReturnToMenu(); break;
                case 7:
                    Check(!Object.FindFirstObjectByType<WorldTimelineInkMap>().IsApplied,"离开地图后恢复场景表现");
                    Check(Camera.main.cullingMask == originalMask,"主相机图层恢复");
                    Finish(null); return;
            }
            stage++; next = EditorApplication.timeSinceStartup + 1.2;
        }
        catch(Exception error) { Finish(error.ToString()); }
    }
    private static void Check(bool value,string message) { if(!value) throw new InvalidOperationException(message); report.checks.Add(message); }
    private static IEnumerator CaptureFrame(string name, bool verify)
    {
        yield return new WaitForEndOfFrame();
        try
        {
            Capture(name,verify);
            if(preview && verify) EditorApplication.isPaused = true;
        }
        catch(Exception error) { Finish(error.ToString()); }
        finally { capturePending = false; }
    }
    private static void Capture(string name, bool verify)
    {
        // Unity's Game view is rendered to its own RT; the player backbuffer can contain an older editor frame.
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var gameView = Resources.FindObjectsOfTypeAll(gameViewType).FirstOrDefault();
        var target = gameViewType.GetField("m_RenderTexture", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(gameView) as RenderTexture;
        if (target == null) throw new InvalidOperationException("Game 窗口没有可读取的实际画面，请保持 Game 标签打开。");
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
            image.Apply();
        }
        finally { RenderTexture.active = previous; }
        try
        {
            File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
            if (!verify) return;
            int neutral = 0, ink = 0, samples = 0;
            for (int y=image.height/3;y<image.height*4/5;y+=5)
            for (int x=image.width/5;x<image.width*4/5;x+=5)
            {
                var c=image.GetPixel(x,y); samples++;
                if(Mathf.Max(c.r,Mathf.Max(c.g,c.b))-Mathf.Min(c.r,Mathf.Min(c.g,c.b))<.12f) neutral++;
                if(c.grayscale<.75f) ink++;
            }
            Check(neutral > samples * .8f,"实机画面以宣纸灰墨为主，非旧地形残留");
            Check(ink > samples * .01f,"实机画面包含墨迹与路线，非纯白空图");
        }
        finally { Object.DestroyImmediate(image); }
    }
    private static void Click(Object ctrl,string field) => ((Button)new SerializedObject(ctrl).FindProperty(field).objectReferenceValue).onClick.Invoke();
    private static void Finish(string error)
    {
        if (report == null) return;
        report.passed = error == null; report.error = error ?? "";
        File.WriteAllText(Path.Combine(output,"Validation.json"),JsonUtility.ToJson(report,true));
        EditorApplication.update -= Tick;
        if (error != null) Debug.LogError(error); else Debug.Log("水墨样板验证通过：" + report.checks.Count + " 项；" + output);
        report = null;
        EditorApplication.isPlaying = false;
    }
    private static void RestoreDirectory(PlayModeStateChange state)
    {
        if(state != PlayModeStateChange.EnteredEditMode) return;
        WorldTimelineDebugView.EditorSaveDirectory = previousDirectory;
        EditorApplication.playModeStateChanged -= RestoreDirectory;
    }
}
