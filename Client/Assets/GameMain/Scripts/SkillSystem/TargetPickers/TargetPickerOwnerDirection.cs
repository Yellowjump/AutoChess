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
    public class TargetPickerOwnerDirection:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.OwnerDirection;
        public bool LengthUseWeapon;
        public TableParamInt WeaponLength;
        public TableParamInt ValidAngle;
        public override void ReadFromFile(BinaryReader reader)
        {
            LengthUseWeapon = reader.ReadBoolean();
            WeaponLength.ReadFromFile(reader);
            ValidAngle.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(LengthUseWeapon);
            WeaponLength.WriteToFile(writer);
            ValidAngle.WriteToFile(writer);
        }

        public override void Clone(TargetPickerBase copy)
        {
            if (copy is TargetPickerOwnerDirection TargetPicker)
            {
                TargetPicker.LengthUseWeapon = LengthUseWeapon;
                WeaponLength.Clone(TargetPicker.WeaponLength);
                ValidAngle.Clone(TargetPicker.ValidAngle);
            }
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            WeaponLength.SetSkillValue(dataTable);
            ValidAngle.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            //if (WeaponLength != null)
            //{
            //    ReferencePool.Release(WeaponLength);
            //    WeaponLength = null;
            //}
            //if (ValidAngle != null)
            //{
            //    ReferencePool.Release(ValidAngle);
            //    ValidAngle = null;
            //}
        }
    }
}
