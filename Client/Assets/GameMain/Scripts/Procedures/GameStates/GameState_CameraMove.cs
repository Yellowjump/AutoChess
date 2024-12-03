using System;
using DataTable;
using Entity;
using GameFramework.Event;
using GameFramework.Fsm;
using Maze;
using SkillSystem;
using UnityGameFramework.Runtime;
using SelfEventArg;

namespace Procedure.GameStates
{
    /// <summary>
    /// 相机move
    /// </summary>
    public class GameState_CameraMove:FsmState<ProcedureGame>
    {
        private float cameraMoveDuration = 0;
        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            var curPoint = SelfDataManager.Instance.CurAreaPoint;
            if (curPoint == null)
            {
                Log.Error("No CurPoint");
                return;
            }
            GameEntry.HeroManager.CheckToAreaPointCamera(curPoint.Index);
            cameraMoveDuration = 0;
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            cameraMoveDuration += elapseSeconds;
            if (cameraMoveDuration >= 2)
            {
                var eventArg = MapFreshOpaqueEventArgs.Create(0);
                GameEntry.Event.Fire(this,eventArg);
                var curPoint = SelfDataManager.Instance.CurAreaPoint;
                if (curPoint.CurType is MazePointType.SmallBattle or MazePointType.EliteBattle or MazePointType.BossBattle)
                {
                    ChangeState<GameState_FormationBeforeBattle>(fsm);
                }
                else if (curPoint.CurType is MazePointType.Event or MazePointType.Chest or MazePointType.Store)
                {
                    ChangeState<GameState_Event>(fsm);
                }
            }
            else
            {
                var eventArg = MapFreshOpaqueEventArgs.Create((2 - cameraMoveDuration) / 2);
                GameEntry.Event.Fire(this,eventArg);
            }
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
        }

        protected override void OnDestroy(IFsm<ProcedureGame> fsm)
        {
            base.OnDestroy(fsm);
        }
    }
}