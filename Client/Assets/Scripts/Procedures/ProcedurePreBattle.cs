using DataTable;
using GameFramework;
using GameFramework.Procedure;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Procedure
{
    public class ProcedurePreBattle : ProcedureBase
    {
        int cishu = 0;
        float prebattleTime;
        UIForm goumaiUI;
        int goumaiUIID;
        protected override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);    
        }
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            Log.Info("hfk,进入准备状态时间是:"+Time.time);
            prebattleTime = Time.time;
            string welcomeMessage = Utility.Text.Format("Hello! This is an empty project based on Game Framework {0}.", Version.GameFrameworkVersion);
            Log.Info(welcomeMessage);
            /*Log.Warning(welcomeMessage);
            Log.Error(welcomeMessage);*/
            if (cishu ==0)//首次进入时添加购买棋子UI
            {
                GameEntry.UI.OpenUIForm(UICtrlName.JieMianUIPrefab, "middle");
                
                cishu++;
            }
            var assetPath = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (assetPath.HasDataRow(101))
            {
                Log.Info("assetID 101 path is :" + assetPath[101].AssetPath);
            }
            goumaiUI = GameEntry.UI.GetUIForm(UICtrlName.JieMianUIPrefab);
        }
        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

            if (Time.time - prebattleTime > 5)
            {
                ChangeState<ProcedureBattle>(procedureOwner);
            }
            else
            {
                if (goumaiUI==null)
                {
                    goumaiUI = GameEntry.UI.GetUIForm(UICtrlName.JieMianUIPrefab);
                }
                goumaiUI.GetComponent<UIguanli>()._slderJindu.value = (Time.time - prebattleTime) / 5;
            }
        }
    }
}

