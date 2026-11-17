using GameFramework.Fsm;
using Entity;
using System.Collections.Generic;
using System.Linq;
using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

public class StateMove0 : FsmState<EntityQizi>,IReference
{
    Vector2Int nextPosIndex;
    Vector3 startpos;
    Vector3 nextpos;
    private const float MoveOneCellDuration = 0.5f;
    private const int PounceWindupMs = 250;
    private const float PounceCellDuration = 0.04f;
    private float pounceWindupRemaining;
    private int pounceWindupMs;
    private LineRenderer pounceWarning;
    private bool pouncing;
    private float _moveAccumulate = 0f;
    private bool _moving = false;
    protected override void OnEnter(IFsm<EntityQizi> fsm)
    {
        base.OnEnter(fsm);
        if (fsm == null || fsm.Owner == null)
        {
            return;
        }

        var owner = fsm.Owner;
        _moving = false;
        _moveAccumulate = 0f;
        pouncing = false;
        pounceWindupRemaining = 0f;
        pounceWindupMs = PounceWindupMs;
        if (owner.CanOpenWithPounce)
        {
            owner.CanOpenWithPounce = false;
            var target = owner.CurAttackTarget;
            bool regionalBadger = owner.TimelineAppearance == TimelineRegionalGuardianCast.BadgerAppearance;
            pouncing = target != null && target.IsValid &&
                (regionalBadger || target.NormalSkillList.All(skill => skill.SkillRange > 1000)) &&
                HotGameEntry.HeroManager.GetCombatDistanceSquare(owner, target) > 9f;
            if (pouncing)
            {
                if (regionalBadger)
                {
                    pounceWindupMs = TimelineRegionalGuardianCast.PounceWindupMs;
                    pounceWarning = TimelineRegionalGuardianCast.WarnPounce(owner, target);
                }
                pounceWindupRemaining = pounceWindupMs / 1000f;
                owner.PlayMonsterWindup(pounceWindupMs, false);
                owner.ShowWindupProgress(0f);
            }
        }
        startpos = owner.LogicPosition;
        nextPosIndex = new Vector2Int(-1, -1);
    }
    protected override void OnUpdate(IFsm<EntityQizi> fsm, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        var owner = fsm.Owner;
        if (owner == null)
        {
            Log.Error("Owner is null");
            return;
        }
        if (pounceWindupRemaining > 0f)
        {
            pounceWindupRemaining -= elapseSeconds;
            owner.ShowWindupProgress(pounceWindupRemaining > 0f
                ? 1f - pounceWindupRemaining * 1000f / pounceWindupMs : -1f);
            if (pounceWindupRemaining > 0f) return;
            TimelineRegionalGuardianCast.ClearWarning(ref pounceWarning);
        }
        float stepDuration = pouncing ? PounceCellDuration : MoveOneCellDuration;
        //判断是否在移动中
        if (_moving)
        {
            _moveAccumulate += elapseSeconds * (owner.FrostSlowSeconds > 0f && !pouncing ? .65f : 1f);
            if (_moveAccumulate < stepDuration)
            {
                owner.LogicPosition = Vector3.Lerp(startpos, nextpos, _moveAccumulate / stepDuration);
                /*owner.LogicPosition = new Vector3(Mathf.Lerp(startpos.x, nextpos.x, _moveAccumulate/MoveOneCellDuration), 0, Mathf.Lerp(startpos.z, nextpos.z, _moveAccumulate/MoveOneCellDuration));*/
                return;
            }
            else
            {
                _moveAccumulate = 0;
                owner.LogicPosition = nextpos;
                HotGameEntry.HeroManager.CompleteStep(owner);
                //已到达目标点
                _moving = false;
            }
        }
        //判断是否可以changeState或者确定下一移动目标点
        CheckChangeStateOrMovePos(fsm);
    }

    private void CheckChangeStateOrMovePos(IFsm<EntityQizi> fsm)
    {
        if (fsm == null||fsm.Owner==null)
        {
            Log.Error("Fsm is null or Owner is null");
            return;
        }
        var owner = fsm.Owner;
        var battle = HotGameEntry.HeroManager;
        var result = CheckCastSkillResult.Error;
        EntityQizi target = null;
        List<int> checkNormalSkillList = owner.GetNormalSkillCheckSort();
        var noNextPosMove = true;
        for (var index = 0; index < checkNormalSkillList.Count; index++)
        {
            result = owner.CheckCanCastSkill(out target,checkNormalSkillList[index]);
            if (result == CheckCastSkillResult.CanCast)
            {
                owner.CurAttackTarget = target;
                owner.IsPounceStrike = pouncing;
                owner.CastNormalSkill(checkNormalSkillList[index]);
                if (target != null)
                {
                    owner.GObj?.transform.LookAt(target.GObj.transform);
                }
                ListPool<int>.Release(checkNormalSkillList);
                ChangeState<StateAttack0>(fsm);
                return;
            }
            else if (result == CheckCastSkillResult.TargetOutRange)
            {
                owner.CurAttackTarget = target;
                Vector2Int ownerIndex = HotGameEntry.HeroManager.GetIndexQizi(owner);
                nextPosIndex = HotGameEntry.HeroManager.Findpath(owner, target, owner.NormalSkillList[checkNormalSkillList[index]].SkillRange);
                if (nextPosIndex.x != -1 && nextPosIndex.y != -1 && nextPosIndex != ownerIndex &&
                    HotGameEntry.HeroManager.TryBeginStep(owner, nextPosIndex))
                {
                    noNextPosMove = false;
                    _moving = true;
                    startpos = owner.LogicPosition;
                    nextpos = HotGameEntry.HeroManager.GetGeziPos(nextPosIndex.y,nextPosIndex.x);
                    owner.AddAnimCommandRun();
                    owner.GObj?.transform.LookAt(nextpos);
                    break;
                }
            }
        }
        ListPool<int>.Release(checkNormalSkillList);
        if (noNextPosMove)
        {
            ChangeState<StateIdle0>(fsm);
        }

        
    }
    protected override void OnLeave(IFsm<EntityQizi> fsm, bool isShutdown)
    {
        if (pouncing) fsm.Owner?.ShowWindupProgress(-1f);
        TimelineRegionalGuardianCast.ClearWarning(ref pounceWarning);
        if (_moving && fsm.Owner != null)
            HotGameEntry.HeroManager.CancelStep(fsm.Owner);
        _moving = false;
        base.OnLeave(fsm, isShutdown);
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
