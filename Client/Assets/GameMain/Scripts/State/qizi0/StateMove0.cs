using GameFramework.Fsm;
using Entity;
using System.Collections.Generic;
using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

public class StateMove0 : FsmState<EntityQizi>,IReference
{
    protected override void OnEnter(IFsm<EntityQizi> fsm)
    {
        base.OnEnter(fsm);
        if (fsm == null || fsm.Owner == null)
        {
            return;
        }

        CombatMovementBridge.End(fsm.Owner.CombatUnitHandle);
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
        //判断是否在移动中
        if (CombatMovementBridge.ConsumeAdvanced(owner.CombatUnitHandle, elapseSeconds,
                out bool arrived, out _, out _, out _))
        {
            if (!arrived)
            {
                return;
            }
            //棋盘占位已在开始移动时写入 ECS。
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
        var result = CheckCastSkillResult.Error;
        EntityQizi target = null;
        int skillCount = owner.NormalSkillList.Count;
        var noNextPosMove = true;
        for (var offset = 0; offset < skillCount; offset++)
        {
            int skillIndex = CombatAttributeBridge.GetNormalSkillIndexAtOffset(
                owner.CombatUnitHandle, skillCount, offset);
            result = owner.CheckCanCastSkill(out target, skillIndex);
            if (result == CheckCastSkillResult.CanCast)
            {
                owner.CurAttackTarget = target;
                owner.CastNormalSkill(skillIndex);
                if (target != null)
                {
                    Vector3 targetPosition = target.LogicPosition;
                    CombatAttributeBridge.QueueFacing(owner.CombatUnitHandle,
                        targetPosition.x, targetPosition.y, targetPosition.z);
                }
                ChangeState<StateAttack0>(fsm);
                return;
            }
            else if (result == CheckCastSkillResult.TargetOutRange)
            {
                owner.CurAttackTarget = target;
                Vector2Int ownerIndex = HotGameEntry.HeroManager.GetIndexQizi(owner);
                Vector2Int targetIndex = HotGameEntry.HeroManager.GetIndexQizi(target);
                Vector2Int nextPosIndex = HotGameEntry.HeroManager.Findpath(ownerIndex, targetIndex,
                    CombatSkillSlotBridge.GetNormalRange(owner.CombatUnitHandle, skillIndex));
                if (nextPosIndex.x != -1 && nextPosIndex.y != -1&&nextPosIndex!=ownerIndex)
                {
                    noNextPosMove = false;
                    Vector3 nextpos = HotGameEntry.HeroManager.GetGeziPos(nextPosIndex.y,nextPosIndex.x);
                    CombatMovementBridge.Begin(owner.CombatUnitHandle, nextpos.x, nextpos.y, nextpos.z);
                    owner.AddAnimCommandRun();
                    CombatAttributeBridge.QueueFacing(owner.CombatUnitHandle,
                        nextpos.x, nextpos.y, nextpos.z);
                    CombatBoardBridge.MoveOccupant(ownerIndex.y,
                        ownerIndex.x, nextPosIndex.y, nextPosIndex.x, owner.HeroUID);
                    break;
                }
            }
        }
        if (noNextPosMove)
        {
            ChangeState<StateIdle0>(fsm);
        }

        
    }
    protected override void OnLeave(IFsm<EntityQizi> fsm, bool isShutdown)
    {
        base.OnLeave(fsm, isShutdown);
        if (fsm != null && fsm.Owner != null)
        {
            CombatMovementBridge.End(fsm.Owner.CombatUnitHandle);
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
