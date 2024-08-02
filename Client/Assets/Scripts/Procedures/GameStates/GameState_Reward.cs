using GameFramework.Event;
using GameFramework.Fsm;
using Maze;
using SelfEventArg;
using UnityGameFramework.Runtime;

namespace Procedure.GameStates
{
    /// <summary>
    /// 胜利奖励
    /// </summary>
    public class GameState_Reward:FsmState<ProcedureGame>
    {
        private bool _continueToMap;
        protected override void OnInit(IFsm<ProcedureGame> fsm)
        {
            GameEntry.Event.Subscribe(PassPointEventArgs.EventId,OnGetRewardAndContinue);
        }

        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            SelfDataManager.Instance.CurMaze.PassCurPoint();
            GameEntry.UI.OpenUIForm(UICtrlName.BattleRewardPanel, "middle");
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
            if (_continueToMap)
            {
                _continueToMap = false;
                ChangeState<GameState_Map>(fsm);
            }
            QiziGuanLi.Instance.OnLogicUpdate(GameEntry.LogicDeltaTime,realElapseSeconds);
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
        }
        public void OnGetRewardAndContinue(object sender, GameEventArgs e)
        {
            _continueToMap = true;
            QiziGuanLi.instance.FreshBattle();
        }
    }
}