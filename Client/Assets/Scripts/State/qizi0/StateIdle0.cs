using GameFramework.Fsm;
using liuchengguanli;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

public class StateIdle0 : FsmState<Fsm_qizi0>
{
    EntityQizi qizi;//状态机挂载的棋子类
    EntityQizi targetqizi;//目标棋子
    float mindistance = 10000;
    int zhenying;//0为自己阵营，1为敌方
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
        qizi.animator.Play("WAIT00");
        targetqizi = null;
        mindistance = 10000;
        timebegin = Time.time;
    }
    private void Findtarget()
    {
        targetqizi = null;
        mindistance = 10000;
        if (zhenying == 0)
        {
            foreach (EntityQizi qz in QiziGuanLi.Instance.DirenList)
            {
                float distence = (qizi.GObj.transform.position.x - qz.GObj.transform.position.x) * (qizi.GObj.transform.position.x - qz.GObj.transform.position.x) + (qizi.GObj.transform.position.z - qz.GObj.transform.position.z) * (qizi.GObj.transform.position.z - qz.GObj.transform.position.z);
                if (distence < mindistance)
                {
                    targetqizi = qz;
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
                    targetqizi = qz;
                    mindistance = distence;
                }
            }
        }
    }
    protected override void OnUpdate(IFsm<Fsm_qizi0> fsm, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        if (Time.time - timebegin > 0.5f&&qizi.y != -4.5 && QiziGuanLi.Instance.dangqianliucheng == 1)//0.5s每次
        {
            timebegin = Time.time+0.5f;
            Findtarget();
            if (targetqizi!=null)
            {
                if (qizi.gongjiDistence* qizi.gongjiDistence<mindistance)
                {
                    //距离不够，开始寻路
                    ChangeState<StateMove0>(fsm);
                }
                else //距离够，切换到攻击状态
                {
                    ChangeState<StateAttack0>(fsm);
                }
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
