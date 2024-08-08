using GameFramework.Fsm;
using Entity;
using System.Collections;
using System.Collections.Generic;
using SkillSystem;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Analytics;
using UnityGameFramework.Runtime;
using static UnityEngine.GraphicsBuffer;

public class StateMove0 : FsmState<EntityQizi>
{
    int zhenying = 0;//0表明是自己这边的棋子，1表明是敌方阵营的
    EntityQizi qizitarget;//目标棋子
    float mindistance = 10000;
    float timebegin;//记录进入状态的时间
    float timetemp;//记录进入update的时间
    bool findpath = false;
    Vector2Int nextPosIndex;
    Vector3 startpos;
    Vector3 nextpos;
    private const float MoveOneCellDuration = 0.5f;
    private float _moveAccumulate = 0f;
    private bool _moving = false;
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
        startpos = owner.LogicPosition;
        //Log.Info("hfk:" + qizi.level);
        //找到距离该棋子最近的棋子
        qizitarget = null;
        mindistance = 10000;
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
                owner.LogicPosition = new Vector3(Mathf.Lerp(startpos.x, nextpos.x, _moveAccumulate/MoveOneCellDuration), 0, Mathf.Lerp(startpos.z, nextpos.z, _moveAccumulate/MoveOneCellDuration));
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
        /*if (Time.time- timetemp > 0.5f)//0.5s每次
        {
            timetemp = Time.time+0.5f;
            Findtarget();
            if (GameEntry.HeroManager.dangqianliucheng == 0 || qizitarget == null)
            {
                ChangeState<StateIdle0>(fsm);
            }
            if (qizi.gongjiDistence * qizi.gongjiDistence <= mindistance)
            {
                Vector2Int start = GameEntry.HeroManager.getIndexQige(qizi.GObj.transform.position);
                //寻路
                while (nextPosIndex.x==-1&&qizitarget!=null)
                {
                    Vector2Int end = GameEntry.HeroManager.getIndexQige(qizitarget.GObj.transform.position);
                    nextPosIndex = GameEntry.HeroManager.Findpath(start, end, qizi.gongjiDistence);
                    if (nextPosIndex.x != -1)
                    {
                        qizi.animator.Play("RUN00_F");
                        nextpos = GameEntry.HeroManager.qigepos[nextPosIndex.x][nextPosIndex.y];
                        GameEntry.HeroManager.qige[nextPosIndex.x][nextPosIndex.y] = 1;
                        GameEntry.HeroManager.qige[start.x][start.y] = 0;
                        findpath = true;
                        qizi.GObj.transform.LookAt(nextpos);
                        timebegin = Time.time;
                    }
                    else
                    {
                        FindNexttarget();
                        findpath = false;
                        qizi.animator.Play("WAIT00");
                        //ChangeState<StateIdle0>(fsm);
                    }
                }
            }
            else
            {
                ChangeState<StateAttack0>(fsm);
            }
        }
        if (findpath)
        {
            qizi.GObj.transform.position = new Vector3(Mathf.Lerp(startpos.x, nextpos.x, (Time.time - timebegin)/0.5f), 0, Mathf.Lerp(startpos.z, nextpos.z, (Time.time - timebegin) / 0.5f));
            if (Time.time - timebegin > 0.5f)
            {
                findpath = false;
                startpos = nextpos;
                //ChangeState<StateIdle0>(fsm);
            }
        }*/
    }

    private void CheckChangeStateOrMovePos(IFsm<EntityQizi> fsm)
    {
        if (fsm == null||fsm.Owner==null)
        {
            Log.Error("Fsm is null or Owner is null");
            return;
        }
        var owner = fsm.Owner;
        var result = owner.CheckCanCastSkill(out var target, true);
        if (result == CheckCastSkillResult.CanCast)
        {
            owner.CurAttackTarget = target;
            ChangeState<StateAttack0>(fsm);
            return;
        }
        else if (result == CheckCastSkillResult.TargetOutRange)
        {
            owner.CurAttackTarget = target;
            Vector2Int ownerIndex = GameEntry.HeroManager.GetIndexQizi(owner);
            Vector2Int targetIndex = GameEntry.HeroManager.GetIndexQizi(target);
            nextPosIndex = GameEntry.HeroManager.Findpath(ownerIndex, targetIndex, owner.gongjiDistence);
            if (nextPosIndex != new Vector2Int(-1, -1))
            {
                _moving = true;
                startpos = owner.LogicPosition;
                nextpos = GameEntry.HeroManager.qigepos[nextPosIndex.y][nextPosIndex.x];
                GameEntry.HeroManager.qige[nextPosIndex.y][nextPosIndex.x] = owner.HeroUID;
                GameEntry.HeroManager.qige[ownerIndex.y][ownerIndex.x] =-1;
                return;
            }
        }
        result = owner.CheckCanCastSkill(out target, false);
        if (result == CheckCastSkillResult.CanCast)
        {
            owner.CurAttackTarget = target;
            ChangeState<StateAttack0>(fsm);
        }
        else if (result == CheckCastSkillResult.TargetOutRange)
        {
            owner.CurAttackTarget = target;
            Vector2Int ownerIndex = GameEntry.HeroManager.GetIndexQizi(owner);
            Vector2Int targetIndex = GameEntry.HeroManager.GetIndexQizi(target);
            nextPosIndex = GameEntry.HeroManager.Findpath(ownerIndex, targetIndex, owner.gongjiDistence);
            if (nextPosIndex != new Vector2Int(-1, -1)&&nextPosIndex!=ownerIndex)
            {
                _moving = true;
                startpos = owner.LogicPosition;
                nextpos = GameEntry.HeroManager.qigepos[nextPosIndex.y][nextPosIndex.x];
                owner.AddAnimCommand("RUN00_F");
                owner.GObj?.transform.LookAt(nextpos);
                GameEntry.HeroManager.qige[ownerIndex.y][ownerIndex.x] = -1;
                GameEntry.HeroManager.qige[nextPosIndex.y][nextPosIndex.x] = owner.HeroUID;
            }
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
