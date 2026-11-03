using System;
using GameFramework.Procedure;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Zizouqi.Bootstrap
{
    public abstract class BootstrapStepProcedure : ProcedureBase
    {
        private bool m_Completed;
        private bool m_Success;

        protected abstract Type NextProcedure { get; }
        protected abstract void Begin(Action<bool> completed);

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            m_Completed = false;
            m_Success = false;
            Begin(success => { m_Success = success; m_Completed = true; });
        }

        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
            if (m_Completed && m_Success)
            {
                m_Completed = false;
                ChangeState(procedureOwner, NextProcedure);
            }
        }
    }

    public sealed class ProcedureBootstrapInit : BootstrapStepProcedure
    {
        protected override Type NextProcedure => typeof(ProcedureCheckVersion);
        protected override void Begin(Action<bool> completed) => BootstrapLoader.Instance.BeginInitialization(completed);
    }

    public sealed class ProcedureCheckVersion : BootstrapStepProcedure
    {
        protected override Type NextProcedure => typeof(ProcedureUpdateResources);
        protected override void Begin(Action<bool> completed) => BootstrapLoader.Instance.BeginVersionCheck(completed);
    }

    public sealed class ProcedureUpdateResources : BootstrapStepProcedure
    {
        protected override Type NextProcedure => typeof(ProcedureLoadHotUpdate);
        protected override void Begin(Action<bool> completed) => BootstrapLoader.Instance.BeginResourceUpdate(completed);
    }

    public sealed class ProcedureLoadHotUpdate : ProcedureBase
    {
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            BootstrapLoader.Instance.BeginHotUpdateLoad();
        }
    }
}
