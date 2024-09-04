using GameFramework.Event;
using GameFramework.Fsm;
using Maze;
using SelfEventArg;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Procedure.GameStates
{
    /// <summary>
    /// 战斗
    /// </summary>
    public class GameState_Battle:FsmState<ProcedureGame>
    {
        float battleTime;
        private float timeAccumulator = 0f;
        private bool _battleEnd = false;
        private bool _battleEndWin = false;
        protected override void OnInit(IFsm<ProcedureGame> fsm)
        {
            base.OnInit(fsm);
            GameEntry.Event.Subscribe(BattleStopEventArgs.EventId,OnBattleEnd);
        }

        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            battleTime = Time.time;
            _battleEnd = false;
            _battleEndWin = false;
            GameEntry.HeroManager.StartBattle();
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
            if (_battleEnd)
            {
                _battleEnd = false;
                if (_battleEndWin)
                {
                    _battleEndWin = false;
                    ChangeState<GameState_Reward>(fsm);
                }
                else
                {
                    ChangeState<GameState_Lose>(fsm);
                }
                return;
            }
            if (Time.time-battleTime>30)
            {
                ChangeState<GameState_Lose>(fsm);
            }
            else
            {
                timeAccumulator += elapseSeconds;
                // 当累积时间大于等于固定的DeltaTime时，执行selfLogicUpdate方法
                while (timeAccumulator >= GameEntry.LogicDeltaTime)
                {
                    GameEntry.HeroManager.OnLogicUpdate(GameEntry.LogicDeltaTime,realElapseSeconds);
                    timeAccumulator -= GameEntry.LogicDeltaTime;
                }
            }
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
            timeAccumulator = 0;
        }
        public void OnBattleEnd(object sender,GameEventArgs e)
        {
            BattleStopEventArgs ne = (BattleStopEventArgs)e;
            if (ne == null)
            {
                return;
            }

            if (_battleEnd == false)
            {
                _battleEnd = true;
                _battleEndWin = ne.Win;
            }
        }
    }
}