using System.IO;

namespace SkillSystem
{
    public class CommandCauseDamage:CommandBase
    {
        public override CommandType CurCommandType => CommandType.CauseDamage;
        public DamageComputeType CurDamageComputeType = DamageComputeType.CommonDamage;
        public TableParamInt ParamInt1 = new TableParamInt();
        public TableParamInt ParamInt2 = new TableParamInt();
        public TableParamInt ParamInt3 = new TableParamInt();
        public override void OnExecute(OneTrigger trigger)
        {
            if (trigger != null && trigger.CurTarget != null)
            {
                //对当前 target 造成伤害
            }
        }
        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)CurDamageComputeType);
            ParamInt1.WriteToFile(writer);
            ParamInt2.WriteToFile(writer);
            ParamInt3.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurDamageComputeType = (DamageComputeType)reader.ReadInt32();
            ParamInt1.ReadFromFile(reader);
            ParamInt2.ReadFromFile(reader);
            ParamInt3.ReadFromFile(reader);
        }
    }
}