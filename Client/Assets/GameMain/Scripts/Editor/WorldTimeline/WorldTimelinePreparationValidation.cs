using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    var data = WorldTimelineTableConfig.Create();
                    var lives = new List<SavedLife>();
                    int[] items = { 8102, 8109, 8111, 8102, 8104 };
                    for (int i = 0; i < items.Length; i++)
                    {
                        var character = data.Cultivators.Single(candidate => candidate.Item == items[i]);
                        var sim = HexTimelineDemoFactory.Create(playerNode: character.Birth, cultivation: data.Cultivation,
                            startingItem: character.Item, weaponAttack: character.Attack);
                        if (i == 3) sim.SubmitPlayerAction(WorldAction.Wait());
                        lives.Add(new SavedLife(i + 1, sim.RecordPlayerLife()));
                    }
                    WorldTimelineSaveStore.Save(Path.Combine(report.saveDirectory, "cultivator-lives.dat"), lives);
                    Click(menu, "_btnStart"); break;
                case 1:
                    panel = Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>();
                    if (panel == null || panel.Session == null) return;
                    session = panel.Session;
                    Check(session.IsPreparing && session.Simulation == null, "选人时尚未创建模拟");
                    Check(session.PreparationCharacterCount == 3 && session.PreparationHistoryCount == 4,
                        "三个可选修士，重复前世归并");
                    Check(session.PreparationCycle == 2 && session.PreparationAvailableCount == 2 &&
                        !session.SelectPreparationCharacter(0), "本轮已使用的修士禁用");
                    Check(session.SelectPreparationCharacter(1) && !session.GetPreparationHistory(0).JoinsRun,
                        "选择孤松排除孤松的前世");
                    Click(panel, "characters", 2, "button");
                    Check(session.GetPreparationCharacter(2).Name == "祁墨" && session.PreparationSelectedIndex == 2,
                        "祁墨的原生卡片可以选中");
                    Check(session.PreparationCompanionCount == 3 && !session.GetPreparationHistory(1).JoinsRun &&
                        session.GetPreparationHistory(0).JoinsRun && !ReadButton(panel, "clearHistories").gameObject.activeSelf,
                        "切换本世后自动更新登场名单，清空按钮隐藏");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "SelectionHistory.png")); break;
                case 2: Click(panel, "begin"); break;
                case 3:
                    if (session.IsPreparing || Object.FindFirstObjectByType<WorldTimelinePanelCtrl>() == null) return;
                    Check(session.Simulation.State.Year == 0 && session.PlayerName == "祁墨", "开启轮回进入元年地图");
                    Check(session.Simulation.State.Actors.Count == 5, "本世、天选和三条合资格前世同时载入");
                    var histories = session.Simulation.Histories.Select(history => history.Record).ToArray();
                    Check(histories.Single(life => life.InitialItems.Contains(8102)).Actions.Count == 1 &&
                        histories.Any(life => life.InitialItems.Contains(8104)), "保留最近的赤砾路线和旧角色的前世");
                    Check(session.Simulation.State.Actors[1].Items.Contains(8111), "初始道具从框架配置表载入");
                    Check(new[] { 28, 29, 30 }.All(session.Simulation.Stories.Config.Events.ContainsKey), "三条角色剧情已载入");
                    var nodes = session.Simulation.State.Nodes;
                    Check(session.LocationName(nodes[HexWorldMap.Id(-8, 2)]) == "廊下的废簿" &&
                        session.LocationName(nodes[HexWorldMap.Id(0, -7)]) == "洗剑泉",
                        "祁墨可看到自己的剧情节点，其他角色的节点保留地名");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "MapStarted.png")); break;
                case 4: session.ReturnToMenu(); break;
                case 5:
                    if (Object.FindFirstObjectByType<WorldTimelineDebugView>() != null) return;
                    Check(WorldTimelineDebugView.HasUnfinishedRun, "未完成角色保存续玩存档");
                    Click(Object.FindFirstObjectByType<MainTitlePanelCtrl>(), "_btnContinue"); break;
                case 6:
                    session = Object.FindFirstObjectByType<WorldTimelineDebugView>();
                    if (session == null || session.IsPreparing || session.Simulation == null) return;
                    Check(session.PlayerName == "祁墨" && session.Simulation.State.Year == 0 &&
                        session.Simulation.Histories.Count == 3, "续玩恢复同一角色与三条前世");
                    typeof(WorldTimelineDebugView).GetMethod("Submit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(session, new object[] { WorldAction.Wait(40) }); break;
                case 7:
                    if (session.IsBusy || !session.Simulation.State.IsEnded) return;
                    Click(Object.FindFirstObjectByType<WorldTimelinePanelCtrl>(), "nextLife"); break;
                case 8:
                    panel = Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>();
                    if (panel == null || panel.Session == null) return;
                    session = panel.Session;
                    Check(session.IsPreparing && session.PreparationAvailableCount == 3, "下一轮的三位角色重新可选");
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
