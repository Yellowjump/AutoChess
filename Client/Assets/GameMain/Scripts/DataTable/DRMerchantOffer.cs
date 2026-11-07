using System.IO;
using System.Text;
using UnityGameFramework.Runtime;

namespace DataTable
{
    public sealed class DRMerchantOffer : DataRowBase
    {
        private int id;
        public override int Id => id;
        public int MerchantId { get; private set; }
        public int Q { get; private set; }
        public int R { get; private set; }
        public int ItemId { get; private set; }
        public int LegacyPrice { get; private set; }

        public override bool ParseDataRow(string value, object userData)
        {
            var cells = value.Split(DataTableExtension.DataSplitSeparators);
            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim(DataTableExtension.DataTrimSeparators);
            id = int.Parse(cells[1]);
            MerchantId = int.Parse(cells[2]);
            Q = int.Parse(cells[3]);
            R = int.Parse(cells[4]);
            ItemId = int.Parse(cells[5]);
            LegacyPrice = int.Parse(cells[6]);
            return true;
        }

        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using var stream = new MemoryStream(bytes, startIndex, length, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            id = reader.Read7BitEncodedInt32();
            MerchantId = reader.Read7BitEncodedInt32();
            Q = reader.Read7BitEncodedInt32();
            R = reader.Read7BitEncodedInt32();
            ItemId = reader.Read7BitEncodedInt32();
            LegacyPrice = reader.Read7BitEncodedInt32();
            return true;
        }
    }
}
