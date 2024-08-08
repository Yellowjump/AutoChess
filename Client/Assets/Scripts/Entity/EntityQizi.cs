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
        public Vector2Int SavePos;//进入战斗时的位置
        public float gongjiDistence;//攻击距离

        public float AtkSpeed=1;//每秒攻击次数
        
        public Slider xuetiao;
        public Slider power;
        public Image levelImage;
        public Animator animator;//动画管理器

        public bool IsValid = true;
        public override void Init(int i)
        {
            IsValid = true;
            HeroID = i;
            HeroUID = GameEntry.HeroManager.QiziCurUniqueIndex++;
            level = 1;
            money = 1;
            GameEntry.HeroManager.GetHeroObjByID(HeroID,OnGetHeroGObjCallback);
            xueliangnow = xueliangsum;
            powernow = 0;//初始化蓝量
            gongjiDistence = 1.2f;//初始化攻击距离
            InitAttribute();
            InitSkill();
            InitState();
        }

        private void OnGetHeroGObjCallback(GameObject obj)
        {
            GObj = obj;
            GObj.SetActive(true);
            GObj.transform.position = LogicPosition;
            GObj.transform.localScale = Vector3.one;
            xuetiao = GObj.transform.Find("xuetiao_qizi/xuetiao").GetComponent<Slider>();
            power = GObj.transform.Find("xuetiao_qizi/pow").GetComponent<Slider>();
            levelImage = this.GObj.transform.Find("xuetiao_qizi/level").GetComponent<Image>();
            animator = this.GObj.GetComponent<Animator>();
        }
        /// <summary>
        /// 战斗结束后回到初始状态
        /// </summary>
        public void ReInit()
        {
            GObj?.SetActive(true);
            IsValid = true;
            CurBuffList.Clear();
            fsm.ChangeStatePublic<StateIdle0>();
            DestoryAttribute();
            InitAttribute();
            InitPassiveSkill();
        }
        public void Remove()
        {
            animator?.Play("WAIT00");
            GameEntry.HeroManager.ReleaseHeroGameObject(HeroID,GObj,OnGetHeroGObjCallback);
            GObj = null;
            DestoryState();
            DestorySkill();
            DestoryAttribute();
            GameEntry.HeroManager.ReleaseEntityQizi(this);
        }

        public void OnLogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (IsValid == false)
            {
                return;
            }
            SinceLastNormalAtk += elapseSeconds;
            UpdateState(elapseSeconds,realElapseSeconds);
            UpdateSkill(elapseSeconds, realElapseSeconds);
            UpdateShowSlider();
            UpdateAnimCommand();
        }

        public void OnDead()
        {
            IsValid = false;
            GameEntry.HeroManager.OnEntityDead(this);
            GObj?.SetActive(false);
        }
    }
}
