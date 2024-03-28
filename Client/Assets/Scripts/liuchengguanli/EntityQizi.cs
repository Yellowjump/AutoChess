using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace liuchengguanli
{
    public class EntityQizi : EntityBase
    {
        public int level;
        public int money;
        public override void Init(int i)
        {
            this.Index = i;
            this.level = 1;
            this.money = QiziGuanLi.Instance.qizi[i];
            this.GObj = Pool.instance.PoolObject[i].Get();
            this.GObj.transform.localScale = Vector3.one;
            QiziGuanLi.Instance.QiziList.Add(this);
            //Log.Info("hfk:qizichushihua:" + this.GObj.name+"list.size: " + Pool.instance.list.Count + "list[0]position:" + Pool.instance.list[0].GObj.transform.localPosition);
        }
    }
}
