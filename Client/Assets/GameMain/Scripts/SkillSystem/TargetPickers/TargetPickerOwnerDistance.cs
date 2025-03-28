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
    public class TargetPickerOwnerDistance:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.OwnerDistance;
        public bool LengthUseWeapon;
        public TableParamInt WeaponLength;
        public CampType TargetCamp;
        public override void GetTarget(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.ParentTriggerList != null)
            {
                var owner = trigger.ParentTriggerList.Owner;
                var weaponLength = WeaponLength.Value;
                if (LengthUseWeapon)
                {
                    var skill = trigger.ParentTriggerList.ParentSkill;
                    if (skill != null&&skill.FromItemID!=0)
                    {
                        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
                        if (itemTable.HasDataRow(skill.FromItemID))
                        {
                            var itemData = itemTable[skill.FromItemID];
                            var lengthArray = itemData.AssetObjLength;
                            if (lengthArray.Length != 0)
                            {
                                weaponLength = lengthArray[0];
                            }
                        }
                    }
                }

                var campList = ListPool<EntityQizi>.Get();
                GameEntry.HeroManager.GetEntityQiziList(TargetCamp,owner.BelongCamp,ref campList);
                foreach (var oneEntity in campList)
                {
                    if (!oneEntity.IsValid)
                    {
                        continue;
                    }
                    if (oneEntity.LogicPosition.Vector3DistanceNoY(owner.LogicPosition) <weaponLength/1000f)
                    {
                        trigger.CurTargetList.Add(oneEntity);
                    }
                }
                ListPool<EntityQizi>.Release(campList);
            }
        }

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