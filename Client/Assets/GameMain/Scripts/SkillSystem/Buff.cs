using System;
using System.IO;
using Entity;
using UnityGameFramework.Runtime;

namespace SkillSystem
{

    public class Buff:TriggerList
    {
        public string EditorDesc = string.Empty;
        public int BuffID;
        public int TempleteID;
        public BuffTag OwnBuffTag;
        public int MaxLayerNum;
        public bool FreshOtherBuffDuration;
        public bool IsAura => MaxLayerNum < 0;
        public int DurationMs;//总持续时间，无限是0
        public float RemainMs;//剩余时间
        public bool IsValid = true;
        public override void Clone(TriggerList copy)
        {
            if (copy is Buff copyBuff)
            {
                copyBuff.TempleteID = TempleteID;
                copyBuff.EditorDesc = EditorDesc;
                copyBuff.OwnBuffTag = OwnBuffTag;
            }
            base.Clone(copy);
        }
        public void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
            EditorDesc = reader.ReadString();
            OwnBuffTag = GetCombinedTags(reader.ReadInt32());
            base.ReadFromFile(reader);
        }

        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            writer.Write(EditorDesc);
            writer.Write((int)OwnBuffTag);
            base.WriteToFile(writer);
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

        public bool CheckBuffTag(BuffTag checkTag)
        {
            return (checkTag & OwnBuffTag) == checkTag;
        }
        public override void OnDestory()
        {
            if (IsValid == false)
            {
                return;
            }
            IsValid = false;
            base.OnDestory();
            if (IsAura)
            {
                if (Owner is EntityQizi qizi)
                {
                    qizi.OnAuraBuffDestroy(this);
                }
            }
        }

        public override void Clear()
        {
            BuffID = 0;
            TempleteID = 0;
            OwnBuffTag = BuffTag.None;;
            MaxLayerNum = 0;
            FreshOtherBuffDuration = false;
            DurationMs = 0;
            RemainMs = 0;
            IsValid = true;
            base.Clear();
        }
    }
}