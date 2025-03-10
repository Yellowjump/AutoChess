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
        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.CurTargetList != null && trigger.CurTargetList.Count > 0)
            {
                foreach (var oneTarget in trigger.CurTargetList)
                {
                    if (CreateOrRemove)
                    {
                        var serialId = GameEntry.Sound.PlaySfxSound(AudioID.Value, oneTarget.LogicHitPosition);
                        if (serialId != null)
                        {
                            trigger.ParentTriggerList.paramInt = serialId.Value;
                        }
                    }
                    else
                    {
                        var serialID = trigger.ParentTriggerList.paramInt;
                        GameEntry.Sound.StopSound(serialID);
                    }
                }
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
            ReferencePool.Release(AudioID);
            AudioID = null;
        }
    }
}