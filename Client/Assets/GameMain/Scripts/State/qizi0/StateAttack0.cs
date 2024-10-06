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
    private bool curSpSkill = false;
    private int curNormalIndex = 0;
    private float durationAccumulate = 0f;
    protected override void OnInit(IFsm<EntityQizi> fsm)
    {
        base.OnInit(fsm);
    }
    protected override void OnEnter(IFsm<EntityQizi> fsm)
    {
        base.OnEnter(fsm);
        CheckCastSkill(fsm);
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
        var curSkill = curSpSkill ? owner.SpSkill : owner.NormalSkillList[curNormalIndex];
        if (durationAccumulate * 1000 >= curSkill.ShakeBeforeMs && durationAccumulate * 1000 < curSkill.ShakeBeforeMs + elapseSeconds * 1000)
        {
            curSkill.OnSkillBeforeShakeEnd();
        }

        if (durationAccumulate * 1000 >= curSkill.DefaultAnimationDurationMs && durationAccumulate * 1000 < curSkill.DefaultAnimationDurationMs + elapseSeconds * 1000)
        {
            curSkill.OnDestory();
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
            if (target != null)
            {
                owner.GObj?.transform.LookAt(target.GObj.transform);
            }
            curSpSkill = true;
            return;
        }

        for (var normalSkillIndex = 0; normalSkillIndex < owner.NormalSkillList.Count; normalSkillIndex++)
        {
            result = owner.CheckCanCastSkill(out target, false,normalSkillIndex);
            if (result == CheckCastSkillResult.CanCast)
            {
                owner.CastSkill(false,normalSkillIndex);
                if (target != null)
                {
                    owner.GObj?.transform.LookAt(target.GObj.transform);
                }
                curSpSkill = false;
                curNormalIndex = normalSkillIndex;
                return;
            }
        }
        ChangeState<StateIdle0>(fsm);
    }
    protected override void OnLeave(IFsm<EntityQizi> fsm, bool isShutdown)
    {
        base.OnLeave(fsm, isShutdown);
        durationAccumulate = 0;
        curSpSkill = false;
        curNormalIndex = 0;
    }
    protected override void OnDestroy(IFsm<EntityQizi> fsm)
    {
        base.OnDestroy(fsm);
    }
}
