using GameFramework.Fsm;
using System.Collections;
using System.Collections.Generic;
using Entity;
using UnityEngine;

public class StateUnderControl0 : FsmState<EntityQizi>
{
    protected override void OnInit(IFsm<EntityQizi> fsm)
    {
        base.OnInit(fsm);
    }
    protected override void OnEnter(IFsm<EntityQizi> fsm)
    {
        base.OnEnter(fsm);
    }
    protected override void OnUpdate(IFsm<EntityQizi> fsm, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
    }
    protected override void OnLeave(IFsm<EntityQizi> fsm, bool isShutdown)
    {
        base.OnLeave(fsm, isShutdown);
    }
    protected override void OnDestroy(IFsm<EntityQizi> fsm)
    {
        base.OnDestroy(fsm);
    }
}
