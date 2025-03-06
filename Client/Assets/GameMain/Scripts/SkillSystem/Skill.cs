using System.IO;
using Entity;
using UnityEngine;
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
        public int DefaultAnimationDurationMs;//默认技能动画时长
        public int DefaultSkillCDMs;//默认技能CD也是默认技能时长
        public float LeftSkillCD;//剩余的冷却时间,
        public bool InCD => LeftSkillCD > 0;// 技能还没冷却好，只对自动技能有效，主动技能在attack状态中 每帧计算
        public int ShakeBeforeMs;//技能前摇
        public SkillCastTargetType CurSkillCastTargetType;//释放目标类型
        public int CastPower;//释放时需消耗的蓝量，为负时是产生的蓝量
        public void Cast()
        {
            Caster?.OnTrigger(TriggerType.BeforeSkillCast,this);
            var cdr =  (int)Caster.GetAttribute(AttributeType.CooldownReduce).GetFinalValue();
            var reducePercent = cdr / (cdr + 100f);
            LeftSkillCD =DefaultSkillCDMs * (1 - reducePercent)/1000;
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