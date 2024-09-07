using System.IO;
using GameFramework;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandBase:IReference
    {
        public OneTrigger ParentTrigger;
        public virtual CommandType CurCommandType => CommandType.DoNothing;
        public virtual void OnExecute(OneTrigger trigger,object arg = null)
        {
            
        }
        public virtual void WriteToFile(BinaryWriter writer)
        {
            
        }

        public virtual void ReadFromFile(BinaryReader reader)
        {
            
        }

        public virtual void Clone(CommandBase copy)
        {
            
        }

        public virtual void SetSkillValue(DataRowBase dataTable)
        {
            
        }

        public virtual void Clear()
        {
            
        }
    }
}