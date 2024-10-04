using System;
using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TargetPickerRandomFromList:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.RandomFromList;
        public NumberCheckType CurNumberType;
        public TableParamInt ParamInt1;
        public bool MeetNumber;//完全满足数量，来源list数量不足也循环补足
        public TargetPickerBase WorkTargetPicker;
        public override List<EntityBase> GetTarget(OneTrigger trigger,object arg = null)
        {
            if (WorkTargetPicker != null)
            {
                var workTargetList = WorkTargetPicker.GetTarget(trigger, arg);
                if (workTargetList.Count == 0)
                {
                    return workTargetList;
                }
                var targetNum = 0;
                switch (CurNumberType)
                {
                    case NumberCheckType.ParentSkillContainSubItemNumber:
                        var itemID = ParentTrigger.ParentTriggerList.ParentSkill.FromItemID;
                        targetNum = GetNumberContainSubItem(itemID);
                        break;
                    case NumberCheckType.ArgSkillContainSubItemNumber:
                        if (arg is Skill argSkill)
                        {
                            targetNum = GetNumberContainSubItem(argSkill.FromItemID);
                        }
                        break;
                    case NumberCheckType.FixedNumber:
                        targetNum = ParamInt1.Value;
                        break;
                }

                if (targetNum <= 0)
                {
                    ListPool<EntityBase>.Release(workTargetList);
                    return ListPool<EntityBase>.Get();;
                }

                var retTargetList = GetRandomList(workTargetList, targetNum);
                ListPool<EntityBase>.Release(workTargetList);
                return retTargetList;
            }
            return ListPool<EntityBase>.Get();;
        }
        private int GetNumberContainSubItem(int itemID)
        {
            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            if (itemTable.HasDataRow(itemID))
            {
                var itemData = itemTable[itemID];
                foreach (var oneItemIDAndNum in itemData.CraftList)
                {
                    if (oneItemIDAndNum.Item1 == ParamInt1.Value)
                    {
                        return oneItemIDAndNum.Item2;
                    }
                }
            }
            return 0;
        }
        private List<EntityBase> GetRandomList<EntityBase>(List<EntityBase> listA, int intA)
        {
            List<EntityBase> listB = ListPool<EntityBase>.Get();
            if (listA.Count <= intA && !MeetNumber)
            {
                listB.AddRange(listA);
                listB.Shuffle();
                return listB;
            }
            List<EntityBase> shuffledList = ListPool<EntityBase>.Get();
            while (listB.Count < intA)
            {
                shuffledList.Clear();
                // 随机打乱 shuffledList
                shuffledList.AddRange(listA);
                shuffledList.Shuffle();
                // 从打乱后的 listA 中添加到 listB，直到 listB 满足 intA 的数量
                for (int i = 0; i < shuffledList.Count && listB.Count < intA; i++)
                {
                    listB.Add(shuffledList[i]);
                }
            }
            ListPool<EntityBase>.Release(shuffledList);
            return listB;
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
            WorkTargetPicker.ParentTrigger = ParentTrigger;
        }

        public override void Clone(TargetPickerBase copy)
        {
            if (copy is TargetPickerRandomFromList targetPickerRandomFromList)
            {
                targetPickerRandomFromList.CurNumberType = CurNumberType;
                ParamInt1.Clone(targetPickerRandomFromList.ParamInt1);
                targetPickerRandomFromList.MeetNumber = MeetNumber;
                var copyTargetPicker = SkillFactory.CreateTargetPicker(WorkTargetPicker.CurTargetPickerType);
                copyTargetPicker.ParentTrigger = targetPickerRandomFromList.ParentTrigger;
                WorkTargetPicker.Clone(copyTargetPicker);
                targetPickerRandomFromList.WorkTargetPicker = copyTargetPicker;
            }
        }
        public override void SetSkillValue(DataRowBase dataTable)
        {
            ParamInt1.SetSkillValue(dataTable);
            WorkTargetPicker.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            if (ParamInt1 != null)
            {
                ReferencePool.Release(ParamInt1);
                ParamInt1 = null;
            }
            if (WorkTargetPicker != null)
            {
                ReferencePool.Release(WorkTargetPicker);
                WorkTargetPicker = null;
            }
        }
    }
}