using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WorldTimeline;
using WorldTimelineDebug;
using Object = UnityEngine.Object;

public static class WorldTimelinePreparationValidation
{
    [Serializable] private sealed class Report
    { public bool passed; public string error, saveDirectory; public List<string> checks = new List<string>(); }
    private static Report report;
    private static string output;
    private static int stage;
    private static double next, timeout;
    private static WorldTimelineDebugView session;
    private static WorldTimelinePreparationCtrl panel;
    private static string previousSaveDirectory;

    [MenuItem("Tools/World Timeline/Validate Character Selection Flow %#F6")]
    public static void Validate()
    {
        if (!EditorApplication.isPlaying || report != null || Object.FindFirstObjectByType<WorldTimelineDebugView>() != null)
            throw new InvalidOperationException("请运行 bootstrap 并留在主菜单，再执行选人验证。");
        output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Tests/WorldTimeline/obj/Preparation/" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output);
        previousSaveDirectory = WorldTimelineDebugView.EditorSaveDirectory;
        WorldTimelineDebugView.EditorSaveDirectory = Path.Combine(output, "Saves");
        EditorApplication.playModeStateChanged += RestoreSaveDirectory;
        report = new Report { saveDirectory = WorldTimelineDebugView.EditorSaveDirectory };
        stage = 0; next = EditorApplication.timeSinceStartup; timeout = next + 150;
        EditorApplication.update += Tick;
        Debug.Log("开始原生选人流程验证，使用独立临时存档：" + output);
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish("Play 被提前停止。"); return; }
        if (EditorApplication.timeSinceStartup < next) return;
        if (EditorApplication.timeSinceStartup > timeout) { Finish("等待界面或战斗超时，阶段 " + stage); return; }
        try
        {
            switch (stage)
            {
                case 0:
                    var menu = Object.FindFirstObjectByType<MainTitlePanelCtrl>();
                    if (menu == null || !menu.Available) return;
                    Click(menu, "_btnStart"); break;
                case 1:
                    panel = Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>();
                    if (panel == null || panel.Session == null) return;
                    session = panel.Session;
                    Check(session.IsPreparing && session.Simulation == null, "首次开局先选人，不创建模拟");
                    Check(session.PreparationCharacterCount == 10 && session.PreparationHistoryCount == 0, "十张修士卡与前世空状态");
                    Check(session.SelectPreparationCharacter(4) && session.GetPreparationCharacter(4).Selected, "可切换修士与固定装备详情");
                    Click(panel, "characters", 0, "button");
                    Check(session.PreparationSelectedIndex == 0, "原生卡片 Button 正确绑定选择");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "SelectionEmpty.png")); break;
                case 2: session.ReturnToMenu(); break;
                case 3:
                    if (Object.FindFirstObjectByType<WorldTimelineDebugView>() != null) return;
                    var data = WorldTimelineTableConfig.Create();
                    var lives = new List<SavedLife>();
                    for (int i = 0; i < 12; i++)
                    {
                        var character = data.Cultivators[i % 10];
                        var sim = HexTimelineDemoFactory.Create(playerNode: character.Birth, cultivation: data.Cultivation,
                            startingItem: character.Item, weaponAttack: character.Attack);
                        lives.Add(new SavedLife(i + 1, sim.RecordPlayerLife()));
                    }
                    WorldTimelineSaveStore.Save(Path.Combine(report.saveDirectory, "cultivator-lives.dat"), lives);
                    Click(Object.FindFirstObjectByType<MainTitlePanelCtrl>(), "_btnStart"); break;
                case 4:
                    panel = Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>();
                    if (panel == null || panel.Session == null) return;
                    session = panel.Session;
                    Check(session.PreparationCycle == 2 && session.PreparationAvailableCount == 8, "第二轮已使用的两名修士禁用");
                    Check(!session.SelectPreparationCharacter(0) && session.SelectPreparationCharacter(2), "规则层拒绝重复使用修士");
                    Check(!session.SelectPreparationHistory(2, true), "本世不能同时作为前世登场");
                    session.SelectPreparationHistory(0, true); session.SelectPreparationHistory(10, true);
                    Check(session.PreparationSelectedCount == 1 && !session.GetPreparationHistory(0).Selected, "同一修士多条路线互斥");
                    session.SelectPreparationHistory(3, true); session.SelectPreparationCharacter(3);
                    Check(!session.GetPreparationHistory(3).Selected, "切换本世会取消同身份前世");
                    session.SelectPreparationCharacter(2);
                    Click(panel, "nextPage");
                    Check(ReadButton(panel, "previousPage").interactable, "多页历史路线可翻页");
                    Click(panel, "previousPage");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "SelectionHistory.png")); break;
                case 5: Click(panel, "begin"); break;
                case 6:
                    if (session.IsPreparing || Object.FindFirstObjectByType<WorldTimelinePanelCtrl>() == null) return;
                    Check(session.Simulation.State.Year == 0 && session.PlayerName == "白溪", "开启轮回进入元年地图");
                    Check(session.Simulation.State.Actors.Count == 3, "本世、天选和唯一选中前世同时载入");
                    Check(session.Simulation.State.Actors[1].Items.Contains(8103), "初始道具从框架配置表载入");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "MapStarted.png")); break;
                case 7: session.ReturnToMenu(); break;
                case 8:
                    if (Object.FindFirstObjectByType<WorldTimelineDebugView>() != null) return;
                    Check(WorldTimelineDebugView.HasUnfinishedRun, "未完成角色保存续玩存档");
                    Click(Object.FindFirstObjectByType<MainTitlePanelCtrl>(), "_btnContinue"); break;
                case 9:
                    session = Object.FindFirstObjectByType<WorldTimelineDebugView>();
                    if (session == null || session.IsPreparing || session.Simulation == null) return;
                    Check(session.PlayerName == "白溪" && session.Simulation.State.Year == 0, "继续游戏绕过选人恢复同一角色");
                    session.CultivateYears(40); break;
                case 10:
                    if (session.IsBusy || !session.Simulation.State.IsEnded) return;
                    Click(Object.FindFirstObjectByType<WorldTimelinePanelCtrl>(), "nextLife"); break;
                case 11:
                    panel = Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>();
                    if (panel == null || panel.Session == null) return;
                    session = panel.Session;
                    Check(session.IsPreparing && session.GetPreparationCharacter(2).Used, "结算下一世重开选人并禁用刚使用的角色");
                    Click(panel, "returnMenu"); break;
                default: Finish(null); return;
            }
            stage++; next = EditorApplication.timeSinceStartup + 1;
        }
        catch (Exception error) { Debug.LogException(error); Finish(error.ToString()); }
    }
    private static void Check(bool value, string description)
    { if (!value) throw new InvalidOperationException(description); report.checks.Add(description); }
    private static Button ReadButton(Object owner, string field)
    { return (Button)new SerializedObject(owner).FindProperty(field).objectReferenceValue; }
    private static void Click(Object owner, string field) => ReadButton(owner, field).onClick.Invoke();
    private static void Click(Object owner, string array, int index, string field)
    {
        var slot = new SerializedObject(owner).FindProperty(array).GetArrayElementAtIndex(index);
        ((Button)slot.FindPropertyRelative(field).objectReferenceValue).onClick.Invoke();
    }
    private static void Finish(string error)
    {
        EditorApplication.update -= Tick;
        if (report == null) return;
        report.passed = error == null; report.error = error;
        File.WriteAllText(Path.Combine(output, "Validation.json"), JsonUtility.ToJson(report, true));
        Debug.Log("选人流程验证 " + (report.passed ? "通过" : "失败") + "，检查 " + report.checks.Count + " 项：" + output);
        report = null;
        // Leave the override until Play teardown finishes, so no shutdown save touches the user's run.
        EditorApplication.isPlaying = false;
    }
    private static void RestoreSaveDirectory(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        WorldTimelineDebugView.EditorSaveDirectory = previousSaveDirectory;
        EditorApplication.playModeStateChanged -= RestoreSaveDirectory;
    }
}
