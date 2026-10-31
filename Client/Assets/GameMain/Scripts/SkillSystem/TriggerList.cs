using System.Collections.Generic;
using System.IO;
using DataTable;
using GameFramework.DataTable;
using Entity;
using GameFramework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    /// <summary>
    /// 包含多个触发器的容器
    /// </summary>
    public class TriggerList:IReference
    {
        private static ProfilerCounterValue<int> s_PointTimerAttempts =
            new ProfilerCounterValue<int>("Combat.PointTimer.Attempts");
        private static ProfilerCounterValue<int> s_PointTimerScheduled =
            new ProfilerCounterValue<int>("Combat.PointTimer.Scheduled");
        private static ProfilerCounterValue<int> s_PointTimerMultiple =
            new ProfilerCounterValue<int>("Combat.PointTimer.MultiplePerTick");
        private static ProfilerCounterValue<int> s_PointTimerUnsupported =
            new ProfilerCounterValue<int>("Combat.PointTimer.Unsupported");
        public List<OneTrigger> CurTriggerList;
        public Skill ParentSkill;
        public EntityBase Owner;
        private int m_TriggerGroupHandle;
        private OneTrigger m_ScheduledPointTimer;
        private readonly List<int> m_TriggerTypes = new List<int>();
        private int m_ParamInt;
        public virtual int paramInt
        {
            get => m_ParamInt;
            set => m_ParamInt = value;
        }
        public virtual void WriteToFile(BinaryWriter writer)
        {
            writer.Write(CurTriggerList.Count);
            foreach (var oneTrigger in CurTriggerList)
            {
                oneTrigger.WriteToFile(writer);
            }
        }

        public virtual void ReadFromFile(BinaryReader reader)
        {
            m_ScheduledPointTimer = null;
            CombatTriggerGroupBridge.Release(ref m_TriggerGroupHandle);
            var triggerCount = reader.ReadInt32();
            CurTriggerList.Clear();
            for (int triggerIndex = 0; triggerIndex < triggerCount; triggerIndex++)
            {
                var oneTrigger = SkillFactory.CreateNewEmptyTrigger();
                CurTriggerList.Add(oneTrigger);
                oneTrigger.ReadFromFile(reader);
            }
        }

        public virtual void Clone(TriggerList copy)
        {
            CloneRuntime(copy, false, false, false);
        }

        internal void ClonePointRuntime(TriggerList copy, bool skipOnActive,
            bool skipPerTick)
        {
            CloneRuntime(copy, skipOnActive, skipPerTick, false);
        }

        internal void CloneProjectileRuntime(TriggerList copy)
        {
            CloneRuntime(copy, true, false, true);
        }

        private void CloneRuntime(TriggerList copy, bool skipOnActive,
            bool skipPerTick, bool skipOnHit)
        {
            copy.m_ScheduledPointTimer = null;
            CombatTriggerGroupBridge.Release(ref copy.m_TriggerGroupHandle);
            copy.CurTriggerList ??= ListPool<OneTrigger>.Get();
            int destinationIndex = 0;
            for (int i = 0; i < CurTriggerList.Count; i++)
            {
                OneTrigger oneTrigger = CurTriggerList[i];
                if ((skipOnActive && oneTrigger.CurTriggerType == TriggerType.OnActive) ||
                    (skipPerTick && oneTrigger.CurTriggerType == TriggerType.PerTick) ||
                    (skipOnHit &&
                     oneTrigger.CurTriggerType == TriggerType.OnBulletHitTarget))
                    continue;
                OneTrigger oneCopyTrigger;
                if (destinationIndex < copy.CurTriggerList.Count)
                {
                    oneCopyTrigger = copy.CurTriggerList[destinationIndex];
                }
                else
                {
                    oneCopyTrigger = SkillFactory.CreateNewEmptyTrigger();
                    copy.CurTriggerList.Add(oneCopyTrigger);
                }
                oneTrigger.Clone(oneCopyTrigger);
                oneCopyTrigger.ParentTriggerList = copy;
                destinationIndex++;
            }
            for (int i = copy.CurTriggerList.Count - 1; i >= destinationIndex; i--)
            {
                ReferencePool.Release(copy.CurTriggerList[i]);
                copy.CurTriggerList.RemoveAt(i);
            }
        }

        // A pooled projectile or point keeps its trigger graph, but no invocation state.
        public void ResetForReuse()
        {
            m_ScheduledPointTimer = null;
            CombatTriggerGroupBridge.Release(ref m_TriggerGroupHandle);
            m_TriggerTypes.Clear();
            ParentSkill = null;
            Owner = null;
            if (CurTriggerList == null) return;
            foreach (OneTrigger trigger in CurTriggerList)
            {
                trigger.ResetRuntimeState();
            }
        }

        public virtual void SetSkillValue(DataRowBase dataTable)
        {
            m_ScheduledPointTimer = null;
            foreach (var oneTrigger in CurTriggerList)
            {
                oneTrigger.SetSkillValue(dataTable);
            }
        }
        /// <summary>
        /// 当技能释放或buff激活
        /// </summary>
        public void OnActive()
        {
            if (ParentSkill == null)
            {
                return;
            }
            if (Owner == null)
            {
                return;
            }

            if (Owner is EntityQizi qizi)
            {
                EnsureTriggerGroup();
                List<int> slots = ListPool<int>.Get();
                try
                {
                    CombatTriggerGroupBridge.CollectRegistrations(m_TriggerGroupHandle,
                        (int)TriggerType.OnActive, (int)TriggerType.OnDestory, slots);
                    foreach (int slot in slots)
                    {
                        qizi.AddTriggerListen(CurTriggerList[slot]);
                    }
                }
                finally
                {
                    ListPool<int>.Release(slots);
                }
            }
            OnTrigger(TriggerType.OnActive);
        }
        /// <summary>
        /// 技能或buff 结束
        /// </summary>
        public virtual void OnDestory()
        {
            if (ParentSkill == null)
            {
                return;
            }
            if (Owner == null)
            {
                return;
            }

            if (Owner is EntityQizi qizi)
            {
                EnsureTriggerGroup();
                List<int> slots = ListPool<int>.Get();
                try
                {
                    CombatTriggerGroupBridge.CollectRegistrations(m_TriggerGroupHandle,
                        (int)TriggerType.OnActive, (int)TriggerType.OnDestory, slots);
                    foreach (int slot in slots)
                    {
                        qizi.RemoveTriggerListen(CurTriggerList[slot]);
                    }
                }
                finally
                {
                    ListPool<int>.Release(slots);
                }
            }
            OnTrigger(TriggerType.OnDestory);
        }
        public void OnTrigger(TriggerType triggerType,object arg = null)
        {
            EnsureTriggerGroup();
            List<int> slots = ListPool<int>.Get();
            try
            {
                CombatTriggerGroupBridge.Collect(m_TriggerGroupHandle, (int)triggerType, slots);
                foreach (int slot in slots)
                {
                    CurTriggerList[slot].OnTrigger(arg);
                }
            }
            finally
            {
                ListPool<int>.Release(slots);
            }
        }

        public bool TrySchedulePointTimer(int pointHandle)
        {
            s_PointTimerAttempts.Value++;
            OneTrigger timedTrigger = null;
            foreach (OneTrigger trigger in CurTriggerList)
            {
                if (trigger.CurTriggerType != TriggerType.PerTick)
                {
                    continue;
                }
                if (timedTrigger != null)
                {
                    s_PointTimerMultiple.Value++;
                    return false;
                }
                timedTrigger = trigger;
            }
            if (timedTrigger == null || !timedTrigger.TrySchedulePointTimer(pointHandle))
            {
                s_PointTimerUnsupported.Value++;
                return false;
            }
            m_ScheduledPointTimer = timedTrigger;
            s_PointTimerScheduled.Value++;
            return true;
        }

        public void OnPointTick(object arg, bool tickDue)
        {
            if (!tickDue && m_ScheduledPointTimer != null)
            {
                return;
            }
            if (m_ScheduledPointTimer != null &&
                !m_ScheduledPointTimer.HasScheduledPointTimer)
            {
                m_ScheduledPointTimer = null;
            }
            if (m_ScheduledPointTimer == null)
            {
                OnTrigger(TriggerType.PerTick, arg);
            }
            else if (tickDue)
            {
                m_ScheduledPointTimer.OnScheduledPointTick(arg);
            }
        }

        private void EnsureTriggerGroup()
        {
            bool changed = m_TriggerTypes.Count != CurTriggerList.Count;
            if (!changed)
            {
                for (int i = 0; i < CurTriggerList.Count; i++)
                {
                    if (m_TriggerTypes[i] != (int)CurTriggerList[i].CurTriggerType)
                    {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed && CombatTriggerGroupBridge.IsValid(m_TriggerGroupHandle))
            {
                return;
            }
            m_TriggerTypes.Clear();
            foreach (OneTrigger trigger in CurTriggerList)
            {
                m_TriggerTypes.Add((int)trigger.CurTriggerType);
            }
            CombatTriggerGroupBridge.Create(ref m_TriggerGroupHandle, m_TriggerTypes);
        }

        public virtual void Clear()
        {
            m_ScheduledPointTimer = null;
            CombatTriggerGroupBridge.Release(ref m_TriggerGroupHandle);
            m_TriggerTypes.Clear();
            if (CurTriggerList != null)
            {
                foreach (var oneTrigger in CurTriggerList)
                {
                    ReferencePool.Release(oneTrigger);
                }
                CurTriggerList.Clear();
            }
            ParentSkill = null;
            Owner = null;
        }
    }
}
