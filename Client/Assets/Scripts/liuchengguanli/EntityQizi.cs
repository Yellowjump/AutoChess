using liuchengguanli;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

public class EntityQizi : EntityBase
{

    public override void Init(int i)
    {
        
        this.Index = i;
        this.GObj =Pool.instance.PoolObject[i].Get();
        //Log.Info("hfk:qizichushihua:" + this.GObj.name+"list.size: " + Pool.instance.list.Count + "list[0]position:" + Pool.instance.list[0].GObj.transform.localPosition);
    }
}
