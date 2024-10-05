using System.Collections.Generic;
using System.IO;
using GameFramework;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class ConditionTimed:ConditionBase
    {
        public override ConditionType CurConditionType => ConditionType.Timed;
        public TableParamInt TimeIntervalMs;
        private float _timeAccumulatorMs;
        public override bool OnCheck(OneTrigger trigger,object arg = null)
        {
            if (_timeAccumulatorMs > TimeIntervalMs.Value)
            {
                _timeAccumulatorMs -= TimeIntervalMs.Value;
                return true;
            }

            _timeAccumulatorMs += GameEntry.LogicDeltaTime*1000;
            return false;
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            base.WriteToFile(writer);
            TimeIntervalMs.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            base.ReadFromFile(reader);
            TimeIntervalMs.ReadFromFile(reader);
        }

        public override void Clone(ConditionBase copy)
        {
            base.Clone(copy);
            if (copy is ConditionTimed copyConditionTimed)
            {
                TimeIntervalMs.Clone(copyConditionTimed.TimeIntervalMs);
            }
        }
        public override void SetSkillValue(DataRowBase dataTable)
        {
            TimeIntervalMs.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            if (TimeIntervalMs != null)
            {
                ReferencePool.Release(TimeIntervalMs);
                TimeIntervalMs = null;
            }
        }
    }
}