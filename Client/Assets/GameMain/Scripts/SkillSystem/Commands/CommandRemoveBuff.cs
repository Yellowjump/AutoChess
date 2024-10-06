using System.IO;
using Entity;
using GameFramework;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandRemoveBuff:CommandBase
    {
        public override CommandType CurCommandType => CommandType.RemoveBuff;
        public bool RemoveCurBuff;
        public TableParamInt BuffID;
        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (RemoveCurBuff&&trigger.ParentTriggerList is Buff curBuff)
            {
                curBuff.OnDestory();
                return;
            }
            if (BuffID.Value!=0&&trigger != null && trigger.CurTargetList != null && trigger.CurTargetList.Count > 0)
            {
                foreach (var oneTarget in trigger.CurTargetList)
                {
                    if (oneTarget == null || oneTarget.IsValid == false)
                    {
                        continue;
                    }
                    oneTarget.RemoveBuff(BuffID.Value);
                }
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(RemoveCurBuff);
            BuffID.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            RemoveCurBuff = reader.ReadBoolean();
            BuffID.ReadFromFile(reader);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            BuffID.SetSkillValue(dataTable);
        }

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandRemoveBuff copyCmd)
            {
                copyCmd.RemoveCurBuff = RemoveCurBuff;
                BuffID.Clone(copyCmd.BuffID);
            }
        }

        public override void Clear()
        {
            RemoveCurBuff = true;
            ReferencePool.Release(BuffID);
            BuffID = null;
        }
    }
}