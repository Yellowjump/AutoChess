using System;
using DataTable;
using Entity;
using GameFramework.Event;
using GameFramework.Fsm;
using SkillSystem;
using UnityGameFramework.Runtime;
using SelfEventArg;

namespace Procedure.GameStates
{
    public enum EventType
    {
        TestBattleOrLoseCoin = 1,
    }
    /// <summary>
    /// 战前编队
    /// </summary>
    public class GameState_Event:FsmState<ProcedureGame>
    {
        private int _UIIndex;
        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            var curPoint = SelfDataManager.Instance.CurMazePoint;
            if (curPoint == null)
            {
                Log.Error("No CurPoint");
                return;
            }
            // type 是 event 或者 store 或者 chest都读取 levelInfo
            var levelConfigTable = GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
            var assetsPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (levelConfigTable.HasDataRow(curPoint.CurLevelID))
            {
                var levelData = levelConfigTable[curPoint.CurLevelID];
                if (assetsPathTable.HasDataRow(levelData.LevelInfo))
                {
                    var eventUIPath = assetsPathTable[levelData.LevelInfo].AssetPath;
                    _UIIndex = GameEntry.UI.OpenUIForm(eventUIPath, "middle");
                }
            }
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
            GameEntry.UI.CloseUIForm(_UIIndex);
        }

        protected override void OnDestroy(IFsm<ProcedureGame> fsm)
        {
            base.OnDestroy(fsm);
        }
    }
}