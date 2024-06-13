using System.Collections.Generic;
using System.IO;
using GameFramework;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class ConditionPercent:ConditionBase
    {
        public override ConditionType CurConditionType => ConditionType.Percentage;
        public TableParamInt PercentTarget;
        public override bool OnCheck(OneTrigger trigger,object arg = null)
        {
            var ran =  Utility.Random.GetRandom(100);
            if (ran < PercentTarget.Value)
            {
                return true;
            }
            return false;
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            base.WriteToFile(writer);
            PercentTarget.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            base.ReadFromFile(reader);
            PercentTarget.ReadFromFile(reader);
        }

        public override void Clone(ConditionBase copy)
        {
            base.Clone(copy);
            if (copy is ConditionPercent copyPercent)
            {
                PercentTarget.Clone(copyPercent.PercentTarget);
            }
        }
        public override void SetSkillValue(DataRowBase dataTable)
        {
            PercentTarget.SetSkillValue(dataTable);
        }
    }
}