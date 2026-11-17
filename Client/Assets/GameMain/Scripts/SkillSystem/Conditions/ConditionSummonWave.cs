using System.IO;

namespace SkillSystem
{
    public sealed class ConditionSummonWave : ConditionBase
    {
        public override ConditionType CurConditionType => ConditionType.SummonWave;
        public int MaxWaves = 2;
        public int RepeatHealthPercent = 45;

        public override bool OnCheck(OneTrigger trigger, object arg = null)
        {
            var owner = (trigger?.ParentTriggerList?.ParentSkill ?? arg as Skill)?.Caster;
            if (owner == null || owner.PaperShadowWaves >= MaxWaves ||
                owner.PaperShadows?.Exists(shadow => shadow.IsValid) == true)
                return false;
            if (owner.PaperShadowWaves == 0) return true;
            int health = (int)owner.GetAttribute(AttributeType.Hp).GetFinalValue();
            int maxHealth = (int)owner.GetAttribute(AttributeType.MaxHp).GetFinalValue();
            return health * 100L <= maxHealth * (long)RepeatHealthPercent;
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            base.ReadFromFile(reader);
            MaxWaves = reader.ReadInt32();
            RepeatHealthPercent = reader.ReadInt32();
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            base.WriteToFile(writer);
            writer.Write(MaxWaves);
            writer.Write(RepeatHealthPercent);
        }

        public override void Clone(ConditionBase copy)
        {
            base.Clone(copy);
            var condition = (ConditionSummonWave)copy;
            condition.MaxWaves = MaxWaves;
            condition.RepeatHealthPercent = RepeatHealthPercent;
        }

        public override void Clear()
        {
            base.Clear();
            MaxWaves = 2;
            RepeatHealthPercent = 45;
        }
    }
}
