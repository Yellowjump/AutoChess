using GameFramework.Fsm;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Entity
{
    public partial class EntityQizi : EntityBase
    {
        public int level;
        public int money;
        public int HeroID;//hero表中ID
        public int HeroUID;//qizi唯一id
        public float xueliangsum=1000;//总血量
        public float powersum =100;//总蓝
        public float xueliangnow;//当前血量
        public float powernow;//当前蓝
        public int rowIndex;
        public int columnIndex;//在棋盘上的位置下标,左下角是0，0,如果在备战棋格，rowIndex = -1，columnIndex是第几个
        public float gongjiDistence;//攻击距离

        public float AtkSpeed=1;//每秒攻击次数
        
        public Slider xuetiao;
        public Slider power;
        public Image levelImage;
        public Animator animator;//动画管理器
        public override void Init(int i)
        {
            this.Index = i;
            HeroUID = QiziGuanLi.Instance.QiziCurUniqueIndex++;
            this.level = 1;
            this.money = QiziGuanLi.Instance.qizi[i];
            this.GObj = Pool.instance.PoolObject[i].Get();
            this.GObj.transform.localScale = Vector3.one;
            QiziGuanLi.Instance.QiziList.Add(this);
            QiziGuanLi.Instance.QiziCXList.Add(this);
            xueliangnow = xueliangsum;
            powernow = 0;//初始化蓝量
            gongjiDistence = 1.2f;//初始化攻击距离
            this.xuetiao = this.GObj.transform.Find("xuetiao_qizi/xuetiao").GetComponent<Slider>();
            this.xuetiao.value = this.xueliangnow / this.xueliangnow;
            this.power = this.GObj.transform.Find("xuetiao_qizi/pow").GetComponent<Slider>();
            this.power.value = this.powernow / this.powersum;
            this.levelImage = this.GObj.transform.Find("xuetiao_qizi/level").GetComponent<Image>();
            this.levelImage.sprite = QiziGuanLi.Instance.ListQiziLevelSprite[0];
            this.animator = this.GObj.GetComponent<Animator>();
            //GameEntry.UI.OpenUIForm("Assets/UIPrefab/xuetiao_qizi.prefab", "middle");
            //this.GObj.GetComponent<Fsm_qizi0>().Init();
            //Log.Info("hfk:qizichushihua:" + this.GObj.name+"list.size: " + Pool.instance.list.Count + "list[0]position:" + Pool.instance.list[0].GObj.transform.localPosition);
            HeroID = 1;//todo 后续 从棋子购买处获取ID
            InitAttribute();
            InitSkill();
            InitState();
        }
        public void Remove()
        {
            if (rowIndex==-1)//如果棋子在场下
            {
                QiziGuanLi.Instance.QiziCXList.Remove(this);
                QiziGuanLi.Instance.changxia[columnIndex] = -1;
            }
            else
            {
                QiziGuanLi.Instance.QiziCSList.Remove(this);
            }
            QiziGuanLi.Instance.QiziList.Remove(this);
            Pool.instance.PoolObject[this.Index].Release(this.GObj);
            Pool.instance.PoolEntity.Release(this);
            DestoryState();
        }

        public void OnLogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            SinceLastNormalAtk += elapseSeconds;
            UpdateState(elapseSeconds,realElapseSeconds);
        }
    }
}
