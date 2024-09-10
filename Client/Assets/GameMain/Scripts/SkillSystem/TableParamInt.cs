using System;
using System.IO;
using DataTable;
using GameFramework;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TableParamInt:IReference
    {
        public int Value;
        public GenerateEnumDataTables CurMatchTable;
        public int CurMatchPropertyIndex;
        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write(Value);
            writer.Write((int)CurMatchTable);
            if (CurMatchTable == GenerateEnumDataTables.Skill)
            {
                writer.Write(((DRSkillField)CurMatchPropertyIndex).ToString());
            }
            else if (CurMatchTable == GenerateEnumDataTables.Buff)
            {
                writer.Write(((DRBuffField)CurMatchPropertyIndex).ToString());
            }
        }
        public void ReadFromFile(BinaryReader reader)
        {
            Value = reader.ReadInt32();
            CurMatchTable = (GenerateEnumDataTables)reader.ReadInt32();
            if (CurMatchTable == GenerateEnumDataTables.Skill)
            {
                CurMatchPropertyIndex = (int)Enum.Parse(typeof(DRSkillField), reader.ReadString());
            }
            else if (CurMatchTable == GenerateEnumDataTables.Buff)
            {
                CurMatchPropertyIndex = (int)Enum.Parse(typeof(DRBuffField), reader.ReadString());
            }
        }

        public void Clone(TableParamInt copy)
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
                    Value = skillTable.GetFieldValue<int>((DRSkillField)CurMatchPropertyIndex);
                }
            }
            else if (CurMatchTable == GenerateEnumDataTables.Buff)
            {
                if (dataTable is DRBuff buffTable)
                {
                    Value = buffTable.GetFieldValue<int>((DRBuffField)CurMatchPropertyIndex);
                }
            }
        }

        public void Clear()
        {
            Value = 0;
            CurMatchTable = GenerateEnumDataTables.None;
            CurMatchPropertyIndex = 0;
        }
    }
}