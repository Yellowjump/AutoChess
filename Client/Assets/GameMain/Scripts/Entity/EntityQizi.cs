using System.Collections.Generic;
using DataTable;
using GameFramework;
using SkillSystem;
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
        public int rowIndex;
        public int columnIndex;//在棋盘上的位置下标,左下角是0，0,如果在备战棋格，rowIndex = -1，columnIndex是第几个
        public Vector2Int SavePos;//进入战斗时的位置
        public float gongjiDistence;//攻击距离

        public float AtkSpeed=1;//每秒攻击次数
        public HeroComponent.HpBar HpBar;
        public Slider xuetiao;
        public Slider power;
        public Slider hudun;
        public Image levelImage;
        public Animator animator;//动画管理器

        public List<int> EquipItemList = new List<int>();
        public bool IsValid = true;
        public override void Init(int i)
        {
            IsValid = true;
            HeroID = i;
            HeroUID = GameEntry.HeroManager.QiziCurUniqueIndex++;
            level = 1;
            money = 1;
            gongjiDistence = 1.2f;//初始化攻击距离
            InitAddDefaultItemToList();
            InitAttribute();
            InitSkill();
            InitAnimationClip();
            InitState();
        }

        public override void InitGObj()
        {
            //添加血条预制体到worldcanvas
            GameEntry.HeroManager.AddHpBar(this);
            
            GameEntry.HeroManager.GetHeroObjByID(HeroID,OnGetHeroGObjCallback);
        }

        private void OnGetHeroGObjCallback(GameObject obj)
        {
            GObj = obj;
            GObj.SetActive(true);
            GObj.transform.position = LogicPosition;
            GObj.transform.localScale = Vector3.one;
            GObj.transform.rotation = BelongCamp== CampType.Friend?Quaternion.identity : Quaternion.Euler(new Vector3(0, -180, 0));
            
            animator = this.GObj.GetComponent<Animator>();
            InitAnimation();
            UpdateShowSlider();//加载完obj就刷新一次
        }

        private void InitAddDefaultItemToList()
        {
            var heroTable = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (!heroTable.HasDataRow(HeroID))
            {
                Log.Error($"heroID:{HeroID} invalid no match TableRow");
                return;
            }

            var heroTableData = heroTable[HeroID];
            if (heroTableData.DefaultItemID != null && heroTableData.DefaultItemID.Length != 0)
            {
                foreach (var itemID in heroTableData.DefaultItemID)
                {
                    EquipItemList.Add(itemID);
                }
            }
        }
        /// <summary>
        /// 战斗结束后回到初始状态
        /// </summary>
        public void ReInit()
        {
            RemoveAllSfx();
            GObj?.SetActive(true);
            GObj.transform.rotation = BelongCamp== CampType.Friend?Quaternion.identity : Quaternion.Euler(new Vector3(0, -180, 0));
            IsValid = true;
            CurBuffList.Clear();
            ChangeToIdleState();
            DestoryAttribute();
            InitAttribute();
            DestorySkill();
            InitSkill();
            ReleaseAnim();
            InitAnimationClip();
        }

        public void OnChangeEquipItem()
        {
            DestoryAttribute();
            InitAttribute();
            DestorySkill();
            InitSkill();
        }
        public void Remove()
        {
            RemoveAllSfx();
            GameEntry.HeroManager.ReleaseHeroGameObject(HeroID,GObj,OnGetHeroGObjCallback);
            ReleaseAnim();
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
            UpdateState(elapseSeconds,realElapseSeconds);
            UpdateSkill(elapseSeconds, realElapseSeconds);
            UpdateShowSlider();
            UpdateAnimCommand();
        }

        public void UpdateNoBattle(float elapseSeconds, float realElapseSeconds)
        {
            UpdateShowSlider();
            UpdateAnimCommand();
        }

        public void OnWinBattle()
        {
            ChangeToIdleState();
        }
        public void OnDead()
        {
            ReferencePool.Release(HpBar);
            IsValid = false;
            GameEntry.HeroManager.OnEntityDead(this);
            GObj?.SetActive(false);
        }
    }
}
