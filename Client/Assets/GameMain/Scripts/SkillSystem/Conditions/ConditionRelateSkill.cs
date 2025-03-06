using System.Collections.Generic;
using System.IO;
using DataTable;
using GameFramework;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class ConditionRelateSkill:ConditionBase
    {
        public override ConditionType CurConditionType => ConditionType.RelateSkill;
        public ConditionRelateSkillFrom SkillFrom;
        public ConditionRelateSkillCheckType CheckType;
        public TableParamInt ParamInt1;
        public TableParamInt ParamInt2;
        public override bool OnCheck(OneTrigger trigger,object arg = null)
        {
            Skill targetSkill = null;
            switch (SkillFrom)
            {
                case ConditionRelateSkillFrom.Source:
                    targetSkill = trigger.ParentTriggerList.ParentSkill;
                    break;
                case ConditionRelateSkillFrom.ArgSkill:
                    if (arg is Skill argSkill)
                    {
                        targetSkill = argSkill;
                    }
                    break;
            }

            if (targetSkill == null)
            {
                return false;
            }
            switch (CheckType)
            {
                case ConditionRelateSkillCheckType.SkillType:
                    return targetSkill.CurSkillType ==(SkillType) ParamInt1.Value;
            }
            return false;
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            base.WriteToFile(writer);
            var oneInt = (int)SkillFrom;
            writer.Write(oneInt);
            writer.Write((int)CheckType);
            ParamInt1.WriteToFile(writer);
            ParamInt2.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            base.ReadFromFile(reader);
            var oneInt = reader.ReadInt32();
            SkillFrom = (ConditionRelateSkillFrom)oneInt;
            CheckType = (ConditionRelateSkillCheckType)reader.ReadInt32();
            ParamInt1.ReadFromFile(reader);
            ParamInt2.ReadFromFile(reader);
        }

        public override void Clone(ConditionBase copy)
        {
            base.Clone(copy);
            if (copy is ConditionRelateSkill conditionRelateSkill)
            {
                conditionRelateSkill.SkillFrom = SkillFrom;
                conditionRelateSkill.CheckType = CheckType;
                ParamInt1.Clone(conditionRelateSkill.ParamInt1);
                ParamInt2.Clone(conditionRelateSkill.ParamInt2);
            }
        }
        public override void SetSkillValue(DataRowBase dataTable)
        {
            ParamInt1.SetSkillValue(dataTable);
            ParamInt2.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            if (ParamInt1 != null)
            {
                ReferencePool.Release(ParamInt1);
                ParamInt1 = null;
            }
            if (ParamInt2 != null)
            {
                ReferencePool.Release(ParamInt2);
                ParamInt2 = null;
            }
        }
    }
}