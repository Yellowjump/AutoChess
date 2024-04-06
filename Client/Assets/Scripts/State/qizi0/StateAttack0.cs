using GameFramework.Fsm;
using liuchengguanli;
using Procedure;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

public class StateAttack0 : FsmState<Fsm_qizi0>
{
    EntityQizi qizi;//状态机挂载的棋子类
    protected override void OnInit(IFsm<Fsm_qizi0> fsm)
    {
        base.OnInit(fsm);
    }
    protected override void OnEnter(IFsm<Fsm_qizi0> fsm)
    {
        base.OnEnter(fsm);
        for (int i = 0; i < QiziGuanLi.Instance.QiziList.Count; i++)
        {
            if (QiziGuanLi.Instance.QiziList[i].GObj.transform.localPosition == fsm.Owner.transform.localPosition)
            {
                qizi = QiziGuanLi.Instance.QiziList[i];
                break;
            }
        }
        Log.Info("hfk:" + qizi.level);
    }
    protected override void OnUpdate(IFsm<Fsm_qizi0> fsm, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        if (fsm.Owner.transform.localPosition.z!=-4.5&& QiziGuanLi.Instance.dangqianliucheng == 0)
        {
            ChangeState<StateIdle0>(fsm);
        }
    }
    protected override void OnLeave(IFsm<Fsm_qizi0> fsm, bool isShutdown)
    {
        base.OnLeave(fsm, isShutdown);
    }
    protected override void OnDestroy(IFsm<Fsm_qizi0> fsm)
    {
        base.OnDestroy(fsm);
    }
}
