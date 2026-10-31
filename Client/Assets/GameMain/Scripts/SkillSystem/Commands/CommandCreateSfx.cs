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

        internal void ExecuteTarget(EntityBase oneTarget, bool create, int sfxId)
        {
            if (oneTarget == null || oneTarget.IsValid == false)
            {
                return;
            }
            if (create)
            {
                oneTarget.AddSfx(sfxId);
            }
            else
            {
                oneTarget.RemoveSfx(sfxId);
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
            // ReferencePool.Release(SfxID);
            // SfxID = null;
        }
    }
}
