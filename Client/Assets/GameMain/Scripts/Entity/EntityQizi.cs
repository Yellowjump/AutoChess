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
        public int HeroID;//hero表中ID
        public int HeroUID;//qizi唯一id
        public int rowIndex;
        public int columnIndex;//在棋盘上的位置下标,左下角是0，0,如果在备战棋格，rowIndex = -1，columnIndex是第几个
        public Vector2Int SavePos;//进入战斗时的位置
        public HeroComponent.HpBar HpBar;
        public Slider xuetiao;
        public Slider power;
        public Slider hudun;
        public List<int> EquipItemList = new List<int>();
        public override Vector3 LogicHitPosition => LogicPosition + Vector3.up * ConstValue.EntityQiziHeight;

        public override void Init(int i)
        {
            IsValid = true;
            HeroID = i;
            HeroUID = GameEntry.HeroManager.QiziCurUniqueIndex++;
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

        private void OnGetHeroGObjCallback(GameObject obj,string path)
        {
            GObj = obj;
            GObj.SetActive(true);
            GObj.transform.position = LogicPosition;
            GObj.transform.localScale = Vector3.one;
            GObj.transform.rotation = BelongCamp== CampType.Friend?Quaternion.identity : Quaternion.Euler(new Vector3(0, -180, 0));
            InitAnimation();
            InitObjWeaponHandle();
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
            ChangeToIdleState();
            DestorySkill();
            DestoryAttribute();
            RemoveAllWeapon();
            InitAttribute();
            InitSkill();
        }

        public void OnChangeEquipItem()
        {
            DestorySkill();
            DestoryAttribute();
            InitAttribute();
            InitSkill();
        }

        public void RemoveGObj()
        {
            GameEntry.HeroManager.ReleaseHeroGameObject(HeroID,GObj,OnGetHeroGObjCallback);
            GObj = null;
        }
        public void Remove()
        {
            RemoveAllSfx();
            RemoveGObj();
            ReleaseAnim();
            DestoryState();
            DestorySkill();
            RemoveAllWeapon();
            DestoryAttribute();
            EquipItemList.Clear();
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
