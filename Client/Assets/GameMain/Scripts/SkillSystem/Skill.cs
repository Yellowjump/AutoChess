using System.IO;
using Entity;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

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
        private int m_CastBeforeInit;
        private int m_ShakeBeforeInit;
        private int m_AnimationBeforeInit;
        public int CurCastCDMs => CombatCooldownBridge.TryGetCast(m_CooldownHandle,
            out int castMs, out _, out _) ? castMs : m_CastBeforeInit;
        public int CurShakeBeforeMs => CombatCooldownBridge.TryGetCast(m_CooldownHandle,
            out _, out int shakeMs, out _) ? shakeMs : m_ShakeBeforeInit;
        public int CurAnimationDurationMs => CombatCooldownBridge.TryGetCast(m_CooldownHandle,
            out _, out _, out int animationMs) ? animationMs : m_AnimationBeforeInit;
        public override int paramInt
        {
            get => CombatCooldownBridge.TryGetValue(m_CooldownHandle, out int value) ? value : base.paramInt;
            set
            {
                if (m_CooldownHandle == 0) base.paramInt = value;
                else CombatCooldownBridge.SetValue(m_CooldownHandle, value);
            }
        }
        public float LeftSkillCD
        {
            get => m_CooldownHandle != 0 && CombatCooldownBridge.TryGetRemaining(m_CooldownHandle, out float remaining)
                ? remaining : 0;
            set
            {
                int previousHandle = m_CooldownHandle;
                if (!CombatCooldownBridge.SetRemaining(ref m_CooldownHandle, value))
                {
                    throw new System.InvalidOperationException("The DOTS world must exist before skills are cast.");
                }
                if (previousHandle != m_CooldownHandle)
                {
                    CombatCooldownBridge.Configure(m_CooldownHandle, m_CastBeforeInit,
                        m_ShakeBeforeInit, m_AnimationBeforeInit, base.paramInt);
                    base.paramInt = 0;
                }
            }
        }
        private int m_CooldownHandle;
        internal int CooldownHandle => m_CooldownHandle;
        public bool InCD => LeftSkillCD > 0;// 技能还没冷却好，只对自动技能有效，主动技能在attack状态中 每帧计算
        
        public SkillCastTargetType CurSkillCastTargetType;//释放目标类型
        public int CastPower;//释放时需消耗的蓝量，为负时是产生的蓝量
        public void Cast()
        {
            Caster?.OnTrigger(TriggerType.BeforeSkillCast,this);
            int previousHandle = m_CooldownHandle;
            CombatCooldownBridge.BeginCast(ref m_CooldownHandle, Caster.CombatUnitHandle,
                DefaultSkillCDMs, DefaultShakeBeforeMs, DefaultAnimationDurationMs,
                base.paramInt);
            if (previousHandle != m_CooldownHandle)
            {
                base.paramInt = 0;
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
        public override void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
            EditorDesc = reader.ReadString();
            base.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            writer.Write(EditorDesc);
            base.WriteToFile(writer);
        }
        public void OnSkillBeforeShakeEnd()
        {
            OnTrigger(TriggerType.SkillBeforeShakeEnd);
        }

        public override void OnDestory()
        {
            base.OnDestory();
            if (m_CooldownHandle == 0)
            {
                m_CastBeforeInit = DefaultSkillCDMs;
                m_ShakeBeforeInit = DefaultShakeBeforeMs;
                m_AnimationBeforeInit = DefaultAnimationDurationMs;
            }
            else
            {
                CombatCooldownBridge.ResetAfterDestroy(ref m_CooldownHandle, DefaultSkillCDMs,
                    DefaultShakeBeforeMs, DefaultAnimationDurationMs);
            }
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
            m_CastBeforeInit = 0;
            m_ShakeBeforeInit = 0;
            m_AnimationBeforeInit = 0;
            CombatCooldownBridge.Release(ref m_CooldownHandle);
            base.paramInt = 0;
            base.Clear();
        }
    }
}
