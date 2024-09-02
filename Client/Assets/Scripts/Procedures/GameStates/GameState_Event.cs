using System;
using DataTable;
using Entity;
using GameFramework.Event;
using GameFramework.Fsm;
using SkillSystem;
using UnityGameFramework.Runtime;
using SelfEventArg;

namespace Procedure.GameStates
{
    public enum EventType
    {
        TestBattleOrLoseCoin = 1,
    }
    /// <summary>
    /// 战前编队
    /// </summary>
    public class GameState_Event:FsmState<ProcedureGame>
    {
        private int _UIIndex;
        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            _UIIndex = GameEntry.UI.OpenUIForm(UICtrlName.BattleFormationPanel, "middle");
            GameEntry.UI.OpenUIForm(UICtrlName.BattleMainPanel, "middle");
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
            GameEntry.UI.CloseUIForm(_UIIndex);
        }

        protected override void OnDestroy(IFsm<ProcedureGame> fsm)
        {
            base.OnDestroy(fsm);
        }

        private void InitEnemy()
        {
            var enemyConfigs = GameEntry.DataTable.GetDataTable<DREnemyConfig>("EnemyConfig");
            if (enemyConfigs.HasDataRow(1))
            {
                 var enemyInfo = enemyConfigs[1].EnemyInfo;
                 foreach (var oneInfo in enemyInfo.InfoList)
                 {
                     GameEntry.HeroManager.InitOneEnemy(oneInfo);
                 }
            }
        }
    }
}