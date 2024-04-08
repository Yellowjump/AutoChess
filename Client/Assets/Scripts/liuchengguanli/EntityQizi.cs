using GameFramework.Fsm;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace liuchengguanli
{
    public class EntityQizi : EntityBase
    {
        public int level;
        public int money;
        public float xueliangsum=1000;//总血量
        public float powersum =100;//总蓝
        public float xueliangnow;//当前血量
        public float powernow;//当前蓝
        public float x;
        public float y;//在棋盘上的位置
        public float gongjiDistence;//攻击距离
        Slider xuetiao;
        Slider power;
        Transform xuet;
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
            this.xuetiao = this.GObj.transform.Find("xuetiao_qizi/xuetiao").GetComponent<Slider>();
            this.xuetiao.value = this.xueliangnow / this.xueliangnow;
            this.power = this.GObj.transform.Find("xuetiao_qizi/pow").GetComponent<Slider>();
            this.power.value = this.powernow / this.powersum;
            //GameEntry.UI.OpenUIForm("Assets/UIPrefab/xuetiao_qizi.prefab", "middle");
            //this.GObj.GetComponent<Fsm_qizi0>().Init();
            //Log.Info("hfk:qizichushihua:" + this.GObj.name+"list.size: " + Pool.instance.list.Count + "list[0]position:" + Pool.instance.list[0].GObj.transform.localPosition);
        }
    }
}
