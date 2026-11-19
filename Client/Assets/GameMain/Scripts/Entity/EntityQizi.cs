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
        internal EntityQizi PriorityAttackTarget;
        internal bool UsesUndertow;
        internal bool CanOpenWithPounce, IsPounceStrike;
        internal int FirstStoneSmashDelayMs;
        internal bool HasSwordRecall;
        internal System.Func<bool> BambooActive;
        internal System.Func<WorldTimeline.EchoCast> BeginEchoCast;
        internal WorldTimeline.CriticalFighter Critical;
        internal System.Func<EntityQizi,int,int> RedirectHealthLoss;
        internal System.Action<EntityQizi,int,int> ArtifactHealthLost;
        internal System.Action<EntityQizi,int> ArtifactOverheal;
        internal System.Action<EntityQizi,int> OnAllyHealed;
        internal int ReceiveHealing(int amount, EntityQizi healer = null)
        {
            var hp = GetAttribute(AttributeType.Hp);
            int before = (int)hp.GetFinalValue();
            if (!IsValid || before <= 0 || amount <= 0) return 0;
            hp.AddNum(System.Math.Min(amount,(int)GetAttribute(AttributeType.MaxHp).GetFinalValue()-before));
            int gain = (int)hp.GetFinalValue() - before;
            if (amount > gain) ArtifactOverheal?.Invoke(this,amount-gain);
            if (gain > 0 && healer != null && healer != this && healer.IsValid && healer.BelongCamp == BelongCamp)
                healer.OnAllyHealed?.Invoke(this, gain);
            return gain;
        }
        internal int TechniqueSkillId;
        internal int TechniqueKills;
        internal Dictionary<int, int> LightningMarks;
        internal int LightningApplications, LightningTransfers, LightningJumps;
        internal Dictionary<int, WorldTimeline.SkillBattleOutput> BattleOutput;
        internal System.Action<EntityQizi, EntityQizi, CauseDamageData, int, bool> OnArtifactHit;
        internal WorldTimeline.ArtifactBattleOutput ArtifactOutput;
        internal long BattleDamageTaken, BattleHealthLost;

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
                    UpdateTimelineModelPosition();
                }
            }
        }

        public override Vector3 LogicHitPosition => LogicPosition + Vector3.up *
            (TimelineAppearance == TimelineMonsterAnimation.NamelessSteleAppearance ? 1.55f : ConstValue.EntityQiziHeight);

        private const float SaltPullSlideSeconds = .17f;
        private Vector3 saltPullOffset;
        private float saltPullRemaining;

        internal void PlaySaltPullSlide(Vector3 from)
        {
            PlaySaltImpactMotion(from - LogicPosition);
        }

        internal void PlaySaltImpactMotion(Vector3 offset)
        {
            if (GObj == null) return;
            saltPullOffset = offset;
            saltPullRemaining = SaltPullSlideSeconds;
            UpdateTimelineModelPosition();
        }

        private void UpdateTimelineModelPosition()
        {
            float fraction = saltPullRemaining / SaltPullSlideSeconds;
            GObj.transform.position = HotGameEntry.HeroManager.GetTerrainMatchHeight(
                LogicPosition + saltPullOffset * (fraction * fraction));
        }

        internal float HpBarHeight => IsPaperShadow ? 1.4f : IsCoastalMelee ? 1.15f :
            IsCoastalShooter ? 1.55f : TimelineAppearance switch
        {
            TimelineMonsterAnimation.NamelessSteleAppearance => 3.1f,
            TimelineOuterGuardianCast.ClerkAppearance => 2.15f,
            _ => 2f
        };

        internal bool ShowsPlayerHitFeedback { get; set; }

        public void Init(int i,List<int> equipItem = null, bool recordBattleOutput = false)
        {
            IsValid = true;
            Critical = HotGameEntry.HeroManager.CriticalRules ? new WorldTimeline.CriticalFighter() : null;
            ShowsPlayerHitFeedback = false;
            PriorityAttackTarget = null;
            ReservedMoveAnchor = null;
            TechniqueKills = 0;
            LightningMarks?.Clear();
            LightningApplications = LightningTransfers = LightningJumps = 0;
            BattleOutput = recordBattleOutput ? new Dictionary<int, WorldTimeline.SkillBattleOutput>() : null;
            BattleDamageTaken = BattleHealthLost = 0;
            UndertowAttackCount = 0;
            RegionalGuardianAttacks = 0;
            RegionalPounceWarned = false;
            ContractSealSeconds = 0f;
            OuterAttackCount = LanternHitsLeft = MartenMeleeHits = 0;
            OuterGuardianUsed = IsPaperShadow = IsCoastalShooter = IsCoastalMelee = false;
            PaperShadowWaves = 0;
            OuterShellSeconds = RopeSnareSeconds = 0f;
            FrostSlowSeconds = FrostShieldCooldown = 0f;
            EmberHeartUsed = false;
            SaltShieldUsed = SaltChannelDrained = false;
            SaltGuardianAttacks = SaltFirstAttackDelayMs = 0;
            saltPullRemaining = 0f;
            saltPullOffset = Vector3.zero;
            IceBeadHits = 0;
            OuterShellForward = Vector3.zero;
            PaperShadows?.Clear();
            CanOpenWithPounce = IsPounceStrike = false;
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
            var heroes = HotGameEntry.HeroManager;
            heroes.AddHpBar(this);
            if (heroes.IsTimelineBattle)
            {
                string defaultAppearance = BelongCamp == CampType.Friend ? "ShenYan" : "LuYing";
                string appearance = string.IsNullOrEmpty(TimelineAppearance) ? defaultAppearance : TimelineAppearance;
                var prefab = Resources.Load<GameObject>("StoryCharacters/" + appearance);
                if (prefab == null && appearance != defaultAppearance)
                {
                    Debug.LogWarning($"找不到轮回战斗外观 {appearance}，改用 {defaultAppearance}。");
                    prefab = Resources.Load<GameObject>("StoryCharacters/" + defaultAppearance);
                }
                if (prefab == null)
                    throw new System.InvalidOperationException($"找不到轮回战斗默认外观 {defaultAppearance}。");

                ownsTimelineModel = true;
                var model = UnityEngine.Object.Instantiate(prefab, heroes.InstanceRoot);
                if ((appearance is "GateGolem" or TimelineMonsterAnimation.NamelessSteleAppearance or
                    TimelineMonsterAnimation.QiMoAppearance or TimelineRegionalGuardianCast.BadgerAppearance or
                    TimelineRegionalGuardianCast.KilnAppearance or TimelineRegionalGuardianCast.ContractAppearance ||
                    appearance is WorldTimeline.SaltChannel.CrabAppearance or WorldTimeline.SaltChannel.BeastAppearance ||
                    TimelineOuterGuardianCast.SkillFor(appearance) != 0 ||
                    TimelineCaveHazards.SkillFor(appearance) != 0) &&
                    model.TryGetComponent<TimelineMonsterAnimation>(out var monster))
                    monster.CombineRigidParts();
                if (IsCoastalMelee) model.transform.localScale *= .65f;
                if (IsCoastalShooter) model.transform.localScale *= .82f;
                OnGetHeroGObjCallback(model, null);
                return;
            }
            heroes.GetHeroObjByID(HeroID, OnGetHeroGObjCallback);
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
            HasSwordRecall = false; BambooActive = null; BeginEchoCast = null; Critical = null; RedirectHealthLoss = null; ArtifactHealthLost = null; ArtifactOverheal = null; OnAllyHealed = null;
            ClearHpBar();
            GObj = null;
        }
        public void Remove()
        {
            PriorityAttackTarget = null;
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
            PaperShadows?.Clear();
            OnArtifactHit = null; ArtifactOutput = null;
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
            if (RopeSnareSeconds > 0f) RopeSnareSeconds = Mathf.Max(0f, RopeSnareSeconds - elapseSeconds);
            if (FrostSlowSeconds > 0f) FrostSlowSeconds = Mathf.Max(0f, FrostSlowSeconds - elapseSeconds);
            if (FrostShieldCooldown > 0f) FrostShieldCooldown = Mathf.Max(0f, FrostShieldCooldown - elapseSeconds);
            if (HotGameEntry.HeroManager.CaveHazards?.InFrost(this) == true)
                FrostSlowSeconds = Mathf.Max(FrostSlowSeconds, .23f);
            if (OuterShellSeconds > 0f) OuterShellSeconds = Mathf.Max(0f, OuterShellSeconds - elapseSeconds);
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
            if (saltPullRemaining > 0f)
            {
                saltPullRemaining = Mathf.Max(0f, saltPullRemaining - elapseSeconds);
                if (GObj != null) UpdateTimelineModelPosition();
            }
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
            if (PaperShadows != null)
                foreach (var shadow in PaperShadows)
                    if (shadow.IsValid) shadow.OnDead();
            CommandBurnEffect.SpreadOnDeath(this);
            CommandLightningEffect.SpreadOnDeath(this);
            HotGameEntry.HeroManager.OnEntityDead(this);
            //GObj?.SetActive(false);
        }
    }
}
