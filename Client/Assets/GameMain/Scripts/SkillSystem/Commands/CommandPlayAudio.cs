using System.IO;
using Entity;
using GameFramework;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandPlayAudio:CommandBase
    {
        public override CommandType CurCommandType => CommandType.PlayAudio;
        public bool CreateOrRemove = true;
        public TableParamInt AudioID;

        internal void ExecuteTarget(OneTrigger trigger, EntityBase oneTarget,
            bool create, int audioId)
        {
            if (create)
            {
                var serialId = HotGameEntry.Sound.PlaySfxSoundThisFrame(audioId, oneTarget.LogicHitPosition);
                trigger.ParentTriggerList.paramInt = serialId;
            }
            else
            {
                var sfxDataIndex = trigger.ParentTriggerList.paramInt;
                HotGameEntry.Sound.ReducePlayingSfxCount(sfxDataIndex);
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(CreateOrRemove);
            AudioID.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CreateOrRemove = reader.ReadBoolean();
            AudioID.ReadFromFile(reader);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            AudioID.SetSkillValue(dataTable);
        }

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandPlayAudio copyAudio)
            {
                copyAudio.CreateOrRemove = CreateOrRemove;
                AudioID.Clone(copyAudio.AudioID);
            }
        }

        public override void Clear()
        {
            CreateOrRemove = true;
            // ReferencePool.Release(AudioID);
            // AudioID = null;
        }
    }
}
