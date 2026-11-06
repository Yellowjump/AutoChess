using System.IO;
using System.Text;
using UnityGameFramework.Runtime;

namespace DataTable
{
    public class DRCultivationLevel : DataRowBase
    {
        private int m_Id;
        public override int Id => m_Id;
        /// <summary>等级（1为出生值）。</summary>
        public int Level { get; private set; }
        /// <summary>升下一级经验（满级0）。</summary>
        public long ExperienceToNext { get; private set; }
        /// <summary>属性键。</summary>
        public string Attribute { get; private set; }
        /// <summary>S最小值。</summary>
        public int SMin { get; private set; }
        /// <summary>S最大值。</summary>
        public int SMax { get; private set; }
        /// <summary>A最小值。</summary>
        public int AMin { get; private set; }
        /// <summary>A最大值。</summary>
        public int AMax { get; private set; }
        /// <summary>B最小值。</summary>
        public int BMin { get; private set; }
        /// <summary>B最大值。</summary>
        public int BMax { get; private set; }
        /// <summary>C最小值。</summary>
        public int CMin { get; private set; }
        /// <summary>C最大值。</summary>
        public int CMax { get; private set; }

        public override bool ParseDataRow(string dataRowString, object userData)
        {
            var cells = dataRowString.Split(DataTableExtension.DataSplitSeparators);
            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim(DataTableExtension.DataTrimSeparators);
            m_Id = int.Parse(cells[1]);
            Level = int.Parse(cells[2]);
            ExperienceToNext = long.Parse(cells[3]);
            Attribute = cells[4];
            SMin = int.Parse(cells[5]);
            SMax = int.Parse(cells[6]);
            AMin = int.Parse(cells[7]);
            AMax = int.Parse(cells[8]);
            BMin = int.Parse(cells[9]);
            BMax = int.Parse(cells[10]);
            CMin = int.Parse(cells[11]);
            CMax = int.Parse(cells[12]);
            return true;
        }

        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using (var stream = new MemoryStream(bytes, startIndex, length, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                m_Id = reader.Read7BitEncodedInt32();
                Level = reader.Read7BitEncodedInt32();
                ExperienceToNext = reader.Read7BitEncodedInt64();
                Attribute = reader.ReadString();
                SMin = reader.Read7BitEncodedInt32();
                SMax = reader.Read7BitEncodedInt32();
                AMin = reader.Read7BitEncodedInt32();
                AMax = reader.Read7BitEncodedInt32();
                BMin = reader.Read7BitEncodedInt32();
                BMax = reader.Read7BitEncodedInt32();
                CMin = reader.Read7BitEncodedInt32();
                CMax = reader.Read7BitEncodedInt32();
            }
            return true;
        }
    }
}
