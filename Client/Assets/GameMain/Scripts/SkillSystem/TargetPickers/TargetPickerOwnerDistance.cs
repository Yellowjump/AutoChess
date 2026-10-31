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
    public class TargetPickerOwnerDistance:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.OwnerDistance;
        public bool LengthUseWeapon;
        public TableParamInt WeaponLength;
        public CampType TargetCamp;

        public override void ReadFromFile(BinaryReader reader)
        {
            LengthUseWeapon = reader.ReadBoolean();
            TargetCamp = (CampType)reader.ReadInt32();
            WeaponLength.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(LengthUseWeapon);
            writer.Write((int)TargetCamp);
            WeaponLength.WriteToFile(writer);
        }

        public override void Clone(TargetPickerBase copy)
        {
            if (copy is TargetPickerOwnerDistance TargetPicker)
            {
                TargetPicker.LengthUseWeapon = LengthUseWeapon;
                TargetPicker.TargetCamp = TargetCamp;
                WeaponLength.Clone(TargetPicker.WeaponLength);
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
