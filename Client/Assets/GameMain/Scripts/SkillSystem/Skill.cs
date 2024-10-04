using System.IO;
using Entity;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class Skill:TriggerList
    {
        public int SkillID;
        public int TempleteID;
        public int FromItemID;//来源道具ID
        public SkillType CurSkillType;
        public int SkillRange;//攻击距离
        public EntityQizi Caster;
        public int DefaultAnimationDurationMs;//默认技能动画时长，也是默认技能时长
        public int DefaultSkillCDMs;//默认技能CD
        public float LeftSkillCD;//剩余的冷却时间
        public bool InCD => LeftSkillCD > 0;// 技能还没冷却好
        public int ShakeBeforeMs;//技能前摇
        public SkillCastTargetType CurSkillCastTargetType;//释放目标类型
        public void Cast()
        {
            Caster?.OnTrigger(TriggerType.BeforeSkillCast,this);
            LeftSkillCD = DefaultSkillCDMs/1000.0f;//todo 读取角色CD缩减
            base.OnActive();
            Caster?.OnTrigger(TriggerType.AfterSkillCast,this);
        }
        public override void Clone(TriggerList copy)
        {
            if (copy is Skill copySkill)
            {
                copySkill.TempleteID = TempleteID;
            }
            base.Clone(copy);
        }
        public void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
            base.ReadFromFile(reader);
        }

        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            base.WriteToFile(writer);
        }
        public void SetSkillValue(DataRowBase dataTable)
        {
            base.SetSkillValue(dataTable);
        }

        public void OnSkillBeforeShakeEnd()
        {
            OnTrigger(TriggerType.SkillBeforeShakeEnd);
        }

        public void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (LeftSkillCD > 0)
            {
                LeftSkillCD -= elapseSeconds;
            }
        }
    }
}