using System.IO;

namespace SkillSystem
{
    public class CommandBase
    {
        public virtual CommandType CurCommandType => CommandType.DoNothing;
        public virtual void OnExecute(OneTrigger trigger)
        {
            
        }
        public virtual void WriteToFile(BinaryWriter writer)
        {
            
        }

        public virtual void ReadFromFile(BinaryReader reader)
        {
            
        }
    }
}