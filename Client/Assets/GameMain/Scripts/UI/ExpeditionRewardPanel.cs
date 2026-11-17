using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

public sealed class ExpeditionRewardPanel : MonoBehaviour
{
    private WorldTimelineDebugView view;
    private ExpeditionPaperUi ui;
    private int selected=-1;
    private Button confirm;
    private TMP_Text hint;
    private readonly List<Button> options=new List<Button>();
    public static IEnumerator Present(WorldTimelineDebugView view,TMP_FontAsset font)
    {
        var root=ExpeditionPaperUi.Canvas("战后择宝");
        var panel=root.AddComponent<ExpeditionRewardPanel>();panel.view=view;panel.ui=new ExpeditionPaperUi(root,font);
        bool restoreDebugger=HotGameEntry.Debugger!=null&&HotGameEntry.Debugger.ActiveWindow;
        if(restoreDebugger)HotGameEntry.Debugger.ActiveWindow=false;
        try
        {
            panel.Build();
            while(view!=null && view.Expedition.Equipment.PendingRewards.Count>0)yield return null;
        }
        finally
        {
            if(root!=null){root.SetActive(false);Destroy(root);}
            if(restoreDebugger&&HotGameEntry.Debugger!=null)HotGameEntry.Debugger.ActiveWindow=true;
        }
    }
    private void Build()
    {
        ui.Box("Backdrop",0,0,1920,1080,new Color(.08f,.09f,.07f,.88f));
        ui.Page("RewardLedger",170,76,1580,918);
        ui.Text("Title","战后择宝",260,122,1100,84,62).fontStyle=FontStyles.Bold;
        ui.Text("Subtitle","三件战利品，择一收入同行行囊",265,223,1200,47,31,ExpeditionPaperUi.Muted);
        var rewards=view.Expedition.Equipment.PendingRewards;
        for(int i=0;i<rewards.Count;i++)
        {
            int index=i;var item=ExpeditionEquipment.Definition(rewards[i]);float x=264+i*470;
            var button=ui.Button("RewardChoice"+i,"",x,313,449,450,()=>Select(index));
            ui.Picture("RewardIcon",ui.ItemIcon(item.Id),107,28,235,235,button.transform);
            var title=ui.Text("ItemName",item.Name,20,285,409,58,40);
            title.transform.SetParent(button.transform,false);title.alignment=TextAlignmentOptions.Center;
            var type=ui.Text("Kind",$"通用行装 · 1 / {item.MaxLevel} 阶",21,352,407,40,26,ExpeditionPaperUi.Muted);
            type.transform.SetParent(button.transform,false);type.alignment=TextAlignmentOptions.Center;
            var effect=ui.Text("Effect",item.Effect(1),20,397,409,42,29,ExpeditionPaperUi.Jade);
            effect.transform.SetParent(button.transform,false);effect.alignment=TextAlignmentOptions.Center;
            effect.enableAutoSizing=true;effect.fontSizeMin=22;effect.fontSizeMax=29;
            options.Add(button);
        }
        hint=ui.Text("SelectionHint","选择一件战利品",269,831,820,74,30);
        confirm=ui.Button("ClaimReward","领取所选",1230,829,371,80,()=>view.ClaimTeamReward(selected),ExpeditionPaperUi.Red);
        confirm.interactable=false;
    }
    private void Select(int index)
    {
        selected=index;confirm.interactable=true;
        for(int i=0;i<options.Count;i++)ui.MarkSelection(options[i],i==index);
        var item=ExpeditionEquipment.Definition(view.Expedition.Equipment.PendingRewards[index]);
        hint.text="已选 "+item.Name+"\n收入共享行囊，任意同伴均可穿戴与升级。";
    }
    private void OnDestroy(){ui?.Dispose();}
}
