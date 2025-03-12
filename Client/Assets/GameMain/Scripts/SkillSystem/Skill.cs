using System.IO;
using Entity;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class Skill:TriggerList
    {
        public string EditorDesc = string.Empty;
        public int SkillID;
        public int TempleteID;
        public int FromItemID;//来源道具ID
        public int FromItemUID;//来源道具UID
        public SkillType CurSkillType;
        public int SkillRange;//攻击距离mm
        public EntityQizi Caster;
        public int DefaultAnimationDurationMs;//默认技能动画时长
        public int DefaultSkillCDMs;//默认技能CD也是默认技能时长
        public int DefaultShakeBeforeMs;//技能前摇
        public int CurCastCDMs;//当前次释放的时长
        public int CurShakeBeforeMs;//当前次释放的前摇时间
        public int CurAnimationDurationMs;//当前次释放的动画时间
        public float LeftSkillCD;//剩余的冷却时间,
        public bool InCD => LeftSkillCD > 0;// 技能还没冷却好，只对自动技能有效，主动技能在attack状态中 每帧计算
        
        public SkillCastTargetType CurSkillCastTargetType;//释放目标类型
        public int CastPower;//释放时需消耗的蓝量，为负时是产生的蓝量
        public void Cast()
        {
            Caster?.OnTrigger(TriggerType.BeforeSkillCast,this);
            var cdr =  (int)Caster.GetAttribute(AttributeType.CooldownReduce).GetFinalValue();
            var reducePercent = cdr / (cdr + 100f);
            CurCastCDMs =Mathf.CeilToInt(DefaultSkillCDMs * (1 - reducePercent));
            LeftSkillCD =DefaultSkillCDMs * (1 - reducePercent)/1000;
            CurShakeBeforeMs = DefaultShakeBeforeMs;
            CurAnimationDurationMs = DefaultAnimationDurationMs;
            if (CurCastCDMs < DefaultAnimationDurationMs)//实际CD小于动画时间了，需要动画加速
            {
                CurShakeBeforeMs = Mathf.CeilToInt(DefaultShakeBeforeMs * CurCastCDMs / (float)DefaultAnimationDurationMs);
                CurAnimationDurationMs = CurCastCDMs;
            }
            base.OnActive();
            Caster?.OnTrigger(TriggerType.AfterSkillCast,this);
        }
        public override void Clone(TriggerList copy)
        {
            if (copy is Skill copySkill)
            {
                copySkill.TempleteID = TempleteID;
                copySkill.EditorDesc = EditorDesc;
            }
            base.Clone(copy);
        }
        public void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
            EditorDesc = reader.ReadString();
            base.ReadFromFile(reader);
        }

        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            writer.Write(EditorDesc);
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

        public void LogicUpdateCD(float elapseSeconds, float realElapseSeconds)
        {
            if (LeftSkillCD > 0)
            {
                LeftSkillCD -= elapseSeconds;
            }
        }

        public override void OnDestory()
        {
            base.OnDestory();
            LeftSkillCD = 0;
            CurCastCDMs = DefaultSkillCDMs;
            CurShakeBeforeMs = DefaultShakeBeforeMs;
            CurAnimationDurationMs = DefaultAnimationDurationMs;
        }

        public override void Clear()
        {
            SkillID = 0;
            TempleteID = 0;
            FromItemID = 0;
            FromItemUID = 0;
            CurSkillType = SkillType.NormalSkill;
            SkillRange = 0;
            Caster = null;
            DefaultAnimationDurationMs = 0;
            DefaultSkillCDMs = 0;
            DefaultShakeBeforeMs =0;
            CurCastCDMs = 0;
            CurShakeBeforeMs =0;
            CurAnimationDurationMs =0;
            LeftSkillCD = 0;
            base.Clear();
        }
    }
}