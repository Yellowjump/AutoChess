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
            HotGameEntry.HeroManager.Save();
            ReleaseLastEventPointGObj();
            _fsm = fsm;
            HotGameEntry.HeroManager.ResetToMainCamera();
            HotGameEntry.HeroManager.ShowQige(false);
            var eventArg = MapFreshOpaqueEventArgs.Create(1);
            GameEntry.Event.Fire(this,eventArg);
            GameEntry.Event.Subscribe(EnterPointEventArgs.EventId,EnterPoint);
            //打开titleUI
            _mapUIIndex = GameEntry.UI.OpenUIForm(UICtrlName.AreaPointList, "middle");
            GameEntry.UI.CloseUIForm(UICtrlName.BattleMainPanel);
        }

        private void ReleaseLastEventPointGObj()
        {
            //清除event对应的GameObj
            if (HotGameEntry.HeroManager.CurAreaPoint!=null&&HotGameEntry.HeroManager.CurAreaPoint.LevelGObj != null)
            {
                var levelID = HotGameEntry.HeroManager.CurAreaPoint.CurLevelID;
                var levelTable = GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
                if (levelTable.HasDataRow(levelID))
                {
                    var levelData = levelTable[levelID];
                    HotGameEntry.HeroManager.ReleaseAssetObj(levelData.ParamInt1,HotGameEntry.HeroManager.CurAreaPoint.LevelGObj,null);
                }
                
                HotGameEntry.HeroManager.CurAreaPoint.LevelGObj = null;
            }
        }
        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
            //HotGameEntry.HeroManager.UpdateNoBattle(GameEntry.LogicDeltaTime,realElapseSeconds);
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
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
                HotGameEntry.HeroManager.CurAreaPoint = point;
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
                    point.CurLevelID = HotGameEntry.HeroManager.GetOneRandomLevelIDFormType(point.CurType);
                }
                ChangeState<GameState_BeforeCameraMove>(_fsm);
            }
        }
    }
}