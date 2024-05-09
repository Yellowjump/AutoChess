using System.IO;
using liuchengguanli;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class Skill
    {
        public int SkillID;
        public int TempleteID;
        public SkillType CurSkillType;
        public TriggerList OwnTriggerList;
        public EntityQizi Caster;
        public int CDMs;
        public int ShakeBeforeMs;//技能前摇

        public void Cast()
        {
            OwnTriggerList.OnActive();
        }
        public void Clone(Skill copy)
        {
            copy.TempleteID = TempleteID;
            copy.OwnTriggerList ??= SkillFactory.CreateNewEmptyTriggerList(copy);
            OwnTriggerList.Clone(copy.OwnTriggerList);
        }
        public void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
            OwnTriggerList ??= SkillFactory.CreateNewEmptyTriggerList(this);
            OwnTriggerList.ReadFromFile(reader);
        }

        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            OwnTriggerList?.WriteToFile(writer);
        }
        public void SetSkillValue(DataRowBase dataTable)
        {
            OwnTriggerList?.SetSkillValue(dataTable);
        }
    }
}