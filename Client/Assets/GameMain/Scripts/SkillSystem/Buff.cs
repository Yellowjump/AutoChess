using System;
using System.IO;
using Entity;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{

    public class Buff:TriggerList
    {
        public string EditorDesc = string.Empty;
        public int BuffID;
        public int TempleteID;
        public BuffTag OwnBuffTag;
        public int MaxLayerNum;
        public bool FreshOtherBuffDuration;
        public bool IsAura => m_TimerHandle != 0 &&
            CombatBuffTimerBridge.TryGetAura(m_TimerHandle, out bool isAura)
                ? isAura : MaxLayerNum < 0;
        public int DurationMs;//总持续时间，无限是0
        private int m_TimerHandle;
        internal int TimerHandle => m_TimerHandle;
        private bool m_ValidBeforeInit = true;
        public override int paramInt
        {
            get => m_TimerHandle != 0 && CombatBuffTimerBridge.TryGetValue(m_TimerHandle, out int value)
                ? value : base.paramInt;
            set
            {
                if (m_TimerHandle == 0) base.paramInt = value;
                else CombatBuffTimerBridge.SetValue(m_TimerHandle, value);
            }
        }
        public float RemainMs
        {
            get => m_TimerHandle != 0 && CombatBuffTimerBridge.TryGetRemaining(m_TimerHandle, out float remaining)
                ? remaining : 0;
            set
            {
                int previousHandle = m_TimerHandle;
                if (!CombatBuffTimerBridge.SetRemaining(ref m_TimerHandle, value))
                {
                    throw new InvalidOperationException("The DOTS world must exist before buffs are created.");
                }
                if (previousHandle != m_TimerHandle)
                {
                    CombatBuffTimerBridge.Configure(m_TimerHandle, m_ValidBeforeInit, base.paramInt);
                    base.paramInt = 0;
                }
            }
        }
        public bool IsValid
        {
            get => m_TimerHandle != 0 && CombatBuffTimerBridge.TryGetValid(m_TimerHandle, out bool valid)
                ? valid : m_ValidBeforeInit;
            set
            {
                if (m_TimerHandle == 0) m_ValidBeforeInit = value;
                else CombatBuffTimerBridge.SetValid(m_TimerHandle, value);
            }
        }

        public void ActivateTimer()
        {
            if (m_TimerHandle == 0)
            {
                RemainMs = DurationMs;
            }
            CombatBuffTimerBridge.SetEnabled(m_TimerHandle, true);
        }

        public void BindCombatOwner(int ownerHandle)
        {
            if (m_TimerHandle == 0)
            {
                RemainMs = DurationMs;
            }
            CombatBuffTimerBridge.ConfigureIdentity(m_TimerHandle, ownerHandle,
                BuffID, (int)OwnBuffTag, DurationMs, MaxLayerNum,
                FreshOtherBuffDuration);
        }

        public void BindCombatProjectile(int projectileHandle)
        {
            BindCombatOwner(0);
            CombatBuffTimerBridge.ConfigureProjectileOwner(m_TimerHandle, projectileHandle);
        }

        public bool UpdateTimer(float deltaSeconds)
        {
            float deltaMs = deltaSeconds * 1000f;
            return CombatBuffTimerBridge.AdvanceAndCheckExpired(m_TimerHandle, deltaMs);
        }

        public void PauseTimer()
        {
            CombatBuffTimerBridge.SetEnabled(m_TimerHandle, false);
        }
        public override void Clone(TriggerList copy)
        {
            if (copy is Buff copyBuff)
            {
                copyBuff.TempleteID = TempleteID;
                copyBuff.EditorDesc = EditorDesc;
                copyBuff.OwnBuffTag = OwnBuffTag;
            }
            base.Clone(copy);
        }
        public override void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
            EditorDesc = reader.ReadString();
            OwnBuffTag = GetCombinedTags(reader.ReadInt32());
            base.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            writer.Write(EditorDesc);
            writer.Write((int)OwnBuffTag);
            base.WriteToFile(writer);
        }
        private BuffTag GetCombinedTags(int combinedValue)
        {
            BuffTag result = BuffTag.None;
            foreach (BuffTag tag in Enum.GetValues(typeof(BuffTag)))
            {
                if ((combinedValue & (int)tag) != 0)
                {
                    result |= tag;
                }
            }
            return result;
        }

        public bool CheckBuffTag(BuffTag checkTag)
        {
            return m_TimerHandle != 0 && CombatBuffTimerBridge.TryHasTags(
                m_TimerHandle, (int)checkTag, out bool hasTags)
                ? hasTags : (checkTag & OwnBuffTag) == checkTag;
        }
        public override void OnDestory()
        {
            if (m_TimerHandle == 0)
            {
                if (!m_ValidBeforeInit)
                {
                    return;
                }
                m_ValidBeforeInit = false;
            }
            else if (!CombatBuffTimerBridge.TryBeginDestroy(m_TimerHandle))
            {
                return;
            }
            base.OnDestory();
            if (IsAura)
            {
                if (Owner is EntityQizi qizi)
                {
                    qizi.OnAuraBuffDestroy(this);
                }
            }
        }

        public override void Clear()
        {
            CombatBuffTimerBridge.Release(ref m_TimerHandle);
            m_ValidBeforeInit = true;
            base.paramInt = 0;
            BuffID = 0;
            TempleteID = 0;
            OwnBuffTag = BuffTag.None;;
            MaxLayerNum = 0;
            FreshOtherBuffDuration = false;
            DurationMs = 0;
            base.Clear();
        }
    }
}
