using System;
using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    public class TargetPickerNearest:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.Nearest;
        public CampType TargetCamp;
        public TableParamInt WeaponLength;

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
            //if (WeaponLength != null)
            //{
            //    ReferencePool.Release(WeaponLength);
            //    WeaponLength = null;
            //}
        }
    }
}
