using GameFramework.Fsm;
using Entity;
using System.Collections.Generic;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

public class StateMove0 : FsmState<EntityQizi>
{
    int zhenying = 0;//0表明是自己这边的棋子，1表明是敌方阵营的
    EntityQizi qizitarget;//目标棋子
    float timebegin;//记录进入状态的时间
    float timetemp;//记录进入update的时间
    bool findpath = false;
    Vector2Int nextPosIndex;
    Vector3 startpos;
    Vector3 nextpos;
    private const float MoveOneCellDuration = 0.5f;
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
        startpos = owner.LogicPosition;
        //Log.Info("hfk:" + qizi.level);
        //找到距离该棋子最近的棋子
        qizitarget = null;
        findpath = false;
        timebegin = Time.time;
        timetemp = Time.time;
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
        //判断是否在移动中
        if (_moving)
        {
            _moveAccumulate += elapseSeconds;
            if (_moveAccumulate < MoveOneCellDuration)
            {
                owner.LogicPosition = Vector3.Lerp(startpos, nextpos, _moveAccumulate / MoveOneCellDuration);
                /*owner.LogicPosition = new Vector3(Mathf.Lerp(startpos.x, nextpos.x, _moveAccumulate/MoveOneCellDuration), 0, Mathf.Lerp(startpos.z, nextpos.z, _moveAccumulate/MoveOneCellDuration));*/
                return;
            }
            else
            {
                _moveAccumulate = 0;
                owner.LogicPosition = nextpos;
                //已到达目标点
                _moving = false;
                var v2 = GameEntry.HeroManager.GetIndexQizi(owner);
                owner.columnIndex = v2.x;
                owner.rowIndex = v2.y;
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
                owner.CastNormalSkill(checkNormalSkillList[index]);
                if (target != null)
                {
                    owner.GObj?.transform.LookAt(target.GObj.transform);
                }
                ChangeState<StateAttack0>(fsm);
                return;
            }
            else if (result == CheckCastSkillResult.TargetOutRange)
            {
                owner.CurAttackTarget = target;
                Vector2Int ownerIndex = GameEntry.HeroManager.GetIndexQizi(owner);
                Vector2Int targetIndex = GameEntry.HeroManager.GetIndexQizi(target);
                nextPosIndex = GameEntry.HeroManager.Findpath(ownerIndex, targetIndex, owner.NormalSkillList[checkNormalSkillList[index]].SkillRange);
                if (nextPosIndex.x != -1 && nextPosIndex.y != -1&&nextPosIndex!=ownerIndex)
                {
                    noNextPosMove = false;
                    _moving = true;
                    startpos = owner.LogicPosition;
                    nextpos = GameEntry.HeroManager.GetGeziPos(nextPosIndex.y,nextPosIndex.x);;
                    owner.AddAnimCommandRun();
                    owner.GObj?.transform.LookAt(nextpos);
                    GameEntry.HeroManager.qige[ownerIndex.y][ownerIndex.x] = -1;
                    GameEntry.HeroManager.qige[nextPosIndex.y][nextPosIndex.x] = owner.HeroUID;
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
        base.OnLeave(fsm, isShutdown);
    }
    protected override void OnDestroy(IFsm<EntityQizi> fsm)
    {
        base.OnDestroy(fsm);
    }
}
