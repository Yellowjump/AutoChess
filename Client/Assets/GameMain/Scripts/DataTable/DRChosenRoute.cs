using System.IO;
using System.Text;
using UnityGameFramework.Runtime;

namespace DataTable
{
    public sealed class DRChosenRoute : DataRowBase
    {
        private int id;
        public override int Id => id;
        public int Q { get; private set; }
        public int R { get; private set; }
        public int TravelYears { get; private set; }
        public int GuardianCount { get; private set; }
        public int GuardianHealth { get; private set; }
        public int GuardianAttack { get; private set; }
        public string GuardianAppearance { get; private set; }
        public int RitualPeriod { get; private set; }

        public override bool ParseDataRow(string value, object userData)
        {
            var cells = value.Split(DataTableExtension.DataSplitSeparators);
            id = int.Parse(cells[1]);
            Q = int.Parse(cells[2]);
            R = int.Parse(cells[3]);
            TravelYears = int.Parse(cells[4]);
            GuardianCount = int.Parse(cells[5]);
            GuardianHealth = int.Parse(cells[6]);
            GuardianAttack = int.Parse(cells[7]);
            GuardianAppearance = cells[8].Trim(DataTableExtension.DataTrimSeparators);
            RitualPeriod = int.Parse(cells[9]);
            return true;
        }

        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using var stream = new MemoryStream(bytes, startIndex, length, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            id = reader.Read7BitEncodedInt32();
            Q = reader.Read7BitEncodedInt32();
            R = reader.Read7BitEncodedInt32();
            TravelYears = reader.Read7BitEncodedInt32();
            GuardianCount = reader.Read7BitEncodedInt32();
            GuardianHealth = reader.Read7BitEncodedInt32();
            GuardianAttack = reader.Read7BitEncodedInt32();
            GuardianAppearance = reader.ReadString();
            RitualPeriod = reader.Read7BitEncodedInt32();
            return true;
        }
    }
}
