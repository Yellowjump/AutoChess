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
            eventComp?.Subscribe(EnterPointEventArgs.EventId,EnterPoint);
            eventComp?.Subscribe(ReturnToTitleEventArgs.EventId,ReturnToTitle);
            eventComp?.Subscribe(EventChangeToBattleEventArg.EventId,OnEventChangeToBattle);
            eventComp?.Subscribe(EventCompleteToMapEventArg.EventId,OnEventComplete);
        }

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            GameEntry.UI.OpenUIForm(UICtrlName.GameHudPanel, "tips");
            _gameStateFsm = Fsm<ProcedureGame>.Create("",this, new GameState_Map(),new GameState_Event() ,new GameState_FormationBeforeBattle(),new GameState_Reward(),new GameState_Lose(),new GameState_Battle(),new GameState_SpEvent());
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
                if (point.CurType is MazePointType.UnKnown)
                {
                    List<int> typeList = ListPool<int>.Get();
                    typeList.Add((int)MazePointType.SmallBattle);
                    typeList.Add((int)MazePointType.EliteBattle);
                    typeList.Add((int)MazePointType.Event);
                    typeList.Add((int)MazePointType.Chest);
                    typeList.Add((int)MazePointType.Store);
                    var typeIndex = Utility.Random.GetRandom(typeList.Count);
                    //随机修改当前点为 其他类型
                    point.CurType = (MazePointType)typeList[typeIndex];
                    point.CurLevelID = SelfDataManager.Instance.GetOneRandomLevelIDFormType(point.CurType);
                }
                if (point.CurType is MazePointType.SmallBattle or MazePointType.EliteBattle or MazePointType.BossBattle)
                {
                    _gameStateFsm.ChangeStatePublic<GameState_FormationBeforeBattle>();
                }
                else if (point.CurType is MazePointType.Event or MazePointType.Chest or MazePointType.Store)
                {
                    _gameStateFsm.ChangeStatePublic<GameState_Event>();
                }
            }
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
            SelfDataManager.Instance.CurMazePoint.CurLevelID = ne.BattleLevelID;
            var levelConfigsTable=GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
            if (levelConfigsTable.HasDataRow(ne.BattleLevelID))
            {
                var levelConfigData = levelConfigsTable[ne.BattleLevelID];
                SelfDataManager.Instance.CurMazePoint.CurType = (MazePointType)levelConfigData.MazePointType;
                _gameStateFsm.ChangeStatePublic<GameState_FormationBeforeBattle>();
            }
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