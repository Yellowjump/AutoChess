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
    /// <summary>
    /// 战前编队
    /// </summary>
    public class GameState_FormationBeforeBattle:FsmState<ProcedureGame>
    {
        private bool _willChangeToBattle = false;
        private float m_PrewarmWaitSeconds;
        private int _UIIndex;
        protected override void OnInit(IFsm<ProcedureGame> fsm)
        {
            base.OnInit(fsm);
            GameEntry.Event.Subscribe(FormationToBattleEventArgs.EventId,OnFormationToBattle);
        }

        protected override void OnEnter(IFsm<ProcedureGame> fsm)
        {
            base.OnEnter(fsm);
            _willChangeToBattle = false;
            m_PrewarmWaitSeconds = 0f;
            HotGameEntry.HeroManager.PrepareBattlePoolPrewarm();
            //初始化 敌人
            GameEntry.UI.OpenUIForm(UICtrlName.BattleMainPanel, "middle");
            HotGameEntry.Sound.PlayMusic((int)EnumSound.GameBattleBGM);
        }

        protected override void OnUpdate(IFsm<ProcedureGame> fsm, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
            HotGameEntry.HeroManager.AdvanceBattlePoolPrewarm();
            if (_willChangeToBattle)
            {
                m_PrewarmWaitSeconds += realElapseSeconds;
                bool prewarmReady = HotGameEntry.HeroManager.IsBattlePoolPrewarmReady();
                if (prewarmReady || m_PrewarmWaitSeconds >= 2f)
                {
                    if (!prewarmReady)
                    {
                        Log.Warning("Battle pool prewarm timed out; continuing battle.");
                    }
                    _willChangeToBattle = false;
                    HotGameEntry.HeroManager.FinishBattlePoolPrewarm();
                    ChangeState<GameState_Battle>(fsm);
                    return;
                }
            }
            HotGameEntry.HeroManager.UpdateNoBattle(GameEntry.LogicDeltaTime,realElapseSeconds);
        }

        protected override void OnLeave(IFsm<ProcedureGame> fsm, bool isShutdown)
        {
            base.OnLeave(fsm, isShutdown);
            HotGameEntry.HeroManager.FinishBattlePoolPrewarm();
            GameEntry.UI.CloseUIForm(UICtrlName.BattleFormationPanel);
        }

        protected override void OnDestroy(IFsm<ProcedureGame> fsm)
        {
            base.OnDestroy(fsm);
            GameEntry.Event.Unsubscribe(FormationToBattleEventArgs.EventId,OnFormationToBattle);
        }
        private void OnFormationToBattle(object sender, GameEventArgs e)
        {
            FormationToBattleEventArgs ne = (FormationToBattleEventArgs)e;
            if (ne == null)
            {
                return;
            }

            _willChangeToBattle = true;
        }
    }
}
