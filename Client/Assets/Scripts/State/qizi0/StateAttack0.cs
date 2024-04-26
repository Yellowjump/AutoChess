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
    int zhenying =0;//0表明是自己这边的棋子，1表明是地方阵营的
    EntityQizi qizitarget;//目标棋子
    float mindistance=10000;
    float timebegin;
    protected override void OnInit(IFsm<Fsm_qizi0> fsm)
    {
        base.OnInit(fsm);
    }
    protected override void OnEnter(IFsm<Fsm_qizi0> fsm)
    {
        base.OnEnter(fsm);
        
        if (qizi == null)
        {
            foreach (EntityQizi qz in QiziGuanLi.Instance.QiziList)
            {
                if (qz.GObj.transform.localPosition == fsm.Owner.transform.localPosition)
                {
                    qizi = qz;
                    zhenying = 0;
                    break;
                }
            }
            foreach (EntityQizi qz in QiziGuanLi.Instance.DirenList)
            {
                if (qz.GObj.transform.localPosition == fsm.Owner.transform.localPosition)
                {
                    qizi = qz;
                    zhenying = 1;
                    break;
                }
            }
            //Log.Info("hfk:" + qizi.level);
        }
        qizi.animator.Play("ATTACK");
        //Log.Info("hfk 棋子进入attack");
        qizitarget = null;
        mindistance = 10000;
        //找到距离该棋子最近的棋子
        Findtarget();
        timebegin = Time.time;
    }
    private void Findtarget()
    {
        if (zhenying == 0)
        {
            foreach (EntityQizi qz in QiziGuanLi.Instance.DirenList)
            {
                float distence = (qizi.GObj.transform.position.x - qz.GObj.transform.position.x) * (qizi.GObj.transform.position.x - qz.GObj.transform.position.x) + (qizi.GObj.transform.position.z - qz.GObj.transform.position.z) * (qizi.GObj.transform.position.z - qz.GObj.transform.position.z);
                if (distence < mindistance)
                {
                    qizitarget = qz;
                    mindistance = distence;
                }
            }
        }
        else
        {
            foreach (EntityQizi qz in QiziGuanLi.Instance.QiziCSList)
            {
                float distence = (qizi.GObj.transform.position.x - qz.GObj.transform.position.x) * (qizi.GObj.transform.position.x - qz.GObj.transform.position.x) + (qizi.GObj.transform.position.z - qz.GObj.transform.position.z) * (qizi.GObj.transform.position.z - qz.GObj.transform.position.z);
                if (distence < mindistance)
                {
                    qizitarget = qz;
                    mindistance = distence;
                }
            }
        }
    }
    protected override void OnUpdate(IFsm<Fsm_qizi0> fsm, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        if (Time.time-timebegin>0.5f)
        {
            timebegin = Time.time + 0.5f;
            if (QiziGuanLi.Instance.dangqianliucheng == 0 || qizitarget == null)
            {
                ChangeState<StateIdle0>(fsm);
            }
            if (qizi.gongjiDistence * qizi.gongjiDistence >= mindistance)
            {
                qizi.GObj.transform.LookAt(qizitarget.GObj.transform.position);
                //攻击
            }
            else
            {
                ChangeState<StateMove0>(fsm);
            }
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
