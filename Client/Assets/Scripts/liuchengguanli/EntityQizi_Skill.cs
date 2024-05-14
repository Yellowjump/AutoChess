using GameFramework.Fsm;
using System;
using System.Collections;
using System.Collections.Generic;
using DataTable;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace liuchengguanli
{
    public partial class EntityQizi
    {
        public Skill NormalSkill;
        public Skill SpSkill;
        public Skill PassiveSkill;
        public Dictionary<TriggerType, List<OneTrigger>> CurTriggerDic = new Dictionary<TriggerType, List<OneTrigger>>();
        public List<Buff> CurBuffList = new List<Buff>();

        public EntityQizi CurAttackTarget;
        /// <summary>
        /// 普攻动画加速时间
        /// </summary>
        public float CurNormalAtkAniRate
        {
            get
            {
                if (NormalSkill.DefaultAnimationDurationMs > 1 / AtkSpeed)
                {
                    return NormalSkill.DefaultAnimationDurationMs * AtkSpeed;
                }
                return 1;
            }
        }

        public float NormalAtkInterval => AtkSpeed <= 0 ? float.MaxValue : 1 / AtkSpeed;
        public float SinceLastNormalAtk = float.MaxValue;
        private void InitSkill()
        {
            var heroTable = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (!heroTable.HasDataRow(HeroID))
            {
                Log.Error($"heroID:{HeroID} invalid no match TableRow");
                return;
            }

            var skillID = heroTable[HeroID].SkillID;
            var skillTable = GameEntry.DataTable.GetDataTable<DRSkill>("Skill"); //先只初始化 normalSkill
            if (!skillTable.HasDataRow(skillID))
            {
                Log.Error($"heroID:{HeroID} skillID{skillID} invalid no match TableRow");
                return;
            }

            var skillTableData = skillTable[skillID];
            var skillTemplates = GameEntry.DataTable.GetDataTable<DRSkillTemplate>("SkillTemplate");
            if (!skillTemplates.HasDataRow(skillTableData.TemplateID))
            {
                Log.Error($"skillID{skillID} no match Template{skillTableData.TemplateID}");
                return;
            }

            NormalSkill = new Skill
            {
                SkillID = skillID,
                CurSkillType = SkillType.NormalSkill,
                Caster = this,
                DefaultAnimationDurationMs = skillTableData.Duration,
                ShakeBeforeMs = skillTableData.BeforeShakeEndMs, //todo 后续在skill表中添加前摇时间
                CurSkillCastTargetType = (SkillCastTargetType)skillTableData.TargetType,
            };

            var temp = skillTemplates[skillTableData.TemplateID].SkillTemplate;
            temp.Clone(NormalSkill);
            temp.SetSkillValue(skillTableData);
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
        
        public void OnTrigger(TriggerType triggerType, object arg)
        {
            if (CurTriggerDic == null||!CurTriggerDic.ContainsKey(triggerType)||CurTriggerDic[triggerType]==null||CurTriggerDic[triggerType].Count==0)
            {
                return;
            }

            var typeList = CurTriggerDic[triggerType];
            foreach (var oneTrigger in typeList)
            {
                oneTrigger.OnTrigger();
            }
        }

        public override void AddBuff(Buff buff)
        {
            if (buff == null)
            {
                return;
            }
            // todo 判断是否有 免疫之类的判断
            CurBuffList.Add(buff);
            buff.OnActive();
        }

        public CheckCastSkillResult CheckCanCastSkill(out EntityQizi target,bool isSpSkill = false)
        {
            target = null;
            //判断是否需要蓝量
            if (isSpSkill)
            {
                if (power.value < 1)
                {
                    return CheckCastSkillResult.NoPower;
                }
            }
            //判断目标
            var willCastSkill = isSpSkill ? SpSkill : NormalSkill;
            if (willCastSkill == null)
            {
                return CheckCastSkillResult.Error;
            }

            var inAttackRange = false;
            if (willCastSkill.CurSkillCastTargetType != SkillCastTargetType.NoNeedTarget)
            {
                bool canUseOldTarget = false;
                if (CurAttackTarget != null)
                {
                    //如果之前存在攻击目标
                    //判断是否已死亡
                    //判断是否在范围内
                    canUseOldTarget = CurAttackTarget.xueliangnow >= 0;
                    //判断是否有不可选中之类
                    canUseOldTarget = canUseOldTarget&&!(GetDistanceSquare(CurAttackTarget) > gongjiDistence * gongjiDistence);
                    if (canUseOldTarget)
                    {
                        inAttackRange = true;
                        target = CurAttackTarget;
                    }
                }

                if (canUseOldTarget == false)
                {
                    //之前目标不可用，重新选择目标
                    inAttackRange = GetSkillNewTarget(out target, isSpSkill);
                }
            }
            if (target == null)
            {
                return CheckCastSkillResult.NoValidTarget;
            }
            else if (inAttackRange == false)
            {
                return CheckCastSkillResult.TargetOutRange;
            }

            if (isSpSkill == false&&SinceLastNormalAtk<NormalAtkInterval)
            {
                return CheckCastSkillResult.NormalAtkWait;
            }
            return CheckCastSkillResult.CanCast;
        }
        /// <summary>
        /// 获取技能目标以及是否在攻击范围内
        /// </summary>
        /// <param name="target">技能目标</param>
        /// <param name="isSpSkill"></param>
        /// <returns>是否在攻击范围内</returns>
        public bool GetSkillNewTarget(out EntityQizi target, bool isSpSkill = false)
        {
            target = null;
            //判断目标
            var willCastSkill = isSpSkill ? SpSkill : NormalSkill;
            if (willCastSkill == null)
            {
                return false;
            }

            bool inAttackRange = false;
            switch (willCastSkill.CurSkillCastTargetType)
            {
                case SkillCastTargetType.NearestEnemy:
                    return QiziGuanLi.instance.GetNearestTarget(this,CampType.Enemy,out target);
                case SkillCastTargetType.NoNeedTarget:
                    return true;
            }
            return false;
        }

        public void CastSkill(bool isSpSkill)
        {
            if (isSpSkill)
            {
                SpSkill?.Cast();
            }
            else
            {
                SinceLastNormalAtk = 0;
                NormalSkill.Cast();
            }
        }
    }
}
