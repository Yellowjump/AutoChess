using System.IO;
using System.Text;
using UnityGameFramework.Runtime;

namespace DataTable
{
    public class DROuterLocations : DataRowBase
    {
        private int m_Id;
        public override int Id => m_Id;
        /// <summary>横坐标。</summary>
        public int Q { get; private set; }
        /// <summary>纵坐标。</summary>
        public int R { get; private set; }
        /// <summary>名称。</summary>
        public string Name { get; private set; }
        /// <summary>灵气。</summary>
        public int CultivationPerYear { get; private set; }
        /// <summary>守怪生命。</summary>
        public int MonsterHealth { get; private set; }
        /// <summary>守怪攻击。</summary>
        public int MonsterAttack { get; private set; }
        /// <summary>宝物ID。</summary>
        public int LootItemId { get; private set; }
        /// <summary>宝物攻击加成。</summary>
        public int LootAttackBonus { get; private set; }
        /// <summary>宝物名称。</summary>
        public string LootName { get; private set; }

        public override bool ParseDataRow(string dataRowString, object userData)
        {
            var cells = dataRowString.Split(DataTableExtension.DataSplitSeparators);
            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim(DataTableExtension.DataTrimSeparators);
            m_Id = int.Parse(cells[1]);
            Q = int.Parse(cells[2]);
            R = int.Parse(cells[3]);
            Name = cells[4];
            CultivationPerYear = int.Parse(cells[5]);
            MonsterHealth = int.Parse(cells[6]);
            MonsterAttack = int.Parse(cells[7]);
            LootItemId = int.Parse(cells[8]);
            LootAttackBonus = int.Parse(cells[9]);
            LootName = cells[10];
            return true;
        }

        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using (var stream = new MemoryStream(bytes, startIndex, length, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                m_Id = reader.Read7BitEncodedInt32();
                Q = reader.Read7BitEncodedInt32();
                R = reader.Read7BitEncodedInt32();
                Name = reader.ReadString();
                CultivationPerYear = reader.Read7BitEncodedInt32();
                MonsterHealth = reader.Read7BitEncodedInt32();
                MonsterAttack = reader.Read7BitEncodedInt32();
                LootItemId = reader.Read7BitEncodedInt32();
                LootAttackBonus = reader.Read7BitEncodedInt32();
                LootName = reader.ReadString();
            }
            return true;
        }
    }
}
