using GameFramework.Fsm;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
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
        public Slider xuetiao;
        public Slider power;
        public Image levelImage;
        public override void Init(int i)
        {
            this.Index = i;
            this.level = 1;
            this.money = QiziGuanLi.Instance.qizi[i];
            this.GObj = Pool.instance.PoolObject[i].Get();
            this.GObj.transform.localScale = Vector3.one;
            QiziGuanLi.Instance.QiziList.Add(this);
            QiziGuanLi.Instance.QiziCXList.Add(this);
            xueliangnow = xueliangsum;
            powernow = 0;
            gongjiDistence = 1;
            this.xuetiao = this.GObj.transform.Find("xuetiao_qizi/xuetiao").GetComponent<Slider>();
            this.xuetiao.value = this.xueliangnow / this.xueliangnow;
            this.power = this.GObj.transform.Find("xuetiao_qizi/pow").GetComponent<Slider>();
            this.power.value = this.powernow / this.powersum;
            this.levelImage = this.GObj.transform.Find("xuetiao_qizi/level").GetComponent<Image>();
            this.levelImage.sprite = QiziGuanLi.Instance.ListQiziLevelSprite[0];
            //GameEntry.UI.OpenUIForm("Assets/UIPrefab/xuetiao_qizi.prefab", "middle");
            //this.GObj.GetComponent<Fsm_qizi0>().Init();
            //Log.Info("hfk:qizichushihua:" + this.GObj.name+"list.size: " + Pool.instance.list.Count + "list[0]position:" + Pool.instance.list[0].GObj.transform.localPosition);
        }
        public void Remove()
        {
            if (this.GObj.transform.localPosition.z == -4.5f)//如果棋子在场下
            {
                QiziGuanLi.Instance.QiziCXList.Remove(this);
                QiziGuanLi.Instance.changxia[(int)this.GObj.transform.localPosition.x + 4] = -1;
            }
            else
            {
                QiziGuanLi.Instance.QiziCSList.Remove(this);
            }
            QiziGuanLi.Instance.QiziList.Remove(this);
            Pool.instance.PoolObject[this.Index].Release(this.GObj);
            Pool.instance.PoolEntity.Release(this);
        }
    }
}
