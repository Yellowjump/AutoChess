using System;
using UnityEditor;
using UnityEngine;
using WorldTimelineDebug;
using Object = UnityEngine.Object;

public static class WorldTimelineInstancingValidation
{
    private static WorldTimelineDecorationInstancing renderer;
    private static WorldTimelineBoard board;
    private static Vector3 focus;
    private static float zoom;
    private static bool originalEnabled;
    private static int stage, baseline, instanced;
    private static double deadline;
    private static int frames;

    [MenuItem("Tools/World Timeline/Compare Decoration Draw Calls %&F12")]
    public static void Compare()
    {
        if (!EditorApplication.isPlaying || renderer != null)
            throw new InvalidOperationException("请先运行并进入空闲的轮回地图，且不要重复开始对比。");
        var session = Object.FindFirstObjectByType<WorldTimelineDebugView>();
        board = Object.FindFirstObjectByType<WorldTimelineBoard>();
        renderer = Object.FindFirstObjectByType<WorldTimelineDecorationInstancing>();
        if (board == null || renderer == null || session == null || session.IsBusy)
        { renderer = null; throw new InvalidOperationException("需要空闲的轮回地图和样板实例化组件。"); }
        var view = board.CaptureView(); focus = view.Focus; zoom = view.Zoom;
        originalEnabled = renderer.enabled;
        board.Focus(0, -9); board.Zoom(.4f);
        renderer.enabled = false;
        baseline = instanced = frames = stage = 0;
        deadline = EditorApplication.timeSinceStartup + 1.5;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += PlayState;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || board == null || renderer == null) { Finish(); return; }
        if (EditorApplication.timeSinceStartup < deadline) return;
        if (stage == 0) baseline += UnityStats.drawCalls;
        else instanced += UnityStats.drawCalls;
        if (++frames < 20) return;
        if (stage == 0)
        {
            renderer.enabled = true; stage = 1; frames = 0;
            deadline = EditorApplication.timeSinceStartup + 1.5;
            return;
        }
        Debug.Log($"轮回装饰同视角对比：普通渲染 {baseline / 20f:F1} Draw Calls → 实例化 {instanced / 20f:F1}；"
            + $"实例化提交 {renderer.SubmittedBatches} 组 / {renderer.SubmittedInstances} 个子网格实例。"
            + "包含场景、UI、地形、阴影等全部开销；未推进游戏时间。");
        Finish();
    }

    private static void PlayState(PlayModeStateChange state)
    { if (state == PlayModeStateChange.ExitingPlayMode) Finish(); }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= PlayState;
        if (renderer != null) renderer.enabled = originalEnabled;
        if (board != null) board.RestoreView(focus, zoom);
        renderer = null; board = null;
    }
}
