using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandChangeAttribute:CommandBase
    {
        public override CommandType CurCommandType => CommandType.ChangeAttribute;
        public TableParamInt TargetAttrType;
        //public AttributeType TargetAttrType;
        public bool AddOrReduce;
        public AttributeChangeType CurAttributeChangeType;
        public TableParamInt ParamInt1;
        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.CurTargetList != null && trigger.CurTargetList.Count > 0)
            {
                foreach (var oneTarget in trigger.CurTargetList)
                {
                    EntityQizi target = oneTarget as EntityQizi;
                    var targetAttr = target.GetAttribute((AttributeType)TargetAttrType.Value);
                    if (AddOrReduce)
                    {
                        targetAttr.AddNum(ParamInt1.Value);
                    }
                    else
                    {
                        targetAttr.AddNum(-ParamInt1.Value);
                    }
                }
            }
        }

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandChangeAttribute copyChangeAttribute)
            {
                TargetAttrType.Clone(copyChangeAttribute.TargetAttrType);
                copyChangeAttribute.AddOrReduce = AddOrReduce;
                copyChangeAttribute.CurAttributeChangeType = CurAttributeChangeType;
                ParamInt1.Clone(copyChangeAttribute.ParamInt1);
            }
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            TargetAttrType.ReadFromFile(reader);
            CurAttributeChangeType = (AttributeChangeType)reader.ReadInt32();
            AddOrReduce = reader.ReadBoolean();
            ParamInt1.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            TargetAttrType.WriteToFile(writer);
            writer.Write((int)CurAttributeChangeType);
            writer.Write(AddOrReduce);
            ParamInt1.WriteToFile(writer);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            TargetAttrType.SetSkillValue(dataTable);
            ParamInt1.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            ReferencePool.Release(ParamInt1);
            ParamInt1 = null;
            ReferencePool.Release(TargetAttrType);
            TargetAttrType = null;
        }
        
    }
}