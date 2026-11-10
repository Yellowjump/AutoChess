
using System.Collections.Generic;
using System.Linq;
using DataTable;
using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace Entity
{
    internal static class BattleStatuses
    {
        internal static readonly (BuffTag Tag, string Chinese, string English, string Color)[] Display =
        {
            (BuffTag.Bleeding, "流血", "Bleed", "#FFA69C"),
            (BuffTag.Burning, "燃烧", "Burn", "#FFC07A"),
            (BuffTag.Weakened, "虚弱", "Weakness", "#D3B8FF"),
            (BuffTag.Exposed, "破绽", "Opening", "#F9CB91"),
            (BuffTag.HeavyDamage, "重伤", "Wounded", "#F5B5C0"),
            (BuffTag.Frozen, "冰冻", "Frozen", "#A6DBFF"),
            (BuffTag.Stunned, "眩晕", "Stunned", "#E8D59A"),
            (default, "雷印", "Lightning Mark", "#C8C5FF")
        };

        internal static void Read(EntityQizi owner, long[] counts)
        {
            System.Array.Clear(counts, 0, counts.Length);
            if (!owner.IsValid) return;
            foreach (var buff in owner.CurBuffList)
            {
                if (!buff.IsValid || buff.StackCount <= 0) continue;
                for (int i = 0; i < Display.Length - 1; i++)
                    if (buff.CheckBuffTag(Display[i].Tag)) counts[i] += buff.StackCount;
            }
            if (owner.LightningMarks != null)
                foreach (int count in owner.LightningMarks.Values)
                    counts[Display.Length - 1] += count;
        }
    }

    public partial class EntityQizi
    {
        private const int TideBellSkillId = 9104;
        public List<Skill> NormalSkillList = new List<Skill>();
        public int NextCastNormalSkillIndex = 0;
        public int CurCastNormalSkillIndex = 0;
        public List<Skill> NoAnimAtkSkillList = new List<Skill>();
        public Skill SpSkill;
        public List<Skill> PassiveSkillList = new List<Skill>();
        public Dictionary<TriggerType, List<OneTrigger>> CurTriggerDic = new Dictionary<TriggerType, List<OneTrigger>>();
        public List<Buff> CurBuffList = new List<Buff>();
        public Dictionary<int, List<Buff>> AuraStack = new Dictionary<int, List<Buff>>();
        public EntityQizi CurAttackTarget;
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
            if (TechniqueSkillId != 0)
                InitTechniqueSkill();
            CastPassiveSkill();
        }

        private Skill techniqueSkill;
        internal bool IsTechniqueSkill(Skill skill) => skill != null && ReferenceEquals(skill, techniqueSkill);

        private void InitTechniqueSkill()
        {
            var skill = AddOneNewSkill(TechniqueSkillId);
            if (skill == null)
                throw new System.InvalidOperationException($"Invalid technique combat skill {TechniqueSkillId}.");
            if (skill.CurSkillType != SkillType.NormalSkill)
            {
                ReferencePool.Release(skill);
                throw new System.InvalidOperationException($"Technique skill {TechniqueSkillId} is not a normal skill.");
            }
            techniqueSkill = skill;

            // Some skill templates use the source item for reach, weapon visuals or cast parameters.
            foreach (var item in GameEntry.DataTable.GetDataTable<DRItem>("Item").GetAllDataRows())
                if (item.SkillID == TechniqueSkillId &&
                    (techniqueSkill.FromItemID == 0 || item.Id < techniqueSkill.FromItemID))
                    techniqueSkill.FromItemID = item.Id;
            NormalSkillList.Add(techniqueSkill);
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
            var skillTable = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
            var stages = TechniqueSkillId == 0 ? null :
                GameEntry.DataTable.GetDataTable<DRTechniqueStage>("TechniqueStage")?.GetAllDataRows();
            var activeStage = stages?.FirstOrDefault(stage => stage.SkillId == TechniqueSkillId);
            foreach (var oneItem in EquipItemList)
            {
                if (!itemTable.HasDataRow(oneItem.ItemID))
                {
                    Log.Error($"Error ItemID{oneItem}");
                    continue;
                }

                var itemData = itemTable[oneItem.ItemID];
                var itemSkill = skillTable.GetDataRow(itemData.SkillID);
                bool replacesSkill = TechniqueSkillId != 0 && itemSkill != null &&
                    (itemSkill.Id == TechniqueSkillId ||
                     ((SkillType)itemSkill.SkillType == SkillType.NormalSkill && itemSkill.CastPower < 0) ||
                     (activeStage != null && stages.Any(stage => stage.TechniqueId == activeStage.TechniqueId &&
                         stage.SkillId == itemSkill.Id)));
                if (itemData.SkillID != 0 && !replacesSkill)
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
                    }
                    else if (oneNewSkill.CurSkillType == SkillType.PassiveSkill)
                    {
                        PassiveSkillList.Add(oneNewSkill);
                    }
                    else if (oneNewSkill.CurSkillType == SkillType.NoAnimSkill)
                    {
                        NoAnimAtkSkillList.Add(oneNewSkill);
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
            }

            var spSkillID = heroTableData.SpSkillID;
            var oneSpSkill = AddOneNewSkill(spSkillID);
            if (oneSpSkill != null && oneSpSkill.CurSkillType == SkillType.SpSkill)
            {
                SpSkill = oneSpSkill;
            }
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
                // Legacy table rows use a negative value to identify basic attacks.
                oneSkill.IsBasicAttack = oneSkill.CurSkillType == SkillType.NormalSkill && skillTableData.CastPower < 0;
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
            if (oneTrigger == null||CurTriggerDic == null)
            {
                return;
            }
            var triggerType = oneTrigger.CurTriggerType;
            if (!CurTriggerDic.ContainsKey(triggerType))
            {
                CurTriggerDic.Add(triggerType,ListPool<OneTrigger>.Get());
            }

            var curTypeList = CurTriggerDic[triggerType];
            curTypeList.Add(oneTrigger);
        }
        public void RemoveTriggerListen(OneTrigger oneTrigger)
        {
            if (oneTrigger == null||CurTriggerDic == null)
            {
                return;
            }
            var triggerType = oneTrigger.CurTriggerType;
            if (!CurTriggerDic.ContainsKey(triggerType))
            {
                return;
            }

            var curTypeList = CurTriggerDic[triggerType];
            curTypeList.Remove(oneTrigger);
            if (curTypeList.Count == 0)
            {
                CurTriggerDic.Remove(triggerType);
                ListPool<OneTrigger>.Release(curTypeList);
            }
        }
        
        public void OnTrigger(TriggerType triggerType, object arg = null)
        {
            if (CurTriggerDic == null||!CurTriggerDic.ContainsKey(triggerType)||CurTriggerDic[triggerType]==null||CurTriggerDic[triggerType].Count==0)
            {
                return;
            }

            var typeList = CurTriggerDic[triggerType];
            foreach (var oneTrigger in typeList)
            {
                oneTrigger.OnTrigger(arg);
            }
        }

        public override void AddBuff(Buff buff)
        {
            if (buff == null)
            {
                return;
            }
            if (buff.CanMergeStacks)
            {
                foreach (var activeBuff in CurBuffList)
                {
                    if (!activeBuff.IsValid || activeBuff.BuffID != buff.BuffID || !activeBuff.CanMergeStacks) continue;
                    activeBuff.AddMarkLayer();
                    ReferencePool.Release(buff);
                    return;
                }
            }
            // todo 判断是否有 免疫之类的判断
            if (buff.MaxLayerNum != 0)//有层数限制
            {
                if (buff.MaxLayerNum < 0)//是光环
                {
                    if (!AuraStack.ContainsKey(buff.BuffID))
                    {
                        var buffList = ListPool<Buff>.Get();
                        buffList.Add(buff);
                        AuraStack.Add(buff.BuffID,buffList);
                    }
                    else
                    {
                        var buffList = AuraStack[buff.BuffID];
                        buffList.Add(buff);//之前有该光环效果只进入光环队列不触发
                        return;
                    }
                }
                else
                {
                    var buffCount = 0;
                    var minRemainingTime = float.MaxValue;
                    Buff buffToRemove = null;
                    foreach (var b in CurBuffList)
                    {
                        if (b.BuffID == buff.BuffID&&b.IsValid)
                        {
                            buffCount++;
                            // 如果已存在同 buffID 的 buff，且剩余时间最少的 buff 未找到或当前 buff 剩余时间更少
                            if (b.RemainMs < minRemainingTime)
                            {
                                minRemainingTime = b.RemainMs;
                                buffToRemove = b;
                            }
                            if (buff.FreshOtherBuffDuration)
                            {
                                b.RemainMs = b.DurationMs;//时间刷新重置
                            }
                        }
                    }
                    // 检查是否超过层数限制
                    if (buffCount >= buff.MaxLayerNum && buffToRemove != null)
                    {
                        buffToRemove.OnDestory();
                    }
                }
            }
            CurBuffList.Add(buff);
            buff.OnActive();
        }

        public void OnAuraBuffDestroy(Buff buff)
        {
            if (AuraStack.ContainsKey(buff.BuffID))
            {
                var buffList = AuraStack[buff.BuffID];
                if (buffList.Contains(buff))
                {
                    var index = buffList.FindIndex((bu)=>bu==buff);
                    if (index == 0)
                    {
                        buffList.Remove(buff);
                        if (buffList.Count == 0)
                        {
                            AuraStack.Remove(buff.BuffID);
                            ListPool<Buff>.Release(buffList);
                        }
                        else
                        {
                            var oneBuff = buffList[0];
                            CurBuffList.Add(buff);
                            oneBuff.OnActive();
                        }
                    }
                    else
                    {
                        buffList.Remove(buff);
                    }
                }
            }
        }
        public override void RemoveBuff(int buffID)
        {
            foreach (var oneBuff in CurBuffList)
            {
                if (oneBuff.BuffID == buffID)
                {
                    oneBuff.OnDestory();
                }
            }
        }
        private void CastPassiveSkill()
        {
            foreach (var passiveSkill in PassiveSkillList)
            {
                passiveSkill.Cast();
            }
        }
        private void UpdateSkill(float elapseSeconds, float realElapseSeconds)
        {
            if (CurTriggerDic != null && CurTriggerDic.TryGetValue(TriggerType.PerTick, out var perTickList))
            {
                if (perTickList != null && perTickList.Count > 0)
                {
                    List<OneTrigger> tempList = ListPool<OneTrigger>.Get();
                    tempList.AddRange(perTickList);
                    foreach (var oneT in tempList)
                    {
                        oneT.OnTrigger();
                    }
                    ListPool<OneTrigger>.Release(tempList);
                }
            }
            UpdateBuffTime(elapseSeconds, realElapseSeconds);
            foreach (var oneNormalSkill in NormalSkillList)
            {
                oneNormalSkill.LogicUpdateCD(elapseSeconds,realElapseSeconds);
            }

            foreach (var noAnimSkill in NoAnimAtkSkillList)
            {
                noAnimSkill.LogicUpdateCD(elapseSeconds,realElapseSeconds);
                if (!noAnimSkill.InCD)
                { 
                    noAnimSkill.OnDestory();
                    noAnimSkill.Cast();
                }
            }

            UpdateSfxRemainTime(elapseSeconds, realElapseSeconds);
        }

        private void UpdateBuffTime(float elapseSeconds, float realElapseSeconds)
        {
            List<Buff> tempBuffList = ListPool<Buff>.Get();
            tempBuffList.AddRange(CurBuffList);
            foreach (var buff in tempBuffList)
            {
                if (buff.IsValid == false)
                {
                    continue;
                }
                if (buff.HasDamageLayers) buff.TickDamageLayers(elapseSeconds * 1000f);
                else buff.AdvanceDuration(elapseSeconds * 1000f);
                if (!IsValid) break;
            }
            ListPool<Buff>.Release(tempBuffList);
            for (var i = CurBuffList.Count - 1; i >= 0; i--)
            {
                if (CurBuffList[i].IsValid == false)
                {
                    ReferencePool.Release(CurBuffList[i]);
                    CurBuffList.RemoveAt(i);
                }
            }
        }
        public CheckCastSkillResult CheckCanCastSkill(out EntityQizi target,int normalSkillIndex = 0)
        {
            target = null;
            Skill willCastSkill = null;
            if (normalSkillIndex < NormalSkillList.Count)
            {
                willCastSkill = NormalSkillList[normalSkillIndex];
            }
            if (willCastSkill == null)
            {
                return CheckCastSkillResult.Error;
            }
            var inAttackRange = false;
            if (willCastSkill.CurSkillCastTargetType != SkillCastTargetType.NoNeedTarget)
            {
                bool canUseOldTarget = false;
                if (CurAttackTarget != null&&CurAttackTarget.IsValid)
                {
                    //如果之前存在攻击目标
                    //判断是否已死亡
                    //判断是否在范围内
                    int hp = (int)CurAttackTarget.GetAttribute(AttributeType.Hp).GetFinalValue();
                    canUseOldTarget = hp >= 0;
                    //判断是否有不可选中之类
                    var heroManager = HotGameEntry.HeroManager;
                    canUseOldTarget = canUseOldTarget && heroManager.IsInSkillRange(
                        heroManager.GetCombatDistanceSquare(this, CurAttackTarget), willCastSkill.SkillRange);
                    if (canUseOldTarget)
                    {
                        inAttackRange = true;
                        target = CurAttackTarget;
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
            Skill willCastSkill = null;
            //判断目标
            if (normalSkillIndex < NormalSkillList.Count)
            {
                willCastSkill = NormalSkillList[normalSkillIndex];
            }
            if (willCastSkill == null)
            {
                return false;
            }
            switch (willCastSkill.CurSkillCastTargetType)
            {
                case SkillCastTargetType.NearestEnemy:
                    return HotGameEntry.HeroManager.GetNearestTarget(this,CampType.Enemy,out target,willCastSkill.SkillRange);
                case SkillCastTargetType.NoNeedTarget:
                    return true;
            }
            return false;
        }

        public void CastNormalSkill(int normalSkillIndex = 0)
        {
            if (normalSkillIndex < NormalSkillList.Count)
            {
                CurCastNormalSkillIndex = normalSkillIndex;
                NextCastNormalSkillIndex = normalSkillIndex + 1;
                if (NextCastNormalSkillIndex == NormalSkillList.Count)
                {
                    NextCastNormalSkillIndex = 0;
                }
                NormalSkillList[normalSkillIndex].Cast();
            }
        }

        public int BeCauseDamage(CauseDamageData damageData)
        {
            if (damageData == null)
            {
                return 0;
            }
            //todo 计算护盾
            var hudunAttr = GetAttribute(AttributeType.HuDun);
            var hpAttr = GetAttribute(AttributeType.Hp);
            if (damageData.TriggerDamageRelate && damageData.Caster != null)
            {
                var weakness = damageData.Caster.FindStatusBuff(BuffTag.Weakened, CommandWeaknessEffect.WeaknessBuffId);
                if (weakness != null) damageData.DamageValue *= 1f - weakness.paramInt / 100f;
            }
            var caster = damageData.Caster;
            bool techniqueHit = IsValid && caster != null && caster.BelongCamp != BelongCamp &&
                caster.IsTechniqueSkill(damageData.SourceSkill);
            int healthBefore = (int)hpAttr.GetFinalValue();
            damageData.TargetWasBleeding = damageData.TriggerDamageRelate && HasBleeding;
#if UNITY_EDITOR
            if (damageData.TriggerDamageRelate)
                HotGameEntry.HeroManager.SeedPreviewBurnBeforeHit(this, damageData);
#endif
            damageData.BurnStacksBeforeHit = damageData.TriggerDamageRelate ? FindStatusBuff(BuffTag.Burning, CommandBurnEffect.BurnBuffId)?.StackCount ?? 0 : 0;
            var shieldValue = (int)hudunAttr.GetFinalValue();
            bool stoneSmash = damageData.SourceSkill?.SkillID == StateAttack0.StoneSmashSkillId;
            int bellAbsorbed = 0;
            if (shieldValue >= 0)
            {
                if (shieldValue<=(int)damageData.DamageValue)//护盾值小于伤害值
                {
                    foreach (var curbuff in CurBuffList)
                    {
                        if (curbuff.CheckBuffTag(BuffTag.Shield))
                        {
                            RecordBattleOutput(curbuff.ParentSkill, shieldAbsorbed: curbuff.paramInt);
                            if (stoneSmash && curbuff.ParentSkill?.SkillID == TideBellSkillId)
                                bellAbsorbed += curbuff.paramInt;
                            curbuff.paramInt = 0;
                            curbuff.OnDestory();
                        }
                    }
                    hudunAttr.AddNum(-shieldValue);
                    hpAttr.AddNum(-(int)(damageData.DamageValue-shieldValue));
                }
                else 
                {
                    //护盾值大于伤害值，遍历buff,从剩余时间小的开始扣
                    int curDamageValue = (int)damageData.DamageValue;
                    var shieldList = GetAllShieldList();
                    shieldList.Sort((a,b)=>
                    {
                        if (a.DurationMs != 0 && b.DurationMs != 0)
                        {
                            return a.RemainMs.CompareTo(b.RemainMs);
                        }
                        return (a.DurationMs == 0).CompareTo(b.DurationMs == 0);
                    });
                    while (curDamageValue > 0)
                    {
                        if (shieldList.Count > 0)
                        {
                            var nerBuff = shieldList[0];
                            int absorbed = Mathf.Min(curDamageValue, nerBuff.paramInt);
                            RecordBattleOutput(nerBuff.ParentSkill, shieldAbsorbed: absorbed);
                            if (stoneSmash && nerBuff.ParentSkill?.SkillID == TideBellSkillId)
                                bellAbsorbed += absorbed;
                            if (curDamageValue>nerBuff.paramInt)
                            {
                                curDamageValue -= nerBuff.paramInt;
                                nerBuff.paramInt = 0;
                                shieldList.Remove(nerBuff);
                                nerBuff.OnDestory();
                            }
                            else
                            {
                                nerBuff.paramInt -= curDamageValue;
                                curDamageValue = 0;
                            }
                        }
                        else
                        {
                            Log.Error("No Shield But AttrShield Has Value");
                            break;
                        }
                    }
                    ListPool<Buff>.Release(shieldList);
                    hudunAttr.AddNum(-(int)damageData.DamageValue);
                }
            }
            else
            {
                hpAttr.AddNum(-(int)damageData.DamageValue);
            }

            int healthAfter = (int)hpAttr.GetFinalValue();
            int shieldAfter = (int)hudunAttr.GetFinalValue();
            int healthLost = System.Math.Max(0, healthBefore - healthAfter);
            int shieldLost = System.Math.Max(0, shieldValue - shieldAfter);
            int effectiveDamage = (int)System.Math.Min(int.MaxValue, (long)healthLost + shieldLost);
            var battle = HotGameEntry.HeroManager;
            var visuals = battle.BurnVisuals;
            if (shieldLost > 0 && shieldAfter <= 0)
                visuals?.ShieldShattered(LogicPosition);
            if (effectiveDamage > 0 && stoneSmash && visuals != null)
            {
                Vector3 position = GObj != null ? GObj.transform.position : LogicPosition;
                visuals.StoneImpact(position);
                bellAbsorbed = Mathf.Min(bellAbsorbed, shieldLost);
                if (bellAbsorbed > 0)
                {
                    visuals.BellAbsorbed(position);
                    battle.ShowShieldAbsorbNum(this, bellAbsorbed);
                }
            }
            if (effectiveDamage > 0 && damageData.TriggerDamageRelate &&
                (damageData.SourceSkill?.SkillID is TimelineSlashVisuals.BasicSlashSkillId or TimelineSlashVisuals.LongSlashSkillId) &&
                caster?.GObj != null && GObj != null)
            {
                Vector3 direction = GObj.transform.position - caster.GObj.transform.position;
                visuals?.Slash?.Impact(
                    GObj.transform.position + Vector3.up * .82f, direction);
            }
            RecordBattleOutput(damageData.SourceSkill, damage: effectiveDamage);
            if (techniqueHit && healthBefore > 0 && healthAfter <= 0)
                caster.TechniqueKills++;

            if (damageData.TriggerDamageRelate)
            {
                damageData.HealthLost = healthLost;
                damageData.BrokenShieldValue = shieldValue > 0 && shieldAfter <= 0 ? shieldValue : 0;
                damageData.Caster.OnTrigger(TriggerType.AfterCauseDamage,damageData);
                OnTrigger(TriggerType.AfterBeCauseDamage,damageData);
                if (damageData.BrokenShieldValue > 0 && IsValid)
                    OnTrigger(TriggerType.OnShieldBroken, damageData);
            }
            if (bellAbsorbed <= 0 || healthLost > 0)
                battle.ShowDamageNum(damageData, bellAbsorbed > 0 ? healthLost : null);
            
            var curHp = (int)hpAttr.GetFinalValue();
            if (curHp == 0 && IsValid)
            {
                //death
                OnDead();
            }
            return effectiveDamage;
        }

        public bool HasBleeding => FindBleedingBuff() != null;

        internal Buff FindBleedingBuff(int id = 0) => FindStatusBuff(BuffTag.Bleeding, id);

        internal Buff FindStatusBuff(BuffTag tag, int id)
        {
            foreach (var buff in CurBuffList)
                if (buff.IsValid && (id == 0 || buff.BuffID == id) && buff.CheckBuffTag(tag))
                    return buff;
            return null;
        }

        public List<int> GetNormalSkillCheckSort()
        {
            List<int> checkNormalSkillList = ListPool<int>.Get();
            var nextCastIndex = NextCastNormalSkillIndex;
            for (var index = 0; index < NormalSkillList.Count; index++)
            {
                var curIndex = nextCastIndex + index;
                if (curIndex >= NormalSkillList.Count)
                {
                    curIndex -= NormalSkillList.Count;
                }
                checkNormalSkillList.Add(curIndex);
            }
            return checkNormalSkillList;
        }
        private List<Buff> GetAllShieldList()
        {
            List<Buff> shieldList = ListPool<Buff>.Get();
            foreach (var buff in CurBuffList)
            {
                if (buff.IsValid && buff.CheckBuffTag(BuffTag.Shield))
                {
                    shieldList.Add(buff);
                }
            }
            return shieldList;
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
            techniqueSkill = null;
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

            if (AuraStack != null)
            {
                foreach (var oneKeyValue in AuraStack)
                {
                    var list = oneKeyValue.Value;
                    if (list != null)
                    {
                        if (list.Count > 1)
                        {
                            var buffIndex = -1;
                            foreach (var oneBuff in list)
                            {
                                buffIndex++;
                                if (buffIndex == 0)
                                {
                                    continue;
                                }
                                ReferencePool.Release(oneBuff);
                            }
                        }
                        ListPool<Buff>.Release(list);
                    }
                }
                AuraStack.Clear();
            }
            if (CurBuffList != null)
            {
                foreach (var oneBuff in CurBuffList)
                {
                    ReferencePool.Release(oneBuff);
                }
                CurBuffList.Clear();
            }
            CurTriggerDic.Clear();
        }
    }
}
