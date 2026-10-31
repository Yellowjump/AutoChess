using System;
using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TargetPickerBase:IReference
    {
        public OneTrigger ParentTrigger;
        public virtual TargetPickerType CurTargetPickerType => TargetPickerType.NoTarget;
        public virtual void BindTrigger(OneTrigger trigger)
        {
            ParentTrigger = trigger;
        }
        public virtual void WriteToFile(BinaryWriter writer)
        {
            
        }

        public virtual void ReadFromFile(BinaryReader reader)
        {
            
        }

        public virtual void Clone(TargetPickerBase copy)
        {
            
        }
        public virtual void SetSkillValue(DataRowBase dataTable)
        {
            
        }

        public virtual void Clear()
        {
            
        }

        internal static int LookupWeaponLength(int itemId, int fallbackLengthMm)
        {
            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            if (itemTable.HasDataRow(itemId))
            {
                var lengths = itemTable[itemId].AssetObjLength;
                if (lengths.Length != 0)
                {
                    return lengths[0];
                }
            }
            return fallbackLengthMm;
        }
    }
}
