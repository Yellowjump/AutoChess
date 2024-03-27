namespace SkillSystem
{
    public class CommandBase
    {
        public virtual CommandType CurCommandType => CommandType.DoNothing;
        public void OnExecute(OneTrigger trigger)
        {
            
        }
    }
}