using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SkillSystem
{
    /// <summary>
    /// 包含多个触发器的容器
    /// </summary>
    public class TriggerList
    {
        public int TempleteID = 0;
        public List<OneTrigger> CurTriggerList = new List<OneTrigger>();

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
    }
}