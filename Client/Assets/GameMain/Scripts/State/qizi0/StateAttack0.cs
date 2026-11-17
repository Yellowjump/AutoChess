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
    private TimelineRegionalGuardianCast regionalCast;
    private TimelineOuterGuardianCast outerCast;
    private TimelineSaltChannelCast saltCast;
    private int openingDelayMs;
    private const int PounceStrikeWindupMs = 120;
    private bool pounceStrike;
    private bool useAlternatePose;
    private const float StoneImpactRadius = .80f;
    private bool stoneImpactPrepared;
    private Vector3 stoneImpactCenter;
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
        pounceStrike = owner.IsPounceStrike;
        owner.IsPounceStrike = false;
        regionalCast = TimelineRegionalGuardianCast.Begin(owner, skill, pounceStrike);
        outerCast = TimelineOuterGuardianCast.Begin(owner, skill);
        saltCast = TimelineSaltChannelCast.Begin(owner, skill);
        openingDelayMs = owner.SaltFirstAttackDelayMs;
        owner.SaltFirstAttackDelayMs = 0;
        int windupMs = saltCast?.WindupMs ?? outerCast?.WindupMs ?? regionalCast?.WindupMs ?? steleCast?.WindupMs ??
            (undertow != null ? TimelineUndertow.WindupMs : pounceStrike ? PounceStrikeWindupMs : skill.CurShakeBeforeMs);
        if (skill.SkillID == StoneSmashSkillId)
        {
            openingDelayMs = owner.FirstStoneSmashDelayMs;
            owner.FirstStoneSmashDelayMs = 0;
            if (owner.CurAttackTarget is { IsValid: true } target)
            {
                stoneImpactPrepared = true;
                stoneImpactCenter = target.LogicPosition;
                if (owner.GObj != null)
                    stoneWarning = HotGameEntry.HeroManager.BurnVisuals?.StoneWarning(owner.GObj.transform,
                        stoneImpactCenter, StoneImpactRadius);
            }
        }
        useAlternatePose = steleCast?.IsLine == true || saltCast?.UseHeavyPose == true || outerCast?.UseSignaturePose == true ||
            regionalCast?.UseLavaPose == true ||
            (owner.TimelineAppearance == TimelineOuterGuardianCast.ClerkAppearance &&
                skill.SkillID == TimelineOuterGuardianCast.ClerkBasicAttack);
        owner.PlayMonsterWindup(windupMs + openingDelayMs, useAlternatePose);
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
        if (pounceStrike) curShakeBeforeMs = PounceStrikeWindupMs;
        if (saltCast != null || outerCast != null || regionalCast != null || steleCast != null || undertow != null)
        {
            curShakeBeforeMs = (saltCast?.WindupMs ?? outerCast?.WindupMs ?? regionalCast?.WindupMs ??
                steleCast?.WindupMs ?? TimelineUndertow.WindupMs) + (owner.IsCoastalShooter ? openingDelayMs : 0);
            curAnimEndMs = Mathf.Max(curAnimEndMs, curShakeBeforeMs + (outerCast?.RecoveryMs ?? steleCast?.RecoveryMs ?? 450));
            curCDMs = Mathf.Max(curCDMs, curAnimEndMs);
        }
        if (!m_HasEnterShakeBefore && curShakeBeforeMs > 0 &&
            (saltCast != null || outerCast != null || regionalCast != null || steleCast != null || undertow != null || curSkill.IsActiveDamageSkill || curSkill.SkillID == StoneSmashSkillId))
        {
            float progress = durationAccumulate * 1000f / curShakeBeforeMs;
            owner.ShowWindupProgress(progress < 1f ? progress : -1f);
            if (progress < 1f)
            {
                saltCast?.UpdateWindup(progress);
                steleCast?.UpdateWindup(progress);
                outerCast?.UpdateWindup(progress);
                regionalCast?.UpdateWindup(progress);
            }
            if (stoneWarning != null && progress < 1f)
                TimelineBurnVisuals.UpdateStoneWarning(stoneWarning, stoneImpactCenter, StoneImpactRadius, progress);
        }
        if (m_HasEnterShakeBefore) outerCast?.Advance(elapseSeconds);
        if (m_HasEnterShakeBefore==false&& durationAccumulate * 1000 >= curShakeBeforeMs)
        {
            m_HasEnterShakeBefore = true;
            owner.PlayMonsterStrike(curAnimEndMs - curShakeBeforeMs, useAlternatePose);
            if (saltCast != null) saltCast.Strike();
            else if (outerCast != null) outerCast.Strike();
            else if (regionalCast != null) regionalCast.Strike(owner, curSkill);
            else if (steleCast != null) steleCast.Strike(owner, curSkill);
            else if (undertow != null) undertow.Release(owner);
            else if (curSkill.SkillID == StoneSmashSkillId) StrikeStone(owner, curSkill);
            else if (curSkill.SkillID is >= TimelineCaveHazards.Marten and <= TimelineCaveHazards.Macaque)
            {
                if (owner.CurAttackTarget is { IsValid: true } caveTarget)
                {
                    var battle = HotGameEntry.HeroManager;
                    if (battle.IsInSkillRange(battle.GetCombatDistanceSquare(owner, caveTarget), curSkill.SkillRange))
                        curSkill.OnSkillBeforeShakeEnd();
                    battle.CaveHazards?.Cast(owner, caveTarget, curSkill);
                }
            }
            else curSkill.OnSkillBeforeShakeEnd();
        }
        if (m_HasEnterShakeBefore)
            undertow?.Advance(owner, curSkill, durationAccumulate - curShakeBeforeMs * .001f);
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
        regionalCast?.Clear();
        regionalCast = null;
        outerCast?.Clear();
        outerCast = null;
        saltCast?.Clear();
        saltCast = null;
        TimelineRegionalGuardianCast.ClearWarning(ref stoneWarning);
        stoneImpactPrepared = false;
        openingDelayMs = 0;
        pounceStrike = false;
        durationAccumulate = 0;
        curNormalIndex = 0;
        m_HasEnterShakeBefore = false;
        m_HasAnimEndCheckToIdle = false;
    }

    private void StrikeStone(EntityQizi owner, Skill skill)
    {
        TimelineRegionalGuardianCast.ClearWarning(ref stoneWarning);
        if (!stoneImpactPrepared) return;

        var battle = HotGameEntry.HeroManager;
        if (owner.GObj != null) battle.BurnVisuals?.StoneImpact(stoneImpactCenter, StoneImpactRadius);
        var targets = ListPool<EntityBase>.Get();
        try
        {
            foreach (var enemy in battle.GetEnemyList(owner.BelongCamp))
            {
                if (!enemy.IsValid) continue;
                foreach (var point in battle.GetRangePoints(enemy))
                {
                    var offset = point - stoneImpactCenter;
                    offset.y = 0f;
                    if (offset.sqrMagnitude > StoneImpactRadius * StoneImpactRadius) continue;
                    targets.Add(enemy);
                    break;
                }
            }
            // Resolve the original skill template with the units inside the impact circle.
            if (targets.Count > 0) skill.OnSkillBeforeShakeEnd(targets);
        }
        finally { ListPool<EntityBase>.Release(targets); }
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
