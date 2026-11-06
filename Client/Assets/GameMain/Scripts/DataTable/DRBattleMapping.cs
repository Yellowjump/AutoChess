using System.IO;
using System.Text;
using UnityGameFramework.Runtime;

namespace DataTable
{
    public class DRBattleMapping : DataRowBase
    {
        private int m_Id;
        public override int Id => m_Id;
        /// <summary>Kind。</summary>
        public string Kind { get; private set; }
        /// <summary>TimelineItem。</summary>
        public int TimelineItemId { get; private set; }
        /// <summary>HeroId。</summary>
        public int HeroId { get; private set; }
        /// <summary>NativeItemId。</summary>
        public int NativeItemId { get; private set; }

        public override bool ParseDataRow(string dataRowString, object userData)
        {
            var cells = dataRowString.Split(DataTableExtension.DataSplitSeparators);
            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim(DataTableExtension.DataTrimSeparators);
            m_Id = int.Parse(cells[1]);
            Kind = cells[2];
            TimelineItemId = int.Parse(cells[3]);
            HeroId = int.Parse(cells[4]);
            NativeItemId = int.Parse(cells[5]);
            return true;
        }

        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using (var stream = new MemoryStream(bytes, startIndex, length, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                m_Id = reader.Read7BitEncodedInt32();
                Kind = reader.ReadString();
                TimelineItemId = reader.Read7BitEncodedInt32();
                HeroId = reader.Read7BitEncodedInt32();
                NativeItemId = reader.Read7BitEncodedInt32();
            }
            return true;
        }
    }
}
