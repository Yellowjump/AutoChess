using System;
using System.IO;
using Entity;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class Buff
    {
        public int BuffID;
        public int TempleteID;
        public BuffTag OwnBuffTag;
        public TriggerList OwnTriggerList;
        public EntityQizi Caster;
        public Skill ParentSkill;
        public void Clone(Buff copy)
        {
            copy.BuffID = BuffID;
            copy.OwnBuffTag = OwnBuffTag;
            copy.OwnTriggerList ??= SkillFactory.CreateNewEmptyTriggerList();
            OwnTriggerList.Clone(copy.OwnTriggerList);
        }
        public void OnActive()
        {
            OwnTriggerList.OnActive();
        }
        public void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
            OwnBuffTag = GetCombinedTags(reader.ReadInt32());
            OwnTriggerList ??= SkillFactory.CreateNewEmptyTriggerList();
            OwnTriggerList.ReadFromFile(reader);
        }

        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            writer.Write((int)OwnBuffTag);
            OwnTriggerList.WriteToFile(writer);
        }
        private BuffTag GetCombinedTags(int combinedValue)
        {
            BuffTag result = BuffTag.None;
            foreach (BuffTag tag in Enum.GetValues(typeof(BuffTag)))
            {
                if ((combinedValue & (int)tag) != 0)
                {
                    result |= tag;
                }
            }
            return result;
        }
        public void SetSkillValue(DataRowBase dataTable)
        {
            OwnTriggerList?.SetSkillValue(dataTable);
        }
    }
}