using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Zizouqi.CombatDots
{
    public struct CombatUnit : IComponentData
    {
        public int HeroUid;
        public int Camp;
        public int UpdateGroup;
        public bool Alive;
        public bool ActiveOnBoard;
        public float3 Position;
        public int AttackTargetUid;
        public int NextNormalSkillIndex;
        public int CurrentNormalSkillIndex;
        public int DeploymentRow;
        public int DeploymentColumn;
    }

    // Logic writes animation requests; the presentation phase consumes them.
    public struct CombatUnitAnimationRequest : IComponentData
    {
        public int AssetId;
        public float Speed;
        public bool Pending;
    }

    public struct CombatUnitFacingRequest : IComponentData
    {
        public float3 Target;
        public bool Pending;
    }

    public enum CombatUnitSfxEventKind : byte
    {
        Add,
        Remove,
        Tick
    }

    public struct CombatUnitSfxEvent : IBufferElementData
    {
        public CombatUnitSfxEventKind Kind;
        public int SfxId;
        public float DeltaSeconds;
    }

    public struct CombatAttribute : IBufferElementData
    {
        public int Type;
        public int BaseValue;
        public int AdditionalValue;
        public int PercentIncrease;
        public int MinValue;
        public int MaxValue;
        public int DynamicMaxType;
        public bool HasMin;
        public bool HasMax;
        public bool PercentBased;
    }

    public struct CombatAttributeIntent
    {
        public int HeroUid;
        public int UnitHandle;
        public int AttributeType;
        public int Delta;
    }

    // The ECS buffer is the only runtime store for combat attribute values.
    public static class CombatAttributeBridge
    {
        [BurstCompile]
        private struct ApplyAttributeIntentsJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<CombatUnit> UnitType;
            public BufferTypeHandle<CombatAttribute> AttributeType;
            [ReadOnly] public NativeArray<CombatAttributeIntent> Intents;
            [NativeDisableParallelForRestriction]
            public NativeArray<int> MissingAttributes;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<CombatUnit> units = chunk.GetNativeArray(ref UnitType);
                BufferAccessor<CombatAttribute> buffers =
                    chunk.GetBufferAccessor(ref AttributeType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask,
                    chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out int unitIndex))
                {
                    int uid = units[unitIndex].HeroUid;
                    DynamicBuffer<CombatAttribute> attributes = buffers[unitIndex];
                    for (int intentIndex = 0; intentIndex < Intents.Length; intentIndex++)
                    {
                        CombatAttributeIntent intent = Intents[intentIndex];
                        if (intent.HeroUid != uid) continue;
                        int attributeIndex = FindIndex(attributes, intent.AttributeType);
                        if (attributeIndex < 0)
                        {
                            MissingAttributes[unfilteredChunkIndex] =
                                intent.AttributeType + 1;
                            continue;
                        }
                        CombatAttribute attribute = attributes[attributeIndex];
                        if (attribute.PercentBased)
                            attribute.AdditionalValue += intent.Delta;
                        else
                            attribute.BaseValue += intent.Delta;
                        attributes[attributeIndex] = attribute;
                    }
                }
            }
        }

        private static readonly Dictionary<int, Entity> Units = new Dictionary<int, Entity>();
        private static readonly Dictionary<int, int> HandlesByUid = new Dictionary<int, int>();
        private static World s_World;
        private static EntityQuery s_UnitQuery;
        private static bool s_UnitQueryInitialized;
        private static EntityQuery s_AttributeIntentQuery;
        private static bool s_AttributeIntentQueryInitialized;
        private static int s_NextHandle;
        private static int s_SpatialVersion;
        public static int SpatialVersion => s_SpatialVersion;

        public static void CreateUnit(ref int handle, int heroUid, int camp, bool alive,
            float x, float y, float z)
        {
            EntityManager manager = GetManager();
            Release(ref handle);
            Entity entity = manager.CreateEntity(typeof(CombatUnit),
                typeof(CombatUnitAnimationRequest),
                typeof(CombatUnitFacingRequest));
            manager.SetComponentData(entity, new CombatUnit
            {
                HeroUid = heroUid,
                Camp = camp,
                UpdateGroup = camp,
                Alive = alive,
                Position = new float3(x, y, z),
                DeploymentRow = -1,
                DeploymentColumn = -1
            });
            manager.AddBuffer<CombatAttribute>(entity);
            manager.AddBuffer<CombatSkillSlot>(entity);
            manager.AddBuffer<CombatUnitSkillIntent>(entity);
            manager.AddBuffer<CombatAutomaticSkillIntent>(entity);
            manager.AddBuffer<CombatUnitSfxEvent>(entity);
            manager.AddBuffer<CombatUnitBuffTimer>(entity);
            handle = ++s_NextHandle;
            Units.Add(handle, entity);
            HandlesByUid[heroUid] = handle;
            MarkSpatialMutation();
        }

        public static void Release(ref int handle)
        {
            if (handle != 0)
            {
                MarkSpatialMutation();
            }
            if (handle != 0 && Units.TryGetValue(handle, out Entity registeredEntity) &&
                s_World != null && s_World.IsCreated &&
                s_World.EntityManager.Exists(registeredEntity))
            {
                int heroUid = s_World.EntityManager.GetComponentData<CombatUnit>(registeredEntity).HeroUid;
                if (HandlesByUid.TryGetValue(heroUid, out int registeredHandle) &&
                    registeredHandle == handle)
                {
                    HandlesByUid.Remove(heroUid);
                }
            }
            if (handle != 0 && s_World != null && s_World.IsCreated &&
                Units.TryGetValue(handle, out Entity entity) && s_World.EntityManager.Exists(entity))
            {
                s_World.EntityManager.DestroyEntity(entity);
            }
            Units.Remove(handle);
            handle = 0;
        }

        public static void AddAttribute(int handle, int type, int baseValue, int minValue,
            int maxValue, int dynamicMaxType, bool percentBased)
        {
            DynamicBuffer<CombatAttribute> attributes = GetAttributes(handle);
            if (FindIndex(attributes, type) >= 0)
            {
                throw new InvalidOperationException($"Duplicate combat attribute {type}.");
            }
            attributes.Add(new CombatAttribute
            {
                Type = type,
                BaseValue = baseValue,
                MinValue = minValue,
                MaxValue = maxValue,
                DynamicMaxType = dynamicMaxType,
                HasMin = minValue != int.MinValue,
                HasMax = maxValue != int.MaxValue,
                PercentBased = percentBased
            });
        }

        public static bool TryGetEntity(int handle, out Entity entity)
        {
            return TryGetUnit(handle, out _, out entity);
        }

        public static void QueueAnimation(int handle, int assetId, float speed)
        {
            if (!TryGetUnit(handle, out EntityManager manager, out Entity entity))
                return;
            manager.SetComponentData(entity, new CombatUnitAnimationRequest
            {
                AssetId = assetId,
                Speed = speed,
                Pending = true
            });
        }

        public static bool TryConsumeAnimation(int handle, out int assetId,
            out float speed)
        {
            assetId = 0;
            speed = 1f;
            if (!TryGetUnit(handle, out EntityManager manager, out Entity entity))
                return false;
            CombatUnitAnimationRequest request =
                manager.GetComponentData<CombatUnitAnimationRequest>(entity);
            if (!request.Pending) return false;
            assetId = request.AssetId;
            speed = request.Speed;
            request.Pending = false;
            manager.SetComponentData(entity, request);
            return true;
        }

        public static void QueueFacing(int handle, float x, float y, float z)
        {
            if (!TryGetUnit(handle, out EntityManager manager, out Entity entity))
                return;
            manager.SetComponentData(entity, new CombatUnitFacingRequest
            {
                Target = new float3(x, y, z),
                Pending = true
            });
        }

        public static bool TryConsumeFacing(int handle, out float3 target)
        {
            target = default;
            if (!TryGetUnit(handle, out EntityManager manager, out Entity entity))
                return false;
            CombatUnitFacingRequest request =
                manager.GetComponentData<CombatUnitFacingRequest>(entity);
            if (!request.Pending) return false;
            target = request.Target;
            request.Pending = false;
            manager.SetComponentData(entity, request);
            return true;
        }

        public static void QueueSfxEvent(int handle, CombatUnitSfxEventKind kind,
            int sfxId = 0, float deltaSeconds = 0f)
        {
            if (!TryGetUnit(handle, out EntityManager manager, out Entity entity))
                return;
            manager.GetBuffer<CombatUnitSfxEvent>(entity).Add(
                new CombatUnitSfxEvent
                {
                    Kind = kind,
                    SfxId = sfxId,
                    DeltaSeconds = deltaSeconds
                });
        }

        public static void QueueSfxTick(int handle, float deltaSeconds,
            bool hasActiveSfx)
        {
            if (!TryGetUnit(handle, out EntityManager manager, out Entity entity))
                return;
            DynamicBuffer<CombatUnitSfxEvent> events =
                manager.GetBuffer<CombatUnitSfxEvent>(entity);
            if (!hasActiveSfx && events.Length == 0) return;
            events.Add(new CombatUnitSfxEvent
            {
                Kind = CombatUnitSfxEventKind.Tick,
                DeltaSeconds = deltaSeconds
            });
        }

        public static void DrainSfxEvents(int handle,
            List<CombatUnitSfxEvent> destination)
        {
            destination.Clear();
            if (!TryGetUnit(handle, out EntityManager manager, out Entity entity))
                return;
            DynamicBuffer<CombatUnitSfxEvent> events =
                manager.GetBuffer<CombatUnitSfxEvent>(entity);
            for (int i = 0; i < events.Length; i++)
                destination.Add(events[i]);
            events.Clear();
        }

        public static void ClearSfxEvents(int handle)
        {
            if (TryGetUnit(handle, out EntityManager manager, out Entity entity))
                manager.GetBuffer<CombatUnitSfxEvent>(entity).Clear();
        }

        public static bool TryGetHandleByUid(int heroUid, out int handle)
        {
            EntityManager manager = GetManager();
            if (HandlesByUid.TryGetValue(heroUid, out handle) &&
                Units.TryGetValue(handle, out Entity entity) && manager.Exists(entity))
            {
                return true;
            }
            handle = 0;
            return false;
        }

        public static void CollectGroupUids(int updateGroup, List<int> heroUids)
        {
            EntityManager manager = GetManager();
            using (NativeArray<CombatUnit> units =
                   GetUnitQuery(manager).ToComponentDataArray<CombatUnit>(Allocator.Temp))
            {
                for (int i = 0; i < units.Length; i++)
                {
                    if (units[i].UpdateGroup == updateGroup)
                    {
                        heroUids.Add(units[i].HeroUid);
                    }
                }
            }
            heroUids.Sort();
        }

        public static NativeArray<CombatUnit> GetUnitSnapshot(Allocator allocator)
        {
            EntityManager manager = GetManager();
            return GetUnitQuery(manager).ToComponentDataArray<CombatUnit>(allocator);
        }

        public static void MarkSpatialMutation()
        {
            unchecked
            {
                s_SpatialVersion++;
            }
        }

        public static int CountAliveInUpdateGroup(int updateGroup)
        {
            EntityManager manager = GetManager();
            int aliveCount = 0;
            using (NativeArray<CombatUnit> units =
                   GetUnitQuery(manager).ToComponentDataArray<CombatUnit>(Allocator.Temp))
            {
                for (int i = 0; i < units.Length; i++)
                {
                    if (units[i].UpdateGroup == updateGroup && units[i].Alive)
                    {
                        aliveCount++;
                    }
                }
            }
            return aliveCount;
        }

        public static int GetCamp(int handle)
        {
            return GetUnit(handle).Camp;
        }

        public static int GetHeroUid(int handle)
        {
            return GetUnit(handle).HeroUid;
        }

        public static void SetCamp(int handle, int camp)
        {
            CombatUnit unit = GetUnit(handle);
            unit.Camp = camp;
            SetUnit(handle, unit);
        }

        public static bool IsAlive(int handle)
        {
            return GetUnit(handle).Alive;
        }

        public static bool IsActiveOnBoard(int handle)
        {
            return GetUnit(handle).ActiveOnBoard;
        }

        public static void SetAlive(int handle, bool alive)
        {
            CombatUnit unit = GetUnit(handle);
            unit.Alive = alive;
            SetUnit(handle, unit);
        }

        public static void GetPosition(int handle, out float x, out float y, out float z)
        {
            float3 position = GetUnit(handle).Position;
            x = position.x;
            y = position.y;
            z = position.z;
        }

        public static void SetPosition(int handle, float x, float y, float z)
        {
            CombatUnit unit = GetUnit(handle);
            unit.Position = new float3(x, y, z);
            unit.ActiveOnBoard = true;
            SetUnit(handle, unit);
        }

        public static int GetAttackTargetUid(int handle)
        {
            return GetUnit(handle).AttackTargetUid;
        }

        public static void SetAttackTargetUid(int handle, int targetUid)
        {
            CombatUnit unit = GetUnit(handle);
            unit.AttackTargetUid = targetUid;
            SetUnit(handle, unit, false);
        }

        public static void GetDeploymentCell(int handle, out int row, out int column)
        {
            CombatUnit unit = GetUnit(handle);
            row = unit.DeploymentRow;
            column = unit.DeploymentColumn;
        }

        public static void SetDeploymentCell(int handle, int row, int column)
        {
            CombatUnit unit = GetUnit(handle);
            unit.DeploymentRow = row;
            unit.DeploymentColumn = column;
            SetUnit(handle, unit, false);
        }

        public static int GetNextNormalSkillIndex(int handle)
        {
            return GetUnit(handle).NextNormalSkillIndex;
        }

        public static void SetNextNormalSkillIndex(int handle, int index)
        {
            CombatUnit unit = GetUnit(handle);
            unit.NextNormalSkillIndex = index;
            SetUnit(handle, unit, false);
        }

        public static int GetCurrentNormalSkillIndex(int handle)
        {
            return GetUnit(handle).CurrentNormalSkillIndex;
        }

        public static void SetCurrentNormalSkillIndex(int handle, int index)
        {
            CombatUnit unit = GetUnit(handle);
            unit.CurrentNormalSkillIndex = index;
            SetUnit(handle, unit, false);
        }

        public static int GetNormalSkillIndexAtOffset(int handle, int skillCount, int offset)
        {
            if (skillCount <= 0 || offset < 0 || offset >= skillCount)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            int index = GetUnit(handle).NextNormalSkillIndex + offset;
            return index >= skillCount ? index - skillCount : index;
        }

        public static bool HasNormalSkillPower(int handle, int castPower)
        {
            return GetValue(handle, 3) >= castPower; // AttributeType.Power
        }

        public static void BeginNormalSkill(int handle, int skillCount, int skillIndex)
        {
            if (skillIndex < 0 || skillIndex >= skillCount)
            {
                throw new ArgumentOutOfRangeException(nameof(skillIndex));
            }
            if (!CombatSkillSlotBridge.TryGetNormal(handle, skillIndex,
                    out CombatSkillSlot slot))
            {
                throw new InvalidOperationException("Normal skill slot is missing.");
            }
            CombatUnit unit = GetUnit(handle);
            unit.CurrentNormalSkillIndex = skillIndex;
            unit.NextNormalSkillIndex = skillIndex + 1 == skillCount ? 0 : skillIndex + 1;
            SetUnit(handle, unit, false);
            AddNum(handle, 3, -slot.CastPower); // AttributeType.Power
        }

        public static int GetValue(int handle, int type)
        {
            DynamicBuffer<CombatAttribute> attributes = GetAttributes(handle);
            return GetValue(attributes, type);
        }

        public static float ComputeDamage(int casterHandle, int computeType,
            int fixedValue, int attributeType, int attributePercent)
        {
            switch (computeType)
            {
                case 0: // DamageComputeType.NormalDamage
                    return GetValue(casterHandle, 8); // AttributeType.AttackDamage
                case 1: // DamageComputeType.FixNumAddAttrPercent
                    float attributeValue = attributePercent > 0
                        ? GetValue(casterHandle, attributeType) * attributePercent / 100 : 0;
                    return fixedValue + attributeValue;
                default:
                    return 0;
            }
        }

        public static void AddNum(int handle, int type, int value)
        {
            DynamicBuffer<CombatAttribute> attributes = GetAttributes(handle);
            int index = RequireIndex(attributes, type);
            CombatAttribute attribute = attributes[index];
            if (attribute.PercentBased)
            {
                attribute.AdditionalValue += value;
            }
            else
            {
                attribute.BaseValue += value;
            }
            attributes[index] = attribute;
        }

        public static bool CanApplyIndependentDamage(int casterHandle,
            int targetHandle, float damageValue, bool triggerRelated)
        {
            if (casterHandle == 0 || targetHandle == 0 ||
                float.IsNaN(damageValue) || float.IsInfinity(damageValue) ||
                damageValue < 0 || damageValue >= int.MaxValue ||
                !IsAlive(targetHandle) ||
                (triggerRelated && CombatTriggerIndex.HasDamageListeners(
                    casterHandle, targetHandle)) ||
                GetValue(targetHandle, 5) != 0 ||
                GetValue(targetHandle, 1) <= (int)damageValue)
                return false;
            // No death transition or reactive trigger may occur in the job batch.
            return !CombatBuffTimerBridge.HasActiveWithTag(targetHandle, 1 << 7);
        }

        public static bool TryApplyIndependentDamage(int casterHandle,
            int targetHandle, float damageValue, bool triggerRelated,
            float x, float y, float z)
        {
            if (!CanApplyIndependentDamage(casterHandle, targetHandle,
                    damageValue, triggerRelated)) return false;
            int damage = (int)damageValue;
            AddNum(targetHandle, 1, -damage);
            CombatBoardBridge.QueueDamageNumber(damage, x, y + 0.5f, z);
            return true;
        }

        // Apply each unit's intents in recorded order. The job only writes the
        // attribute buffer belonging to its own chunk, so units can run in parallel.
        public static void ApplyAttributeIntents(List<CombatAttributeIntent> intents)
        {
            if (intents == null || intents.Count == 0) return;
            EntityManager manager = GetManager();
            EntityQuery query = GetAttributeIntentQuery(manager);
            NativeArray<CombatAttributeIntent> nativeIntents =
                new NativeArray<CombatAttributeIntent>(intents.Count,
                    Allocator.TempJob);
            try
            {
                using NativeArray<int> missingAttributes =
                    new NativeArray<int>(query.CalculateChunkCount(),
                        Allocator.TempJob, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < intents.Count; i++)
                    nativeIntents[i] = intents[i];
                var job = new ApplyAttributeIntentsJob
                {
                    UnitType = manager.GetComponentTypeHandle<CombatUnit>(true),
                    AttributeType = manager.GetBufferTypeHandle<CombatAttribute>(false),
                    Intents = nativeIntents,
                    MissingAttributes = missingAttributes
                };
                job.ScheduleParallel(query, default(JobHandle)).Complete();
                for (int i = 0; i < missingAttributes.Length; i++)
                    if (missingAttributes[i] != 0)
                        throw new InvalidOperationException(
                            $"Combat attribute {missingAttributes[i] - 1} is missing.");
            }
            finally
            {
                nativeIntents.Dispose();
            }
        }

        public static void SetBase(int handle, int type, int value)
        {
            DynamicBuffer<CombatAttribute> attributes = GetAttributes(handle);
            int index = RequireIndex(attributes, type);
            CombatAttribute attribute = attributes[index];
            attribute.BaseValue = value;
            attributes[index] = attribute;
        }

        public static bool AddPercent(int handle, int type, int value)
        {
            DynamicBuffer<CombatAttribute> attributes = GetAttributes(handle);
            int index = RequireIndex(attributes, type);
            CombatAttribute attribute = attributes[index];
            if (!attribute.PercentBased)
            {
                return false;
            }
            attribute.PercentIncrease += value;
            attributes[index] = attribute;
            return true;
        }

        // Resolve damage against the current ECS shield value. Buff expiry still
        // runs in the hot-update layer until buff commands have been converted.
        public static void SplitDamage(int damage, int shield, out int shieldLoss,
            out int hpLoss, out bool consumeAllShields)
        {
            if (shield < 0)
            {
                shieldLoss = 0;
                hpLoss = damage;
                consumeAllShields = false;
            }
            else if (shield <= damage)
            {
                shieldLoss = shield;
                hpLoss = damage - shield;
                consumeAllShields = true;
            }
            else
            {
                shieldLoss = damage;
                hpLoss = 0;
                consumeAllShields = false;
            }
        }

        private static int GetValue(DynamicBuffer<CombatAttribute> attributes, int type)
        {
            CombatAttribute attribute = attributes[RequireIndex(attributes, type)];
            int value = attribute.PercentBased
                ? (int)(attribute.BaseValue * (1000 + attribute.PercentIncrease) / 1000f) + attribute.AdditionalValue
                : attribute.BaseValue;
            if (attribute.HasMin && value < attribute.MinValue)
            {
                value = attribute.MinValue;
            }
            if (attribute.HasMax && value > attribute.MaxValue)
            {
                value = attribute.MaxValue;
            }
            if (attribute.DynamicMaxType != 0)
            {
                int maxValue = GetValue(attributes, attribute.DynamicMaxType);
                if (value > maxValue)
                {
                    value = maxValue;
                }
            }
            return value;
        }

        private static int FindIndex(DynamicBuffer<CombatAttribute> attributes, int type)
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].Type == type)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int RequireIndex(DynamicBuffer<CombatAttribute> attributes, int type)
        {
            int index = FindIndex(attributes, type);
            if (index < 0)
            {
                throw new InvalidOperationException($"Combat attribute {type} is missing.");
            }
            return index;
        }

        private static DynamicBuffer<CombatAttribute> GetAttributes(int handle)
        {
            if (!TryGetUnit(handle, out EntityManager manager, out Entity entity))
            {
                throw new InvalidOperationException("Combat unit has been released.");
            }
            return manager.GetBuffer<CombatAttribute>(entity);
        }

        private static CombatUnit GetUnit(int handle)
        {
            if (!TryGetUnit(handle, out EntityManager manager, out Entity entity))
            {
                throw new InvalidOperationException("Combat unit has been released.");
            }
            return manager.GetComponentData<CombatUnit>(entity);
        }

        private static void SetUnit(int handle, CombatUnit unit,
            bool marksSpatialMutation = true)
        {
            if (!TryGetUnit(handle, out EntityManager manager, out Entity entity))
            {
                throw new InvalidOperationException("Combat unit has been released.");
            }
            manager.SetComponentData(entity, unit);
            if (marksSpatialMutation)
            {
                MarkSpatialMutation();
            }
        }

        private static bool TryGetUnit(int handle, out EntityManager manager, out Entity entity)
        {
            manager = GetManager();
            entity = Entity.Null;
            return handle != 0 && Units.TryGetValue(handle, out entity) && manager.Exists(entity);
        }

        private static EntityManager GetManager()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                Units.Clear();
                HandlesByUid.Clear();
                s_World = world;
                s_UnitQuery = default;
                s_UnitQueryInitialized = false;
                s_AttributeIntentQuery = default;
                s_AttributeIntentQueryInitialized = false;
                MarkSpatialMutation();
            }
            if (world == null || !world.IsCreated)
            {
                throw new InvalidOperationException("The DOTS world must exist before combat units are initialized.");
            }
            return world.EntityManager;
        }

        private static EntityQuery GetUnitQuery(EntityManager manager)
        {
            if (!s_UnitQueryInitialized)
            {
                s_UnitQuery = manager.CreateEntityQuery(typeof(CombatUnit));
                s_UnitQueryInitialized = true;
            }
            return s_UnitQuery;
        }

        private static EntityQuery GetAttributeIntentQuery(EntityManager manager)
        {
            if (!s_AttributeIntentQueryInitialized)
            {
                s_AttributeIntentQuery = manager.CreateEntityQuery(
                    ComponentType.ReadOnly<CombatUnit>(),
                    ComponentType.ReadWrite<CombatAttribute>());
                s_AttributeIntentQueryInitialized = true;
            }
            return s_AttributeIntentQuery;
        }
    }
}
