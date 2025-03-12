using System.Collections.Generic;
using GameFramework;
using GameFramework.Fsm;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace Entity
{
    public partial class EntityQizi
    {
        private Fsm<EntityQizi> fsm;
        private readonly List<FsmBase> m_TempFsms;
        private void InitState()
        {
            List<FsmState<EntityQizi>> stateList = ListPool<FsmState<EntityQizi>>.Get();
            stateList.Add(ReferencePool.Acquire<StateIdle0>());
            stateList.Add(ReferencePool.Acquire<StateMove0>());
            stateList.Add(ReferencePool.Acquire<StateAttack0>());
            stateList.Add(ReferencePool.Acquire<StateUnderControl0>());
            stateList.Add(ReferencePool.Acquire<StateBattleWin>());
            stateList.Add(ReferencePool.Acquire<StateBattleLose>());
            fsm = Fsm<EntityQizi>.Create((HeroUID).ToString(),this, stateList);
            ListPool<FsmState<EntityQizi>>.Release(stateList);
            fsm.Start<StateIdle0>();
        }

        private void UpdateState(float elapseSeconds, float realElapseSeconds)
        {
            fsm?.UpdatePublic(elapseSeconds,realElapseSeconds);
        }

        private void ChangeToIdleState()
        {
            fsm?.ChangeStatePublic<StateIdle0>();
        }
        private void ChangeToWinState()
        {
            fsm?.ChangeStatePublic<StateBattleWin>();
        }
        private void ChangeToLoseState()
        {
            fsm?.ChangeStatePublic<StateBattleLose>();
        }
        private void DestoryState()
        {
            fsm?.ShutdownPublic();
        }
    }
}