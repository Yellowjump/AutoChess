// Generated from Config/StoryEvent.xlsx by Tools/Tables/build_story_tables.mjs
using System.IO;
using System.Text;
using UnityGameFramework.Runtime;
namespace DataTable
{
    public sealed class DRStoryEvent : DataRowBase
    {
        private int m_Id;
        public override int Id => m_Id;
        public int Q { get; private set; }
        public int R { get; private set; }
        public string Title { get; private set; }
        public string Text { get; private set; }
        public string Background { get; private set; }
        public int Repeatable { get; private set; }
        public override bool ParseDataRow(string value, object userData)
        {
            var cells = value.Split(DataTableExtension.DataSplitSeparators);
            for(int i=0;i<cells.Length;i++) cells[i]=cells[i].Trim(DataTableExtension.DataTrimSeparators);
            m_Id = int.Parse(cells[1]);
            Q = int.Parse(cells[2]);
            R = int.Parse(cells[3]);
            Title = cells[4];
            Text = cells[5];
            Background = cells[6];
            Repeatable = int.Parse(cells[7]);
            return true;
        }
        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using(var stream = new MemoryStream(bytes,startIndex,length,false))
            using(var reader = new BinaryReader(stream,Encoding.UTF8))
            {
                m_Id = reader.Read7BitEncodedInt32();
                Q = reader.Read7BitEncodedInt32();
                R = reader.Read7BitEncodedInt32();
                Title = reader.ReadString();
                Text = reader.ReadString();
                Background = reader.ReadString();
                Repeatable = reader.Read7BitEncodedInt32();
            }
            return true;
        }
    }
}
