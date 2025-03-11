using GameFramework.Fsm;
using Entity;
using System.Collections;
using System.Collections.Generic;
using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

public class StateIdle0 : FsmState<EntityQizi>,IReference
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
        owner.AddAnimCommandIdle();
    }
    protected override void OnUpdate(IFsm<EntityQizi> fsm, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);
        CheckChangeState(fsm);
    }

    private void CheckChangeState(IFsm<EntityQizi> fsm)
    {
        EntityQizi owner = fsm.Owner;
        if (owner == null)
        {
            Log.Error("owner is null");
            return;
        }

        if (GameEntry.HeroManager.dangqianliucheng == 0||owner.rowIndex==-1)//非战斗状态或者 没上场就一直在idle状态
        {
            return;
        }
        //技能
        var result = CheckCastSkillResult.Error;
        EntityQizi target = null;
        List<int> checkNormalSkillList = owner.GetNormalSkillCheckSort();
        for (var index = 0; index < checkNormalSkillList.Count; index++)
        {
            //普攻
            result = owner.CheckCanCastSkill(out target,checkNormalSkillList[index]);
            if (result == CheckCastSkillResult.CanCast)
            {
                owner.CurAttackTarget = target;
                owner.CastNormalSkill(checkNormalSkillList[index]);
                if (target != null)
                {
                    owner.GObj?.transform.LookAt(target.GObj.transform);
                }
                ChangeState<StateAttack0>(fsm);
                break;
            }
            else if (result == CheckCastSkillResult.TargetOutRange)
            {
                Vector2Int ownerIndex = GameEntry.HeroManager.GetIndexQizi(owner);
                Vector2Int targetIndex = GameEntry.HeroManager.GetIndexQizi(target);
                var nextPosIndex = GameEntry.HeroManager.Findpath(ownerIndex, targetIndex, owner.NormalSkillList[checkNormalSkillList[index]].SkillRange);
                if (nextPosIndex != new Vector2Int(-1, -1))//有路线可走
                {
                    owner.CurAttackTarget = target;
                    ChangeState<StateMove0>(fsm);
                    break;
                }
            }
            else if (result == CheckCastSkillResult.NormalAtkWait)
            {
                owner.CurAttackTarget = target;//普攻等待，但是目标要锁定
            }
        }
        ListPool<int>.Release(checkNormalSkillList);
    }
    protected override void OnLeave(IFsm<EntityQizi> fsm, bool isShutdown)
    {
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
