using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WorldTimeline;
using WorldTimelineDebug;
using Object = UnityEngine.Object;

public static class WorldTimelineStoryValidation
{
    [Serializable] private sealed class Report { public bool passed; public string error, saveDirectory; public List<string> checks = new List<string>(); }
    private static Report report;
    private static string output, previousDirectory;
    private static int stage, year, health, attack;
    private static double next, timeout;
    private static WorldTimelineDebugView session;
    private static Button pendingClick;
    private static double clickAt;
    [MenuItem("Tools/World Timeline/Validate Story Flow %#F3")]
    public static void Validate()
    {
        if (!EditorApplication.isPlaying || report != null || Object.FindFirstObjectByType<WorldTimelineDebugView>() != null)
            throw new InvalidOperationException("请运行 bootstrap 并留在主菜单后验证剧情。");
        output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Tests/WorldTimeline/obj/Story/" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output); previousDirectory = WorldTimelineDebugView.EditorSaveDirectory;
        WorldTimelineDebugView.EditorSaveDirectory = Path.Combine(output, "Saves");
        report = new Report { saveDirectory = WorldTimelineDebugView.EditorSaveDirectory };
        stage = 0; next = EditorApplication.timeSinceStartup; timeout = next + 120;
        EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Restore;
        Debug.Log("开始剧情实际流程验证：" + output);
    }
    private static WorldTimelineStoryCtrl Dialogue => Object.FindFirstObjectByType<WorldTimelineStoryCtrl>();
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish("Play 已停止"); return; }
        if (pendingClick != null)
        {
            if (EditorApplication.timeSinceStartup < clickAt) return;
            var button = pendingClick; pendingClick = null; button.onClick.Invoke();
            next = EditorApplication.timeSinceStartup + .5; return;
        }
        if (EditorApplication.timeSinceStartup < next) return;
        if (EditorApplication.timeSinceStartup > timeout) { Finish("阶段超时：" + stage); return; }
        try
        {
            switch (stage)
            {
                case 0:
                    var menu = Object.FindFirstObjectByType<MainTitlePanelCtrl>(); if (menu == null || !menu.Available) return;
                    Click(menu,"_btnStart"); break;
                case 1:
                    var prep = Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>(); if (prep?.Session == null) return;
                    session = prep.Session; session.SelectPreparationCharacter(0); Click(prep,"begin"); break;
                case 2:
                    if (session.Simulation?.Stories == null || Dialogue == null || !Dialogue.Available) return;
                    Check(session.Simulation.Stories.Config.Events.Count == 5 && session.Simulation.Stories.Config.Items.Count == 3,"框架表载入五个事件与三个道具");
                    Move(-1,-9); break;
                case 3:
                    if (session.Simulation.Stories.PendingChoice?.Definition.Id != 2) return;
                    year = session.Simulation.State.Year;
                    Check(session.IsBusy && year == 1,"玩家抵达非战斗节点弹出剧情并暂停");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"StoryChoice.png")); break;
                case 4:
                    Check(session.Simulation.State.Year == year,"阅读对话不推进世界年份");
                    ClickOption(0); break;
                case 5:
                    if (session.Simulation.Stories.PendingResult == null) return;
                    Check(session.Simulation.State.Actors[1].Items.Contains(9101),"领取玉佩后记录真实道具");
                    Check(session.Simulation.Stories.ClaimedItems.Contains(9101),"唯一遗物写入本局占有状态");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"StoryReward.png"));
                    QueueClick(Dialogue,"proceed"); break;
                case 6: if (session.IsBusy) return; session.ReturnToMenu(); break;
                case 7:
                    if (Object.FindFirstObjectByType<WorldTimelineDebugView>() != null) return;
                    Click(Object.FindFirstObjectByType<MainTitlePanelCtrl>(),"_btnContinue"); break;
                case 8:
                    session = Object.FindFirstObjectByType<WorldTimelineDebugView>();
                    if (session?.Simulation?.Stories == null || session.IsPreparing) return;
                    Check(session.Simulation.State.Actors[1].Items.Contains(9101) && session.Simulation.Stories.PendingChoice == null,
                        "续玩重演原选择，恢复奖励且不弹历史对话");
                    Move(-1,-8); break;
                case 9:
                    if (session.Simulation.Stories.PendingChoice?.Definition.Id != 3) return;
                    Check(session.Simulation.Stories.UnavailableReason(session.Simulation.State.Actors[1],session.Simulation.Stories.Config.Options[301]) == null,
                        "持有玉佩时救人分支可选");
                    ClickOption(0); break;
                case 10:
                    if (session.Simulation.Stories.PendingResult == null) return;
                    var actor = session.Simulation.State.Actors[1];
                    Check(!actor.Items.Contains(9101) && actor.Items.Contains(9103),"救人消耗玉佩并获得敛息诀");
                    Check(actor.CombatAttack == actor.Attack * 115 / 100,"被动技能生成战斗实际攻击值");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"StorySkill.png")); QueueClick(Dialogue,"proceed"); break;
                case 11: if (session.IsBusy) return; Move(0,-8); break;
                case 12:
                    if (session.Simulation.Stories.PendingChoice?.Definition.Id != 5) return;
                    year = session.Simulation.State.Year; ClickOption(0); break;
                case 13:
                    if (session.Simulation.Stories.PendingResult == null) return;
                    Check(session.Simulation.Stories.Flags.Contains("bridge_repaired"),"修桥改变本局世界共享标记");
                    Click(Dialogue,"proceed"); break;
                case 14:
                    if (session.IsBusy) return;
                    Check(session.Simulation.State.Year == year + 1,"修复驻留实际消耗一年并推进所有角色");
                    Move(1,-9); break;
                case 15:
                    if (session.Simulation.Stories.PendingChoice?.Definition.Id != 1) return;
                    Check(!session.Simulation.Stories.Choose(101),"最大生命不足时不可领取石门奖励");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"StoryCondition.png")); ClickOption(1); break;
                case 16:
                    if (session.Simulation.Stories.PendingResult == null) return;
                    Click(Dialogue,"proceed"); break;
                case 17:
                    if (session.IsBusy) return;
                    health=session.Simulation.State.Actors[1].Health; attack=session.Simulation.State.Actors[1].CombatAttack;
                    year=session.Simulation.State.Year; session.ReturnToMenu(); break;
                case 18:
                    if (Object.FindFirstObjectByType<WorldTimelineDebugView>() != null) return;
                    Click(Object.FindFirstObjectByType<MainTitlePanelCtrl>(),"_btnContinue"); break;
                case 19:
                    session=Object.FindFirstObjectByType<WorldTimelineDebugView>(); if(session?.Simulation?.Stories==null || session.IsPreparing)return;
                    var player=session.Simulation.State.Actors[1];
                    Check(player.Health==health && player.CombatAttack==attack && session.Simulation.State.Year==year,
                        "多分支含驻留的续玩属性和年份一致");
                    Check(session.Simulation.Stories.Flags.Contains("bridge_repaired") && player.Items.Contains(9103),
                        "续玩恢复桥梁世界状态、消耗品和被动技能");
                    Check(session.Simulation.RecordPlayerLife().StoryChoices.Count==4,"四次实际选择进入角色路线记录");
                    session.ReturnToMenu(); break;
                default: Finish(null); return;
            }
            stage++; next=EditorApplication.timeSinceStartup+1;
        }
        catch(Exception error){Debug.LogException(error);Finish(error.ToString());}
    }
    private static void Move(int q,int r)
    {
        var board=Object.FindFirstObjectByType<WorldTimelineBoard>();
        int node=HexWorldMap.Id(q,r);
        if(!session.SelectAtScreen(board.ProjectNode(node)))throw new Exception("Cannot pick node "+q+","+r);
        if(session.SelectedNodeId!=node || !session.CanMoveSelected)throw new Exception("Wrong selected node");
        session.MoveSelected();
    }
    private static void Click(Object owner,string field)=>((Button)new SerializedObject(owner).FindProperty(field).objectReferenceValue).onClick.Invoke();
    private static void QueueClick(Object owner,string field)
    { pendingClick=(Button)new SerializedObject(owner).FindProperty(field).objectReferenceValue; clickAt=EditorApplication.timeSinceStartup+1; }
    private static void ClickOption(int slot)=>((Button)new SerializedObject(Dialogue).FindProperty("options").GetArrayElementAtIndex(slot).objectReferenceValue).onClick.Invoke();
    private static void Check(bool value,string description){if(!value)throw new Exception(description);report.checks.Add(description);}
    private static void Finish(string error)
    {
        EditorApplication.update-=Tick;if(report==null)return;
        report.passed=error==null;report.error=error;File.WriteAllText(Path.Combine(output,"Validation.json"),JsonUtility.ToJson(report,true));
        Debug.Log("剧情实际流程验证 "+(report.passed?"通过":"失败")+"："+output);report=null;EditorApplication.isPlaying=false;
    }
    private static void Restore(PlayModeStateChange state)
    {if(state!=PlayModeStateChange.EnteredEditMode)return;WorldTimelineDebugView.EditorSaveDirectory=previousDirectory;EditorApplication.playModeStateChanged-=Restore;}
}
