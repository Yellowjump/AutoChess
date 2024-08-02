using GameFramework.Fsm;
using Maze;
using UnityGameFramework.Runtime;

namespace Procedure.GameStates
{
    /// <summary>
    /// 胜利奖励
    /// </summary>
    public class GameState_Reward:FsmState<ProcedureGame>
    {
        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            SelfDataManager.Instance.CurMaze.PassCurPoint();
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
        }
    }
}