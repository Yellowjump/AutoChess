using System.Collections.Generic;
using System.IO;
using Entity;
using GameFramework;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class OneTrigger:IReference
    {
        public TriggerList ParentTriggerList;
        public TriggerType CurTriggerType;
        public ConditionBase CurCondition;
        public TargetPickerBase CurTargetPicker;
        public List<CommandBase> CurCommandList;
        [FormerlySerializedAs("CurTarget")] public List<EntityBase> CurTargetList;
        public void OnActive()
        {
            if (CurTriggerType == TriggerType.OnActive)
            {
                OnTrigger();
            }
        }
        public void OnTrigger(object arg = null)
        {
            if (CurCondition != null && CurCondition.OnCheck(this,arg)==!CurCondition.ReverseResult)
            {
                if (CurTargetPicker != null)
                {
                    CurTargetList = CurTargetPicker.GetTarget(this,arg);
                }

                if (CurCommandList != null && CurCommandList.Count != 0)
                {
                    foreach (var oneCommand in CurCommandList)
                    {
                        oneCommand?.OnExecute(this,arg);
                    }
                }
                if (CurTargetList != null)
                {
                    ListPool<EntityBase>.Release(CurTargetList);
                }
            }
        }

        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)CurTriggerType);
            writer.Write((int)CurCondition.CurConditionType);
            CurCondition.WriteToFile(writer);
            writer.Write((int)CurTargetPicker.CurTargetPickerType);
            CurTargetPicker.WriteToFile(writer);
            writer.Write(CurCommandList.Count);
            foreach (var oneCommand in CurCommandList)
            {
                writer.Write((int)oneCommand.CurCommandType);
                oneCommand.WriteToFile(writer);
            }
        }
        public void ReadFromFile(BinaryReader reader)
        {
            CurTriggerType = (TriggerType)reader.ReadInt32();
            
            var curConditionType = (ConditionType)reader.ReadInt32();
            CurCondition = SkillFactory.CreateCondition(curConditionType);
            CurCondition.ReadFromFile(reader);
            CurCondition.ParentTrigger = this;
            
            var curTargetPickerType = (TargetPickerType)reader.ReadInt32();
            CurTargetPicker = SkillFactory.CreateTargetPicker(curTargetPickerType);
            CurTargetPicker.ReadFromFile(reader);
            CurTargetPicker.ParentTrigger = this;
            
            var commandCount = reader.ReadInt32();
            CurCommandList.Clear();
            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                var curCommandType = (CommandType)reader.ReadInt32();
                var curCommand = SkillFactory.CreateCommand(curCommandType);
                CurCommandList.Add(curCommand);
                curCommand.ReadFromFile(reader);
                curCommand.ParentTrigger = this;
            }
        }

        public void Clone(OneTrigger copy)
        {
            copy.CurTriggerType = CurTriggerType;
            
            var copyCondition = SkillFactory.CreateCondition(CurCondition.CurConditionType);
            copyCondition.ParentTrigger = copy;
            CurCondition.Clone(copyCondition);
            copy.CurCondition = copyCondition;
            
            var copyTargetPicker = SkillFactory.CreateTargetPicker(CurTargetPicker.CurTargetPickerType);
            copyTargetPicker.ParentTrigger = copy;
            CurTargetPicker.Clone(copyTargetPicker);
            copy.CurTargetPicker = copyTargetPicker;

            copy.CurCommandList.Clear();
            foreach (var oneCommand in CurCommandList)
            {
                var copyCommand = SkillFactory.CreateCommand(oneCommand.CurCommandType);
                copyCommand.ParentTrigger = copy;
                oneCommand.Clone(copyCommand);
                copy.CurCommandList.Add(copyCommand);
            }
        }
        public void SetSkillValue(DataRowBase dataTable)
        {
            CurCondition.SetSkillValue(dataTable);
            CurTargetPicker.SetSkillValue(dataTable);
            foreach (var oneCommand in CurCommandList)
            {
                oneCommand.SetSkillValue(dataTable);
            }
        }

        public void Clear()
        {
            ParentTriggerList = null;
            CurTriggerType = TriggerType.OnActive;
            if (CurCondition != null)
            {
                ReferencePool.Release(CurCondition);
                CurCondition = null;
            }
            if (CurTargetPicker != null)
            {
                ReferencePool.Release(CurTargetPicker);
                CurTargetPicker = null;
            }
            if (CurCommandList != null)
            {
                foreach (var oneCommand in CurCommandList)
                {
                    ReferencePool.Release(oneCommand);
                }
                ListPool<CommandBase>.Release(CurCommandList);
                CurCommandList = null;
            }
            CurTargetList = null;
        }
    }
}