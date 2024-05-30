using System.Collections.Generic;
using System.IO;
using DataTable;
using GameFramework.DataTable;
using Entity;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    /// <summary>
    /// 包含多个触发器的容器
    /// </summary>
    public class TriggerList
    {
        public List<OneTrigger> CurTriggerList = new List<OneTrigger>();
        public Skill ParentSkill;
        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write(CurTriggerList.Count);
            foreach (var oneTrigger in CurTriggerList)
            {
                oneTrigger.WriteToFile(writer);
            }
        }

        public void ReadFromFile(BinaryReader reader)
        {
            var triggerCount = reader.ReadInt32();
            CurTriggerList.Clear();
            for (int triggerIndex = 0; triggerIndex < triggerCount; triggerIndex++)
            {
                var oneTrigger = SkillFactory.CreateNewEmptyTrigger();
                CurTriggerList.Add(oneTrigger);
                oneTrigger.ReadFromFile(reader);
            }
        }

        public void Clone(TriggerList copy)
        {
            copy.CurTriggerList.Clear();
            foreach (var oneTrigger in CurTriggerList)
            {
                OneTrigger oneCopyTrigger = SkillFactory.CreateNewEmptyTrigger();
                oneTrigger.Clone(oneCopyTrigger);
                oneCopyTrigger.ParentTriggerList = copy;
                copy.CurTriggerList.Add(oneCopyTrigger);
            }
        }

        public void SetSkillValue(DataRowBase dataTable)
        {
            foreach (var oneTrigger in CurTriggerList)
            {
                oneTrigger.SetSkillValue(dataTable);
            }
        }
        /// <summary>
        /// 当技能释放或buff激活
        /// </summary>
        public void OnActive()
        {
            if (ParentSkill == null)
            {
                return;
            }

            EntityQizi caster = ParentSkill.Caster;
            if (caster == null)
            {
                return;
            }
            //将触发类型是 当收到/造成伤害 这种监听类型时 放入caster 的 监听 dic里
            foreach (var oneTrigger in CurTriggerList)
            {
                if (oneTrigger.CurTriggerType != TriggerType.OnActive)
                {
                    caster.AddTriggerListen(oneTrigger);
                    //添加到角色的 监听列表中
                }
            }
            OnTrigger(TriggerType.OnActive);
        }
        /// <summary>
        /// 技能或buff 结束
        /// </summary>
        public void OnDestory()
        {
            if (ParentSkill == null)
            {
                return;
            }

            EntityQizi caster = ParentSkill.Caster;
            if (caster == null)
            {
                return;
            }
            //将触发类型是 当收到/造成伤害 这种监听类型时 放入caster 的 监听 dic里
            foreach (var oneTrigger in CurTriggerList)
            {
                if (oneTrigger.CurTriggerType != TriggerType.OnActive||oneTrigger.CurTriggerType != TriggerType.OnDestory)
                {
                    caster.RemoveTriggerListen(oneTrigger);
                    //角色的 监听列表中 移除
                }
            }
            OnTrigger(TriggerType.OnDestory);
        }
        public void OnTrigger(TriggerType triggerType)
        {
            foreach (var oneTrigger in CurTriggerList)
            {
                if (oneTrigger.CurTriggerType == triggerType)
                {
                    oneTrigger.OnTrigger();
                }
            }
        }
    }
}