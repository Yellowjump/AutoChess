using DataTable;
using GameFramework;
using GameFramework.Procedure;
using liuchengguanli;
using UnityChan;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
namespace Procedure
{
    public class ProcedureBattle: ProcedureBase
    {
        float battleTime;
        UIForm goumaiUI;
        private float timeAccumulator = 0f;
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            QiziGuanLi.Instance.dangqianliucheng = 1;
            //遍历敌人list，让敌人棋子放置在棋盘对面
            for (int i = 0; i < QiziGuanLi.Instance.DirenList.Count; i++)
            {
                EntityQizi qz = QiziGuanLi.Instance.DirenList[i];
                qz.GObj.SetActive(true);
                qz.LogicPosition = new Vector3(-qz.x, 0, -qz.y);
                qz.GObj.transform.rotation = Quaternion.Euler(new Vector3(0, -180, 0));
                //跟新qige[][]是否有棋子
                Vector2Int posindex = QiziGuanLi.Instance.getIndexQige(qz.LogicPosition);
                QiziGuanLi.Instance.qige[posindex.x][posindex.y] = qz.HeroUID;
            }
            for (int i=0;i<QiziGuanLi.Instance.QiziCSList.Count;i++)
            {
                EntityQizi qz = QiziGuanLi.Instance.QiziCSList[i];
                qz.LogicPosition = new Vector3(qz.x, 0, qz.y);
                //跟新qige[][]是否有棋子
                Vector2Int posindex = QiziGuanLi.Instance.getIndexQige(qz.LogicPosition);
                QiziGuanLi.Instance.qige[posindex.x][posindex.y] = qz.HeroUID;
            }
            //Log.Info("hfk,进入战斗状态时间是:" + Time.time);
            goumaiUI = GameEntry.UI.GetUIForm(UICtrlName.JieMianUIPrefab);
            battleTime = Time.time;
        }
        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
            if (Time.time-battleTime>30)
            {
                ChangeState<ProcedurePreBattle>(procedureOwner);
            }
            else
            {
                goumaiUI.GetComponent<UIguanli>()._slderJindu.value = (Time.time - battleTime) / 30;
                goumaiUI.GetComponent<UIguanli>().textjindu.text = "战斗阶段剩余：" + (30 - (int)(Time.time - battleTime)) + "s";
                timeAccumulator += elapseSeconds;
                // 当累积时间大于等于固定的DeltaTime时，执行selfLogicUpdate方法
                while (timeAccumulator >= GameEntry.LogicDeltaTime)
                {
                    QiziGuanLi.Instance.OnLogicUpdate(GameEntry.LogicDeltaTime,realElapseSeconds);
                    timeAccumulator -= GameEntry.LogicDeltaTime;
                }
            }
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            timeAccumulator = 0f;
        }
    }
}