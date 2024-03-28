using System.Collections.Generic;
using System.IO;
using liuchengguanli;

namespace SkillSystem
{
    public class OneTrigger
    {
        public TriggerType CurTriggerType;
        public ConditionBase CurCondition;
        public TargetPickerBase CurTargetPicker;
        public List<CommandBase> CurCommandList = new List<CommandBase>();
        public EntityBase CurTarget;
        public void OnActive()
        {
            if (CurTriggerType == TriggerType.OnActive)
            {
                OnTrigger();
            }
        }
        public void OnTrigger()
        {
            if (CurCondition != null && CurCondition.OnCheck(this)==!CurCondition.ReverseResult)
            {
                if (CurTargetPicker != null)
                {
                    CurTarget = CurTargetPicker.GetTarget(this);
                }

                if (CurCommandList != null && CurCommandList.Count != 0)
                {
                    foreach (var oneCommand in CurCommandList)
                    {
                        oneCommand?.OnExecute(this);
                    }
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
            
            var curTargetPickerType = (TargetPickerType)reader.ReadInt32();
            CurTargetPicker = SkillFactory.CreateTargetPicker(curTargetPickerType);
            CurTargetPicker.ReadFromFile(reader);
            
            var commandCount = reader.ReadInt32();
            CurCommandList.Clear();
            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                var curCommandType = (CommandType)reader.ReadInt32();
                var curCommand = SkillFactory.CreateCommand(curCommandType);
                CurCommandList.Add(curCommand);
                curCommand.ReadFromFile(reader);
            }
        }
    }
}