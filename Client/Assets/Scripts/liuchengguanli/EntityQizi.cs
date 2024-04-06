using GameFramework.Fsm;
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
        public int xueliangsum=1000;//总血量
        public int powersum=100;//总蓝
        public int xueliangnow;//当前血量
        public int powernow;//当前蓝
        public float x;
        public float y;//在棋盘上的位置
        public float gongjiDistence;//攻击距离
        //GameObject xuetiao;//血条ui
        public override void Init(int i)
        {
            this.Index = i;
            this.level = 1;
            this.money = QiziGuanLi.Instance.qizi[i];
            this.GObj = Pool.instance.PoolObject[i].Get();
            this.GObj.transform.localScale = Vector3.one;
            QiziGuanLi.Instance.QiziList.Add(this);
            xueliangnow = xueliangsum;
            powernow = 0;
            //this.GObj.GetComponent<Fsm_qizi0>().Init();
            //Log.Info("hfk:qizichushihua:" + this.GObj.name+"list.size: " + Pool.instance.list.Count + "list[0]position:" + Pool.instance.list[0].GObj.transform.localPosition);
        }
    }
}
