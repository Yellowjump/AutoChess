using System;
using GameFramework.Fsm;
using Entity;
using Procedure;
using System.Collections;
using System.Collections.Generic;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

public class StateAttack0 : FsmState<EntityQizi>
{
    private int curNormalIndex = 0;
    private float durationAccumulate = 0f;
    private bool m_HasEnterShakeBefore = false;
    private bool m_HasAnimEndCheckToIdle = false;
    protected override void OnInit(IFsm<EntityQizi> fsm)
    {
        base.OnInit(fsm);
    }
    protected override void OnEnter(IFsm<EntityQizi> fsm)
    {
        base.OnEnter(fsm);
        if (fsm == null || fsm.Owner == null)
        {
            return;
        }
        var owner = fsm.Owner;
        curNormalIndex = owner.CurCastNormalSkillIndex;
    }
    protected override void OnUpdate(IFsm<EntityQizi> fsm, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        /*if (Time.time-timebegin>0.5f)
        {
            timebegin = Time.time + 0.5f;
            if (GameEntry.HeroManager.dangqianliucheng == 0 || qizitarget == null)
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
        var curSkill = owner.NormalSkillList[curNormalIndex];
        var cdr =  (int)owner.GetAttribute(AttributeType.CooldownReduce).GetFinalValue();
        var reducePercent = cdr / (cdr + 100f);
        var curCDMs = Mathf.CeilToInt(curSkill.DefaultSkillCDMs * (1 - reducePercent));
        var curAnimEndMs = curSkill.DefaultAnimationDurationMs;
        var curShakeBeforeMs = curSkill.ShakeBeforeMs;
        if (curCDMs < curAnimEndMs)//实际CD小于动画时间了，需要动画加速
        {
            curShakeBeforeMs = Mathf.CeilToInt(curShakeBeforeMs * curCDMs / (float)curAnimEndMs);
            curAnimEndMs = curCDMs;
        }
        if (m_HasEnterShakeBefore==false&& durationAccumulate * 1000 >= curShakeBeforeMs)
        {
            m_HasEnterShakeBefore = true;
            curSkill.OnSkillBeforeShakeEnd();
        }
        if (m_HasAnimEndCheckToIdle==false&& durationAccumulate * 1000 >= curAnimEndMs)
        {
            m_HasAnimEndCheckToIdle = true;
            owner.AddAnimCommandIdle();
        }
        
        if (durationAccumulate * 1000 >= curCDMs)
        {
            curSkill.OnDestory();
            //技能结束回到idle状态
            ChangeState<StateIdle0>(fsm);
        }
    }
    protected override void OnLeave(IFsm<EntityQizi> fsm, bool isShutdown)
    {
        base.OnLeave(fsm, isShutdown);
        durationAccumulate = 0;
        curNormalIndex = 0;
        m_HasEnterShakeBefore = false;
        m_HasAnimEndCheckToIdle = false;
    }
    protected override void OnDestroy(IFsm<EntityQizi> fsm)
    {
        base.OnDestroy(fsm);
    }
}
