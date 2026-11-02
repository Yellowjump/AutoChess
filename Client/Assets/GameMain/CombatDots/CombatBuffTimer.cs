using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Pool;

namespace Zizouqi.CombatDots
{
    public struct CombatBuffTriggerLink : IBufferElementData
    {
        public Entity Trigger;
        public int Order;
    }

    public struct CombatBuffTimer : IComponentData
    {
        public int Handle;
        public float RemainingMs;
        public bool Enabled;
        public bool Active;
        public bool Valid;
        public int Value;
        public int LastResetTick;
        public int OwnerHandle;
        public int ProjectileHandle;
        public int BuffId;
        public bool IdentityConfigured;
        public int Tags;
        public int DurationMs;
        public int MaxLayerNum;
        public bool RefreshOtherDuration;
        public int ActiveOrder;
        public bool AuraRegistered;
        public int AuraOrder;
    }

    public struct CombatDeferredBuffRelease : IBufferElementData
    {
        public int TimerHandle;
    }

    public struct CombatUnitBuffTimer : IBufferElementData
    {
        public int TimerHandle;
    }

    [DisableAutoCreation]
    public partial class CombatBuffTimerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaMs = CombatBuffTimerBridge.ConsumeDeltaMs();
            Entities.ForEach((ref CombatBuffTimer timer) =>
            {
                if (timer.Enabled)
                {
                    timer.RemainingMs -= deltaMs;
                }
            }).ScheduleParallel();
            Dependency.Complete();
        }
    }

    public static class CombatBuffTimerBridge
    {
        private static readonly Dictionary<int, Entity> Timers = new Dictionary<int, Entity>();
        private static World s_World;
        private static EntityQuery s_TimersQuery;
        private static bool s_QueryInitialized;
        private static int s_NextHandle;
        public static int CurrentTick => CombatBoardBridge.CurrentBuffTick();

        public static void QueueDeferredRelease(int ownerHandle, int timerHandle)
        {
            if (!CombatAttributeBridge.TryGetEntity(ownerHandle, out Entity owner))
            {
                throw new InvalidOperationException("Deferred Buff release requires an active combat unit.");
            }
            EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!manager.HasBuffer<CombatDeferredBuffRelease>(owner))
            {
                manager.AddBuffer<CombatDeferredBuffRelease>(owner);
            }
            manager.GetBuffer<CombatDeferredBuffRelease>(owner).Add(
                new CombatDeferredBuffRelease { TimerHandle = timerHandle });
        }

        public static void DrainDeferredReleases(int ownerHandle, List<int> handles)
        {
            handles.Clear();
            if (!CombatAttributeBridge.TryGetEntity(ownerHandle, out Entity owner))
            {
                return;
            }
            EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!manager.HasBuffer<CombatDeferredBuffRelease>(owner))
            {
                return;
            }
            DynamicBuffer<CombatDeferredBuffRelease> pending =
                manager.GetBuffer<CombatDeferredBuffRelease>(owner);
            for (int i = 0; i < pending.Length; i++)
            {
                handles.Add(pending[i].TimerHandle);
            }
            pending.Clear();
        }

        public static void Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0 || !TryGetManager(out _))
            {
                return;
            }

            CombatBoardBridge.AdvanceBuffClock(deltaSeconds * 1000f);
            s_World.GetOrCreateSystemManaged<CombatBuffTimerSystem>().Update();
        }

        internal static float ConsumeDeltaMs()
        {
            return CombatBoardBridge.ConsumeBuffDeltaMs();
        }

        public static bool SetRemaining(ref int handle, float remainingMs)
        {
            if (!TryGetManager(out EntityManager manager))
            {
                return false;
            }

            if (handle == 0 || !Timers.TryGetValue(handle, out Entity entity) || !manager.Exists(entity))
            {
                Timers.Remove(handle);
                entity = manager.CreateEntity(typeof(CombatBuffTimer));
                handle = ++s_NextHandle;
                Timers.Add(handle, entity);
                manager.SetComponentData(entity, new CombatBuffTimer
                {
                    Handle = handle,
                    Valid = true,
                    LastResetTick = -1
                });
            }

            CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
            timer.RemainingMs = remainingMs;
            if (!timer.Enabled)
            {
                timer.LastResetTick = CurrentTick;
            }
            manager.SetComponentData(entity, timer);
            return true;
        }

        public static void Configure(int handle, bool valid, int value)
        {
            if (TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
                timer.Valid = valid;
                timer.Value = value;
                manager.SetComponentData(entity, timer);
            }
        }

        public static void ConfigureIdentity(int handle, int ownerHandle, int buffId,
            int tags, int durationMs, int maxLayerNum, bool refreshOtherDuration)
        {
            if (TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
                if (timer.OwnerHandle != ownerHandle)
                    RemoveFromOwner(timer.OwnerHandle, handle, manager);
                timer.OwnerHandle = ownerHandle;
                timer.BuffId = buffId;
                timer.IdentityConfigured = true;
                timer.Tags = tags;
                timer.DurationMs = durationMs;
                timer.MaxLayerNum = maxLayerNum;
                timer.RefreshOtherDuration = refreshOtherDuration;
                manager.SetComponentData(entity, timer);
                AddToOwner(ownerHandle, handle, manager);
            }
        }

        // Returns whether the incoming Buff should activate now. Existing Buff
        // callbacks still run in the hot-update layer after this decision.
        public static bool PrepareIncomingStack(int incomingHandle, Func<int, bool> hasBuff,
            out int removeHandle)
        {
            removeHandle = 0;
            if (!TryGetTimer(incomingHandle, out EntityManager manager, out Entity entity))
            {
                return false;
            }
            CombatBuffTimer incoming = manager.GetComponentData<CombatBuffTimer>(entity);
            if (incoming.MaxLayerNum == 0)
            {
                return true;
            }
            if (incoming.MaxLayerNum < 0)
            {
                return RegisterAura(incomingHandle);
            }
            List<int> matches = ListPool<int>.Get();
            try
            {
                CollectActiveById(incoming.OwnerHandle, incoming.BuffId, matches);
                float minRemaining = float.MaxValue;
                foreach (int handle in matches)
                {
                    if (!hasBuff(handle) || !TryGetTimer(handle, out _, out Entity existingEntity))
                    {
                        continue;
                    }
                    CombatBuffTimer existing = manager.GetComponentData<CombatBuffTimer>(existingEntity);
                    if (existing.RemainingMs < minRemaining)
                    {
                        minRemaining = existing.RemainingMs;
                        removeHandle = handle;
                    }
                    if (incoming.RefreshOtherDuration)
                    {
                        existing.RemainingMs = existing.DurationMs;
                        if (!existing.Enabled)
                        {
                            existing.LastResetTick = CurrentTick;
                        }
                        manager.SetComponentData(existingEntity, existing);
                    }
                }
                if (matches.Count < incoming.MaxLayerNum)
                {
                    removeHandle = 0;
                }
                return true;
            }
            finally
            {
                ListPool<int>.Release(matches);
            }
        }

        public static void ConfigureProjectileOwner(int handle, int projectileHandle)
        {
            if (TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
                timer.ProjectileHandle = projectileHandle;
                manager.SetComponentData(entity, timer);
            }
        }

        public static void SetActive(int handle, bool active)
        {
            if (TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
                timer.Active = active;
                if (active)
                {
                    timer.ActiveOrder = CombatBoardBridge.AllocateBuffActiveOrder();
                }
                manager.SetComponentData(entity, timer);
            }
        }

        public static bool RegisterAura(int handle)
        {
            if (!TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                return false;
            }
            CombatBuffTimer aura = manager.GetComponentData<CombatBuffTimer>(entity);
            bool hasEarlierAura = false;
            if (TryGetOwnerTimers(aura.OwnerHandle, manager,
                    out DynamicBuffer<CombatUnitBuffTimer> ownerTimers))
            {
                for (int i = 0; i < ownerTimers.Length; i++)
                {
                    if (!TryGetTimer(ownerTimers[i].TimerHandle, out _, out Entity other))
                        continue;
                    CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(other);
                    if (timer.AuraRegistered && timer.OwnerHandle == aura.OwnerHandle &&
                        timer.BuffId == aura.BuffId)
                    {
                        hasEarlierAura = true;
                        break;
                    }
                }
            }
            else if (aura.OwnerHandle == 0)
            {
                using NativeArray<CombatBuffTimer> timers = GetTimerQuery(manager)
                    .ToComponentDataArray<CombatBuffTimer>(Allocator.Temp);
                foreach (CombatBuffTimer timer in timers)
                    if (timer.AuraRegistered &&
                        timer.OwnerHandle == 0 && timer.BuffId == aura.BuffId)
                    {
                        hasEarlierAura = true;
                        break;
                    }
            }
            aura.AuraRegistered = true;
            aura.AuraOrder = CombatBoardBridge.AllocateAuraOrder();
            manager.SetComponentData(entity, aura);
            return !hasEarlierAura;
        }

        public static int RemoveAura(int handle, out bool wasActive)
        {
            wasActive = false;
            if (!TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                return 0;
            }
            CombatBuffTimer aura = manager.GetComponentData<CombatBuffTimer>(entity);
            wasActive = aura.Active;
            if (!aura.AuraRegistered)
            {
                return 0;
            }
            aura.AuraRegistered = false;
            manager.SetComponentData(entity, aura);
            if (!wasActive)
            {
                return 0;
            }

            int nextHandle = 0;
            int nextOrder = int.MaxValue;
            if (TryGetOwnerTimers(aura.OwnerHandle, manager,
                    out DynamicBuffer<CombatUnitBuffTimer> ownerTimers))
            {
                for (int i = 0; i < ownerTimers.Length; i++)
                {
                    if (!TryGetTimer(ownerTimers[i].TimerHandle, out _, out Entity other))
                        continue;
                    CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(other);
                    if (timer.AuraRegistered && timer.Valid && timer.OwnerHandle == aura.OwnerHandle &&
                        timer.BuffId == aura.BuffId && timer.AuraOrder < nextOrder)
                    {
                        nextHandle = timer.Handle;
                        nextOrder = timer.AuraOrder;
                    }
                }
            }
            else if (aura.OwnerHandle == 0)
            {
                using NativeArray<CombatBuffTimer> timers = GetTimerQuery(manager)
                    .ToComponentDataArray<CombatBuffTimer>(Allocator.Temp);
                foreach (CombatBuffTimer timer in timers)
                    if (timer.AuraRegistered && timer.Valid && timer.OwnerHandle == 0 &&
                        timer.BuffId == aura.BuffId && timer.AuraOrder < nextOrder)
                    {
                        nextHandle = timer.Handle;
                        nextOrder = timer.AuraOrder;
                    }
            }
            return nextHandle;
        }

        public static void CollectActiveById(int ownerHandle, int buffId, List<int> handles)
        {
            CollectActive(ownerHandle, buffId, 0, handles);
        }

        public static void CollectActiveWithTag(int ownerHandle, int tag, List<int> handles)
        {
            CollectActive(ownerHandle, 0, tag, handles);
        }

        public static bool HasActiveWithTag(int ownerHandle, int tag)
        {
            if (ownerHandle == 0 || !TryGetManager(out EntityManager manager))
                return false;
            if (!TryGetOwnerTimers(ownerHandle, manager,
                    out DynamicBuffer<CombatUnitBuffTimer> handles)) return false;
            for (int i = 0; i < handles.Length; i++)
            {
                if (!TryGetTimer(handles[i].TimerHandle, out _, out Entity entity))
                    continue;
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
                if (timer.OwnerHandle == ownerHandle &&
                    timer.ProjectileHandle == 0 && timer.Valid &&
                    timer.Active && (timer.Tags & tag) == tag)
                    return true;
            }
            return false;
        }

        public static void CollectActiveForOwner(int ownerHandle, List<int> handles)
        {
            CollectActive(ownerHandle, 0, 0, handles, true);
        }

        // The parallel timer system has already advanced ordinary active Buffs.
        // Only expired timers and timers reset after that system ran need a
        // managed visit during this unit's ordered update.
        public static void CollectDueForOwner(int ownerHandle, List<int> handles)
        {
            handles.Clear();
            if (!TryGetManager(out EntityManager manager) ||
                !TryGetOwnerTimers(ownerHandle, manager,
                    out DynamicBuffer<CombatUnitBuffTimer> ownerTimers))
                return;
            int currentTick = CurrentTick;
            for (int i = 0; i < ownerTimers.Length; i++)
            {
                if (!TryGetTimer(ownerTimers[i].TimerHandle, out _,
                        out Entity timerEntity)) continue;
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(
                    timerEntity);
                if (timer.OwnerHandle != ownerHandle || timer.ProjectileHandle != 0 ||
                    !timer.Valid || !timer.Active) continue;
                if (timer.LastResetTick == currentTick ||
                    (timer.DurationMs != 0 && timer.RemainingMs <= 0))
                    handles.Add(timer.Handle);
            }
            handles.Sort(CompareActiveOrder);
        }

        public static void PauseActiveForOwner(int ownerHandle)
        {
            List<int> handles = ListPool<int>.Get();
            try
            {
                CollectActiveForOwner(ownerHandle, handles);
                foreach (int handle in handles)
                {
                    SetEnabled(handle, false);
                }
            }
            finally
            {
                ListPool<int>.Release(handles);
            }
        }

        public static void CollectActiveForProjectile(int projectileHandle, List<int> handles)
        {
            CollectActive(0, 0, 0, handles, true, projectileHandle);
        }

        public static void CollectActiveByProjectileId(int projectileHandle, int buffId,
            List<int> handles)
        {
            CollectActive(0, buffId, 0, handles, false, projectileHandle);
        }

        public static void SortShieldsByExpiry(List<int> handles)
        {
            handles.Sort((left, right) =>
            {
                if (!TryGetTimer(left, out EntityManager manager, out Entity leftEntity) ||
                    !TryGetTimer(right, out _, out Entity rightEntity))
                {
                    return left.CompareTo(right);
                }
                CombatBuffTimer a = manager.GetComponentData<CombatBuffTimer>(leftEntity);
                CombatBuffTimer b = manager.GetComponentData<CombatBuffTimer>(rightEntity);
                int order = a.DurationMs != 0 && b.DurationMs != 0
                    ? a.RemainingMs.CompareTo(b.RemainingMs)
                    : (a.DurationMs == 0).CompareTo(b.DurationMs == 0);
                return order != 0 ? order : a.ActiveOrder.CompareTo(b.ActiveOrder);
            });
        }

        public static float GetRemaining(int handle)
        {
            return TryGetRemaining(handle, out float remaining) ? remaining : 0;
        }

        // Buff destruction can recursively fire another damage event. Keep the
        // shield roster local and read each remaining shield value after callbacks.
        public static bool ResolveIncomingDamage(int ownerHandle, int damage,
            Func<int, bool> hasBuff, Action<int> destroyBuff)
        {
            const int shieldAttribute = 5;
            const int hpAttribute = 1;
            const int shieldTag = 1 << 7;
            int shieldValue = CombatAttributeBridge.GetValue(ownerHandle, shieldAttribute);
            CombatAttributeBridge.SplitDamage(damage, shieldValue,
                out int shieldLoss, out int hpLoss, out bool consumeAllShields);

            bool missingShield = false;
            if (consumeAllShields || shieldValue >= 0)
            {
                List<int> shields = ListPool<int>.Get();
                try
                {
                    CollectActiveWithTag(ownerHandle, shieldTag, shields);
                    for (int i = shields.Count - 1; i >= 0; i--)
                    {
                        if (!hasBuff(shields[i]))
                            shields.RemoveAt(i);
                    }
                    if (consumeAllShields)
                    {
                        foreach (int handle in shields)
                        {
                            SetValue(handle, 0);
                            destroyBuff(handle);
                        }
                    }
                    else
                    {
                        SortShieldsByExpiry(shields);
                        int remainingDamage = damage;
                        int index = 0;
                        while (remainingDamage > 0)
                        {
                            if (index >= shields.Count)
                            {
                                missingShield = true;
                                break;
                            }
                            int handle = shields[index];
                            TryGetValue(handle, out int value);
                            if (remainingDamage > value)
                            {
                                remainingDamage -= value;
                                SetValue(handle, 0);
                                index++;
                                destroyBuff(handle);
                            }
                            else
                            {
                                SetValue(handle, value - remainingDamage);
                                remainingDamage = 0;
                            }
                        }
                    }
                }
                finally
                {
                    ListPool<int>.Release(shields);
                }
            }
            if (shieldValue >= 0)
            {
                CombatAttributeBridge.AddNum(ownerHandle, shieldAttribute, -shieldLoss);
            }
            if (shieldValue < 0 || consumeAllShields)
            {
                CombatAttributeBridge.AddNum(ownerHandle, hpAttribute, -hpLoss);
            }
            return missingShield;
        }

        public static void ApplyShield(int buffHandle, bool remove, int changeType,
            int fixedValue, int attributeType, int attributePercent)
        {
            if (!TryGetTimer(buffHandle, out EntityManager manager, out Entity entity))
            {
                return;
            }
            CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
            const int shieldTag = 1 << 7;
            const int shieldAttribute = 5;
            const int shieldBoostAttribute = 6;
            if ((timer.Tags & shieldTag) == 0 || timer.OwnerHandle == 0)
            {
                return;
            }
            if (remove)
            {
                if (timer.Value > 0)
                {
                    CombatAttributeBridge.AddNum(timer.OwnerHandle, shieldAttribute, -timer.Value);
                }
                return;
            }

            float shieldValue = 0;
            switch (changeType)
            {
                case 0: // AttributeChangeType.ConstNum
                    shieldValue = fixedValue;
                    break;
                case 1: // AttributeChangeType.FixNumAddAttrPercent
                    float attributeValue = attributePercent > 0
                        ? CombatAttributeBridge.GetValue(timer.OwnerHandle, attributeType) *
                          attributePercent / 100f : 0;
                    shieldValue = fixedValue + attributeValue;
                    break;
            }
            int boost = CombatAttributeBridge.GetValue(timer.OwnerHandle, shieldBoostAttribute);
            timer.Value = Mathf.FloorToInt(shieldValue * (1 + boost / 100f));
            manager.SetComponentData(entity, timer);
            CombatAttributeBridge.AddNum(timer.OwnerHandle, shieldAttribute, timer.Value);
        }

        private static void CollectActive(int ownerHandle, int buffId, int tag,
            List<int> handles, bool allIds = false, int projectileHandle = 0)
        {
            handles.Clear();
            if (ownerHandle == 0 && projectileHandle == 0)
            {
                return;
            }
            if (!TryGetManager(out EntityManager manager))
            {
                return;
            }
            if (projectileHandle != 0)
            {
                if (!CombatProjectileBridge.TryCollectBuffTimers(projectileHandle,
                        handles)) return;
                for (int i = handles.Count - 1; i >= 0; i--)
                {
                    if (!TryGetTimer(handles[i], out _, out Entity timerEntity))
                    {
                        handles.RemoveAt(i);
                        continue;
                    }
                    CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(
                        timerEntity);
                    if (timer.ProjectileHandle != projectileHandle || !timer.Valid ||
                        !timer.Active ||
                        !(allIds || (tag == 0 ? timer.BuffId == buffId :
                            (timer.Tags & tag) == tag)))
                        handles.RemoveAt(i);
                }
                handles.Sort(CompareActiveOrder);
                return;
            }
            if (!TryGetOwnerTimers(ownerHandle, manager,
                    out DynamicBuffer<CombatUnitBuffTimer> ownerTimers)) return;
            for (int i = 0; i < ownerTimers.Length; i++)
            {
                if (!TryGetTimer(ownerTimers[i].TimerHandle, out _, out Entity timerEntity))
                    continue;
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(timerEntity);
                if (timer.OwnerHandle == ownerHandle && timer.ProjectileHandle == 0 &&
                    timer.Valid && timer.Active &&
                    (allIds || (tag == 0 ? timer.BuffId == buffId :
                        (timer.Tags & tag) == tag)))
                    handles.Add(timer.Handle);
            }
            handles.Sort(CompareActiveOrder);
        }

        private static int CompareActiveOrder(int left, int right)
        {
            TryGetTimer(left, out EntityManager manager, out Entity leftEntity);
            TryGetTimer(right, out _, out Entity rightEntity);
            CombatBuffTimer a = manager.GetComponentData<CombatBuffTimer>(leftEntity);
            CombatBuffTimer b = manager.GetComponentData<CombatBuffTimer>(rightEntity);
            return a.ActiveOrder.CompareTo(b.ActiveOrder);
        }

        private static EntityQuery GetTimerQuery(EntityManager manager)
        {
            if (!s_QueryInitialized)
            {
                s_TimersQuery = manager.CreateEntityQuery(ComponentType.ReadOnly<CombatBuffTimer>());
                s_QueryInitialized = true;
            }
            return s_TimersQuery;
        }

        public static bool TryGetValid(int handle, out bool valid)
        {
            valid = false;
            if (!TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                return false;
            }
            valid = manager.GetComponentData<CombatBuffTimer>(entity).Valid;
            return true;
        }

        public static void SetValid(int handle, bool valid)
        {
            if (TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
                timer.Valid = valid;
                manager.SetComponentData(entity, timer);
            }
        }

        public static bool TryBeginDestroy(int handle)
        {
            if (!TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                return false;
            }
            CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
            if (!timer.Valid)
            {
                return false;
            }
            timer.Valid = false;
            timer.Enabled = false;
            manager.SetComponentData(entity, timer);
            return true;
        }

        public static bool TryGetAura(int handle, out bool isAura)
        {
            isAura = false;
            if (!TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                return false;
            }
            CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
            if (!timer.IdentityConfigured)
            {
                return false;
            }
            isAura = timer.MaxLayerNum < 0;
            return true;
        }

        public static bool TryHasTags(int handle, int checkTags, out bool hasTags)
        {
            hasTags = false;
            if (!TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                return false;
            }
            CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
            if (!timer.IdentityConfigured)
            {
                return false;
            }
            hasTags = (timer.Tags & checkTags) == checkTags;
            return true;
        }

        public static bool TryGetValue(int handle, out int value)
        {
            value = 0;
            if (!TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                return false;
            }
            value = manager.GetComponentData<CombatBuffTimer>(entity).Value;
            return true;
        }

        public static void SetValue(int handle, int value)
        {
            if (TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
                timer.Value = value;
                manager.SetComponentData(entity, timer);
            }
        }

        public static bool TryGetRemaining(int handle, out float remainingMs)
        {
            remainingMs = 0;
            if (!TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                return false;
            }

            remainingMs = manager.GetComponentData<CombatBuffTimer>(entity).RemainingMs;
            return true;
        }

        public static void SetEnabled(int handle, bool enabled)
        {
            if (TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
                timer.Enabled = enabled;
                if (enabled)
                {
                    timer.LastResetTick = CurrentTick;
                }
                manager.SetComponentData(entity, timer);
            }
        }

        public static bool AdvanceAndCheckExpired(int handle, float deltaMs)
        {
            if (TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                CombatBuffTimer timer = manager.GetComponentData<CombatBuffTimer>(entity);
                if (timer.LastResetTick == CurrentTick)
                {
                    timer.RemainingMs -= deltaMs;
                    timer.LastResetTick = -1;
                    manager.SetComponentData(entity, timer);
                }
                return timer.DurationMs != 0 && timer.RemainingMs <= 0;
            }
            return false;
        }

        public static void Release(ref int handle)
        {
            if (TryGetTimer(handle, out EntityManager manager, out Entity entity))
            {
                if (manager.HasBuffer<CombatBuffTriggerLink>(entity))
                {
                    int count = manager.GetBuffer<CombatBuffTriggerLink>(entity).Length;
                    for (int i = 0; i < count; i++)
                    {
                        Entity trigger = manager.GetBuffer<CombatBuffTriggerLink>(entity)[i].Trigger;
                        if (manager.Exists(trigger)) manager.DestroyEntity(trigger);
                    }
                }
                RemoveFromOwner(manager.GetComponentData<CombatBuffTimer>(entity).OwnerHandle,
                    handle, manager);
                manager.DestroyEntity(entity);
            }
            Timers.Remove(handle);
            handle = 0;
        }

        public static bool TryGetEntity(int handle, out Entity entity)
        {
            return TryGetTimer(handle, out _, out entity);
        }

        private static bool TryGetOwnerTimers(int ownerHandle, EntityManager manager,
            out DynamicBuffer<CombatUnitBuffTimer> handles)
        {
            handles = default;
            if (!CombatAttributeBridge.TryGetEntity(ownerHandle, out Entity owner) ||
                !manager.HasBuffer<CombatUnitBuffTimer>(owner)) return false;
            handles = manager.GetBuffer<CombatUnitBuffTimer>(owner);
            return true;
        }

        private static void AddToOwner(int ownerHandle, int timerHandle,
            EntityManager manager)
        {
            if (!TryGetOwnerTimers(ownerHandle, manager, out var handles)) return;
            for (int i = 0; i < handles.Length; i++)
                if (handles[i].TimerHandle == timerHandle) return;
            handles.Add(new CombatUnitBuffTimer { TimerHandle = timerHandle });
        }

        private static void RemoveFromOwner(int ownerHandle, int timerHandle,
            EntityManager manager)
        {
            if (!TryGetOwnerTimers(ownerHandle, manager, out var handles)) return;
            for (int i = handles.Length - 1; i >= 0; i--)
                if (handles[i].TimerHandle == timerHandle) handles.RemoveAt(i);
        }

        private static bool TryGetTimer(int handle, out EntityManager manager, out Entity entity)
        {
            manager = default;
            entity = default;
            return handle != 0 && TryGetManager(out manager) &&
                   Timers.TryGetValue(handle, out entity) && manager.Exists(entity);
        }

        private static bool TryGetManager(out EntityManager manager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                Timers.Clear();
                s_World = world;
                s_TimersQuery = default;
                s_QueryInitialized = false;
            }
            if (world == null || !world.IsCreated)
            {
                manager = default;
                return false;
            }
            manager = world.EntityManager;
            return true;
        }
    }
}
