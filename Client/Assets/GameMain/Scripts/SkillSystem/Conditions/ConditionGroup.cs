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
                copyGroup.CurLogicOperator = CurLogicOperator;
                for (int i = 0; i < ConditionList.Count; i++)
                {
                    ConditionBase oneCondition = ConditionList[i];
                    ConditionBase copyCondition;
                    if (i < copyGroup.ConditionList.Count &&
                        copyGroup.ConditionList[i].CurConditionType == oneCondition.CurConditionType)
                    {
                        copyCondition = copyGroup.ConditionList[i];
                    }
                    else
                    {
                        if (i < copyGroup.ConditionList.Count)
                        {
                            ReferencePool.Release(copyGroup.ConditionList[i]);
                            copyCondition = SkillFactory.CreateCondition(oneCondition.CurConditionType);
                            copyGroup.ConditionList[i] = copyCondition;
                        }
                        else
                        {
                            copyCondition = SkillFactory.CreateCondition(oneCondition.CurConditionType);
                            copyGroup.ConditionList.Add(copyCondition);
                        }
                    }
                    copyCondition.ParentTrigger = copyGroup.ParentTrigger;
                    oneCondition.Clone(copyCondition);
                }
                for (int i = copyGroup.ConditionList.Count - 1; i >= ConditionList.Count; i--)
                {
                    ReferencePool.Release(copyGroup.ConditionList[i]);
                    copyGroup.ConditionList.RemoveAt(i);
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
                ConditionList.Clear();
                /*ListPool<ConditionBase>.Release(ConditionList);
                ConditionList = null;*/
            }
        }
    }
}
