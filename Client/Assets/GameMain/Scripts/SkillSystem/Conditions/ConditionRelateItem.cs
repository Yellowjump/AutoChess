using System.Collections.Generic;
using System.IO;
using DataTable;
using GameFramework;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class ConditionRelateItem:ConditionBase
    {
        public override ConditionType CurConditionType => ConditionType.RelateItem;
        public ConditionRelateItemFrom ItemFrom;
        public ConditionRelateItemCheckType CheckType;
        public TableParamInt ParamInt1;
        public TableParamInt ParamInt2;
        public override bool OnCheck(OneTrigger trigger,object arg = null)
        {
            var targetItemID = 0;
            switch (ItemFrom)
            {
                case ConditionRelateItemFrom.SkillItem:
                    /*targetItemID = trigger.ParentTriggerList.ParentSkill.FromItemID;
                    break;*/
                case ConditionRelateItemFrom.ArgSkillItem:
                    if (arg is Skill argSkill)
                    {
                        targetItemID = argSkill.FromItemID;
                    }
                    break;
                default:
                    targetItemID = trigger.ParentTriggerList.ParentSkill.FromItemID;
                    break;
            }

            if (targetItemID == 0)
            {
                return false;
            }
            switch (CheckType)
            {
                case ConditionRelateItemCheckType.ContainItem:
                    return CheckItemContainSubItemID(targetItemID, ParamInt1.Value);
            }
            return false;
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            base.WriteToFile(writer);
            var oneInt = (int)ItemFrom;
            writer.Write(oneInt);
            writer.Write((int)CheckType);
            ParamInt1.WriteToFile(writer);
            ParamInt2.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            base.ReadFromFile(reader);
            var oneInt = reader.ReadInt32();
            ItemFrom = (ConditionRelateItemFrom)oneInt;
            CheckType = (ConditionRelateItemCheckType)reader.ReadInt32();
            ParamInt1.ReadFromFile(reader);
            ParamInt2.ReadFromFile(reader);
        }

        public override void Clone(ConditionBase copy)
        {
            base.Clone(copy);
            if (copy is ConditionRelateItem conditionRelateItem)
            {
                conditionRelateItem.ItemFrom = ItemFrom;
                conditionRelateItem.CheckType = CheckType;
                ParamInt1.Clone(conditionRelateItem.ParamInt1);
                ParamInt2.Clone(conditionRelateItem.ParamInt2);
            }
        }
        public override void SetSkillValue(DataRowBase dataTable)
        {
            ParamInt1.SetSkillValue(dataTable);
            ParamInt2.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            if (ParamInt1 != null)
            {
                ReferencePool.Release(ParamInt1);
                ParamInt1 = null;
            }
            if (ParamInt2 != null)
            {
                ReferencePool.Release(ParamInt2);
                ParamInt2 = null;
            }
        }

        private bool CheckItemContainSubItemID(int itemID,int subItemID)
        {
            if (itemID == 0)
            {
                Log.Error("No Valid ItemID");
                return false;
            }

            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            if (!itemTable.HasDataRow(itemID))
            {
                Log.Error($"itemTable Has No ID:{itemID}");
                return false;
            }
            var itemData = itemTable[itemID];
            foreach (var oneIDAndNum in itemData.CraftList)
            {
                if (oneIDAndNum.Item1 == subItemID)
                {
                    return true;
                }
            }
            return false;
        }
    }
}