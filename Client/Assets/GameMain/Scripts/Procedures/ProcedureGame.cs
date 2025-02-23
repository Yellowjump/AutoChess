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
    public class ProcedureGame: ProcedureBase
    {
        private Fsm<ProcedureGame> _gameStateFsm;
        private bool _exitGame;
        protected override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
            var eventComp = GameEntry.GetComponent<EventComponent>();
            eventComp?.Subscribe(ReturnToTitleEventArgs.EventId,ReturnToTitle);
            eventComp?.Subscribe(EventChangeToBattleEventArg.EventId,OnEventChangeToBattle);
            eventComp?.Subscribe(EventCompleteToMapEventArg.EventId,OnEventComplete);
        }

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            GameEntry.HeroManager.StartToMapCamera();
            GameEntry.UI.OpenUIForm(UICtrlName.GameHudPanel, "tips");
            _gameStateFsm = Fsm<ProcedureGame>.Create("",this, new GameState_Map(),new GameState_BeforeCameraMove(),new GameState_CameraMove(), new GameState_Event() ,new GameState_FormationBeforeBattle(),new GameState_Reward(),new GameState_Lose(),new GameState_Battle(),new GameState_SpEvent());
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
            GameEntry.Event.Unsubscribe(ReturnToTitleEventArgs.EventId,ReturnToTitle);
        }

        public void ReturnToTitle(object sender, GameEventArgs e)
        {
            GameEntry.HeroManager.GameOver();
            ReferencePool.Release(_gameStateFsm);
            _gameStateFsm = null;
            _exitGame = true;
        }

        private void OnEventChangeToBattle(object sender, GameEventArgs e)
        {
            EventChangeToBattleEventArg ne = (EventChangeToBattleEventArg)e;
            if (ne == null)
            {
                return;
            }
            _gameStateFsm.ChangeStatePublic<GameState_FormationBeforeBattle>();
            //SelfDataManager.Instance.CurAreaPoint.CurLevelID = ne.BattleLevelID;
            /*var levelConfigsTable=GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
            if (levelConfigsTable.HasDataRow(ne.BattleLevelID))
            {
                var levelConfigData = levelConfigsTable[ne.BattleLevelID];
                SelfDataManager.Instance.CurAreaPoint.CurType = (MazePointType)levelConfigData.MazePointType;
            }*/
        }

        private void OnEventComplete(object sender, GameEventArgs e)
        {
            EventCompleteToMapEventArg ee = (EventCompleteToMapEventArg)e;
            if (ee == null)
            {
                return;
            }
            SelfDataManager.Instance.PassCurPoint();
            _gameStateFsm.ChangeStatePublic<GameState_Map>();
        }
    }
}