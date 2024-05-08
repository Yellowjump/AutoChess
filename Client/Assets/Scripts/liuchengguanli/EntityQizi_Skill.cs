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
        private void InitSkill()
        {
            var heroTable = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (!heroTable.HasDataRow(ID))
            {
                Log.Error($"heroID:{ID} invalid no match TableRow");
                return;
            }

            var skillID = heroTable[ID].SkillID;
            var skillTable = GameEntry.DataTable.GetDataTable<DRSkill>("Skill"); //先只初始化 normalSkill
            if (!skillTable.HasDataRow(skillID))
            {
                Log.Error($"heroID:{ID} skillID{skillID} invalid no match TableRow");
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
                CDMs = skillTableData.CD,
                ShakeBeforeMs = skillTableData.CD //todo 后续在skill表中添加前摇时间
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
    }
}
