using GameFramework.Procedure;
using Entity;
using GameFramework.Event;
using GameFramework.Fsm;
using Maze;
using Procedure.GameStates;
using UnityEngine;
using UnityGameFramework.Runtime;
using SelfEventArg;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
namespace Procedure
{
    /// <summary>
    /// 游戏内主阶段，会有几个子状态
    /// </summary>
    public class ProcedureGame: ProcedureBase
    {
        private Fsm<ProcedureGame> _gameStateFsm;
        private bool _exitGame;
        protected override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
            var eventComp = GameEntry.GetComponent<EventComponent>();
            eventComp?.Subscribe(EnterPointEventArgs.EventId,EnterPoint);
            eventComp?.Subscribe(ReturnToTitleEventArgs.EventId,ReturnToTitle);
        }

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            GameEntry.UI.OpenUIForm(UICtrlName.GameHudPanel, "tips");
            _gameStateFsm = Fsm<ProcedureGame>.Create("",this, new GameState_Map(),new GameState_FormationBeforeBattle(),new GameState_Reward(),new GameState_Lose(),new GameState_Battle(),new GameState_SpEvent());
            _gameStateFsm.Start<GameState_Map>();
        }
        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            if (_exitGame)
            {
                _exitGame = false;
                ChangeState<ProcedureTitle>(procedureOwner);
            }
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
            _gameStateFsm?.UpdatePublic(elapseSeconds,realElapseSeconds);
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            var hud = GameEntry.UI.GetUIForm(UICtrlName.GameHudPanel);
            if (hud != null)
            {
                GameEntry.UI.CloseUIForm(hud);
            }
        }

        protected override void OnDestroy(ProcedureOwner procedureOwner)
        {
            base.OnDestroy(procedureOwner);
            GameEntry.Event.Unsubscribe(EnterPointEventArgs.EventId,EnterPoint);
            GameEntry.Event.Unsubscribe(ReturnToTitleEventArgs.EventId,ReturnToTitle);
        }

        public void EnterPoint(object sender,GameEventArgs e)
        {
            EnterPointEventArgs ne = (EnterPointEventArgs)e;
            if (ne == null)
            {
                return;
            }
            MazePoint point = ne.TargetPoint;
            if (point != null && point.CurPassState == MazePoint.PointPassState.Unlock)
            {
                SelfDataManager.Instance.CurMazePoint = point;
                //if (point.CurType is MazePointType.Start or MazePointType.SmallBattle or MazePointType.EliteBattle or MazePointType.End)
                {
                    _gameStateFsm.ChangeStatePublic<GameState_FormationBeforeBattle>();
                }
            }
        }

        public void ReturnToTitle(object sender, GameEventArgs e)
        {
            GameEntry.HeroManager.GameOver();
            _gameStateFsm.Clear();
            _gameStateFsm = null;
            _exitGame = true;
        }
    }
}