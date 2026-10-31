using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    public class CommandHuDun:CommandBase
    {
        public override CommandType CurCommandType => CommandType.CreateHuDun;
        public bool RemoveCurHudun = false;
        public AttributeChangeType HudunChangeType;
        public TableParamInt ParamInt1;
        public TableParamInt ParamInt2;
        public TableParamInt ParamInt3;

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandHuDun copyCreateHuDunt)
            {
                copyCreateHuDunt.HudunChangeType = HudunChangeType;
                copyCreateHuDunt.RemoveCurHudun = RemoveCurHudun;
                ParamInt1.Clone(copyCreateHuDunt.ParamInt1);
                ParamInt2.Clone(copyCreateHuDunt.ParamInt2);
                ParamInt3.Clone(copyCreateHuDunt.ParamInt3);
            }
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            RemoveCurHudun = reader.ReadBoolean();
            HudunChangeType = (AttributeChangeType)reader.ReadInt32();
            ParamInt1.ReadFromFile(reader);
            ParamInt2.ReadFromFile(reader);
            ParamInt3.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(RemoveCurHudun);
            writer.Write((int)HudunChangeType);
            ParamInt1.WriteToFile(writer);
            ParamInt2.WriteToFile(writer);
            ParamInt3.WriteToFile(writer);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            ParamInt1.SetSkillValue(dataTable);
            ParamInt2.SetSkillValue(dataTable);
            ParamInt3.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            // ReferencePool.Release(ParamInt1);
            // ParamInt1 = null;
            // ReferencePool.Release(ParamInt2);
            // ParamInt2 = null;
            // ReferencePool.Release(ParamInt3);
            // ParamInt3 = null;
        }
        
    }
}
