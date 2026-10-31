using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine.Pool;

namespace Zizouqi.CombatDots
{
    public enum CombatCommandKind : byte
    {
        ChangeAttribute,
        Shield,
        Damage,
        Repeat,
        RemoveBuff,
        RemoveProjectile,
        CreateBuff,
        CreateProjectile,
        CreatePositionPoint,
        Presentation,
        NoOp
    }

    public enum CombatPresentationKind : byte
    {
        Animation,
        Audio,
        Sfx,
        Weapon
    }

    public struct CombatCommandNode : IBufferElementData
    {
        public CombatCommandKind Kind;
        public int EndIndex;
        public int ManagedIndex;
        public int RepeatNumberType;
        public int RepeatParameter;
        public int AttributeType;
        public int Delta;
        public int ShieldMode;
        public int ShieldFixedValue;
        public int ShieldAttributeType;
        public int ShieldAttributePercent;
        public bool RemoveShield;
        public int DamageMode;
        public int DamageFixedValue;
        public int DamageAttributeType;
        public int DamageAttributePercent;
        public bool TriggerDamageRelate;
        public bool RemoveCurrent;
        public int RemovalId;
        public int BuffIdStart;
        public int BuffIdCount;
        public bool CreateTemporaryBuff;
        public int ProjectileId;
        public int ProjectileParameter;
        public float ProjectileOffsetX;
        public float ProjectileOffsetY;
        public float ProjectileOffsetZ;
        public int PositionPointMode;
        public int PositionPointDurationMs;
        public bool PositionPointLookAtTarget;
        public float PositionPointOffsetX;
        public float PositionPointOffsetY;
        public float PositionPointOffsetZ;
        public float PositionPointOffsetMaxX;
        public float PositionPointOffsetMaxY;
        public float PositionPointOffsetMaxZ;
        public CombatPresentationKind PresentationKind;
        public int PresentationId;
        public int PresentationHandleType;
        public bool PresentationEnable;
        public bool PresentationUseItemWeapon;
    }

    public struct CombatCommandBuffId : IBufferElementData
    {
        public int Value;
    }

    public interface ICombatCommandCallbacks
    {
        int CountSubItem(int subItemId);
        int CurrentBuffHandle();
        int DamageCasterHandle(int commandIndex);
        void ExecuteDamage(int commandIndex, int targetIndex, float damage,
            bool triggerDamageRelate);
        bool RemoveCurrent(int commandIndex);
        void RemoveTarget(int commandIndex, int targetIndex,
            CombatSelectedTarget target, int buffId);
        void CreateTarget(int commandIndex, int targetIndex,
            CombatSelectedTarget target, int buffId,
            CombatCommandNode node);
        bool PresentTarget(int commandIndex, int targetIndex,
            CombatSelectedTarget target, CombatCommandNode node);
    }

    public static class CombatCommandProgramBridge
    {
        private const int MaxFlattenedPureOperations = 512;
        private static readonly ProfilerMarker PureAttributeMarker =
            new ProfilerMarker("Combat.Trigger.CommandProgram.PureAttributes");
        private static readonly ProfilerMarker PureShieldMarker =
            new ProfilerMarker("Combat.Trigger.CommandProgram.PureShields");
        private static readonly ProfilerMarker IndependentDamageMarker =
            new ProfilerMarker("Combat.Trigger.CommandProgram.IndependentDamage");
        private static readonly ProfilerMarker ParallelDamageMarker =
            new ProfilerMarker("Combat.Trigger.CommandProgram.IndependentDamage.Parallel");
        private sealed class ProgramStorage
        {
            public Entity Entity;
            public NativeArray<CombatCommandNode> Nodes;
            public NativeArray<CombatCommandNode> AttributeOperations;
            public NativeArray<CombatCommandNode> ShieldOperations;
            public NativeArray<CombatCommandBuffId> BuffIds;
            public int InvocationDepth;
            public bool ReleaseRequested;
            public int RefCount = 1;
            public bool OnlyAttributeChanges;
            public bool UseAttributeOperations;
            public bool OnlyShieldChanges;
            public bool UseShieldOperations;
        }

        private static readonly Dictionary<int, ProgramStorage> Programs =
            new Dictionary<int, ProgramStorage>();
        private static World s_World;
        private static int s_NextHandle;

        public static bool IsValid(int handle)
        {
            return handle != 0 && TryGetManager(out EntityManager manager) &&
                Programs.TryGetValue(handle, out ProgramStorage storage) &&
                manager.Exists(storage.Entity);
        }

        public static bool IsPureAttributeProgram(int handle)
        {
            return IsValid(handle) && Programs[handle].OnlyAttributeChanges;
        }

        public static void Retain(int handle)
        {
            EntityManager manager = GetManager();
            if (!Programs.TryGetValue(handle, out ProgramStorage storage) ||
                !manager.Exists(storage.Entity))
            {
                throw new InvalidOperationException("Combat command program was released before sharing.");
            }
            storage.RefCount++;
        }

        public static void Create(ref int handle, List<CombatCommandNode> nodes,
            List<int> buffIds)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            Entity entity = manager.CreateEntity();
            var storage = new ProgramStorage
            {
                Entity = entity,
                OnlyAttributeChanges = nodes.Count != 0,
                OnlyShieldChanges = nodes.Count != 0
            };
            try
            {
                DynamicBuffer<CombatCommandNode> buffer =
                    manager.AddBuffer<CombatCommandNode>(entity);
                storage.Nodes = new NativeArray<CombatCommandNode>(nodes.Count,
                    Allocator.Persistent);
                for (int i = 0; i < nodes.Count; i++)
                {
                    buffer.Add(nodes[i]);
                    storage.Nodes[i] = nodes[i];
                    if (nodes[i].Kind != CombatCommandKind.ChangeAttribute ||
                        nodes[i].EndIndex != i + 1)
                        storage.OnlyAttributeChanges = false;
                    if (nodes[i].Kind != CombatCommandKind.Shield ||
                        nodes[i].EndIndex != i + 1)
                        storage.OnlyShieldChanges = false;
                }
                if (!storage.OnlyAttributeChanges && nodes.Count != 0)
                {
                    var attributeOperations = new List<CombatCommandNode>();
                    if (TryCollectPureOperations(nodes, 0, nodes.Count,
                            attributeOperations, 0,
                            CombatCommandKind.ChangeAttribute))
                    {
                        storage.OnlyAttributeChanges = true;
                        storage.UseAttributeOperations = true;
                        storage.AttributeOperations =
                            new NativeArray<CombatCommandNode>(attributeOperations.Count,
                                Allocator.Persistent);
                        for (int i = 0; i < attributeOperations.Count; i++)
                            storage.AttributeOperations[i] = attributeOperations[i];
                    }
                }
                if (!storage.OnlyShieldChanges && nodes.Count != 0)
                {
                    var shieldOperations = new List<CombatCommandNode>();
                    if (TryCollectPureOperations(nodes, 0, nodes.Count,
                            shieldOperations, 0, CombatCommandKind.Shield))
                    {
                        storage.OnlyShieldChanges = true;
                        storage.UseShieldOperations = true;
                        storage.ShieldOperations =
                            new NativeArray<CombatCommandNode>(shieldOperations.Count,
                                Allocator.Persistent);
                        for (int i = 0; i < shieldOperations.Count; i++)
                            storage.ShieldOperations[i] = shieldOperations[i];
                    }
                }
                DynamicBuffer<CombatCommandBuffId> buffIdBuffer =
                    manager.AddBuffer<CombatCommandBuffId>(entity);
                storage.BuffIds = new NativeArray<CombatCommandBuffId>(buffIds.Count,
                    Allocator.Persistent);
                for (int i = 0; i < buffIds.Count; i++)
                {
                    var id = new CombatCommandBuffId { Value = buffIds[i] };
                    buffIdBuffer.Add(id);
                    storage.BuffIds[i] = id;
                }
                handle = ++s_NextHandle;
                Programs.Add(handle, storage);
            }
            catch
            {
                DisposeStorage(storage);
                if (manager.Exists(entity))
                {
                    manager.DestroyEntity(entity);
                }
                throw;
            }
        }

        // Flatten fixed repeats at compile time, keeping the same command order.
        // Item-dependent repeat counts remain on the original callback path.
        private static bool TryCollectPureOperations(List<CombatCommandNode> nodes,
            int start, int end, List<CombatCommandNode> operations, int depth,
            CombatCommandKind allowedKind)
        {
            if (depth > 32) return false;
            for (int index = start; index < end;)
            {
                CombatCommandNode node = nodes[index];
                if (node.EndIndex <= index || node.EndIndex > end) return false;
                switch (node.Kind)
                {
                    case CombatCommandKind.NoOp:
                        break;
                    case CombatCommandKind.Repeat:
                        if (node.RepeatNumberType != 0) return false;
                        if (node.RepeatParameter > 0)
                        {
                            int childStart = operations.Count;
                            if (!TryCollectPureOperations(nodes, index + 1,
                                    node.EndIndex, operations, depth + 1,
                                    allowedKind))
                                return false;
                            int childCount = operations.Count - childStart;
                            if (childCount != 0)
                            {
                                int extra = node.RepeatParameter - 1;
                                if (extra > (MaxFlattenedPureOperations -
                                             operations.Count) / childCount)
                                    return false;
                                for (int repetition = 0; repetition < extra; repetition++)
                                    for (int child = 0; child < childCount; child++)
                                        operations.Add(operations[childStart + child]);
                            }
                        }
                        break;
                    default:
                        if (node.Kind != allowedKind ||
                            operations.Count == MaxFlattenedPureOperations)
                            return false;
                        operations.Add(node);
                        break;
                }
                index = node.EndIndex;
            }
            return true;
        }

        public static bool TryExecutePureShields(int handle, int buffHandle)
        {
            EntityManager manager = GetManager();
            if (!Programs.TryGetValue(handle, out ProgramStorage storage) ||
                !manager.Exists(storage.Entity) || !storage.OnlyShieldChanges)
                return false;
            using var marker = PureShieldMarker.Auto();
            NativeArray<CombatCommandNode> operations = storage.UseShieldOperations
                ? storage.ShieldOperations : storage.Nodes;
            for (int i = 0; i < operations.Length; i++)
            {
                CombatCommandNode node = operations[i];
                CombatBuffTimerBridge.ApplyShield(buffHandle, node.RemoveShield,
                    node.ShieldMode, node.ShieldFixedValue,
                    node.ShieldAttributeType, node.ShieldAttributePercent);
            }
            return true;
        }

        // This fast path has no reactive callbacks, shield changes, or death.
        // Preflight every distinct target before changing any HP value.
        public static bool TryExecuteIndependentDamage(int handle,
            int targetSelectionHandle, int casterHandle)
        {
            EntityManager manager = GetManager();
            if (casterHandle == 0 ||
                !Programs.TryGetValue(handle, out ProgramStorage storage) ||
                !manager.Exists(storage.Entity) || storage.Nodes.Length != 1 ||
                storage.Nodes[0].Kind != CombatCommandKind.Damage ||
                storage.Nodes[0].EndIndex != 1)
                return false;
            CombatCommandNode node = storage.Nodes[0];
            DynamicBuffer<CombatSelectedTarget> targets =
                CombatTargetSelectionBridge.GetBuffer(targetSelectionHandle);
            bool hasUnitTarget = false;
            for (int i = 0; i < targets.Length; i++)
                if (targets[i].Kind == CombatSelectedTargetKind.Unit)
                {
                    hasUnitTarget = true;
                    break;
                }
            if (!hasUnitTarget) return true;
            float damage = CombatAttributeBridge.ComputeDamage(casterHandle,
                node.DamageMode, node.DamageFixedValue,
                node.DamageAttributeType, node.DamageAttributePercent);
            int unitCount = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                CombatSelectedTarget target = targets[i];
                if (target.Kind != CombatSelectedTargetKind.Unit) continue;
                // Damage to the caster can change a later command's formula.
                if (target.Handle == 0 || target.Handle == casterHandle ||
                    !CombatAttributeBridge.TryGetEntity(target.Handle, out _))
                    return false;
                for (int previous = 0; previous < i; previous++)
                    if (targets[previous].Kind == CombatSelectedTargetKind.Unit &&
                        targets[previous].Handle == target.Handle)
                        return false;
                if (!CombatAttributeBridge.CanApplyIndependentDamage(
                        casterHandle, target.Handle, damage,
                        node.TriggerDamageRelate)) return false;
                unitCount++;
            }

            using var marker = IndependentDamageMarker.Auto();
            if (unitCount >= 32)
            {
                List<CombatAttributeIntent> intents =
                    ListPool<CombatAttributeIntent>.Get();
                try
                {
                    for (int i = 0; i < targets.Length; i++)
                    {
                        CombatSelectedTarget target = targets[i];
                        if (target.Kind != CombatSelectedTargetKind.Unit) continue;
                        int uid = CombatAttributeBridge.GetHeroUid(target.Handle);
                        if (!CombatAttributeBridge.TryGetHandleByUid(uid,
                                out int registeredHandle) ||
                            registeredHandle != target.Handle)
                            return false;
                        for (int previous = 0; previous < intents.Count; previous++)
                            if (intents[previous].HeroUid == uid)
                                return false;
                        intents.Add(new CombatAttributeIntent
                        {
                            HeroUid = uid,
                            UnitHandle = target.Handle,
                            AttributeType = 1,
                            Delta = -(int)damage
                        });
                    }
                    using (ParallelDamageMarker.Auto())
                        CombatAttributeBridge.ApplyAttributeIntents(intents);
                }
                finally
                {
                    ListPool<CombatAttributeIntent>.Release(intents);
                }
                for (int i = 0; i < targets.Length; i++)
                {
                    CombatSelectedTarget target = targets[i];
                    if (target.Kind != CombatSelectedTargetKind.Unit) continue;
                    CombatAttributeBridge.GetPosition(target.Handle,
                        out float x, out float y, out float z);
                    CombatBoardBridge.QueueDamageNumber((int)damage,
                        x, y + 0.5f, z);
                }
                return true;
            }
            for (int i = 0; i < targets.Length; i++)
            {
                CombatSelectedTarget target = targets[i];
                if (target.Kind != CombatSelectedTargetKind.Unit) continue;
                CombatAttributeBridge.GetPosition(target.Handle,
                    out float x, out float y, out float z);
                CombatAttributeBridge.AddNum(target.Handle, 1, -(int)damage);
                CombatBoardBridge.QueueDamageNumber((int)damage,
                    x, y + 0.5f, z);
            }
            return true;
        }

        // Attribute-only programs have no callback or structural side effects.
        // The compiled nodes and selected targets are both ECS buffers.
        public static bool TryExecutePureAttributes(int handle, int targetSelectionHandle)
        {
            EntityManager manager = GetManager();
            if (!Programs.TryGetValue(handle, out ProgramStorage storage) ||
                !manager.Exists(storage.Entity) || !storage.OnlyAttributeChanges)
                return false;
            using var marker = PureAttributeMarker.Auto();
            NativeArray<CombatCommandNode> operations = storage.UseAttributeOperations
                ? storage.AttributeOperations : storage.Nodes;

            DynamicBuffer<CombatSelectedTarget> targets =
                CombatTargetSelectionBridge.GetBuffer(targetSelectionHandle);
            for (int i = 0; i < targets.Length; i++)
            {
                CombatSelectedTarget target = targets[i];
                if (target.Kind != CombatSelectedTargetKind.Unit ||
                    !CombatAttributeBridge.TryGetEntity(target.Handle, out _))
                    return false;
            }

            int operationCount = operations.Length * targets.Length;
            int distinctCount = 0;
            int first = 0, second = 0, third = 0;
            if (operationCount >= 32)
            {
                for (int i = 0; i < targets.Length && distinctCount < 4; i++)
                {
                    int uid = CombatAttributeBridge.GetHeroUid(targets[i].Handle);
                    if ((distinctCount > 0 && uid == first) ||
                        (distinctCount > 1 && uid == second) ||
                        (distinctCount > 2 && uid == third))
                        continue;
                    if (distinctCount == 0) first = uid;
                    else if (distinctCount == 1) second = uid;
                    else if (distinctCount == 2) third = uid;
                    distinctCount++;
                }
            }

            if (distinctCount < 4)
            {
                for (int nodeIndex = 0; nodeIndex < operations.Length; nodeIndex++)
                {
                    CombatCommandNode node = operations[nodeIndex];
                    for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                        CombatAttributeBridge.AddNum(targets[targetIndex].Handle,
                            node.AttributeType, node.Delta);
                }
                return true;
            }

            List<CombatAttributeIntent> intents = ListPool<CombatAttributeIntent>.Get();
            try
            {
                for (int nodeIndex = 0; nodeIndex < operations.Length; nodeIndex++)
                {
                    CombatCommandNode node = operations[nodeIndex];
                    for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                    {
                        int unitHandle = targets[targetIndex].Handle;
                        intents.Add(new CombatAttributeIntent
                        {
                            HeroUid = CombatAttributeBridge.GetHeroUid(unitHandle),
                            UnitHandle = unitHandle,
                            AttributeType = node.AttributeType,
                            Delta = node.Delta
                        });
                    }
                }
                CombatAttributeBridge.ApplyAttributeIntents(intents);
            }
            finally
            {
                ListPool<CombatAttributeIntent>.Release(intents);
            }
            return true;
        }

        // Command data is immutable after compilation. Keep its native snapshot alive
        // through recursive callbacks, including callbacks that release this program.
        public static void Execute(int handle, int targetSelectionHandle,
            ICombatCommandCallbacks callbacks)
        {
            EntityManager manager = GetManager();
            if (!Programs.TryGetValue(handle, out ProgramStorage storage) ||
                !manager.Exists(storage.Entity))
            {
                throw new InvalidOperationException("Combat command program was released before execution.");
            }
            storage.InvocationDepth++;
            try
            {
                using (NativeArray<CombatSelectedTarget> targets =
                       CombatTargetSelectionBridge.Snapshot(targetSelectionHandle))
                {
                    ExecuteRange(storage.Nodes, 0, storage.Nodes.Length, targets,
                        storage.BuffIds, callbacks);
                }
            }
            finally
            {
                storage.InvocationDepth--;
                if (storage.InvocationDepth == 0 && storage.ReleaseRequested)
                {
                    DisposeStorage(storage);
                }
            }
        }

        public static void Release(ref int handle)
        {
            if (handle != 0 && TryGetManager(out EntityManager manager) &&
                Programs.TryGetValue(handle, out ProgramStorage storage))
            {
                if (--storage.RefCount > 0)
                {
                    handle = 0;
                    return;
                }
                Programs.Remove(handle);
                if (manager.Exists(storage.Entity))
                {
                    manager.DestroyEntity(storage.Entity);
                }
                storage.ReleaseRequested = true;
                if (storage.InvocationDepth == 0)
                {
                    DisposeStorage(storage);
                }
            }
            handle = 0;
        }

        private static void DisposeStorage(ProgramStorage storage)
        {
            if (storage.Nodes.IsCreated)
            {
                storage.Nodes.Dispose();
            }
            if (storage.AttributeOperations.IsCreated)
            {
                storage.AttributeOperations.Dispose();
            }
            if (storage.ShieldOperations.IsCreated)
            {
                storage.ShieldOperations.Dispose();
            }
            if (storage.BuffIds.IsCreated)
            {
                storage.BuffIds.Dispose();
            }
        }

        private static void ExecuteRange(NativeArray<CombatCommandNode> nodes, int start,
            int end, NativeArray<CombatSelectedTarget> targets,
            NativeArray<CombatCommandBuffId> buffIds, ICombatCommandCallbacks callbacks)
        {
            for (int index = start; index < end; index = nodes[index].EndIndex)
            {
                CombatCommandNode node = nodes[index];
                switch (node.Kind)
                {
                    case CombatCommandKind.Repeat:
                        int count = node.RepeatNumberType == 0 ? node.RepeatParameter :
                            node.RepeatNumberType == 1
                                ? callbacks.CountSubItem(node.RepeatParameter) : 0;
                        for (int iteration = 0; iteration < count; iteration++)
                        {
                            ExecuteRange(nodes, index + 1, node.EndIndex,
                                targets, buffIds, callbacks);
                        }
                        break;
                    case CombatCommandKind.Shield:
                        CombatBuffTimerBridge.ApplyShield(callbacks.CurrentBuffHandle(), node.RemoveShield,
                            node.ShieldMode, node.ShieldFixedValue, node.ShieldAttributeType,
                            node.ShieldAttributePercent);
                        break;
                    case CombatCommandKind.ChangeAttribute:
                        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                        {
                            if (targets[targetIndex].Kind != CombatSelectedTargetKind.Unit)
                            {
                                throw new InvalidCastException("Attribute target must be a combat unit.");
                            }
                            CombatAttributeBridge.AddNum(targets[targetIndex].Handle,
                                node.AttributeType, node.Delta);
                        }
                        break;
                    case CombatCommandKind.Damage:
                        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                        {
                            CombatSelectedTarget target = targets[targetIndex];
                            if (target.Kind != CombatSelectedTargetKind.Unit || target.Handle == 0 ||
                                !CombatAttributeBridge.IsAlive(target.Handle))
                            {
                                continue;
                            }
                            int casterHandle = callbacks.DamageCasterHandle(node.ManagedIndex);
                            float damage = casterHandle == 0 ? 0 :
                                CombatAttributeBridge.ComputeDamage(casterHandle,
                                    node.DamageMode, node.DamageFixedValue,
                                    node.DamageAttributeType, node.DamageAttributePercent);
                            callbacks.ExecuteDamage(node.ManagedIndex, targetIndex, damage,
                                node.TriggerDamageRelate);
                        }
                        break;
                    case CombatCommandKind.RemoveBuff:
                    case CombatCommandKind.RemoveProjectile:
                        if (node.RemoveCurrent && callbacks.RemoveCurrent(node.ManagedIndex))
                        {
                            break;
                        }
                        if (node.Kind == CombatCommandKind.RemoveBuff && node.RemovalId == 0)
                        {
                            break;
                        }
                        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                        {
                            callbacks.RemoveTarget(node.ManagedIndex, targetIndex,
                                targets[targetIndex], node.RemovalId);
                        }
                        break;
                    case CombatCommandKind.CreateBuff:
                        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                        {
                            if (node.CreateTemporaryBuff)
                            {
                                callbacks.CreateTarget(node.ManagedIndex, targetIndex,
                                    targets[targetIndex], 0, node);
                            }
                            else
                            {
                                for (int buffIndex = 0; buffIndex < node.BuffIdCount; buffIndex++)
                                {
                                    callbacks.CreateTarget(node.ManagedIndex, targetIndex,
                                        targets[targetIndex],
                                        buffIds[node.BuffIdStart + buffIndex].Value, node);
                                }
                            }
                        }
                        break;
                    case CombatCommandKind.CreateProjectile:
                    case CombatCommandKind.CreatePositionPoint:
                        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                        {
                            callbacks.CreateTarget(node.ManagedIndex, targetIndex,
                                targets[targetIndex], 0, node);
                        }
                        break;
                    case CombatCommandKind.Presentation:
                        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                        {
                            if (callbacks.PresentTarget(node.ManagedIndex, targetIndex,
                                    targets[targetIndex], node))
                            {
                                break;
                            }
                        }
                        break;
                }
            }
        }

        private static EntityManager GetManager()
        {
            if (!TryGetManager(out EntityManager manager))
            {
                throw new InvalidOperationException("Combat commands require an active ECS World.");
            }
            return manager;
        }

        private static bool TryGetManager(out EntityManager manager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World || world == null || !world.IsCreated)
            {
                foreach (ProgramStorage storage in Programs.Values)
                {
                    storage.ReleaseRequested = true;
                    if (storage.InvocationDepth == 0)
                    {
                        DisposeStorage(storage);
                    }
                }
                Programs.Clear();
                s_World = world;
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
