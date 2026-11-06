using System.IO;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public sealed class CommandShieldDamageModifier : CommandBase
    {
        public override CommandType CurCommandType => CommandType.ModifyDamageWhileShielded;
        public TableParamInt Percent;

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            var wearer = trigger.ParentTriggerList.ParentSkill.Caster;
            if (arg is not CauseDamageData damage || wearer == null || !wearer.IsValid ||
                damage.DamageValue <= 0 || (int)wearer.GetAttribute(AttributeType.HuDun).GetFinalValue() <= 0)
                return;

            bool applies = trigger.CurTriggerType switch
            {
                TriggerType.BeforeCauseDamage => damage.Caster == wearer,
                TriggerType.BeforeBeCauseDamage => damage.Target == wearer,
                _ => false
            };
            if (applies)
                damage.DamageValue *= Mathf.Max(0f, 1f + Percent.Value / 100f);
        }

        public override void WriteToFile(BinaryWriter writer) => Percent.WriteToFile(writer);
        public override void ReadFromFile(BinaryReader reader) => Percent.ReadFromFile(reader);
        public override void SetSkillValue(DataRowBase row) => Percent.SetSkillValue(row);

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandShieldDamageModifier modifier) Percent.Clone(modifier.Percent);
        }
    }
}
