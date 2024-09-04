using System;
using System.IO;
using DataTable;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TableParamInt
    {
        public int Value;
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
            Value = reader.ReadInt32();
            CurMatchTable = (GenerateEnumDataTables)reader.ReadInt32();
            CurMatchPropertyIndex = reader.ReadInt32();
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
    }
}