using DataTable;
using GameFramework;
using GameFramework.Procedure;
using liuchengguanli;
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

            QiziGuanLi.Instance.dangqianliucheng = 0;
            //Log.Info("hfk,进入准备状态时间是:"+Time.time);
            prebattleTime = Time.time;
            //string welcomeMessage = Utility.Text.Format("Hello! This is an empty project based on Game Framework {0}.", Version.GameFrameworkVersion);
            //Log.Info(welcomeMessage);
            if (cishu ==0)//首次进入时添加购买棋子UI
            {
                QiziGuanLi.Instance.InitDirenList();
                GameEntry.UI.OpenUIForm(UICtrlName.JieMianUIPrefab, "middle");
                cishu++;
            }
            var assetPath = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (assetPath.HasDataRow(101))
            {
                Log.Info("assetID 101 path is :" + assetPath[101].AssetPath);
            }
            goumaiUI = GameEntry.UI.GetUIForm(UICtrlName.JieMianUIPrefab);

            for (int i = 0; i < QiziGuanLi.Instance.DirenList.Count; i++)
            {
                EntityQizi qz = QiziGuanLi.Instance.DirenList[i];
                qz.GObj.SetActive(false);
            }//遍历场上棋子List让他们回到entityqizi中保存的位置
            for (int i = 0; i < QiziGuanLi.Instance.QiziCSList.Count; i++)
            {
                EntityQizi qz = QiziGuanLi.Instance.QiziCSList[i];
                qz.GObj.transform.position = new Vector3(qz.x, 0, qz.y);
                //跟新qige[][]是否有棋子
                Vector2Int posindex = QiziGuanLi.Instance.getIndexQige(qz.GObj.transform.position);
            }
            //归零qige[][]
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < QiziGuanLi.Instance.qige[i].Length; j++)
                {
                    QiziGuanLi.Instance.qige[i][j] = 0;
                }
            }
        }
        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

            if (Time.time - prebattleTime > 20)
            {
                ChangeState<ProcedureBattle>(procedureOwner);
            }
            else
            {
                if (goumaiUI == null)
                {
                    goumaiUI = GameEntry.UI.GetUIForm(UICtrlName.JieMianUIPrefab);
                }
                goumaiUI.GetComponent<UIguanli>()._slderJindu.value = (Time.time - prebattleTime) / 20;
                goumaiUI.GetComponent<UIguanli>().textjindu.text = "准备阶段剩余：" + (20 - (int)(Time.time - prebattleTime)) + "s";
            }
        }
    }
}

