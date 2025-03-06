using System;
using System.IO;
using DataTable;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TableParamVector3:IReference
    {
        public Vector3 Value = Vector3.zero;
        public GenerateEnumDataTables CurMatchTable;
        public int CurMatchPropertyIndex;
        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)CurMatchTable);
            if (CurMatchTable == GenerateEnumDataTables.None)
            {
                writer.Write(Value.x);
                writer.Write(Value.y);
                writer.Write(Value.z);
            }
            else if (CurMatchTable == GenerateEnumDataTables.Skill)
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
            CurMatchTable = (GenerateEnumDataTables)reader.ReadInt32();
            if (CurMatchTable == GenerateEnumDataTables.None)
            {
                var tempValueX = reader.ReadSingle();
                var tempValueY = reader.ReadSingle();
                var tempValueZ = reader.ReadSingle();
                Value = new Vector3(tempValueX, tempValueY, tempValueZ);
            }
            else if (CurMatchTable == GenerateEnumDataTables.Skill)
            {
                CurMatchPropertyIndex = (int)Enum.Parse(typeof(DRSkillField), reader.ReadString());
            }
            else if (CurMatchTable == GenerateEnumDataTables.Buff)
            {
                CurMatchPropertyIndex = (int)Enum.Parse(typeof(DRBuffField), reader.ReadString());
            }
        }

        public void Clone(TableParamVector3 copy)
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
                    var stringValue = skillTable.GetFieldValue<string>((DRSkillField)CurMatchPropertyIndex);
                    Value = stringValue.ParseVector3();
                }
            }
            else if (CurMatchTable == GenerateEnumDataTables.Buff)
            {
                if (dataTable is DRBuff buffTable)
                {
                    var stringValue = buffTable.GetFieldValue<string>((DRBuffField)CurMatchPropertyIndex);
                    Value = stringValue.ParseVector3();
                }
            }
        }
        public void Clear()
        {
            Value = Vector3.zero;
            CurMatchTable = GenerateEnumDataTables.None;
            CurMatchPropertyIndex = 0;
        }
    }
}