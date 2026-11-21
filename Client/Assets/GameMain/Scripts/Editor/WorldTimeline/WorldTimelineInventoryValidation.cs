using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Entity;
using GameFramework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;
using Object=UnityEngine.Object;

public static class WorldTimelineInventoryValidation
{
    [Serializable] private sealed class Report{public bool passed;public string error,saveDirectory;public List<string> checks=new List<string>();}
    private static Report report;private static string output,previousDirectory;
    private static int stage;private static double next,timeout;
    private static WorldTimelineDebugView session;private static int[] originalItems;
    private static WorldTimelineInventoryCtrl Inventory=>Object.FindFirstObjectByType<WorldTimelineInventoryCtrl>();
    [MenuItem("Tools/World Timeline/Validate Inventory Flow")]
    public static void Validate()
    {
        if(!EditorApplication.isPlaying || report!=null || Object.FindFirstObjectByType<WorldTimelineDebugView>()!=null)
            throw new InvalidOperationException("请运行 bootstrap 并留在主菜单。");
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Tests/WorldTimeline/obj/Inventory/"+DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output);previousDirectory=WorldTimelineDebugView.EditorSaveDirectory;
        WorldTimelineDebugView.EditorSaveDirectory=Path.Combine(output,"Saves");report=new Report{saveDirectory=WorldTimelineDebugView.EditorSaveDirectory};
        stage=0;next=EditorApplication.timeSinceStartup;timeout=next+120;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Restore;
    }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying){Finish("Play 已停止");return;}
        if(EditorApplication.timeSinceStartup<next)return;
        if(EditorApplication.timeSinceStartup>timeout){Finish("阶段超时 "+stage);return;}
        try
        {
            switch(stage)
            {
                case 0:
                    var menu=Object.FindFirstObjectByType<MainTitlePanelCtrl>();if(menu==null||!menu.Available)return;
                    CheckNativeEquipment();Click(menu,"_btnStart");break;
                case 1:
                    var prep=Object.FindFirstObjectByType<WorldTimelinePreparationCtrl>();if(prep?.Session==null)return;
                    session=prep.Session;session.SelectPreparationCharacter(0);Click(prep,"begin");break;
                case 2:
                    var hud=Object.FindFirstObjectByType<WorldTimelinePanelCtrl>();if(hud==null||!hud.Available)return;
                    // This editor-only legacy regression opens the retired screen directly.
                    GameEntry.UI.OpenUIForm(UICtrlName.WorldTimelineInventory,"tips",userData:session);
                    break;
                case 3:
                    if(Inventory?.Session==null)return;
                    Check(session.InventoryOpen&&!Object.FindFirstObjectByType<WorldTimelinePanelCtrl>().CanInteractWithMap,"装备界面阻止地图误操作");
                    var mapBinding=new SerializedObject(Object.FindFirstObjectByType<WorldTimelinePanelCtrl>());
                    Check(!((GameObject)mapBinding.FindProperty("headerSection").objectReferenceValue).activeSelf,"背包打开时隐藏地图顶部");
                    var sections=mapBinding.FindProperty("mapSections");
                    Check(Enumerable.Range(0,sections.arraySize).All(i=>!((GameObject)sections.GetArrayElementAtIndex(i).objectReferenceValue).activeSelf),
                        "地图操作控件隐藏，不覆盖装备界面");
                    Check(Inventory.VisibleItemCount==1&&session.ReadEquipment().Single().Name=="青竹剑","初始装备来自真实角色数据");
                    Check(session.Simulation.State.Year==0,"查看装备不消耗年份");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"InventoryInitial.png"));break;
                case 4:
                    ClickArray(Inventory,"filters",3);Check(Inventory.VisibleItemCount==0,"功法空分类显示空状态");
                    Click(Inventory,"close");break;
                case 5:
                    Check(!session.InventoryOpen&&Object.FindFirstObjectByType<WorldTimelinePanelCtrl>().CanInteractWithMap,"关闭装备恢复地图交互");
                    var board=Object.FindFirstObjectByType<WorldTimelineBoard>();
                    Check(session.SelectAtScreen(board.ProjectNode(HexWorldMap.Id(-1,-9))),"地图节点可选择");session.MoveSelected();break;
                case 6:
                    if(session.Simulation.Stories.PendingChoice==null)return;
                    session.Simulation.Stories.Choose(201);break;
                case 7:
                    if(session.Simulation.Stories.PendingResult==null)return;session.Simulation.Stories.Continue();break;
                case 8:
                    if(session.IsBusy)return;Object.FindFirstObjectByType<WorldTimelinePanelCtrl>().OpenEquipment();break;
                case 9:
                    if(Inventory?.Session==null)return;
                    Check(session.ReadEquipment().Any(x=>x.Id==9101)&&Inventory.VisibleItemCount==2,"剧情新获得遗物自动出现在装备界面");
                    ClickArray(Inventory,"filters",3);
                    Check(Inventory.VisibleItemCount==0,"没有功法时分类保持真实");
                    ClickArray(Inventory,"filters",1);
                    Check(Inventory.VisibleItemCount==1,"兵器分类独立显示");
                    ClickArray(Inventory,"filters",2);Check(Inventory.VisibleItemCount==1,"遗物分类显示新获得的玉佩");
                    ClickArray(Inventory,"filters",0);ClickSlot(Inventory,1);break;
                case 10:
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"InventoryRelic.png"));
                    Check(Text(Inventory,"itemName")=="青髓玉佩"&&Text(Inventory,"description").Contains("80"),"点击物品显示真实名称与效果");
                    originalItems=session.Simulation.State.Actors[1].Items.ToArray();
                    break;
                case 11:
                    for(int i=0;i<41;i++)session.Simulation.State.Actors[1].Items.Add(90000+i);
                    for(int i=0;i<40;i++)session.Simulation.State.Actors[1].Items.Add(8101);
                    ClickArray(Inventory,"filters",0);
                    Check(Inventory.VisibleItemCount==43,"四十三种物品不受六格限制");
                    var scroll=(ScrollRect)new SerializedObject(Inventory).FindProperty("itemScroll").objectReferenceValue;
                    scroll.verticalNormalizedPosition=0;
                    Check(scroll.content.rect.height>scroll.viewport.rect.height,"多行目录可滚动");
                    scroll.content.GetChild(42).GetComponent<Button>().onClick.Invoke();
                    Check(Text(Inventory,"itemName")=="遗物 #90040","最后一件也能查看详情");break;
                case 12:
                    ClickArray(Inventory,"filters",1);
                    Check(Inventory.VisibleItemCount==1&&session.ReadEquipment().Single(x=>x.Id==8101).Count==41,"同名物品合并显示数量而不丢失实例");
                    var actor=session.Simulation.State.Actors[1];actor.Items.Clear();actor.Items.AddRange(originalItems);
                    Click(Inventory,"close");session.ReturnToMenu();break;
                case 13:
                    if(Object.FindFirstObjectByType<WorldTimelineDebugView>()!=null)return;Click(Object.FindFirstObjectByType<MainTitlePanelCtrl>(),"_btnContinue");break;
                case 14:
                    session=Object.FindFirstObjectByType<WorldTimelineDebugView>();if(session?.Simulation==null||session.IsPreparing)return;
                    Check(session.ReadEquipment().Any(x=>x.Id==9101)&&session.ReadEquipment().Sum(x=>x.Count)==2,"续玩恢复真实装备且不保留临时压力数据");
                    Object.FindFirstObjectByType<WorldTimelinePanelCtrl>().OpenEquipment();break;
                case 15:
                    if(Inventory?.Session==null)return;Check(Inventory.VisibleItemCount==2,"续玩后装备入口正常");
                    Click(Inventory,"close");session.ReturnToMenu();break;
                default:Finish(null);return;
            }
            stage++;next=EditorApplication.timeSinceStartup+1;
        }
        catch(Exception error){Debug.LogException(error);Finish(error.ToString());}
    }
    private static void CheckNativeEquipment()
    {
        var manager=HotGameEntry.HeroManager;
        if(manager.QiziCSList.Count!=0||manager.InBattle)throw new Exception("Native combat must be idle");
        int uid=manager.QiziCurUniqueIndex,itemUid=manager.ItemUniqueIndex;
        var simulationFlag=manager.GetType().GetProperty("IsTimelineSimulation");bool oldSimulation=(bool)simulationFlag.GetValue(manager);
        EntityQizi hero=null;var created=new List<OneItemData>();
        try
        {
            simulationFlag.SetValue(manager,true);hero=manager.GetNewEntityQizi();hero.Init(2,new List<int>());manager.QiziCSList.Add(hero);
            for(int i=0;i<20;i++)
            {
                var item=manager.AddEquipItemToEquip(1);created.Add(item);manager.ItemBagList.Add(item);
                if(!manager.TryEquipItem(hero.HeroUID,item))throw new Exception("Equipment rejected at count "+i);
            }
            Check(hero.EquipItemList.Count==20,"原生装备接口实际接收二十件装备");
            Check(hero.NormalSkillList.Count==20,"二十件装备全部进入技能列表，无截断");
            Check(!manager.TryEquipItem(hero.HeroUID,created[0]),"同一背包实例不能重复装备");
            var brush = manager.AddEquipItemToEquip(9145);
            created.Add(brush);
            manager.ItemBagList.Add(brush);
            Check(manager.TryEquipItem(hero.HeroUID,brush) &&
                hero.NormalSkillList.Count(skill => skill.FromItemUID == brush.UniqueID && skill.SkillID == 9147) == 1 &&
                hero.PassiveSkillList.Count(skill => skill.FromItemUID == brush.UniqueID && skill.SkillID == 9145) == 1 &&
                hero.GetSkillByItemUniqID(brush.UniqueID)?.SkillID == 9147, "寻隙笔同时提供主动和被动，优先显示主动");
            Check(!manager.TryEquipItem(-99999,created[0]),"无效角色不会抛空引用");
        }
        finally
        {
            if(hero!=null){manager.QiziCSList.Remove(hero);hero.Remove();}
            foreach(var item in created)if(manager.ItemBagList.Remove(item))ReferencePool.Release(item);
            manager.QiziCurUniqueIndex=uid;manager.ItemUniqueIndex=itemUid;simulationFlag.SetValue(manager,oldSimulation);
        }
    }
    private static string Text(Object owner,string field)=>((TMP_Text)new SerializedObject(owner).FindProperty(field).objectReferenceValue).text;
    private static void Click(Object owner,string field)=>((Button)new SerializedObject(owner).FindProperty(field).objectReferenceValue).onClick.Invoke();
    private static void ClickArray(Object owner,string field,int index)=>((Button)new SerializedObject(owner).FindProperty(field).GetArrayElementAtIndex(index).objectReferenceValue).onClick.Invoke();
    private static void ClickSlot(Object owner,int index)=>((Button)new SerializedObject(owner).FindProperty("slots").GetArrayElementAtIndex(index).FindPropertyRelative("button").objectReferenceValue).onClick.Invoke();
    private static void Check(bool value,string message){if(!value)throw new Exception(message);report.checks.Add(message);}
    private static void Finish(string error)
    {
        EditorApplication.update-=Tick;if(report==null)return;report.passed=error==null;report.error=error;
        File.WriteAllText(Path.Combine(output,"Validation.json"),JsonUtility.ToJson(report,true));
        Debug.Log("装备实际流程验证 "+(report.passed?"通过":"失败")+"："+output);report=null;EditorApplication.isPlaying=false;
    }
    private static void Restore(PlayModeStateChange state)
    {if(state!=PlayModeStateChange.EnteredEditMode)return;WorldTimelineDebugView.EditorSaveDirectory=previousDirectory;EditorApplication.playModeStateChanged-=Restore;}
}
