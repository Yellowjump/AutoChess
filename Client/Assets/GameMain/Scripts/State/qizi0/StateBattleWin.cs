using GameFramework.Fsm;
using Entity;
using System.Collections;
using System.Collections.Generic;
using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

public class StateBattleWin : FsmState<EntityQizi>,IReference
{
    protected override void OnEnter(IFsm<EntityQizi> fsm)
    {
        base.OnEnter(fsm);
        if (fsm == null || fsm.Owner == null)
        {
            return;
        }

        var owner = fsm.Owner;
        owner.AddAnimCommandWin();
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
