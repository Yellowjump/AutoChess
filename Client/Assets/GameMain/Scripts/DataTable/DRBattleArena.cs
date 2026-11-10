using System.IO;
using System.Text;
using UnityGameFramework.Runtime;

namespace DataTable
{
    public sealed class DRBattleArena : DataRowBase
    {
        private int id;
        public override int Id => id;
        public string Rule { get; private set; }
        public int Q { get; private set; }
        public int R { get; private set; }
        public int MinDepth { get; private set; }
        public string Arena { get; private set; }

        public override bool ParseDataRow(string value, object userData)
        {
            var cells = value.Split(DataTableExtension.DataSplitSeparators);
            id = int.Parse(cells[1]);
            Rule = cells[2].Trim(DataTableExtension.DataTrimSeparators);
            Q = int.Parse(cells[3]);
            R = int.Parse(cells[4]);
            MinDepth = int.Parse(cells[5]);
            Arena = cells[6].Trim(DataTableExtension.DataTrimSeparators);
            return true;
        }

        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using var stream = new MemoryStream(bytes, startIndex, length, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            id = reader.Read7BitEncodedInt32();
            Rule = reader.ReadString();
            Q = reader.Read7BitEncodedInt32();
            R = reader.Read7BitEncodedInt32();
            MinDepth = reader.Read7BitEncodedInt32();
            Arena = reader.ReadString();
            return true;
        }
    }
}
