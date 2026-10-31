using System.Collections.Generic;
using GameFramework;
using SkillSystem;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Entity.Bullet
{
    public class BulletMarkPoint:BulletBase
    {
        private int m_DurationBeforeInit;
        public int AllDuration
        {
            get => m_ProjectileHandle == 0 ? m_DurationBeforeInit :
                CombatProjectileBridge.GetDuration(m_ProjectileHandle);
            set
            {
                if (m_ProjectileHandle == 0) m_DurationBeforeInit = value;
                else CombatProjectileBridge.SetDuration(m_ProjectileHandle, value);
            }
        }
        private readonly Dictionary<int, Buff> m_BuffByTimerHandle = new Dictionary<int, Buff>();
        private readonly List<int> m_ExpiredBuffHandles = new List<int>();
        internal override void BindRuntime(int handle, int parameter)
        {
            base.BindRuntime(handle, parameter);
        }
        public override void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            LogicUpdate(elapseSeconds, realElapseSeconds, null);
        }

        public void LogicUpdate(float elapseSeconds, float realElapseSeconds,
            CombatProjectileMotionBatch motion)
        {
            if (m_ProjectileHandle == 0)
            {
                return;
            }
            int executingHandle = m_ProjectileHandle;
            base.LogicUpdate(elapseSeconds,realElapseSeconds);
            if (m_ProjectileHandle != executingHandle)
            {
                return;
            }
            UpdateBuffTime(elapseSeconds, realElapseSeconds);
            if (m_ProjectileHandle != executingHandle) return;
            bool completed;
            if (motion == null || !motion.TryApplyMarkPoint(m_ProjectileHandle,
                    out completed))
                completed = CombatProjectileBridge.AdvanceMarkPoint(m_ProjectileHandle,
                    elapseSeconds);
            if (completed)
            {
                OnDead();
            }
        }
        private void UpdateBuffTime(float elapseSeconds, float realElapseSeconds)
        {
            int projectileHandle = m_ProjectileHandle;
            List<int> activeHandles = ListPool<int>.Get();
            CombatBuffTimerBridge.CollectActiveForProjectile(projectileHandle, activeHandles);
            foreach (int handle in activeHandles)
            {
                if (m_ProjectileHandle != projectileHandle) break;
                if (!m_BuffByTimerHandle.TryGetValue(handle, out Buff buff) || !buff.IsValid)
                {
                    continue;
                }
                if (buff.UpdateTimer(elapseSeconds))
                {
                    buff.OnDestory();
                }
                else
                {
                    buff.OnTrigger(TriggerType.PerTick);
                }
            }
            ListPool<int>.Release(activeHandles);
            if (m_ProjectileHandle != projectileHandle) return;
            m_ExpiredBuffHandles.Clear();
            foreach (KeyValuePair<int, Buff> entry in m_BuffByTimerHandle)
            {
                if (!entry.Value.IsValid)
                    m_ExpiredBuffHandles.Add(entry.Key);
            }
            foreach (int handle in m_ExpiredBuffHandles)
            {
                if (m_ProjectileHandle != projectileHandle) break;
                if (!m_BuffByTimerHandle.TryGetValue(handle, out Buff buff))
                    continue;
                m_BuffByTimerHandle.Remove(handle);
                CombatProjectileBridge.RemoveBuffTimer(projectileHandle, handle);
                ReferencePool.Release(buff);
            }
            m_ExpiredBuffHandles.Clear();
        }
        public override void AddBuff(Buff buff)
        {
            if (buff == null)
            {
                return;
            }
            buff.BindCombatProjectile(m_ProjectileHandle);
            m_BuffByTimerHandle[buff.TimerHandle] = buff;
            CombatProjectileBridge.AddBuffTimer(m_ProjectileHandle,
                buff.TimerHandle);
            CombatBuffTimerBridge.SetActive(buff.TimerHandle, true);
            buff.ActivateTimer();
            buff.OnActive();
        }

        public override void RemoveBuff(int buffID)
        {
            List<int> handles = ListPool<int>.Get();
            CombatBuffTimerBridge.CollectActiveByProjectileId(m_ProjectileHandle, buffID, handles);
            foreach (int handle in handles)
            {
                if (m_BuffByTimerHandle.TryGetValue(handle, out Buff buff))
                {
                    buff.OnDestory();
                }
            }
            ListPool<int>.Release(handles);
        }
        public override void OnDead()
        {
            CombatTriggerProjectileSpawnQueue.FlushBeforeRecycle(this);
            OwnerTriggerList?.OnTrigger(TriggerType.OnDestory);
            List<int> activeHandles = ListPool<int>.Get();
            CombatBuffTimerBridge.CollectActiveForProjectile(m_ProjectileHandle, activeHandles);
            foreach (int handle in activeHandles)
            {
                if (!m_BuffByTimerHandle.TryGetValue(handle, out Buff buff) || !buff.IsValid)
                {
                    continue;
                }
                buff.OnDestory();
            }
            ListPool<int>.Release(activeHandles);
            IsValid = false;
            HotGameEntry.HeroManager.DestoryBullet(this);
        }
        public override void Clear()
        {
            m_DurationBeforeInit = 0;
            foreach (Buff buff in m_BuffByTimerHandle.Values)
                ReferencePool.Release(buff);
            m_BuffByTimerHandle.Clear();
            m_ExpiredBuffHandles.Clear();
            if (m_ProjectileHandle != 0)
                CombatProjectileBridge.ClearBuffTimers(m_ProjectileHandle);
            base.Clear();
        }
    }
}
