using System;
using System.IO;

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
    }
}