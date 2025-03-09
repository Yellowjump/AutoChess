using System;
using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TargetPickerNearest:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.Nearest;
        public CampType TargetCamp;
        public TableParamInt WeaponLength;
        public override List<EntityBase> GetTarget(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.ParentTriggerList != null)
            {
                var owner = trigger.ParentTriggerList.Owner;
                var weaponLength = WeaponLength.Value;
                if (weaponLength <= 0)
                {
                    weaponLength = int.MaxValue;
                }
                GameEntry.HeroManager.GetNearestTarget(owner, TargetCamp, out var target, weaponLength);
                if (target != null)
                {
                    List<EntityBase> targetList = ListPool<EntityBase>.Get();
                    targetList.Add(target);
                    return targetList;
                }
            }
            return null;
        }
        public override void ReadFromFile(BinaryReader reader)
        {
            TargetCamp = (CampType)reader.ReadInt32();
            WeaponLength.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)TargetCamp);
            WeaponLength.WriteToFile(writer);
        }

        public override void Clone(TargetPickerBase copy)
        {
            if (copy is TargetPickerNearest targetPickerNearestCopy)
            {
                targetPickerNearestCopy.TargetCamp = TargetCamp;
                WeaponLength.Clone(targetPickerNearestCopy.WeaponLength);
            }
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            WeaponLength.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            if (WeaponLength != null)
            {
                ReferencePool.Release(WeaponLength);
                WeaponLength = null;
            }
        }
    }
}