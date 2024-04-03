using GameFramework;
using GameFramework.FileSystem;
using GameFramework.Fsm;
using liuchengguanli;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

public class Fsm_qizi0 : MonoBehaviour
{
    private static int ID = 0;
    private IFsm<Fsm_qizi0> fsm;
    void Start()
    {
        List<FsmState<Fsm_qizi0>> stateList = new List<FsmState<Fsm_qizi0>>() {new StateIdle0(),new StateMove0(),new StateAttack0(),new StateUnderControl0() };
        fsm = GameEntry.Fsm.CreateFsm<Fsm_qizi0>((ID++).ToString(),this,stateList);
        fsm.Start<StateIdle0>();
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnDestroy()
    {
        GameEntry.Fsm.DestroyFsm(fsm);
    }
}
