using GameFramework.Fsm;
using liuchengguanli;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Analytics;
using UnityGameFramework.Runtime;
using static UnityEngine.GraphicsBuffer;

public class StateMove0 : FsmState<Fsm_qizi0>
{
    EntityQizi qizi;//状态机挂载的棋子类
    int zhenying = 0;//0表明是自己这边的棋子，1表明是敌方阵营的
    EntityQizi qizitarget;//目标棋子
    float mindistance = 10000;
    float timebegin;//记录进入状态的时间
    float timetemp;//记录进入update的时间
    bool findpath = false;
    Vector2Int nextPosIndex;
    Vector3 startpos;
    Vector3 nextpos;
    protected override void OnInit(IFsm<Fsm_qizi0> fsm)
    {
        base.OnInit(fsm);
    }
    protected override void OnEnter(IFsm<Fsm_qizi0> fsm)
    {
        base.OnEnter(fsm);
        if (qizi==null)//第一次进入获取自身棋子类
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
        }
        startpos = qizi.GObj.transform.position;
        //Log.Info("hfk:" + qizi.level);
        //找到距离该棋子最近的棋子
        qizitarget = null;
        mindistance = 10000;
        findpath = false;
        timebegin = Time.time;
        timetemp = Time.time;
        nextPosIndex = new Vector2Int(-1, -1);
    }
    private void Findtarget()
    {
        qizitarget = null;
        mindistance = 10000;
        if (zhenying == 0)
        {
            foreach (EntityQizi qz in QiziGuanLi.Instance.DirenList)
            {
                float distance = (qizi.GObj.transform.position.x - qz.GObj.transform.position.x) * (qizi.GObj.transform.position.x - qz.GObj.transform.position.x) + (qizi.GObj.transform.position.z - qz.GObj.transform.position.z) * (qizi.GObj.transform.position.z - qz.GObj.transform.position.z);
                if (distance < mindistance)
                {
                    qizitarget = qz;
                    mindistance = distance;
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
        if (Time.time- timetemp > 0.5f)//0.5s每次
        {
            timetemp = Time.time+0.5f;
            Findtarget();
            if (QiziGuanLi.Instance.dangqianliucheng == 0 || qizitarget == null)
            {
                ChangeState<StateIdle0>(fsm);
            }
            if (qizi.gongjiDistence * qizi.gongjiDistence <= mindistance)
            {
                //寻路
                Vector2Int start = QiziGuanLi.Instance.getIndexQige(qizi.GObj.transform.position);
                Vector2Int end = QiziGuanLi.Instance.getIndexQige(qizitarget.GObj.transform.position);
                nextPosIndex=QiziGuanLi.Instance.Findpath(start, end,qizi.gongjiDistence);
                if (nextPosIndex.x != -1)
                {
                    qizi.animator.Play("RUN00_F");
                    nextpos = QiziGuanLi.Instance.qigepos[nextPosIndex.x][nextPosIndex.y];
                    QiziGuanLi.Instance.qige[nextPosIndex.x][nextPosIndex.y] = 1;
                    QiziGuanLi.Instance.qige[start.x][start.y] = 0;
                    findpath = true;
                    timebegin = Time.time;
                }
                else
                {
                    findpath = false;
                    qizi.animator.Play("WAIT00");
                    //ChangeState<StateIdle0>(fsm);
                }
            }
            else
            {
                ChangeState<StateAttack0>(fsm);
            }
        }
        if (findpath)
        {
            qizi.GObj.transform.position = new Vector3(Mathf.Lerp(startpos.x, nextpos.x, (Time.time - timebegin)/0.5f), 0, Mathf.Lerp(startpos.z, nextpos.z, (Time.time - timebegin) / 0.5f));
            if (Time.time - timebegin > 0.5f)
            {
                findpath = false;
                startpos = nextpos;
                //ChangeState<StateIdle0>(fsm);
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
