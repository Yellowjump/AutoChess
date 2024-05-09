using System.Collections.Generic;
using GameFramework.Fsm;
using UnityGameFramework.Runtime;

namespace liuchengguanli
{
    public partial class EntityQizi
    {
        private IFsm<EntityQizi> fsm;
        private void InitState()
        {
            List<FsmState<EntityQizi>> stateList = new List<FsmState<EntityQizi>>() { new StateIdle0(), new StateMove0(), new StateAttack0(), new StateUnderControl0() };
            fsm = GameEntry.Fsm.CreateFsm<EntityQizi>((HeroUID).ToString(),this, stateList);
            fsm.Start<StateIdle0>();
        }

        private void DestoryState()
        {
            GameEntry.Fsm.DestroyFsm(fsm);
        }
    }
}