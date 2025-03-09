using System.Collections.Generic;
using System.IO;
using GameFramework;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class ConditionTimed:ConditionBase
    {
        public override ConditionType CurConditionType => ConditionType.Timed;
        public TableParamInt TimeIntervalMs;
        public TableParamInt PassNumber;//能触发次数
        private float _timeAccumulatorMs;
        private int HavePass = 0;
        public override bool OnCheck(OneTrigger trigger,object arg = null)
        {
            if (_timeAccumulatorMs > TimeIntervalMs.Value)
            {
                if (PassNumber.Value!=0 && PassNumber.Value<=HavePass)
                {
                    return false;
                }
                _timeAccumulatorMs -= TimeIntervalMs.Value;
                HavePass ++;
                return true;
            }

            _timeAccumulatorMs += GameEntry.LogicDeltaTime*1000;
            return false;
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            base.WriteToFile(writer);
            PassNumber.WriteToFile(writer);
            TimeIntervalMs.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            base.ReadFromFile(reader);
            PassNumber.ReadFromFile(reader);
            TimeIntervalMs.ReadFromFile(reader);
        }

        public override void Clone(ConditionBase copy)
        {
            base.Clone(copy);
            if (copy is ConditionTimed copyConditionTimed)
            {
                TimeIntervalMs.Clone(copyConditionTimed.TimeIntervalMs);
                PassNumber.Clone(copyConditionTimed.PassNumber);
            }
        }
        public override void SetSkillValue(DataRowBase dataTable)
        {
            TimeIntervalMs.SetSkillValue(dataTable);
            PassNumber.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            if (TimeIntervalMs != null)
            {
                ReferencePool.Release(TimeIntervalMs);
                TimeIntervalMs = null;
            }
            if (PassNumber != null)
            {
                ReferencePool.Release(PassNumber);
                PassNumber = null;
            }
            HavePass = 0;
            _timeAccumulatorMs = 0;
        }
    }
}