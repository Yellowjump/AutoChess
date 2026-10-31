
using System;
using System.Collections.Generic;
using DataTable;
using GameFramework;
using SkillSystem;
using Unity.Profiling;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Entity
{
    public partial class EntityQizi
    {
        private static readonly ProfilerMarker TickTriggerMarker =
            new ProfilerMarker("Combat.Unit.TickTrigger");
        private static readonly ProfilerMarker BuffUpdateMarker =
            new ProfilerMarker("Combat.Unit.Buff");
        private static readonly ProfilerMarker AutomaticSkillMarker =
            new ProfilerMarker("Combat.Unit.AutomaticSkill");
        private static readonly ProfilerMarker SfxUpdateMarker =
            new ProfilerMarker("Combat.Unit.Sfx");

        public List<Skill> NormalSkillList = new List<Skill>();
        private int m_NextNormalSkillIndexBeforeInit;
        private int m_CurrentNormalSkillIndexBeforeInit;
        private int m_AttackTargetUidBeforeInit;
        internal int CombatUnitHandle => m_CombatUnitHandle;
        public int NextCastNormalSkillIndex
        {
            get => m_CombatUnitHandle == 0 ? m_NextNormalSkillIndexBeforeInit :
                CombatAttributeBridge.GetNextNormalSkillIndex(m_CombatUnitHandle);
            set
            {
                if (m_CombatUnitHandle == 0) m_NextNormalSkillIndexBeforeInit = value;
                else CombatAttributeBridge.SetNextNormalSkillIndex(m_CombatUnitHandle, value);
            }
        }
        public int CurCastNormalSkillIndex
        {
            get => m_CombatUnitHandle == 0 ? m_CurrentNormalSkillIndexBeforeInit :
                CombatAttributeBridge.GetCurrentNormalSkillIndex(m_CombatUnitHandle);
            set
            {
                if (m_CombatUnitHandle == 0) m_CurrentNormalSkillIndexBeforeInit = value;
                else CombatAttributeBridge.SetCurrentNormalSkillIndex(m_CombatUnitHandle, value);
            }
        }
        public List<Skill> NoAnimAtkSkillList = new List<Skill>();
        public Skill SpSkill;
        public List<Skill> PassiveSkillList = new List<Skill>();
        private readonly Dictionary<int, OneTrigger> m_TriggerByRegistrationId = new Dictionary<int, OneTrigger>();
        public List<Buff> CurBuffList = new List<Buff>();
        private readonly Dictionary<int, Buff> m_BuffByTimerHandle = new Dictionary<int, Buff>();
        private Func<int, bool> m_HasBuff;
        private Action<int> m_DestroyBuffForDamage;
        private CombatUnitSkillPlanBatch m_UnitSkillPlans;
        private bool m_UsePreparedSkillDecisions;
        private bool m_UsePreparedAutomaticSkills;
        private Func<int, int> m_GetAutomaticCooldownHandle;
        private Action<int> m_AddAutomaticIntent;
        private Action<int> m_CastAutomaticSkillAction;

        public void PrepareSkillDecisions()
        {
            if (m_CombatUnitHandle == 0)
            {
                return;
            }
            m_UsePreparedSkillDecisions = false;
            m_UsePreparedAutomaticSkills = false;
            if (!IsValid || fsm == null ||
                !CombatBoardBridge.FindOccupant(HeroUID, out _, out _))
            {
                return;
            }
            CombatAutomaticSkillIntentBridge.Clear(m_CombatUnitHandle);
            if (fsm.CurrentState is StateIdle0 ||
                fsm.CurrentState is StateMove0)
            {
                m_UsePreparedSkillDecisions = true;
            }
            CombatSkillSlotBridge.Execute(m_CombatUnitHandle,
                CombatSkillSlotKind.Automatic,
                m_GetAutomaticCooldownHandle ??= GetAutomaticCooldownHandle,
                m_AddAutomaticIntent ??= AddAutomaticIntent);
            m_UsePreparedAutomaticSkills = true;
        }

        private int GetAutomaticCooldownHandle(int index) =>
            NoAnimAtkSkillList[index].CooldownHandle;

        private void AddAutomaticIntent(int index) =>
            CombatAutomaticSkillIntentBridge.Add(m_CombatUnitHandle, index);
        public EntityQizi CurAttackTarget
        {
            get
            {
                int targetUid = m_CombatUnitHandle == 0 ? m_AttackTargetUidBeforeInit :
                    CombatAttributeBridge.GetAttackTargetUid(m_CombatUnitHandle);
                return targetUid == 0 ? null : HotGameEntry.HeroManager.GetEntityByUID(targetUid);
            }
            set
            {
                int targetUid = value == null ? 0 : value.HeroUID;
                if (m_CombatUnitHandle == 0) m_AttackTargetUidBeforeInit = targetUid;
                else CombatAttributeBridge.SetAttackTargetUid(m_CombatUnitHandle, targetUid);
            }
        }
        /// <summary>
        /// 普攻动画加速时间
        /// </summary>
        /*public float CurNormalAtkAniRate
        {
            get
            {
                if (NormalSkill.DefaultAnimationDurationMs > 1 / AtkSpeed)
                {
                    return NormalSkill.DefaultAnimationDurationMs * AtkSpeed;
                }
                return 1;
            }
        }*/
        
        private void InitSkill()
        {
            InitItemSkillAndAttr();
            InitHeroTableSkill();
            CastPassiveSkill();
        }

        private void InitItemSkillAndAttr()
        {
            var heroTable = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (!heroTable.HasDataRow(HeroID))
            {
                Log.Error($"heroID:{HeroID} invalid no match TableRow");
                return;
            }
            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            foreach (var oneItem in EquipItemList)
            {
                if (!itemTable.HasDataRow(oneItem.ItemID))
                {
                    Log.Error($"Error ItemID{oneItem}");
                    continue;
                }

                var itemData = itemTable[oneItem.ItemID];
                if (itemData.SkillID != 0)
                {
                    var skillID = itemData.SkillID;
                    var oneNewSkill = AddOneNewSkill(skillID);
                    if (oneNewSkill == null)
                    {
                        continue;
                    }
                    oneNewSkill.FromItemID = oneItem.ItemID;
                    oneNewSkill.FromItemUID = oneItem.UniqueID;
                    if (oneNewSkill.CurSkillType == SkillType.NormalSkill)
                    {
                        NormalSkillList.Add(oneNewSkill);
                        RegisterCombatSkillSlot(oneNewSkill, CombatSkillSlotKind.Normal,
                            NormalSkillList.Count - 1);
                    }
                    else if (oneNewSkill.CurSkillType == SkillType.PassiveSkill)
                    {
                        PassiveSkillList.Add(oneNewSkill);
                        RegisterCombatSkillSlot(oneNewSkill, CombatSkillSlotKind.Passive,
                            PassiveSkillList.Count - 1);
                    }
                    else if (oneNewSkill.CurSkillType == SkillType.NoAnimSkill)
                    {
                        NoAnimAtkSkillList.Add(oneNewSkill);
                        RegisterCombatSkillSlot(oneNewSkill, CombatSkillSlotKind.Automatic,
                            NoAnimAtkSkillList.Count - 1);
                    }
                }
                if (itemData.AttrAdd != null && itemData.AttrAdd.Count != 0)
                {
                    //添加属性
                    foreach (var typeAndValue in itemData.AttrAdd)
                    {
                        TryAddAttrValue((AttributeType)typeAndValue.Item1, typeAndValue.Item2);
                        if (typeAndValue.Item1 == (int)AttributeType.MaxHp)
                        {
                            TryAddAttrValue(AttributeType.Hp, typeAndValue.Item2);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 初始化被动技能
        /// </summary>
        public void InitHeroTableSkill()
        {
            var heroTable = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (!heroTable.HasDataRow(HeroID))
            {
                Log.Error($"heroID:{HeroID} invalid no match TableRow");
                return;
            }

            var heroTableData = heroTable[HeroID];
            var passiveSkillID = heroTableData.PassiveSkillID;
            var passiveSkill = AddOneNewSkill(passiveSkillID);
            if (passiveSkill != null && passiveSkill.CurSkillType == SkillType.PassiveSkill)
            {
                PassiveSkillList.Add(passiveSkill);
                RegisterCombatSkillSlot(passiveSkill, CombatSkillSlotKind.Passive,
                    PassiveSkillList.Count - 1);
            }

            var spSkillID = heroTableData.SpSkillID;
            var oneSpSkill = AddOneNewSkill(spSkillID);
            if (oneSpSkill != null && oneSpSkill.CurSkillType == SkillType.SpSkill)
            {
                SpSkill = oneSpSkill;
                RegisterCombatSkillSlot(oneSpSkill, CombatSkillSlotKind.Special, 0);
            }
        }

        private void RegisterCombatSkillSlot(Skill skill, CombatSkillSlotKind kind, int index)
        {
            CombatSkillSlotBridge.Add(m_CombatUnitHandle, kind, index, skill.SkillID,
                skill.CastPower, skill.SkillRange, (int)skill.CurSkillCastTargetType);
        }

        private Skill AddOneNewSkill(int skillID)
        {
            if (skillID != 0)
            {
                var skillTable = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
                if (!skillTable.HasDataRow(skillID))
                {
                    Log.Error($"heroID:{HeroID} skillID{skillID} invalid no match TableRow");
                    return null;
                }

                //持有技能
                var skillTableData = skillTable[skillID];
                var skillTemplates = GameEntry.DataTable.GetDataTable<DRSkillTemplate>("SkillTemplate");
                if (!skillTemplates.HasDataRow(skillTableData.TemplateID))
                {
                    Log.Error($"skillID{skillID} no match Template{skillTableData.TemplateID}");
                    return null;
                }
                var oneSkill = SkillFactory.CreateNewSkill();
                
                oneSkill.SkillID = skillID;
                oneSkill.CurSkillType = (SkillType)skillTableData.SkillType;
                oneSkill.SkillRange = skillTableData.SkillRange;
                oneSkill.Caster = this;
                oneSkill.DefaultAnimationDurationMs = skillTableData.AniDuration;
                oneSkill.DefaultShakeBeforeMs = skillTableData.BeforeShakeEndMs;
                oneSkill.CurSkillCastTargetType = (SkillCastTargetType)skillTableData.TargetType;
                oneSkill.DefaultSkillCDMs = skillTableData.CDMs;
                oneSkill.CastPower = skillTableData.CastPower;
                var temp = skillTemplates[skillTableData.TemplateID].SkillTemplate;
                temp.Clone(oneSkill);
                oneSkill.Owner = this;
                oneSkill.SetSkillValue(skillTableData);
                return oneSkill;
            }
            return null;
        }

        public Skill GetSkillByItemUniqID(int itemUid)
        {
            foreach (var passiveSkill in PassiveSkillList)
            {
                if (passiveSkill.FromItemUID == itemUid)
                {
                    return passiveSkill;
                }
            }
            foreach (var noAnimSkill in NoAnimAtkSkillList)
            {
                if (noAnimSkill.FromItemUID == itemUid)
                {
                    return noAnimSkill;
                }
            }
            foreach (var normalSkill in NormalSkillList)
            {
                if (normalSkill.FromItemUID == itemUid)
                {
                    return normalSkill;
                }
            }

            return null;
        }
        public void AddTriggerListen(OneTrigger oneTrigger)
        {
            if (oneTrigger == null)
            {
                return;
            }
            if (!CombatTriggerIndex.TryCreateOwner(m_CombatUnitHandle) ||
                !CombatTriggerIndex.TryAdd(m_CombatUnitHandle, (int)oneTrigger.CurTriggerType,
                    out int registrationId))
            {
                throw new System.InvalidOperationException("Combat trigger registration requires a live ECS unit.");
            }
            m_TriggerByRegistrationId.Add(registrationId, oneTrigger);
        }
        public void RemoveTriggerListen(OneTrigger oneTrigger)
        {
            if (oneTrigger == null || !CombatTriggerIndex.IsOwnerValid(m_CombatUnitHandle))
            {
                return;
            }
            var matches = ListPool<int>.Get();
            try
            {
                CombatTriggerIndex.TryCollect(m_CombatUnitHandle, (int)oneTrigger.CurTriggerType, matches);
                foreach (int registrationId in matches)
                {
                    if (m_TriggerByRegistrationId.TryGetValue(registrationId, out var registered) &&
                        ReferenceEquals(registered, oneTrigger))
                    {
                        CombatTriggerIndex.Remove(m_CombatUnitHandle, registrationId);
                        m_TriggerByRegistrationId.Remove(registrationId);
                        return;
                    }
                }
            }
            finally
            {
                ListPool<int>.Release(matches);
            }
        }
        
        public void OnTrigger(TriggerType triggerType, object arg = null)
        {
            if (m_TriggerByRegistrationId.Count == 0)
            {
                return;
            }
            var matches = ListPool<int>.Get();
            try
            {
                if (!CombatTriggerIndex.TryCollect(m_CombatUnitHandle, (int)triggerType, matches))
                {
                    throw new System.InvalidOperationException("Combat trigger index was lost while the unit is active.");
                }
                foreach (int registrationId in matches)
                {
                    if (m_TriggerByRegistrationId.TryGetValue(registrationId, out var trigger))
                    {
                        trigger.OnTrigger(arg);
                    }
                }
            }
            finally
            {
                ListPool<int>.Release(matches);
            }
        }

        public override void AddBuff(Buff buff)
        {
            if (buff == null)
            {
                return;
            }
            buff.BindCombatOwner(m_CombatUnitHandle);
            m_BuffByTimerHandle[buff.TimerHandle] = buff;
            // todo 判断是否有 免疫之类的判断
            if (!CombatBuffTimerBridge.PrepareIncomingStack(buff.TimerHandle,
                    m_HasBuff ??= HasBuff, out int removeHandle))
            {
                return;
            }
            if (removeHandle != 0 && m_BuffByTimerHandle.TryGetValue(removeHandle,
                    out Buff buffToRemove))
            {
                buffToRemove.OnDestory();
            }
            CurBuffList.Add(buff);
            CombatBuffTimerBridge.SetActive(buff.TimerHandle, true);
            buff.ActivateTimer();
            buff.OnActive();
        }

        public void OnAuraBuffDestroy(Buff buff)
        {
            int nextHandle = CombatBuffTimerBridge.RemoveAura(buff.TimerHandle,
                out bool wasActive);
            if (!wasActive && !CurBuffList.Contains(buff))
            {
                CombatBuffTimerBridge.QueueDeferredRelease(m_CombatUnitHandle,
                    buff.TimerHandle);
            }
            if (nextHandle != 0 && m_BuffByTimerHandle.TryGetValue(nextHandle, out Buff nextBuff))
            {
                CurBuffList.Add(nextBuff);
                CombatBuffTimerBridge.SetActive(nextHandle, true);
                nextBuff.ActivateTimer();
                nextBuff.OnActive();
            }
        }
        public override void RemoveBuff(int buffID)
        {
            List<int> handles = ListPool<int>.Get();
            CombatBuffTimerBridge.CollectActiveById(m_CombatUnitHandle, buffID, handles);
            foreach (int handle in handles)
            {
                if (m_BuffByTimerHandle.TryGetValue(handle, out Buff buff))
                {
                    buff.OnDestory();
                }
            }
            ListPool<int>.Release(handles);
        }
        private void CastPassiveSkill()
        {
            CombatSkillSlotBridge.Execute(m_CombatUnitHandle, CombatSkillSlotKind.Passive,
                null, index => PassiveSkillList[index].Cast());
        }
        private void UpdateSkill(float elapseSeconds, float realElapseSeconds)
        {
            using (TickTriggerMarker.Auto())
            {
                OnTrigger(TriggerType.PerTick);
            }
            using (BuffUpdateMarker.Auto())
            {
                UpdateBuffTime(elapseSeconds, realElapseSeconds);
            }
            using (AutomaticSkillMarker.Auto())
            {
                if (m_UsePreparedAutomaticSkills)
                {
                    CombatAutomaticSkillIntentBridge.Execute(m_CombatUnitHandle,
                        m_CastAutomaticSkillAction ??= CastAutomaticSkill);
                }
                else
                {
                    CombatSkillSlotBridge.Execute(m_CombatUnitHandle,
                        CombatSkillSlotKind.Automatic,
                        m_GetAutomaticCooldownHandle ??= GetAutomaticCooldownHandle,
                        m_CastAutomaticSkillAction ??= CastAutomaticSkill);
                }
            }
            using (SfxUpdateMarker.Auto())
            {
                CombatAttributeBridge.QueueSfxTick(m_CombatUnitHandle,
                    elapseSeconds, _sfxList.Count != 0);
            }
        }

        private void CastAutomaticSkill(int index)
        {
            Skill noAnimSkill = NoAnimAtkSkillList[index];
            noAnimSkill.OnDestory();
            noAnimSkill.Cast();
        }

        private void UpdateBuffTime(float elapseSeconds, float realElapseSeconds)
        {
            List<int> dueHandles = ListPool<int>.Get();
            try
            {
                CombatBuffTimerBridge.CollectDueForOwner(m_CombatUnitHandle,
                    dueHandles);
                foreach (int handle in dueHandles)
                {
                    if (!m_BuffByTimerHandle.TryGetValue(handle, out Buff buff) ||
                        !buff.IsValid) continue;
                    if (buff.UpdateTimer(elapseSeconds))
                        buff.OnDestory();
                }
            }
            finally
            {
                ListPool<int>.Release(dueHandles);
            }
            for (var i = CurBuffList.Count - 1; i >= 0; i--)
            {
                if (CurBuffList[i].IsValid == false)
                {
                    m_BuffByTimerHandle.Remove(CurBuffList[i].TimerHandle);
                    ReferencePool.Release(CurBuffList[i]);
                    CurBuffList.RemoveAt(i);
                }
            }
            List<int> deferredHandles = ListPool<int>.Get();
            try
            {
                CombatBuffTimerBridge.DrainDeferredReleases(m_CombatUnitHandle,
                    deferredHandles);
                foreach (int handle in deferredHandles)
                {
                    if (m_BuffByTimerHandle.TryGetValue(handle, out Buff buff))
                    {
                        m_BuffByTimerHandle.Remove(handle);
                        ReferencePool.Release(buff);
                    }
                }
            }
            finally
            {
                ListPool<int>.Release(deferredHandles);
            }
        }
        public CheckCastSkillResult CheckCanCastSkill(out EntityQizi target,int normalSkillIndex = 0)
        {
            target = null;
            if (normalSkillIndex < 0 || normalSkillIndex >= NormalSkillList.Count ||
                NormalSkillList[normalSkillIndex] == null)
                return CheckCastSkillResult.Error;
            if (m_UsePreparedSkillDecisions &&
                CombatUnitSkillIntentBridge.TryGet(m_CombatUnitHandle,
                    normalSkillIndex, out CombatUnitSkillIntent intent))
            {
                target = intent.TargetUid == 0 ? null :
                    HotGameEntry.HeroManager.GetEntityByUID(intent.TargetUid);
                return target == null && intent.TargetUid != 0
                    ? CheckCastSkillResult.NoValidTarget
                    : (CheckCastSkillResult)intent.Result;
            }
            Skill willCastSkill = null;
            if (normalSkillIndex >= 0 && normalSkillIndex < NormalSkillList.Count)
            {
                willCastSkill = NormalSkillList[normalSkillIndex];
            }
            if (willCastSkill == null || !CombatSkillSlotBridge.TryGetNormal(
                    m_CombatUnitHandle, normalSkillIndex, out CombatSkillSlot slot))
            {
                return CheckCastSkillResult.Error;
            }
            bool hasPower = m_UnitSkillPlans == null
                ? CombatAttributeBridge.HasNormalSkillPower(m_CombatUnitHandle,
                    slot.CastPower)
                : m_UnitSkillPlans.HasPower(HeroUID, slot.CastPower);
            if (!hasPower)
            {
                return CheckCastSkillResult.NoPower;
            }
            var inAttackRange = false;
            if (slot.TargetType != (int)SkillCastTargetType.NoNeedTarget)
            {
                bool canUseOldTarget = false;
                EntityQizi oldTarget = m_UnitSkillPlans == null
                    ? CurAttackTarget
                    : HotGameEntry.HeroManager.GetEntityByUID(
                        m_UnitSkillPlans.GetAttackTargetUid(HeroUID));
                if (oldTarget != null && oldTarget.IsValid)
                {
                    //如果之前存在攻击目标
                    //判断是否已死亡
                    //判断是否在范围内
                    if (m_UnitSkillPlans == null)
                    {
                        int hp = CombatAttributeBridge.GetValue(
                            oldTarget.CombatUnitHandle, (int)AttributeType.Hp);
                        canUseOldTarget = hp >= 0 &&
                            CombatTargetQueries.IsInNormalSkillRange(m_CombatUnitHandle,
                                oldTarget.CombatUnitHandle, slot.RangeMm);
                    }
                    else
                    {
                        canUseOldTarget = m_UnitSkillPlans.IsOldTargetInRange(
                            HeroUID, oldTarget.HeroUID, slot.RangeMm);
                    }
                    if (canUseOldTarget)
                    {
                        inAttackRange = true;
                        target = oldTarget;
                    }
                }

                if (canUseOldTarget == false)
                {
                    //之前目标不可用，重新选择目标
                    inAttackRange = GetSkillNewTarget(out target,normalSkillIndex);
                }
                if (target == null)
                {
                    return CheckCastSkillResult.NoValidTarget;
                }
                else if (inAttackRange == false)
                {
                    return CheckCastSkillResult.TargetOutRange;
                }
            }
            /*if (willCastSkill.InCD)
            {
                return CheckCastSkillResult.NormalAtkWait;
            }*/
            return CheckCastSkillResult.CanCast;
        }
        /// <summary>
        /// 获取技能目标以及是否在攻击范围内
        /// </summary>
        /// <param name="target">技能目标</param>
        /// <param name="isSpSkill"></param>
        /// <returns>是否在攻击范围内</returns>
        public bool GetSkillNewTarget(out EntityQizi target,int normalSkillIndex =0)
        {
            target = null;
            //判断目标
            if (normalSkillIndex < 0 || normalSkillIndex >= NormalSkillList.Count ||
                NormalSkillList[normalSkillIndex] == null ||
                !CombatSkillSlotBridge.TryGetNormal(m_CombatUnitHandle,
                    normalSkillIndex, out CombatSkillSlot slot))
            {
                return false;
            }
            switch ((SkillCastTargetType)slot.TargetType)
            {
                case SkillCastTargetType.NearestEnemy:
                    int targetUid;
                    bool withinRange = m_UnitSkillPlans == null
                        ? CombatTargetQueries.FindNearest(m_CombatUnitHandle,
                            (int)CampType.Enemy, slot.RangeMm, out targetUid)
                        : m_UnitSkillPlans.FindNearest(HeroUID, slot.RangeMm,
                            out targetUid);
                    if (targetUid != 0)
                    {
                        target = HotGameEntry.HeroManager.GetEntityByUID(targetUid);
                    }
                    if (target != null && !target.IsValid)
                    {
                        target = null;
                    }
                    return withinRange && target != null;
                case SkillCastTargetType.NoNeedTarget:
                    return true;
            }
            return false;
        }

        public void CastNormalSkill(int normalSkillIndex = 0)
        {
            if (normalSkillIndex >= 0 && normalSkillIndex < NormalSkillList.Count)
            {
                CombatAttributeBridge.BeginNormalSkill(m_CombatUnitHandle,
                    NormalSkillList.Count, normalSkillIndex);
                NormalSkillList[normalSkillIndex].Cast();
            }
        }

        public void BeCauseDamage(CauseDamageData damageData)
        {
            if (damageData == null)
            {
                return;
            }
            bool missingShield = CombatBuffTimerBridge.ResolveIncomingDamage(
                m_CombatUnitHandle, (int)damageData.DamageValue,
                m_HasBuff ??= HasBuff,
                m_DestroyBuffForDamage ??= DestroyBuffForDamage);
            if (missingShield)
            {
                Log.Error("No Shield But AttrShield Has Value");
            }

            if (damageData.TriggerDamageRelate)
            {
                damageData.Caster.OnTrigger(TriggerType.AfterCauseDamage,damageData);
                OnTrigger(TriggerType.AfterBeCauseDamage,damageData);
            }
            UnityEngine.Vector3 damagePosition = LogicPosition +
                UnityEngine.Vector3.up * 0.5f;
            CombatBoardBridge.QueueDamageNumber((int)damageData.DamageValue,
                damagePosition.x, damagePosition.y, damagePosition.z);
            
            int curHp = CombatAttributeBridge.GetValue(m_CombatUnitHandle, (int)AttributeType.Hp);
            if (curHp == 0)
            {
                //death
                OnDead();
            }
        }

        private bool HasBuff(int handle) =>
            m_BuffByTimerHandle.ContainsKey(handle);

        private void DestroyBuffForDamage(int handle)
        {
            if (m_BuffByTimerHandle.TryGetValue(handle, out Buff buff))
                buff.OnDestory();
        }

        private void DestorySkill()
        {
            CurAttackTarget = null;
            if (NormalSkillList != null)
            {
                foreach (var oneSkill in NormalSkillList)
                {
                    ReferencePool.Release(oneSkill);
                }
                NormalSkillList.Clear();
            }
            if (NoAnimAtkSkillList != null)
            {
                foreach (var oneSkill in NoAnimAtkSkillList)
                {
                    ReferencePool.Release(oneSkill);
                }
                NoAnimAtkSkillList.Clear();
            }
            if (SpSkill != null)
            {
                ReferencePool.Release(SpSkill);
                SpSkill = null;
            }
            if (PassiveSkillList != null)
            {
                foreach (var oneSkill in PassiveSkillList)
                {
                    ReferencePool.Release(oneSkill);
                }
                PassiveSkillList.Clear();
            }

            foreach (Buff buff in m_BuffByTimerHandle.Values)
            {
                ReferencePool.Release(buff);
            }
            m_BuffByTimerHandle.Clear();
            CurBuffList.Clear();
            CombatTriggerIndex.Release(m_CombatUnitHandle);
            m_TriggerByRegistrationId.Clear();
        }
    }
}
