using System.IO;
using System.Collections.Generic;
using Entity;
using GameFramework;
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
        public ConditionBase CastCondition { get; private set; }

        public void SetCastCondition(ConditionBase condition)
        {
            if (CastCondition == condition) return;
            if (CastCondition != null) ReferencePool.Release(CastCondition);
            CastCondition = condition;
        }

        public bool MeetsCastCondition() => CastCondition == null ||
            CastCondition.OnCheck(null, this) != CastCondition.ReverseResult;
        public int DefaultAnimationDurationMs;//默认技能动画时长
        public int DefaultSkillCDMs;//默认技能CD也是默认技能时长
        public int DefaultShakeBeforeMs;//技能前摇
        public int CurCastCDMs;//当前次释放的时长
        public int CurShakeBeforeMs;//当前次释放的前摇时间
        public int CurAnimationDurationMs;//当前次释放的动画时间
        public float LeftSkillCD;//剩余的冷却时间,
        public bool InCD => LeftSkillCD > 0;// 技能还没冷却好，只对自动技能有效，主动技能在attack状态中 每帧计算
        
        public SkillCastTargetType CurSkillCastTargetType;//释放目标类型
        public bool IsBasicAttack { get; internal set; }
        public int BasicAttackSpeedBonus { get; internal set; }
        public bool IsActiveDamageSkill => !IsBasicAttack && CurSkillType is SkillType.NormalSkill or SkillType.SpSkill;
        private HashSet<int> ignitedTargets;
        private HashSet<int> lightningTargets;
        internal bool TryMarkLightningTarget(int targetId) => (lightningTargets ??= new HashSet<int>()).Add(targetId);
        public bool TryIgniteTarget(int targetId) => (ignitedTargets ??= new HashSet<int>()).Add(targetId);
        public void Cast()
        {
            if (MeetsCastCondition()) CastAfterConditionCheck();
        }

        internal void CastAfterConditionCheck()
        {
            ignitedTargets?.Clear();
            lightningTargets?.Clear();
            BasicAttackSpeedBonus = 0;
            Caster?.OnTrigger(TriggerType.BeforeSkillCast,this);
            var cdr =  (int)Caster.GetAttribute(AttributeType.CooldownReduce).GetFinalValue();
            var reducePercent = cdr / (cdr + 100f);
            float cooldownMs = DefaultSkillCDMs * (1 - reducePercent) / (1f + BasicAttackSpeedBonus / 100f);
            if (Caster.RopeSnareSeconds > 0f && IsBasicAttack) cooldownMs /= .72f;
            CurCastCDMs = Mathf.CeilToInt(cooldownMs);
            LeftSkillCD = cooldownMs / 1000;
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
                var condition = CastCondition == null ? null : SkillFactory.CreateCondition(CastCondition.CurConditionType);
                if (condition != null) CastCondition.Clone(condition);
                copySkill.SetCastCondition(condition);
            }
            base.Clone(copy);
        }
        private const int ExtensionMarker = 0x31545853; // SXT1
        private const int CastConditionField = 1;

        public override void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
            EditorDesc = reader.ReadString();
            base.ReadFromFile(reader);
            SetCastCondition(null);
            var stream = reader.BaseStream;
            if (stream.Position == stream.Length) return;

            int marker = reader.ReadInt32();
            if (marker != ExtensionMarker)
            {
                // The first cast-condition format stored the type directly after the trigger list.
                ReadCastCondition(reader, (ConditionType)marker);
                return;
            }

            while (stream.Position < stream.Length)
            {
                int field = reader.ReadInt32();
                int length = reader.ReadInt32();
                if (length < 0 || length > stream.Length - stream.Position)
                    throw new InvalidDataException("Invalid skill template extension length.");

                if (field == CastConditionField)
                {
                    using var payload = new BinaryReader(new MemoryStream(reader.ReadBytes(length), false));
                    ReadCastCondition(payload, (ConditionType)payload.ReadInt32());
                }
                else
                {
                    stream.Position += length;
                }
            }
        }

        private void ReadCastCondition(BinaryReader reader, ConditionType type)
        {
            SetCastCondition(SkillFactory.CreateCondition(type));
            CastCondition.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            writer.Write(EditorDesc);
            base.WriteToFile(writer);
            writer.Write(ExtensionMarker);
            if (CastCondition == null) return;

            using var stream = new MemoryStream();
            using var payload = new BinaryWriter(stream);
            payload.Write((int)CastCondition.CurConditionType);
            CastCondition.WriteToFile(payload);
            payload.Flush();
            writer.Write(CastConditionField);
            writer.Write((int)stream.Length);
            stream.WriteTo(writer.BaseStream);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            base.SetSkillValue(dataTable);
            CastCondition?.SetSkillValue(dataTable);
        }
        public void OnSkillBeforeShakeEnd()
        {
            OnTrigger(TriggerType.SkillBeforeShakeEnd);
        }

        internal void OnSkillBeforeShakeEnd(IReadOnlyList<EntityBase> targets)
        {
            foreach (var trigger in CurTriggerList)
                if (trigger.CurTriggerType == TriggerType.SkillBeforeShakeEnd)
                    trigger.OnTrigger(null, targets);
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
            SetCastCondition(null);
            ignitedTargets?.Clear();
            lightningTargets?.Clear();
            SkillID = 0;
            TempleteID = 0;
            FromItemID = 0;
            FromItemUID = 0;
            CurSkillType = SkillType.NormalSkill;
            SkillRange = 0;
            IsBasicAttack = false;
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
