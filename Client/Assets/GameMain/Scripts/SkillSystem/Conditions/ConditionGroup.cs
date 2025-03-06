using System.Collections.Generic;
using System.IO;
using GameFramework;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class ConditionGroup:ConditionBase
    {
        public override ConditionType CurConditionType => ConditionType.ConditionGroup;
        public LogicOperator CurLogicOperator = LogicOperator.And;
        public List<ConditionBase> ConditionList;
        public override bool OnCheck(OneTrigger trigger,object arg = null)
        {
            if (ConditionList == null || ConditionList.Count == 0)
            {
                return true;
            }

            bool tempResult = CurLogicOperator == LogicOperator.And ? true : false;
            foreach (var oneCondition in ConditionList)
            {
                bool oneResult = oneCondition.OnCheck(trigger) == !oneCondition.ReverseResult;
                switch (CurLogicOperator)
                {
                    case LogicOperator.And:
                        tempResult = tempResult && oneResult;
                        break;
                    case LogicOperator.Or:
                        tempResult = tempResult || oneResult;
                        break;
                }

                if ((CurLogicOperator == LogicOperator.And && !tempResult) || (CurLogicOperator == LogicOperator.Or && tempResult))
                {
                    return tempResult; // 如果遇到false（与门）或true（或门），直接返回结果
                }
            }
            return tempResult;
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            base.ReadFromFile(reader);
            CurLogicOperator = (LogicOperator)reader.ReadInt32();
            var conditionCount = reader.ReadInt32();
            ConditionList.Clear();
            for (int i = 0; i < conditionCount; i++)
            {
                ConditionType oneConditionType = (ConditionType)reader.ReadInt32();
                var oneCondition = SkillFactory.CreateCondition(oneConditionType);
                oneCondition.ReadFromFile(reader);
                ConditionList.Add(oneCondition);
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            base.WriteToFile(writer);
            writer.Write((int)CurLogicOperator);
            writer.Write(ConditionList.Count);
            foreach (var oneCondition in ConditionList)
            {
                writer.Write((int)oneCondition.CurConditionType);
                oneCondition.WriteToFile(writer);
            }
        }

        public override void Clone(ConditionBase copy)
        {
            base.Clone(copy);
            if (copy is ConditionGroup copyGroup)
            {
                copyGroup.ConditionList.Clear();
                foreach (var oneCondition in ConditionList)
                {
                    var copyCondition = SkillFactory.CreateCondition(oneCondition.CurConditionType);
                    copyCondition.ParentTrigger = copyGroup.ParentTrigger;
                    oneCondition.Clone(copyCondition);
                    copyGroup.ConditionList.Add(copyCondition);
                }
            }
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            foreach (var oneCondition in ConditionList)
            {
                oneCondition.SetSkillValue(dataTable);
            }
        }

        public override void Clear()
        {
            if (ConditionList != null)
            {
                foreach (var oneCondition in ConditionList)
                {
                    ReferencePool.Release(oneCondition);
                }
                ListPool<ConditionBase>.Release(ConditionList);
                ConditionList = null;
            }
        }
    }
}