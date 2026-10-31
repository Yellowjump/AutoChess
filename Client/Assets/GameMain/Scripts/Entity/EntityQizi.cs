using System.Collections.Generic;
using DataTable;
using GameFramework;
using SkillSystem;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Entity
{
    public partial class EntityQizi : EntityBase
    {
        private static readonly ProfilerMarker StateUpdateMarker =
            new ProfilerMarker("Combat.Unit.State");
        private static readonly ProfilerMarker SkillUpdateMarker =
            new ProfilerMarker("Combat.Unit.Skill");
        private static readonly ProfilerMarker SliderUpdateMarker =
            new ProfilerMarker("Combat.Unit.Slider");
        private static readonly ProfilerMarker AnimationUpdateMarker =
            new ProfilerMarker("Combat.Unit.Animation");
        private static readonly ProfilerMarker PosePresentationMarker =
            new ProfilerMarker("Combat.Presentation.UnitPose");
        private static readonly ProfilerMarker SfxPresentationMarker =
            new ProfilerMarker("Combat.Presentation.UnitSfx");

        public int HeroID;//hero表中ID
        public int HeroUID;//qizi唯一id
        public HeroComponent.HpBar HpBar;
        public Slider xuetiao;
        public Slider power;
        public Slider hudun;
        private Vector3 m_LastPresentedLogicPosition;
        private bool m_HasPresentedLogicPosition;
        public List<OneItemData> EquipItemList = new List<OneItemData>();

        public override CampType BelongCamp
        {
            get => m_CombatUnitHandle != 0
                ? (CampType)Zizouqi.CombatDots.CombatAttributeBridge.GetCamp(m_CombatUnitHandle)
                : _belongCamp;
            set
            {
                if (m_CombatUnitHandle == 0)
                {
                    _belongCamp = value;
                }
                else
                {
                    Zizouqi.CombatDots.CombatAttributeBridge.SetCamp(m_CombatUnitHandle, (int)value);
                }
            }
        }

        public override bool IsValid
        {
            get => m_CombatUnitHandle != 0
                ? Zizouqi.CombatDots.CombatAttributeBridge.IsAlive(m_CombatUnitHandle)
                : _isValid;
            set
            {
                if (m_CombatUnitHandle == 0)
                {
                    _isValid = value;
                }
                else
                {
                    Zizouqi.CombatDots.CombatAttributeBridge.SetAlive(m_CombatUnitHandle, value);
                }
            }
        }

        public override Vector3 LogicPosition
        {
            get
            {
                if (m_CombatUnitHandle == 0)
                {
                    return _logicPosition;
                }
                Zizouqi.CombatDots.CombatAttributeBridge.GetPosition(m_CombatUnitHandle,
                    out float x, out float y, out float z);
                return new Vector3(x, y, z);
            }
            set
            {
                if (m_CombatUnitHandle == 0)
                {
                    _logicPosition = value;
                }
                else
                {
                    Zizouqi.CombatDots.CombatAttributeBridge.SetPosition(m_CombatUnitHandle,
                        value.x, value.y, value.z);
                }
                if (!HotGameEntry.HeroManager.InBattle)
                    SyncCombatPositionPresentation(value.x, value.y, value.z);
            }
        }

        internal void SyncCombatPositionPresentation(float x, float y, float z)
        {
            if (GObj != null)
            {
                GObj.transform.position = HotGameEntry.HeroManager.GetTerrainMatchHeight(
                    new Vector3(x, y, z));
                m_LastPresentedLogicPosition = new Vector3(x, y, z);
                m_HasPresentedLogicPosition = true;
            }
        }

        public override Vector3 LogicHitPosition => LogicPosition + Vector3.up * ConstValue.EntityQiziHeight;

        public void Init(int i,List<int> equipItem = null)
        {
            IsValid = true;
            HeroID = i;
            HeroUID = CombatBoardBridge.AllocateHeroUid();
            if (equipItem == null)
            {
                InitAddDefaultItemToList();
            }
            else
            {
                AddInitItemFromList(equipItem);
            }
            InitAttribute();
            InitSkill();
            InitAnimationClip();
            InitState();
        }

        public override void InitGObj()
        {
            //添加血条预制体到worldcanvas
            HotGameEntry.HeroManager.AddHpBar(this);
            
            HotGameEntry.HeroManager.GetHeroObjByID(HeroID,OnGetHeroGObjCallback);
        }

        private void OnGetHeroGObjCallback(GameObject obj,string path)
        {
            GObj = obj;
            GObj.SetActive(true);
            GObj.transform.position = HotGameEntry.HeroManager.GetTerrainMatchHeight(LogicPosition);
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
                    EquipItemList.Add(HotGameEntry.HeroManager.AddEquipItemToEquip(itemID));
                }
            }
        }

        private void AddInitItemFromList(List<int> itemList)
        {
            if (itemList == null||itemList.Count==0)
            {
                return;
            }

            foreach (var itemID in itemList)
            {
                EquipItemList.Add(HotGameEntry.HeroManager.AddEquipItemToEquip(itemID));
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
            CombatAttributeBridge.GetDeploymentCell(m_CombatUnitHandle,
                out int deploymentRow, out int deploymentColumn);
            DestorySkill();
            DestoryAttribute();
            InitAttribute();
            CombatAttributeBridge.SetDeploymentCell(m_CombatUnitHandle,
                deploymentRow, deploymentColumn);
            LogicPosition = _logicPosition;
            InitSkill();
        }

        public void RemoveGObj()
        {
            HotGameEntry.HeroManager.ReleaseHeroGameObject(HeroID,GObj,OnGetHeroGObjCallback);
            ClearHpBar();
            GObj = null;
            m_HasPresentedLogicPosition = false;
        }
        public void Remove()
        {
            ClearHpBar();
            RemoveAllSfx();
            RemoveGObj();
            ReleaseAnim();
            DestoryState();
            DestorySkill();
            RemoveAllWeapon();
            DestoryAttribute();
            foreach (var oneItem in EquipItemList)
            {
                ReferencePool.Release(oneItem);
            }
            EquipItemList.Clear();
            HotGameEntry.HeroManager.ReleaseEntityQizi(this);
        }

        private void ClearHpBar()
        {
            if (HpBar != null)
            {
                ReferencePool.Release(HpBar);
                HpBar = null;
            }
        }
        public void OnLogicUpdate(float elapseSeconds, float realElapseSeconds,
            CombatUnitSkillPlanBatch skillPlans = null)
        {
            if (IsValid == false)
            {
                return;
            }
            m_UnitSkillPlans = skillPlans;
            try
            {
                using (StateUpdateMarker.Auto())
                {
                    UpdateState(elapseSeconds, realElapseSeconds);
                }
                using (SkillUpdateMarker.Auto())
                {
                    UpdateSkill(elapseSeconds, realElapseSeconds);
                }
            }
            finally
            {
                m_UnitSkillPlans = null;
                m_UsePreparedSkillDecisions = false;
                m_UsePreparedAutomaticSkills = false;
            }
        }

        public void UpdateCombatPresentation()
        {
            if (!IsValid)
            {
                using (SfxPresentationMarker.Auto())
                {
                    ReplaySfxEvents();
                }
                return;
            }
            using (PosePresentationMarker.Auto())
            {
                Vector3 position = LogicPosition;
                if (GObj != null && (!m_HasPresentedLogicPosition ||
                    position != m_LastPresentedLogicPosition))
                    SyncCombatPositionPresentation(position.x, position.y, position.z);
                if (GObj != null && m_CombatUnitHandle != 0 &&
                    CombatAttributeBridge.TryConsumeFacing(m_CombatUnitHandle,
                        out Unity.Mathematics.float3 facing))
                {
                    GObj.transform.LookAt(HotGameEntry.HeroManager.GetTerrainMatchHeight(
                        new Vector3(facing.x, facing.y, facing.z)));
                }
            }
            using (SliderUpdateMarker.Auto())
            {
                UpdateShowSlider();
            }
            using (AnimationUpdateMarker.Auto())
            {
                UpdateAnimCommand();
            }
            using (SfxPresentationMarker.Auto())
            {
                ReplaySfxEvents();
            }
        }

        public void UpdateNoBattle(float elapseSeconds, float realElapseSeconds)
        {
            UpdateShowSlider();
            UpdateAnimCommand();
            ReplaySfxEvents();
        }

        public void OnWinBattle()
        {
            ChangeToWinState();
        }
        public void OnLoseBattle()
        {
            ChangeToLoseState();
        }
        public void OnDead()
        {
            CombatBuffTimerBridge.PauseActiveForOwner(m_CombatUnitHandle);
            RemoveGObj();
            IsValid = false;
            HotGameEntry.HeroManager.OnEntityDead(this);
            //GObj?.SetActive(false);
        }
    }
}
