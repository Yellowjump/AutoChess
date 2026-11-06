using System.IO;
using System.Text;
using UnityGameFramework.Runtime;

namespace DataTable
{
    public class DRStartingCultivators : DataRowBase
    {
        private int m_Id;
        public override int Id => m_Id;
        /// <summary>名字。</summary>
        public string Name { get; private set; }
        /// <summary>横坐标。</summary>
        public int Q { get; private set; }
        /// <summary>纵坐标。</summary>
        public int R { get; private set; }
        /// <summary>道具ID。</summary>
        public int ItemId { get; private set; }
        /// <summary>道具。</summary>
        public string WeaponName { get; private set; }
        /// <summary>攻击加成。</summary>
        public int AttackBonus { get; private set; }

        public override bool ParseDataRow(string dataRowString, object userData)
        {
            var cells = dataRowString.Split(DataTableExtension.DataSplitSeparators);
            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim(DataTableExtension.DataTrimSeparators);
            m_Id = int.Parse(cells[1]);
            Name = cells[2];
            Q = int.Parse(cells[3]);
            R = int.Parse(cells[4]);
            ItemId = int.Parse(cells[5]);
            WeaponName = cells[6];
            AttackBonus = int.Parse(cells[7]);
            return true;
        }

        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using (var stream = new MemoryStream(bytes, startIndex, length, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                m_Id = reader.Read7BitEncodedInt32();
                Name = reader.ReadString();
                Q = reader.Read7BitEncodedInt32();
                R = reader.Read7BitEncodedInt32();
                ItemId = reader.Read7BitEncodedInt32();
                WeaponName = reader.ReadString();
                AttackBonus = reader.Read7BitEncodedInt32();
            }
            return true;
        }
    }
}
