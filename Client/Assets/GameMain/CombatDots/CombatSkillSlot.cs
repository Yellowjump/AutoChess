using System;
using Unity.Collections;
using Unity.Entities;

namespace Zizouqi.CombatDots
{
    public enum CombatSkillSlotKind : byte
    {
        Normal = 1,
        Automatic = 2,
        Passive = 3,
        Special = 4
    }

    public struct CombatSkillSlot : IBufferElementData
    {
        public CombatSkillSlotKind Kind;
        public int Index;
        public int SkillId;
        public int CastPower;
        public int RangeMm;
        public int TargetType;
    }

    // Decisions from the unit-phase snapshot. Applied later in unit order.
    public struct CombatUnitSkillIntent : IBufferElementData
    {
        public int SkillIndex;
        public int Result;
        public int TargetUid;
    }

    public struct CombatAutomaticSkillIntent : IBufferElementData
    {
        public int Index;
    }

    public static class CombatAutomaticSkillIntentBridge
    {
        public static void Clear(int unitHandle)
        {
            GetIntents(unitHandle).Clear();
        }

        public static void Add(int unitHandle, int index)
        {
            GetIntents(unitHandle).Add(new CombatAutomaticSkillIntent { Index = index });
        }

        public static void Execute(int unitHandle, Action<int> cast)
        {
            DynamicBuffer<CombatAutomaticSkillIntent> intents = GetIntents(unitHandle);
            if (intents.Length == 0)
            {
                return;
            }
            var indices = new NativeArray<int>(intents.Length, Allocator.Temp);
            try
            {
                for (int i = 0; i < intents.Length; i++)
                {
                    indices[i] = intents[i].Index;
                }
                for (int i = 0; i < indices.Length; i++)
                {
                    cast(indices[i]);
                }
            }
            finally
            {
                indices.Dispose();
            }
        }

        private static DynamicBuffer<CombatAutomaticSkillIntent> GetIntents(
            int unitHandle)
        {
            if (!CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
            {
                throw new InvalidOperationException("Automatic skill intent owner was released.");
            }
            EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!manager.HasBuffer<CombatAutomaticSkillIntent>(entity))
            {
                manager.AddBuffer<CombatAutomaticSkillIntent>(entity);
            }
            return manager.GetBuffer<CombatAutomaticSkillIntent>(entity);
        }
    }

    public static class CombatUnitSkillIntentBridge
    {
        public static void Clear(int unitHandle)
        {
            GetIntents(unitHandle).Clear();
        }

        public static void Add(int unitHandle, int skillIndex, int result, int targetUid)
        {
            GetIntents(unitHandle).Add(new CombatUnitSkillIntent
            {
                SkillIndex = skillIndex,
                Result = result,
                TargetUid = targetUid
            });
        }

        public static bool TryGet(int unitHandle, int skillIndex,
            out CombatUnitSkillIntent intent)
        {
            DynamicBuffer<CombatUnitSkillIntent> intents = GetIntents(unitHandle);
            for (int i = 0; i < intents.Length; i++)
            {
                if (intents[i].SkillIndex == skillIndex)
                {
                    intent = intents[i];
                    return true;
                }
            }
            intent = default;
            return false;
        }

        private static DynamicBuffer<CombatUnitSkillIntent> GetIntents(int unitHandle)
        {
            if (!CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
            {
                throw new InvalidOperationException("Skill intent owner has been released.");
            }
            EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!manager.HasBuffer<CombatUnitSkillIntent>(entity))
            {
                manager.AddBuffer<CombatUnitSkillIntent>(entity);
            }
            return manager.GetBuffer<CombatUnitSkillIntent>(entity);
        }
    }

    public static class CombatSkillSlotBridge
    {
        public static void Add(int unitHandle, CombatSkillSlotKind kind, int index,
            int skillId, int castPower, int rangeMm, int targetType)
        {
            GetSlots(unitHandle).Add(new CombatSkillSlot
            {
                Kind = kind,
                Index = index,
                SkillId = skillId,
                CastPower = castPower,
                RangeMm = rangeMm,
                TargetType = targetType
            });
        }

        public static bool TryGetNormal(int unitHandle, int index, out CombatSkillSlot slot)
        {
            DynamicBuffer<CombatSkillSlot> slots = GetSlots(unitHandle);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Kind == CombatSkillSlotKind.Normal && slots[i].Index == index)
                {
                    slot = slots[i];
                    return true;
                }
            }
            slot = default;
            return false;
        }

        public static int GetNormalRange(int unitHandle, int index)
        {
            if (!TryGetNormal(unitHandle, index, out CombatSkillSlot slot))
            {
                throw new InvalidOperationException("Normal skill slot is missing.");
            }
            return slot.RangeMm;
        }

        // Keep the slot order while callbacks may recursively execute combat logic.
        public static void Execute(int unitHandle, CombatSkillSlotKind kind,
            Func<int, int> cooldownHandle, Action<int> cast)
        {
            DynamicBuffer<CombatSkillSlot> slots = GetSlots(unitHandle);
            var indices = new NativeArray<int>(slots.Length, Allocator.Temp);
            try
            {
                int count = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].Kind == kind)
                    {
                        indices[count++] = slots[i].Index;
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    int index = indices[i];
                    if (kind != CombatSkillSlotKind.Automatic ||
                        CombatCooldownBridge.IsReady(cooldownHandle(index)))
                    {
                        cast(index);
                    }
                }
            }
            finally
            {
                indices.Dispose();
            }
        }

        private static DynamicBuffer<CombatSkillSlot> GetSlots(int unitHandle)
        {
            if (!CombatAttributeBridge.TryGetEntity(unitHandle, out Entity entity))
            {
                throw new InvalidOperationException("Skill slot owner has been released.");
            }
            return World.DefaultGameObjectInjectionWorld.EntityManager
                .GetBuffer<CombatSkillSlot>(entity);
        }
    }
}
