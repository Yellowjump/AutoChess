using DataTable;
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
        protected override void OnInit(IFsm<ProcedureGame> fsm)
        {
        }

        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            HotGameEntry.Sound.StopMusic();
            GameEntry.UI.OpenUIForm(UICtrlName.BattleRewardPanel, "middle");
            HotGameEntry.HeroManager.OnBattleWin();
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
            HotGameEntry.HeroManager.UpdateNoBattle(GameEntry.LogicDeltaTime,realElapseSeconds);
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            HotGameEntry.HeroManager.FreshBattle();
            HotGameEntry.HeroManager.ReleaseFriendGObj();
            HotGameEntry.Sound.PlayMusic((int)EnumSound.GameStartBGM);
            base.OnLeave(fsm, isShutdown);
        }
    }
}