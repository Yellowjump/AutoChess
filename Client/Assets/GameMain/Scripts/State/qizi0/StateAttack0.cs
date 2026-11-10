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

public class StateAttack0 : FsmState<EntityQizi>,IReference
{
    internal const int StoneSmashSkillId = 9192;
    private int curNormalIndex = 0;
    private float durationAccumulate = 0f;
    private bool m_HasEnterShakeBefore = false;
    private bool m_HasAnimEndCheckToIdle = false;
    private TimelineUndertow undertow;
    private TimelineNamelessSteleCast steleCast;
    private int openingDelayMs;
    private LineRenderer stoneWarning;
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
        var skill = owner.NormalSkillList[curNormalIndex];
        undertow = TimelineUndertow.Begin(owner);
        steleCast = TimelineNamelessSteleCast.Begin(owner, skill);
        int windupMs = steleCast?.WindupMs ??
            (undertow != null ? TimelineUndertow.WindupMs : skill.CurShakeBeforeMs);
        if (skill.SkillID == StoneSmashSkillId)
        {
            openingDelayMs = owner.FirstStoneSmashDelayMs;
            owner.FirstStoneSmashDelayMs = 0;
            windupMs += openingDelayMs;
            if (owner.GObj != null && windupMs > 0)
                stoneWarning = HotGameEntry.HeroManager.BurnVisuals?.StoneWarning(owner.GObj.transform);
        }
        owner.PlayMonsterWindup(windupMs, steleCast?.IsLine == true);
    }
    protected override void OnUpdate(IFsm<EntityQizi> fsm, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        /*if (Time.time-timebegin>0.5f)
        {
            timebegin = Time.time + 0.5f;
            if (HotGameEntry.HeroManager.dangqianliucheng == 0 || qizitarget == null)
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
        var curCDMs = curSkill.CurCastCDMs + openingDelayMs;
        var curAnimEndMs = curSkill.CurAnimationDurationMs + openingDelayMs;
        var curShakeBeforeMs = curSkill.CurShakeBeforeMs + openingDelayMs;
        if (steleCast != null || undertow != null)
        {
            curShakeBeforeMs = steleCast?.WindupMs ?? TimelineUndertow.WindupMs;
            curAnimEndMs = Mathf.Max(curAnimEndMs, curShakeBeforeMs + 450);
            curCDMs = Mathf.Max(curCDMs, curAnimEndMs);
        }
        if (!m_HasEnterShakeBefore && curShakeBeforeMs > 0 &&
            (steleCast != null || undertow != null || curSkill.IsActiveDamageSkill || curSkill.SkillID == StoneSmashSkillId))
        {
            float progress = durationAccumulate * 1000f / curShakeBeforeMs;
            owner.ShowWindupProgress(progress < 1f ? progress : -1f);
            if (stoneWarning != null && progress < 1f)
                TimelineBurnVisuals.UpdateStoneWarning(stoneWarning, stoneWarning.transform.parent.position, progress);
        }
        if (m_HasEnterShakeBefore==false&& durationAccumulate * 1000 >= curShakeBeforeMs)
        {
            m_HasEnterShakeBefore = true;
            ClearStoneWarning();
            owner.PlayMonsterStrike(curAnimEndMs - curShakeBeforeMs, steleCast?.IsLine == true);
            if (steleCast != null) steleCast.Strike(owner, curSkill);
            else if (undertow != null) undertow.Strike(owner, curSkill);
            else curSkill.OnSkillBeforeShakeEnd();
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
        fsm.Owner?.ShowWindupProgress(-1f);
        undertow?.Clear();
        undertow = null;
        steleCast?.Clear();
        steleCast = null;
        ClearStoneWarning();
        openingDelayMs = 0;
        durationAccumulate = 0;
        curNormalIndex = 0;
        m_HasEnterShakeBefore = false;
        m_HasAnimEndCheckToIdle = false;
    }

    private void ClearStoneWarning()
    {
        if (stoneWarning == null) return;
        stoneWarning.enabled = false;
        UnityEngine.Object.Destroy(stoneWarning.gameObject);
        stoneWarning = null;
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
