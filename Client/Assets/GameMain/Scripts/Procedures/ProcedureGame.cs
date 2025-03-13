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
            
        }

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            var eventComp = GameEntry.GetComponent<EventComponent>();
            eventComp?.Subscribe(ReturnToTitleEventArgs.EventId,OnEventReturnToTitle);
            eventComp?.Subscribe(EventChangeToBattleEventArg.EventId,OnEventChangeToBattle);
            eventComp?.Subscribe(EventCompleteToMapEventArg.EventId,OnEventComplete);
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
                ChangeState<ProcedureGameToTitle>(procedureOwner);
            }
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
            _gameStateFsm?.UpdatePublic(elapseSeconds,realElapseSeconds);
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            GameEntry.UI.CloseUIForm(UICtrlName.GameHudPanel);
            GameEntry.UI.CloseUIForm(UICtrlName.AreaPointList);
            GameEntry.Event.Unsubscribe(ReturnToTitleEventArgs.EventId,OnEventReturnToTitle);
            GameEntry.Event.Unsubscribe(EventChangeToBattleEventArg.EventId,OnEventChangeToBattle);
            GameEntry.Event.Unsubscribe(EventCompleteToMapEventArg.EventId,OnEventComplete);
        }

        protected override void OnDestroy(ProcedureOwner procedureOwner)
        {
            base.OnDestroy(procedureOwner);
            
            //清除所有数据
            
        }

        private void OnEventReturnToTitle(object sender, GameEventArgs e)
        {
            ReturnToTitleEventArgs ne = (ReturnToTitleEventArgs)e;
            if (ne == null)
            {
                return;
            }
            ReturnToTitle();
        }
        private void ReturnToTitle()
        {
            GameEntry.UI.CloseUIForm(UICtrlName.BattleMainPanel);
            GameEntry.UI.CloseUIForm(UICtrlName.BattleRewardPanel);
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
        }

        private void OnEventComplete(object sender, GameEventArgs e)
        {
            EventCompleteToMapEventArg ee = (EventCompleteToMapEventArg)e;
            if (ee == null)
            {
                return;
            }

            if (GameEntry.HeroManager.PassCurPoint())
            {
                _gameStateFsm.ChangeStatePublic<GameState_Map>();
            }
            else
            {
                GameEntry.HeroManager.DeleteGameRecord();
                ConfirmPanelData newConfirmData = ReferencePool.Acquire<ConfirmPanelData>();
                newConfirmData.Content = GameEntry.Localization.GetString(EnumLanguage.GameFinish);
                newConfirmData.ShowSingleConfirmBtn = true;
                newConfirmData.ConfirmCallback = ReturnToTitle;
                GameEntry.UI.OpenUIForm(UICtrlName.ConfirmPanel, "tips", newConfirmData);
            }
        }
    }
}