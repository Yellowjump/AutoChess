using System;
using GameFramework.Fsm;
using Entity;
using Procedure;
using System.Collections;
using System.Collections.Generic;
using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

public class StateAttack0 : FsmState<EntityQizi>,IReference
{
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
        CombatAttackCastBridge.Begin(owner.CombatUnitHandle, owner.CurCastNormalSkillIndex);
    }
    protected override void OnUpdate(IFsm<EntityQizi> fsm, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        /*if (Time.time-timebegin>0.5f)
        {
            timebegin = Time.time + 0.5f;
            if (!HotGameEntry.HeroManager.InBattle || qizitarget == null)
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
        //update 技能时间
        var curSkill = owner.NormalSkillList[CombatAttackCastBridge.GetNormalSkillIndex(owner.CombatUnitHandle)];
        var curCDMs = curSkill.CurCastCDMs;
        var curAnimEndMs = curSkill.CurAnimationDurationMs;
        var curShakeBeforeMs = curSkill.CurShakeBeforeMs;
        if (CombatAttackCastBridge.TryConsumeShake(owner.CombatUnitHandle, curShakeBeforeMs))
        {
            curSkill.OnSkillBeforeShakeEnd();
        }
        if (CombatAttackCastBridge.TryConsumeAnimationEnd(owner.CombatUnitHandle, curAnimEndMs))
        {
            owner.AddAnimCommandIdle();
        }
        
        if (CombatAttackCastBridge.IsComplete(owner.CombatUnitHandle, curCDMs))
        {
            curSkill.OnDestory();
            //技能结束回到idle状态
            ChangeState<StateIdle0>(fsm);
        }
    }
    protected override void OnLeave(IFsm<EntityQizi> fsm, bool isShutdown)
    {
        base.OnLeave(fsm, isShutdown);
        if (fsm != null && fsm.Owner != null)
        {
            CombatAttackCastBridge.End(fsm.Owner.CombatUnitHandle);
        }
    }
    protected override void OnDestroy(IFsm<EntityQizi> fsm)
    {
        base.OnDestroy(fsm);
        ReferencePool.Release(this);
    }

    public void Clear()
    {
        
    }
}
