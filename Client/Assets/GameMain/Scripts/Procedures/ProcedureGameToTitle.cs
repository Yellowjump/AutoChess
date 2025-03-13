using System.Collections.Generic;
using DataTable;
using GameFramework.Procedure;
using Entity;
using GameFramework;
using GameFramework.Event;
using GameFramework.Fsm;
using Maze;
using Procedure.GameStates;
using UnityEngine;
using UnityGameFramework.Runtime;
using SelfEventArg;
using UnityEngine.Pool;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
namespace Procedure
{
    /// <summary>
    /// 游戏内主阶段，会有几个子状态
    /// </summary>
    public class ProcedureGameToTitle: ProcedureBase
    {
        private bool _checkToTitle;
        protected override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
        }

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            GameEntry.Event.Subscribe(GCFinishEventArg.EventId,OnGCFinished);
            GameEntry.UI.OpenUIForm(UICtrlName.LoadingPanel, "top");
            GameEntry.Resource.UnloadUnusedAssets(true);
        }
        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            if (_checkToTitle)
            {
                _checkToTitle = false;
                ChangeState<ProcedureTitle>(procedureOwner);
            }
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            GameEntry.Event.Unsubscribe(GCFinishEventArg.EventId,OnGCFinished);
        }

        protected override void OnDestroy(ProcedureOwner procedureOwner)
        {
            base.OnDestroy(procedureOwner);
        }
        private void OnGCFinished(object sender, GameEventArgs e)
        {
            GCFinishEventArg ne = (GCFinishEventArg)e;
            if (ne == null)
            {
                return;
            }
            _checkToTitle = true;
        }
    }
}