using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine.Pool;
using UnityEngine.UIElements;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandRepeatExecuteCmd:CommandBase
    {
        public override CommandType CurCommandType => CommandType.RepeatExecute;
        public NumberCheckType CurType;
        public TableParamInt ParamInt1;
        public List<CommandBase> CurCommandList;
        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (CurCommandList != null && CurCommandList.Count != 0)
            {
                var executeNum = 0;
                switch (CurType)
                {
                    case NumberCheckType.ParentSkillContainSubItemNumber:
                        executeNum = GetNumberContainSubItem();
                        break;
                    case NumberCheckType.FixedNumber:
                        executeNum = ParamInt1.Value;
                        break;
                }

                if (executeNum <= 0)
                {
                    return;
                }
                for (int i = 0; i < executeNum; i++)
                {
                    foreach (var oneCommand in CurCommandList)
                    {
                        oneCommand.OnExecute(trigger,arg);
                    }
                }
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)CurType);
            ParamInt1.WriteToFile(writer);
            writer.Write(CurCommandList.Count);
            foreach (var oneCommand in CurCommandList)
            {
                writer.Write((int)oneCommand.CurCommandType);
                oneCommand.WriteToFile(writer);
            }
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurType = (NumberCheckType)reader.ReadInt32();
            ParamInt1.ReadFromFile(reader);
            var count = reader.ReadInt32();
            CurCommandList.Clear();
            for (int i = 0; i < count; i++)
            {
                var curCommandType = (CommandType)reader.ReadInt32();
                var curCommand = SkillFactory.CreateCommand(curCommandType);
                CurCommandList.Add(curCommand);
                curCommand.ReadFromFile(reader);
                curCommand.ParentTrigger = ParentTrigger;
            }
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            ParamInt1.SetSkillValue(dataTable);
            foreach (var oneCommand in CurCommandList)
            {
                oneCommand.SetSkillValue(dataTable);
            }
        }

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandRepeatExecuteCmd commandRepeatExecuteCmd)
            {
                commandRepeatExecuteCmd.CurType = CurType;
                ParamInt1.Clone(commandRepeatExecuteCmd.ParamInt1);
                commandRepeatExecuteCmd.CurCommandList.Clear();
                foreach (var oneCommand in CurCommandList)
                {
                    var copyCommand = SkillFactory.CreateCommand(oneCommand.CurCommandType);
                    copyCommand.ParentTrigger = commandRepeatExecuteCmd.ParentTrigger;
                    oneCommand.Clone(copyCommand);
                    commandRepeatExecuteCmd.CurCommandList.Add(copyCommand);
                }
            }
        }

        public override void Clear()
        {
            ReferencePool.Release(ParamInt1);
            ParamInt1 = null;
            if (CurCommandList != null)
            {
                foreach (var oneCommand in CurCommandList)
                {
                    ReferencePool.Release(oneCommand);
                }
                ListPool<CommandBase>.Release(CurCommandList);
                CurCommandList = null;
            }
        }

        private int GetNumberContainSubItem()
        {
            var itemID = ParentTrigger.ParentTriggerList.ParentSkill.FromItemID;
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
    }
}