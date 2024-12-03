using System;
using DataTable;
using Entity;
using GameFramework.Event;
using GameFramework.Fsm;
using Maze;
using SkillSystem;
using UnityGameFramework.Runtime;
using SelfEventArg;
using UnityEngine;

namespace Procedure.GameStates
{
    /// <summary>
    /// 点击areaPoint点后| 相机move前| 加载
    /// </summary>
    public class GameState_BeforeCameraMove:FsmState<ProcedureGame>
    {
        private IFsm<ProcedureGame> m_fsm;
        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            m_fsm = fsm;
            var curPoint = SelfDataManager.Instance.CurAreaPoint;
            if (curPoint == null)
            {
                Log.Error("No CurPoint");
                return;
            }
            //如果是 battle，那就先放置 棋盘
            //如果是 商店，放置对应的 gameObject
            if (curPoint.CurType == MazePointType.Store)
            {
                GameEntry.HeroManager.GetNewObjFromPool("Assets/GameMain/Prefeb/LevelObj/outwild.prefab",OnGetLevelObjCallback);
                return;
            }
            else
            {
                GameEntry.HeroManager.MoveQigeRoot(SelfDataManager.Instance.CurAreaPoint.Pos);
                InitEnemy();
                GameEntry.HeroManager.FreshFriendEntityPos();
                GameEntry.HeroManager.InitFriendGobj();
                ChangeState<GameState_CameraMove>(fsm);
            }
        }

        private void OnGetLevelObjCallback(GameObject obj, string path)
        {
            obj.transform.position = SelfDataManager.Instance.CurAreaPoint.Pos;
            ChangeState<GameState_CameraMove>(m_fsm);
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
        }

        protected override void OnDestroy(IFsm<ProcedureGame> fsm)
        {
            base.OnDestroy(fsm);
        }
        private void InitEnemy()
        {
            var curPoint = SelfDataManager.Instance.CurAreaPoint;
            if (curPoint == null)
            {
                Log.Error("No CurPoint");
                return;
            }

            var levelConfigTable = GameEntry.DataTable.GetDataTable<DRLevelConfig>("LevelConfig");
            var enemyConfigs = GameEntry.DataTable.GetDataTable<DREnemyConfig>("EnemyConfig");
            if (levelConfigTable.HasDataRow(curPoint.CurLevelID))
            {
                var levelData = levelConfigTable[curPoint.CurLevelID];
                if (enemyConfigs.HasDataRow(levelData.LevelInfo))
                {
                    var enemyInfo = enemyConfigs[levelData.LevelInfo].EnemyInfo;
                    foreach (var oneInfo in enemyInfo.InfoList)
                    {
                        GameEntry.HeroManager.InitOneEnemy(oneInfo);
                    }
                }
            }
        }
    }
}