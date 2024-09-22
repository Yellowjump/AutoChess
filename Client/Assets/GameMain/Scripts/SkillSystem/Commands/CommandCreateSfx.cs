using System.IO;
using Entity;
using GameFramework;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandCreateSfx:CommandBase
    {
        public override CommandType CurCommandType => CommandType.CreateSfx;
        public bool CreateOrRemove = true;
        public TableParamInt SfxID;
        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.CurTargetList != null && trigger.CurTargetList.Count > 0)
            {
                foreach (var oneTarget in trigger.CurTargetList)
                {
                    EntityQizi target = oneTarget as EntityQizi;
                    if (target == null || target.IsValid == false)
                    {
                        continue;
                    }

                    if (CreateOrRemove)
                    {
                        target.AddSfx(SfxID.Value);
                    }
                    else
                    {
                        target.RemoveSfx(SfxID.Value);
                    }
                }
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(CreateOrRemove);
            SfxID.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CreateOrRemove = reader.ReadBoolean();
            SfxID.ReadFromFile(reader);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            SfxID.SetSkillValue(dataTable);
        }

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandCreateSfx copyAnim)
            {
                copyAnim.CreateOrRemove = CreateOrRemove;
                SfxID.Clone(copyAnim.SfxID);
            }
        }

        public override void Clear()
        {
            CreateOrRemove = true;
            ReferencePool.Release(SfxID);
            SfxID = null;
        }
    }
}