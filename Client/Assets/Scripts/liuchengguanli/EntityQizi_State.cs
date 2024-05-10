using System.Collections.Generic;
using GameFramework.Fsm;
using UnityGameFramework.Runtime;

namespace liuchengguanli
{
    public partial class EntityQizi
    {
        private Fsm<EntityQizi> fsm;
        private readonly List<FsmBase> m_TempFsms;
        private void InitState()
        {
            List<FsmState<EntityQizi>> stateList = new List<FsmState<EntityQizi>>() { new StateIdle0(), new StateMove0(), new StateAttack0(), new StateUnderControl0() };
            fsm = Fsm<EntityQizi>.Create((HeroUID).ToString(),this, stateList);
            fsm.Start<StateIdle0>();
        }

        private void UpdateState(float elapseSeconds, float realElapseSeconds)
        {
            fsm?.UpdatePublic(elapseSeconds,realElapseSeconds);
        }
        private void DestoryState()
        {
            fsm?.ShutdownPublic();
        }
    }
}