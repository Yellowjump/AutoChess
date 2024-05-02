using System.Collections.Generic;
using System.IO;
using DataTable;
using GameFramework.DataTable;
using liuchengguanli;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    /// <summary>
    /// 包含多个触发器的容器
    /// </summary>
    public class TriggerList
    {
        public int TempleteID = 0;
        public List<OneTrigger> CurTriggerList = new List<OneTrigger>();
        public TriggerListType CurTriggerListType;
        public int SkillOrBuffID = 0;//todo 要怎么setSkillValue 读取不同的表数据
        public Skill ParentSkill;
        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            writer.Write(CurTriggerList.Count);
            foreach (var oneTrigger in CurTriggerList)
            {
                oneTrigger.WriteToFile(writer);
            }
        }

        public void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
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
            copy.TempleteID = TempleteID;
            copy.CurTriggerListType = CurTriggerListType;
            copy.CurTriggerList.Clear();
            foreach (var oneTrigger in CurTriggerList)
            {
                OneTrigger oneCopyTrigger = SkillFactory.CreateNewEmptyTrigger();
                oneTrigger.Clone(oneCopyTrigger);
                oneCopyTrigger.ParentTriggerList = copy;
                copy.CurTriggerList.Add(oneCopyTrigger);
            }
            copy.SkillOrBuffID = SkillOrBuffID;
        }

        public void SetSkillValue(DataRowBase dataTable)
        {
            foreach (var oneTrigger in CurTriggerList)
            {
                oneTrigger.SetSkillValue(dataTable);
            }
        }
        /// <summary>
        /// 当技能释放
        /// </summary>
        public void OnCast()
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