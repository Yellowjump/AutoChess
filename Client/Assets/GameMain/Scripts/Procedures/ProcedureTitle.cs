using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameFramework.Procedure;
using Entity;
using GameFramework.Event;
using Maze;
using SelfEventArg;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
namespace Procedure
{
    public class ProcedureTitle: ProcedureBase
    {
        private bool moveToNewGame = false;
        private bool moveToContinueGame = false;
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            GameEntry.Event.Subscribe(MoveToNewGameEventArgs.EventId,OnEventStartNewGame);
            GameEntry.Event.Subscribe(MoveToContinueGameEventArgs.EventId,OnEventStartContinueGame);
            //打开titleUI
            GameEntry.UI.OpenUIForm(UICtrlName.MainTitlePanel, "middle");
            GameEntry.HeroManager.InitStartCamera();
            GameEntry.Sound.PlayMusic((int)EnumSound.GameStartBGM);
        }
        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
            if (moveToNewGame)
            {
                moveToNewGame = false;
                //初始化关卡数据
                InitNewGameData();
                ChangeState<ProcedureGame>(procedureOwner);
            }
            else if (moveToContinueGame)
            {
                moveToContinueGame = false;
                //读取存档数据
                InitContinueGameData();
                ChangeState<ProcedureGame>(procedureOwner);
            }
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            var mainTitle = GameEntry.UI.GetUIForm(UICtrlName.MainTitlePanel);
            GameEntry.UI.CloseUIForm(mainTitle);
            GameEntry.Event.Unsubscribe(MoveToNewGameEventArgs.EventId,OnEventStartNewGame);
            GameEntry.Event.Unsubscribe(MoveToContinueGameEventArgs.EventId,OnEventStartContinueGame);
        }

        private void InitNewGameData()
        {
            //一局关卡游戏初始化
            var mazeGen = new MazeGeneratorFromAreaPointTable();
            GameEntry.HeroManager.CurAreaList = mazeGen.InitMap();
            //创建所有点得相机
            GameEntry.HeroManager.InitAreaPointCamera();
            /*var fakeMazeGen = new MazeGenerator();
            GameEntry.HeroManager.CurMazeList = fakeMazeGen.GenerateMaze();*/
            GameEntry.HeroManager.AddNewFriendHero(1);
            
            GameEntry.HeroManager.AddNewFriendHero(4);
            GameEntry.HeroManager.TryAddCoin(200);
            GameEntry.HeroManager.ItemBagList.Clear();
        }

        private void InitContinueGameData()
        {
            var gameData = GameEntry.HeroManager.Load();
            if (gameData == null)
            {
                Log.Error("GameData error");
                InitNewGameData();
                return;
            }

            GameEntry.HeroManager.InitDataFormData(gameData);
        }
        private void OnEventStartNewGame(object sender, GameEventArgs e)
        {
            MoveToNewGameEventArgs ne = (MoveToNewGameEventArgs)e;
            if (ne == null)
            {
                return;
            }
            MoveToNewGame();
        }
        public void MoveToNewGame()
        {
            moveToNewGame = true;
        }
        private void OnEventStartContinueGame(object sender, GameEventArgs e)
        {
            MoveToContinueGameEventArgs ne = (MoveToContinueGameEventArgs)e;
            if (ne == null)
            {
                return;
            }
            MoveToContinueGame();
        }
        public void MoveToContinueGame()
        {
            moveToContinueGame = true;
        }
    }
}