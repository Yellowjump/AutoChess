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
        public bool PassOnly = false;//只触发一次
        private float _timeAccumulatorMs;
        private bool HavePass = false;
        public override bool OnCheck(OneTrigger trigger,object arg = null)
        {
            if (_timeAccumulatorMs > TimeIntervalMs.Value)
            {
                if (PassOnly && HavePass)
                {
                    return false;
                }
                _timeAccumulatorMs -= TimeIntervalMs.Value;
                HavePass = true;
                return true;
            }

            _timeAccumulatorMs += GameEntry.LogicDeltaTime*1000;
            return false;
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            base.WriteToFile(writer);
            writer.Write(PassOnly);
            TimeIntervalMs.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            base.ReadFromFile(reader);
            PassOnly = reader.ReadBoolean();
            TimeIntervalMs.ReadFromFile(reader);
        }

        public override void Clone(ConditionBase copy)
        {
            base.Clone(copy);
            if (copy is ConditionTimed copyConditionTimed)
            {
                TimeIntervalMs.Clone(copyConditionTimed.TimeIntervalMs);
                copyConditionTimed.PassOnly = PassOnly;
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
            PassOnly = false;
            HavePass = false;
            _timeAccumulatorMs = 0;
        }
    }
}