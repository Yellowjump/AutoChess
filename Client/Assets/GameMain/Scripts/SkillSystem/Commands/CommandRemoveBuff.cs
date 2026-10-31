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
            // ReferencePool.Release(BuffID);
            // BuffID = null;
        }
    }
}