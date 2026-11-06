// Generated from Config/StoryItem.xlsx by Tools/Tables/build_story_tables.mjs
using System.IO;
using System.Text;
using UnityGameFramework.Runtime;
namespace DataTable
{
    public sealed class DRStoryItem : DataRowBase
    {
        private int m_Id;
        public override int Id => m_Id;
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int AttackBonus { get; private set; }
        public int HealthBonus { get; private set; }
        public int CombatAttackPercent { get; private set; }
        public override bool ParseDataRow(string value, object userData)
        {
            var cells = value.Split(DataTableExtension.DataSplitSeparators);
            for(int i=0;i<cells.Length;i++) cells[i]=cells[i].Trim(DataTableExtension.DataTrimSeparators);
            m_Id = int.Parse(cells[1]);
            Name = cells[2];
            Description = cells[3];
            AttackBonus = int.Parse(cells[4]);
            HealthBonus = int.Parse(cells[5]);
            CombatAttackPercent = int.Parse(cells[6]);
            return true;
        }
        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using(var stream = new MemoryStream(bytes,startIndex,length,false))
            using(var reader = new BinaryReader(stream,Encoding.UTF8))
            {
                m_Id = reader.Read7BitEncodedInt32();
                Name = reader.ReadString();
                Description = reader.ReadString();
                AttackBonus = reader.Read7BitEncodedInt32();
                HealthBonus = reader.Read7BitEncodedInt32();
                CombatAttackPercent = reader.Read7BitEncodedInt32();
            }
            return true;
        }
    }
}
