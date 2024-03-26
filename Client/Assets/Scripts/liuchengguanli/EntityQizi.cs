using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace liuchengguanli
{
    public class EntityQizi : EntityBase
    {
        int level;
        public override void Init(int i)
        {

            this.Index = i;
            this.level = 1;
            this.GObj = Pool.instance.PoolObject[i].Get();
            QiziGuanLi.Instance.QiziList.Add(this);
            //Log.Info("hfk:qizichushihua:" + this.GObj.name+"list.size: " + Pool.instance.list.Count + "list[0]position:" + Pool.instance.list[0].GObj.transform.localPosition);
        }
    }
}
