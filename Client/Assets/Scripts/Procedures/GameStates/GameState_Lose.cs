using GameFramework.Fsm;
using Maze;
using UnityGameFramework.Runtime;

namespace Procedure.GameStates
{
    /// <summary>
    /// 失败
    /// </summary>
    public class GameState_Lose:FsmState<ProcedureGame>
    {
        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            GameEntry.UI.OpenUIForm(UICtrlName.BattleLosePanel, "middle");
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
            GameEntry.HeroManager.OnLogicUpdate(GameEntry.LogicDeltaTime,realElapseSeconds);
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
        }
    }
}