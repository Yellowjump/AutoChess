using System;
using System.IO;
using DataTable;
using GameFramework;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TableParamString:IReference
    {
        public string Value = string.Empty;
        public GenerateEnumDataTables CurMatchTable;
        public int CurMatchPropertyIndex;
        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write(Value);
            writer.Write((int)CurMatchTable);
            writer.Write(CurMatchPropertyIndex);
        }
        public void ReadFromFile(BinaryReader reader)
        {
            Value = reader.ReadString();
            CurMatchTable = (GenerateEnumDataTables)reader.ReadInt32();
            CurMatchPropertyIndex = reader.ReadInt32();
        }

        public void Clone(TableParamString copy)
        {
            copy.Value = Value;
            copy.CurMatchTable = CurMatchTable;
            copy.CurMatchPropertyIndex = CurMatchPropertyIndex;
        }

        public void SetSkillValue(DataRowBase dataTable)
        {
            if (CurMatchTable == GenerateEnumDataTables.Skill)
            {
                if (dataTable is DRSkill skillTable)
                {
                    Value = skillTable.GetFieldValue<string>((DRSkillField)CurMatchPropertyIndex);
                }
            }
            else if (CurMatchTable == GenerateEnumDataTables.Buff)
            {
                if (dataTable is DRBuff buffTable)
                {
                    Value = buffTable.GetFieldValue<string>((DRBuffField)CurMatchPropertyIndex);
                }
            }
        }
        public void Clear()
        {
            Value = string.Empty;
            CurMatchTable = GenerateEnumDataTables.None;
            CurMatchPropertyIndex = 0;
        }
    }
}