using GameFramework.Fsm;
using Entity;
using Procedure;
using System.Collections;
using System.Collections.Generic;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;

public class StateAttack0 : FsmState<EntityQizi>
{
    EntityQizi qizi;//状态机挂载的棋子类
    int zhenying =0;//0表明是自己这边的棋子，1表明是地方阵营的
    EntityQizi qizitarget;//目标棋子
    float mindistance=10000;
    float timebegin;
    private bool curSpSkill = false;
    private float durationAccumulate = 0f;
    protected override void OnInit(IFsm<EntityQizi> fsm)
    {
        base.OnInit(fsm);
    }
    protected override void OnEnter(IFsm<EntityQizi> fsm)
    {
        base.OnEnter(fsm);
        
        //Log.Info("hfk 棋子进入attack");
        qizitarget = null;
        mindistance = 10000;
        //找到距离该棋子最近的棋子
        //Findtarget();
        timebegin = Time.time;
        CheckCastSkill(fsm);
    }
    protected override void OnUpdate(IFsm<EntityQizi> fsm, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        /*if (Time.time-timebegin>0.5f)
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
        }*/
        if (fsm == null || fsm.Owner == null)
        {
            return;
        }

        var owner = fsm.Owner;
        durationAccumulate += elapseSeconds;
        //update 技能时间
        var curSkill = curSpSkill ? owner.SpSkill : owner.NormalSkill;
        if (durationAccumulate * 1000 >= curSkill.ShakeBeforeMs && durationAccumulate * 1000 < curSkill.ShakeBeforeMs + elapseSeconds * 1000)
        {
            curSkill.OnSkillBeforeShakeEnd();
        }

        if (durationAccumulate * 1000 >= curSkill.DefaultAnimationDurationMs && durationAccumulate * 1000 < curSkill.DefaultAnimationDurationMs + elapseSeconds * 1000)
        {
            //技能结束回到idle状态
            ChangeState<StateIdle0>(fsm);
        }
    }

    private void CheckCastSkill(IFsm<EntityQizi> fsm)
    {
        if (fsm == null || fsm.Owner == null)
        {
            return;
        }

        var owner = fsm.Owner;
        var result = owner.CheckCanCastSkill(out var target, true);
        if (result == CheckCastSkillResult.CanCast)
        {
            owner.CastSkill(true);
            owner.animator.Play("ATTACK");
            owner.GObj.transform.LookAt(target.GObj.transform);
            curSpSkill = true;
            return;
        }

        result = owner.CheckCanCastSkill(out target, false);
        if (result == CheckCastSkillResult.CanCast)
        {
            owner.CastSkill(false);
            owner.animator.Play("ATTACK");
            owner.GObj.transform.LookAt(target.GObj.transform);
            curSpSkill = false;
            return;
        }
        ChangeState<StateIdle0>(fsm);
    }
    protected override void OnLeave(IFsm<EntityQizi> fsm, bool isShutdown)
    {
        base.OnLeave(fsm, isShutdown);
        durationAccumulate = 0;
        curSpSkill = false;
    }
    protected override void OnDestroy(IFsm<EntityQizi> fsm)
    {
        base.OnDestroy(fsm);
    }
}
