using System.IO;
using Entity;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandPlayAnim:CommandBase
    {
        public override CommandType CurCommandType => CommandType.PlayAnim;
        public TableParamString AnimName = new TableParamString();
        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (trigger.CurTarget != null&&trigger.CurTarget is EntityQizi qizi )
            {
                qizi.animator.Play(AnimName.Value);
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            AnimName.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            AnimName.ReadFromFile(reader);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            AnimName.SetSkillValue(dataTable);
        }

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandPlayAnim copyAnim)
            {
                AnimName.Clone(copyAnim.AnimName);
            }
        }
    }
}