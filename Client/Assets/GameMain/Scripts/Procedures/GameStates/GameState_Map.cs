using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameFramework.Event;
using GameFramework.Fsm;
using Maze;
using SelfEventArg;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace Procedure.GameStates
{
    /// <summary>
    /// 地图选点
    /// </summary>
    public class GameState_Map:FsmState<ProcedureGame>
    {
        private int _mapUIIndex;
        private IFsm<ProcedureGame> _fsm;
        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            ReleaseLastEventPointGObj();
            _fsm = fsm;
            GameEntry.HeroManager.ResetToMainCamera();
            var eventArg = MapFreshOpaqueEventArgs.Create(1);
            GameEntry.Event.Fire(this,eventArg);
            GameEntry.Event.Subscribe(EnterPointEventArgs.EventId,EnterPoint);
            //打开titleUI
            _mapUIIndex = GameEntry.UI.OpenUIForm(UICtrlName.AreaPointList, "middle");
            var battleMainForm = GameEntry.UI.GetUIForm(UICtrlName.BattleMainPanel);
            if (battleMainForm != null)
            {
                GameEntry.UI.CloseUIForm(battleMainForm);
            }
        }

        private void ReleaseLastEventPointGObj()
        {
            //清除event对应的GameObj
            if (SelfDataManager.Instance.CurAreaPoint!=null&&SelfDataManager.Instance.CurAreaPoint.LevelGObj != null)
            {
                var levelID = SelfDataManager.Instance.CurAreaPoint.CurLevelID;
                var levelTable = GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
                if (levelTable.HasDataRow(levelID))
                {
                    var levelData = levelTable[levelID];
                    GameEntry.HeroManager.ReleaseAssetObj(levelData.ParamInt1,SelfDataManager.Instance.CurAreaPoint.LevelGObj,null);
                }
                
                SelfDataManager.Instance.CurAreaPoint.LevelGObj = null;
            }
        }
        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
            GameEntry.HeroManager.UpdateNoBattle(GameEntry.LogicDeltaTime,realElapseSeconds);
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
            GameEntry.UI.CloseUIForm(_mapUIIndex);
            GameEntry.Event.Unsubscribe(EnterPointEventArgs.EventId,EnterPoint);
        }
        public void EnterPoint(object sender,GameEventArgs e)
        {
            EnterPointEventArgs ne = (EnterPointEventArgs)e;
            if (ne == null)
            {
                return;
            }
            AreaPoint point = ne.TargetPoint;
            if (point != null && point.CurPassState == AreaPoint.PointPassState.Unlock)
            {
                SelfDataManager.Instance.CurAreaPoint = point;
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
                ChangeState<GameState_BeforeCameraMove>(_fsm);
            }
        }
    }
}