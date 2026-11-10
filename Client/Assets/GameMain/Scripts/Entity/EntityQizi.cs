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
        public string TimelineAppearance;
        internal bool UsesUndertow;
        internal int FirstStoneSmashDelayMs;
        internal bool HasSwordRecall;
        internal int TechniqueSkillId;
        internal int TechniqueKills;
        internal Dictionary<int, int> LightningMarks;
        internal int LightningApplications, LightningTransfers, LightningJumps;
        internal Dictionary<int, WorldTimeline.SkillBattleOutput> BattleOutput;

        internal static void RecordBattleOutput(Skill skill, long damage = 0, long shield = 0, long healing = 0,
            long shieldAbsorbed = 0)
        {
            var output = skill?.Caster?.BattleOutput;
            if (output == null || (damage <= 0 && shield <= 0 && healing <= 0 && shieldAbsorbed <= 0)) return;
            output.TryGetValue(skill.SkillID, out var previous);
            output[skill.SkillID] = new WorldTimeline.SkillBattleOutput(skill.SkillID, skill.FromItemID,
                previous.Damage + damage, previous.Shield + shield, previous.Healing + healing,
                previous.ShieldAbsorbed + shieldAbsorbed);
        }
        internal int UndertowAttackCount;
        internal int NamelessSteleAttackCount;
        internal bool NamelessSteleSealed;
        private bool ownsTimelineModel;
        public int HeroUID;//qizi唯一id
        public bool UsesLargeFootprint { get; private set; }
        internal Vector2Int? ReservedMoveAnchor { get; set; }

        public int rowIndex;
        public int columnIndex;//在棋盘上的位置下标,左下角是0，0,如果在备战棋格，rowIndex = -1，columnIndex是第几个
        public Vector2Int SavePos;//进入战斗时的位置
        public HeroComponent.HpBar HpBar;
        public Slider xuetiao;
        internal Slider WindupProgress;
        public Slider hudun;
        public List<OneItemData> EquipItemList = new List<OneItemData>();

        public override Vector3 LogicPosition
        {
            get=>base.LogicPosition;
            set
            {
                _logicPosition = value;
                if (GObj != null)
                {
                    GObj.transform.position = HotGameEntry.HeroManager.GetTerrainMatchHeight(value);
                }
            }
        }

        public override Vector3 LogicHitPosition => LogicPosition + Vector3.up *
            (TimelineAppearance == TimelineMonsterAnimation.NamelessSteleAppearance ? 1.55f : ConstValue.EntityQiziHeight);

        internal float HpBarHeight => TimelineAppearance == TimelineMonsterAnimation.NamelessSteleAppearance ? 3.1f : 2f;

        public void Init(int i,List<int> equipItem = null, bool recordBattleOutput = false)
        {
            IsValid = true;
            ReservedMoveAnchor = null;
            TechniqueKills = 0;
            LightningMarks?.Clear();
            LightningApplications = LightningTransfers = LightningJumps = 0;
            BattleOutput = recordBattleOutput ? new Dictionary<int, WorldTimeline.SkillBattleOutput>() : null;
            UndertowAttackCount = 0;
            NamelessSteleAttackCount = 0;
            NamelessSteleSealed = false;
            FirstStoneSmashDelayMs = 0;
            HeroID = i;
            UsesLargeFootprint = BelongCamp == CampType.Enemy && HotGameEntry.HeroManager.IsLargeBossHero(i, TimelineAppearance);
            HeroUID = ++HotGameEntry.HeroManager.QiziCurUniqueIndex;
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
            if (!string.IsNullOrEmpty(TimelineAppearance))
            {
                var prefab = Resources.Load<GameObject>("StoryCharacters/" + TimelineAppearance);
                if (prefab != null)
                {
                    ownsTimelineModel = true;
                    var model = UnityEngine.Object.Instantiate(prefab, HotGameEntry.HeroManager.InstanceRoot);
                    if ((TimelineAppearance is "GateGolem" or TimelineMonsterAnimation.NamelessSteleAppearance) &&
                        model.TryGetComponent<TimelineMonsterAnimation>(out var monster))
                        monster.CombineRigidParts();
                    OnGetHeroGObjCallback(model, null);
                    return;
                }
            }
            HotGameEntry.HeroManager.GetHeroObjByID(HeroID,OnGetHeroGObjCallback);
        }

        private void OnGetHeroGObjCallback(GameObject obj,string path)
        {
            GObj = obj;
            GObj.SetActive(true);
            GObj.transform.position = HotGameEntry.HeroManager.GetTerrainMatchHeight(LogicPosition);
            if (!ownsTimelineModel) GObj.transform.localScale = Vector3.one;
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
            DestorySkill();
            DestoryAttribute();
            InitAttribute();
            InitSkill();
        }

        public void RemoveGObj()
        {
            if (ownsTimelineModel)
            {
                if (GObj != null)
                {
                    GObj.SetActive(false);
                    UnityEngine.Object.Destroy(GObj);
                }
            }
            else HotGameEntry.HeroManager.ReleaseHeroGameObject(HeroID,GObj,OnGetHeroGObjCallback);
            ownsTimelineModel = false;
            TimelineAppearance = null;
            UsesUndertow = false;
            HasSwordRecall = false;
            ClearHpBar();
            GObj = null;
        }
        public void Remove()
        {
            HotGameEntry.HeroManager.ReleaseFootprint(this);
            ClearHpBar();
            RemoveAllSfx();
            RemoveGObj();
            ReleaseAnim();
            DestoryState();
            DestorySkill();
            RemoveAllWeapon();
            TechniqueSkillId = 0;
            DestoryAttribute();
            foreach (var oneItem in EquipItemList)
            {
                ReferencePool.Release(oneItem);
            }
            EquipItemList.Clear();
            BattleOutput = null;
            HotGameEntry.HeroManager.ReleaseEntityQizi(this);
        }

        private void ClearHpBar()
        {
            xuetiao = null;
            WindupProgress = null;
            hudun = null;
            if (HpBar != null)
            {
                ReferencePool.Release(HpBar);
                HpBar = null;
            }
        }
        public void OnLogicUpdate(float elapseSeconds, float realElapseSeconds, bool stationaryTarget = false)
        {
            if (IsValid == false)
            {
                return;
            }
            if (monsterAnimation != null) monsterAnimation.Advance(elapseSeconds);
            if (stationaryTarget)
            {
                UpdateBuffTime(elapseSeconds, realElapseSeconds);
            }
            else
            {
                UpdateState(elapseSeconds,realElapseSeconds);
                UpdateSkill(elapseSeconds, realElapseSeconds);
            }
            UpdateShowSlider();
            UpdateAnimCommand();
        }

        public void UpdateNoBattle(float elapseSeconds, float realElapseSeconds)
        {
            if (monsterAnimation != null) monsterAnimation.Advance(elapseSeconds);
            UpdateShowSlider();
            UpdateAnimCommand();
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
            RemoveGObj();
            IsValid = false;
            CommandBurnEffect.SpreadOnDeath(this);
            CommandLightningEffect.SpreadOnDeath(this);
            HotGameEntry.HeroManager.OnEntityDead(this);
            //GObj?.SetActive(false);
        }
    }
}
