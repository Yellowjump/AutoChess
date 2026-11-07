using System.IO;
using System.Text;
using UnityGameFramework.Runtime;

namespace DataTable
{
    public sealed class DRTechniqueStage : DataRowBase
    {
        private int id;
        public override int Id => id;
        public int TechniqueId { get; private set; }
        public int Rank { get; private set; }
        public int SkillId { get; private set; }
        public string Requirement { get; private set; }
        public int Value { get; private set; }

        public override bool ParseDataRow(string value, object userData)
        {
            var cells = value.Split(DataTableExtension.DataSplitSeparators);
            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim(DataTableExtension.DataTrimSeparators);
            id = int.Parse(cells[1]);
            TechniqueId = int.Parse(cells[2]);
            Rank = int.Parse(cells[3]);
            SkillId = int.Parse(cells[4]);
            Requirement = cells[5];
            Value = int.Parse(cells[6]);
            return true;
        }

        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using (var stream = new MemoryStream(bytes, startIndex, length, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                id = reader.Read7BitEncodedInt32();
                TechniqueId = reader.Read7BitEncodedInt32();
                Rank = reader.Read7BitEncodedInt32();
                SkillId = reader.Read7BitEncodedInt32();
                Requirement = reader.ReadString();
                Value = reader.Read7BitEncodedInt32();
            }
            return true;
        }
    }
}
