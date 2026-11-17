using System.IO;
using DataTable;
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
            if (HotGameEntry.HeroManager.IsTimelineSimulation) return;
            if (trigger != null && trigger.CurTargetList != null && trigger.CurTargetList.Count > 0)
            {
                foreach (var oneTarget in trigger.CurTargetList)
                {
                    if (CreateOrRemove)
                    {
                        var cue = (EnumSound)AudioID.Value;
                        bool timeline = HotGameEntry.HeroManager.IsTimelineBattle;
                        // Timeline melee plays on confirmed damage, including hits absorbed by a shield.
                        if (timeline && cue == EnumSound.Chop) return;
                        bool burst = cue is EnumSound.GenFlySword or EnumSound.FlySwordFly or EnumSound.FlySwordHit or
                            EnumSound.Bullet_Hit or EnumSound.ShortBoom;
                        var serialId = timeline && burst
                            ? HotGameEntry.Sound.PlayTimelineCue(cue, oneTarget.LogicHitPosition)
                            : HotGameEntry.Sound.PlaySfxSoundThisFrame((int)cue, oneTarget.LogicHitPosition);
                        trigger.ParentTriggerList.paramInt = serialId;
                    }
                    else
                    {
                        var sfxDataIndex = trigger.ParentTriggerList.paramInt;
                        HotGameEntry.Sound.ReducePlayingSfxCount(sfxDataIndex);
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
            // ReferencePool.Release(AudioID);
            // AudioID = null;
        }
    }
}
