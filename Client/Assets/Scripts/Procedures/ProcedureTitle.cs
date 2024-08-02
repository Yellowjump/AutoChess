using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameFramework.Procedure;
using Entity;
using Maze;
using UnityChan;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
namespace Procedure
{
    public class ProcedureTitle: ProcedureBase
    {
        private bool moveToGame = false;
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            //打开titleUI
            GameEntry.UI.OpenUIForm(UICtrlName.MainTitlePanel, "middle");
        }
        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
            if (moveToGame)
            {
                moveToGame = false;
                ChangeState<ProcedureGame>(procedureOwner);
            }
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            var mainTitle = GameEntry.UI.GetUIForm(UICtrlName.MainTitlePanel);
            GameEntry.UI.CloseUIForm(mainTitle);
        }

        public void MoveToGame()
        {
            moveToGame = true;
        }
    }
}