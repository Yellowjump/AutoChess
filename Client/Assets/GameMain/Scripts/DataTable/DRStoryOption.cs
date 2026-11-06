// Generated from Config/StoryOption.xlsx by Tools/Tables/build_story_tables.mjs
using System.IO;
using System.Text;
using UnityGameFramework.Runtime;
namespace DataTable
{
    public sealed class DRStoryOption : DataRowBase
    {
        private int m_Id;
        public override int Id => m_Id;
        public int EventId { get; private set; }
        public string Text { get; private set; }
        public string Condition { get; private set; }
        public int Value { get; private set; }
        public string Flag { get; private set; }
        public string Hint { get; private set; }
        public int Weight { get; private set; }
        public int ItemId { get; private set; }
        public int CostItemId { get; private set; }
        public int HealthCost { get; private set; }
        public int Heal { get; private set; }
        public int Experience { get; private set; }
        public int TimeYears { get; private set; }
        public string SetFlag { get; private set; }
        public string Result { get; private set; }
        public override bool ParseDataRow(string value, object userData)
        {
            var cells = value.Split(DataTableExtension.DataSplitSeparators);
            for(int i=0;i<cells.Length;i++) cells[i]=cells[i].Trim(DataTableExtension.DataTrimSeparators);
            m_Id = int.Parse(cells[1]);
            EventId = int.Parse(cells[2]);
            Text = cells[3];
            Condition = cells[4];
            Value = int.Parse(cells[5]);
            Flag = cells[6];
            Hint = cells[7];
            Weight = int.Parse(cells[8]);
            ItemId = int.Parse(cells[9]);
            CostItemId = int.Parse(cells[10]);
            HealthCost = int.Parse(cells[11]);
            Heal = int.Parse(cells[12]);
            Experience = int.Parse(cells[13]);
            TimeYears = int.Parse(cells[14]);
            SetFlag = cells[15];
            Result = cells[16];
            return true;
        }
        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using(var stream = new MemoryStream(bytes,startIndex,length,false))
            using(var reader = new BinaryReader(stream,Encoding.UTF8))
            {
                m_Id = reader.Read7BitEncodedInt32();
                EventId = reader.Read7BitEncodedInt32();
                Text = reader.ReadString();
                Condition = reader.ReadString();
                Value = reader.Read7BitEncodedInt32();
                Flag = reader.ReadString();
                Hint = reader.ReadString();
                Weight = reader.Read7BitEncodedInt32();
                ItemId = reader.Read7BitEncodedInt32();
                CostItemId = reader.Read7BitEncodedInt32();
                HealthCost = reader.Read7BitEncodedInt32();
                Heal = reader.Read7BitEncodedInt32();
                Experience = reader.Read7BitEncodedInt32();
                TimeYears = reader.Read7BitEncodedInt32();
                SetFlag = reader.ReadString();
                Result = reader.ReadString();
            }
            return true;
        }
    }
}
