using System;
using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    public class TargetPickerRandomFromList:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.RandomFromList;
        public NumberCheckType CurNumberType;
        public TableParamInt ParamInt1;
        public bool MeetNumber;//完全满足数量，来源list数量不足也循环补足
        public TargetPickerBase WorkTargetPicker;
        public override void BindTrigger(OneTrigger trigger)
        {
            base.BindTrigger(trigger);
            WorkTargetPicker?.BindTrigger(trigger);
        }
        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)CurNumberType);
            ParamInt1.WriteToFile(writer);
            writer.Write(MeetNumber);
            writer.Write((int)WorkTargetPicker.CurTargetPickerType);
            WorkTargetPicker.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurNumberType = (NumberCheckType)reader.ReadInt32();
            ParamInt1.ReadFromFile(reader);
            MeetNumber = reader.ReadBoolean();
            var curTargetPickerType = (TargetPickerType)reader.ReadInt32();
            WorkTargetPicker = SkillFactory.CreateTargetPicker(curTargetPickerType);
            WorkTargetPicker.ReadFromFile(reader);
        }

        public override void Clone(TargetPickerBase copy)
        {
            if (copy is TargetPickerRandomFromList targetPickerRandomFromList)
            {
                targetPickerRandomFromList.CurNumberType = CurNumberType;
                ParamInt1.Clone(targetPickerRandomFromList.ParamInt1);
                targetPickerRandomFromList.MeetNumber = MeetNumber;
                if (targetPickerRandomFromList.WorkTargetPicker == null ||
                    targetPickerRandomFromList.WorkTargetPicker.CurTargetPickerType !=
                    WorkTargetPicker.CurTargetPickerType)
                {
                    if (targetPickerRandomFromList.WorkTargetPicker != null)
                    {
                        ReferencePool.Release(targetPickerRandomFromList.WorkTargetPicker);
                    }
                    targetPickerRandomFromList.WorkTargetPicker =
                        SkillFactory.CreateTargetPicker(WorkTargetPicker.CurTargetPickerType);
                }
                var copyTargetPicker = targetPickerRandomFromList.WorkTargetPicker;
                WorkTargetPicker.Clone(copyTargetPicker);
                targetPickerRandomFromList.BindTrigger(targetPickerRandomFromList.ParentTrigger);
            }
        }
        public override void SetSkillValue(DataRowBase dataTable)
        {
            ParamInt1.SetSkillValue(dataTable);
            WorkTargetPicker.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            //if (ParamInt1 != null)
            //{
            //    ReferencePool.Release(ParamInt1);
            //    ParamInt1 = null;
            //}
            if (WorkTargetPicker != null)
            {
                ReferencePool.Release(WorkTargetPicker);
                WorkTargetPicker = null;
            }
        }
    }
}
