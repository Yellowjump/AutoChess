using GameFramework.Event;
using GameFramework.Fsm;
using Maze;
using SelfEventArg;
using Unity.Profiling;
using UnityEngine;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Procedure.GameStates
{
    /// <summary>
    /// 战斗
    /// </summary>
    public class GameState_Battle:FsmState<ProcedureGame>
    {
        private const int MaxLogicStepsPerFrame = 2;
        private static readonly ProfilerMarker LogicStepMarker =
            new ProfilerMarker("Combat.LogicStep");

        protected override void OnInit(IFsm<ProcedureGame> fsm)
        {
            base.OnInit(fsm);
            GameEntry.Event.Subscribe(BattleStopEventArgs.EventId,OnBattleEnd);
        }

        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            GameEntry.CurrentLogicDeltaTime = GameEntry.BattleLogicDeltaTime;
            CombatBoardBridge.PrepareBattle(Time.time);
            HotGameEntry.HeroManager.StartBattle();
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
            bool battleWon;
            if (CombatBoardBridge.TryConsumeBattleOutcome(out battleWon))
            {
                if (battleWon)
                {
                    ChangeState<GameState_Reward>(fsm);
                }
                else
                {
                    ChangeState<GameState_Lose>(fsm);
                }
                return;
            }
            if (CombatBoardBridge.HasBattleTimedOut(Time.time, ConstValue.BattleMaxDuration))
            {
                ChangeState<GameState_Lose>(fsm);
            }
            else
            {
                CombatBoardBridge.AddFrameTime(elapseSeconds);
                // Keep a slow frame from replaying an unbounded backlog on the main thread.
                CombatBoardBridge.LimitFixedStepBacklog(
                    MaxLogicStepsPerFrame * GameEntry.BattleLogicDeltaTime);
                while (CombatBoardBridge.TryConsumeFixedStep(GameEntry.BattleLogicDeltaTime))
                {
                    using (LogicStepMarker.Auto())
                    {
                        HotGameEntry.HeroManager.OnLogicUpdate(
                            GameEntry.BattleLogicDeltaTime,
                            GameEntry.BattleLogicDeltaTime);
                    }
                }
                HotGameEntry.HeroManager.UpdateBattlePresentation(elapseSeconds,
                    realElapseSeconds);
            }
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
            GameEntry.CurrentLogicDeltaTime = GameEntry.LogicDeltaTime;
            CombatBoardBridge.ResetFixedStepAccumulator();
        }
        public void OnBattleEnd(object sender,GameEventArgs e)
        {
            BattleStopEventArgs ne = (BattleStopEventArgs)e;
            if (ne == null)
            {
                return;
            }

            CombatBoardBridge.QueueBattleOutcome(ne.Win);
        }
    }
}
