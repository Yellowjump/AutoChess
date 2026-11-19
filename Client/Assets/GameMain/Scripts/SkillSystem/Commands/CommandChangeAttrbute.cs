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
                    var type = (AttributeType)TargetAttrType.Value;
                    var targetAttr = target.GetAttribute(type);
                    bool track = type is AttributeType.Hp or AttributeType.HuDun;
                    int before = track ? (int)targetAttr.GetFinalValue() : 0;
                    if (AddOrReduce)
                    {
                        if (type == AttributeType.Hp && ParamInt1.Value > 0) target.ReceiveHealing(ParamInt1.Value, trigger.ParentTriggerList.ParentSkill?.Caster);
                        else targetAttr.AddNum(ParamInt1.Value);
                    }
                    else
                    {
                        targetAttr.AddNum(-ParamInt1.Value);
                    }
                    if (track)
                    {
                        long gain = (long)(int)targetAttr.GetFinalValue() - before;
                        EntityQizi.RecordBattleOutput(trigger.ParentTriggerList.ParentSkill,
                            damage: type == AttributeType.Hp ? System.Math.Max(0, -gain) : 0,
                            shield: type == AttributeType.HuDun ? System.Math.Max(0, gain) : 0,
                            healing: type == AttributeType.Hp ? System.Math.Max(0, gain) : 0);
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
            // ReferencePool.Release(ParamInt1);
            // ParamInt1 = null;
            // ReferencePool.Release(TargetAttrType);
            // TargetAttrType = null;
        }
        
    }
}
