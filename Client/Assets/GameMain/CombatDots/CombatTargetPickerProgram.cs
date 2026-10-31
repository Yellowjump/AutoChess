using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine.Pool;

namespace Zizouqi.CombatDots
{
    public enum CombatTargetPickerKind : byte
    {
        None,
        SkillCasterCurTarget,
        SkillCaster,
        Nearest,
        Arg,
        TriggerOwner,
        RelatedDamageTarget,
        OwnerDirection,
        RandomFromList,
        Bullet,
        OwnerDistance
    }

    public struct CombatTargetPickerNode : IBufferElementData
    {
        public CombatTargetPickerKind Kind;
        public int TargetCamp;
        public int LengthMm;
        public bool UseWeaponLength;
        public int AngleDegrees;
        public int DamageTargetType;
        public int NumberType;
        public int NumberParameter;
        public bool MeetNumber;
        public int ChildIndex;
        public int BulletId;
        public bool SameCaster;
    }

    public struct CombatPickerSource
    {
        public bool IsUnit;
        public int UnitHandle;
        public float X;
        public float Z;
        public int Camp;
    }

    public interface ICombatTargetPickerCallbacks
    {
        void AppendCaster(int selectionHandle);
        void AppendArgument(int selectionHandle);
        void AppendOwner(int selectionHandle);
        void AppendCasterAttackTarget(int selectionHandle);
        bool TryGetCasterUnit(out int unitHandle);
        bool TryGetOwner(bool includePosition, out CombatPickerSource source);
        bool TryGetDamage(out int casterUid, out int targetUid);
        bool ProjectDamageSource(int source);
        int GetParentItemId();
        int GetArgItemId(out bool hasArgSkill);
        int ResolveProjectileCasterUid(bool sameCaster);
        void CollectPendingProjectiles(int bulletId, bool sameCaster,
            List<CombatProjectileSelection.PendingProjectile> pending);
        bool ProjectUnit(int heroUid);
        bool HasManagedUnit(int heroUid);
        bool ProjectOptionalUnit(int heroUid);
        bool ProjectProjectile(int handle, int spawnOrder, bool pending);
        int LookupWeaponLength(int itemId, int fallbackMm);
        int RollIndex(int maxExclusive);
        int CountSubItem(int itemId, int subItemId);
        object CaptureTargets();
        void ApplyChoices(int selectionHandle, object captured);
        void ClearTargets();
        void ReleaseTargets(object captured);
    }

    public static class CombatTargetPickerProgramBridge
    {
        private static readonly Dictionary<int, Entity> Programs = new Dictionary<int, Entity>();
        private static readonly Dictionary<int, int> RefCounts = new Dictionary<int, int>();
        private static World s_World;
        private static int s_NextHandle;

        public static bool IsValid(int handle)
        {
            return handle != 0 && TryGetManager(out EntityManager manager) &&
                Programs.TryGetValue(handle, out Entity entity) && manager.Exists(entity) &&
                RefCounts.ContainsKey(handle);
        }

        public static void Retain(int handle)
        {
            if (!IsValid(handle))
            {
                throw new InvalidOperationException("Combat target picker program was released before sharing.");
            }
            RefCounts[handle]++;
        }

        public static void Execute(int programHandle, int nodeIndex,
            int selectionHandle, ICombatTargetPickerCallbacks callbacks)
        {
            CombatTargetPickerNode node = GetNode(programHandle, nodeIndex);
            switch (node.Kind)
            {
                case CombatTargetPickerKind.None:
                    break;
                case CombatTargetPickerKind.SkillCaster:
                    callbacks.AppendCaster(selectionHandle);
                    break;
                case CombatTargetPickerKind.Arg:
                    callbacks.AppendArgument(selectionHandle);
                    break;
                case CombatTargetPickerKind.TriggerOwner:
                    callbacks.AppendOwner(selectionHandle);
                    break;
                case CombatTargetPickerKind.SkillCasterCurTarget:
                    if (callbacks.TryGetCasterUnit(out int casterHandle))
                    {
                        if (casterHandle == 0)
                        {
                            callbacks.AppendCasterAttackTarget(selectionHandle);
                        }
                        else
                        {
                            SelectAttackTarget(programHandle, nodeIndex, selectionHandle,
                                casterHandle, callbacks.ProjectOptionalUnit);
                        }
                    }
                    break;
                case CombatTargetPickerKind.Nearest:
                    if (callbacks.TryGetOwner(false, out CombatPickerSource nearestSource))
                    {
                        SelectNearest(programHandle, nodeIndex, selectionHandle,
                            nearestSource.IsUnit, nearestSource.UnitHandle,
                            nearestSource.X, nearestSource.Z, nearestSource.Camp,
                            callbacks.ProjectUnit);
                    }
                    break;
                case CombatTargetPickerKind.OwnerDistance:
                    if (callbacks.TryGetOwner(true, out CombatPickerSource distanceSource))
                    {
                        SelectWithinDistance(programHandle, nodeIndex, selectionHandle,
                            distanceSource.X, distanceSource.Z, distanceSource.Camp,
                            callbacks.GetParentItemId(), callbacks.LookupWeaponLength,
                            callbacks.ProjectUnit);
                    }
                    break;
                case CombatTargetPickerKind.OwnerDirection:
                    if (callbacks.TryGetOwner(false, out CombatPickerSource sectorSource) &&
                        sectorSource.IsUnit)
                    {
                        SelectSectorFromAttackTarget(programHandle, nodeIndex,
                            selectionHandle, sectorSource.UnitHandle,
                            callbacks.GetParentItemId(), callbacks.HasManagedUnit,
                            callbacks.LookupWeaponLength, callbacks.ProjectUnit);
                    }
                    break;
                case CombatTargetPickerKind.RelatedDamageTarget:
                    if (callbacks.TryGetDamage(out int damageCasterUid,
                            out int damageTargetUid))
                    {
                        SelectDamageTarget(programHandle, nodeIndex, selectionHandle,
                            damageCasterUid, damageTargetUid,
                            callbacks.ProjectDamageSource);
                    }
                    break;
                case CombatTargetPickerKind.Bullet:
                    SelectProjectiles(programHandle, nodeIndex, selectionHandle,
                        callbacks.ResolveProjectileCasterUid,
                        callbacks.CollectPendingProjectiles,
                        callbacks.ProjectProjectile);
                    break;
                case CombatTargetPickerKind.RandomFromList:
                    if (node.ChildIndex < 0)
                    {
                        break;
                    }
                    Execute(programHandle, node.ChildIndex, selectionHandle, callbacks);
                    if (CombatTargetSelectionBridge.GetBuffer(selectionHandle).Length == 0)
                    {
                        break;
                    }
                    object captured = callbacks.CaptureTargets();
                    try
                    {
                        int argItemId = callbacks.GetArgItemId(out bool hasArgSkill);
                        if (!SelectRandom(programHandle, nodeIndex, selectionHandle,
                                callbacks.GetParentItemId, argItemId, hasArgSkill,
                                callbacks.RollIndex, callbacks.CountSubItem))
                        {
                            callbacks.ClearTargets();
                            break;
                        }
                        callbacks.ApplyChoices(selectionHandle, captured);
                    }
                    finally
                    {
                        callbacks.ReleaseTargets(captured);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Combat picker {node.Kind} has no ECS execution.");
            }
        }

        public static void Create(ref int handle, List<CombatTargetPickerNode> nodes)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            Entity entity = manager.CreateEntity();
            DynamicBuffer<CombatTargetPickerNode> buffer =
                manager.AddBuffer<CombatTargetPickerNode>(entity);
            for (int i = 0; i < nodes.Count; i++)
            {
                buffer.Add(nodes[i]);
            }
            handle = ++s_NextHandle;
            Programs.Add(handle, entity);
            RefCounts.Add(handle, 1);
        }

        private static CombatTargetPickerNode GetNode(int handle, int index)
        {
            EntityManager manager = GetManager();
            if (!Programs.TryGetValue(handle, out Entity entity) || !manager.Exists(entity))
            {
                throw new InvalidOperationException("Combat target picker program was released.");
            }
            return manager.GetBuffer<CombatTargetPickerNode>(entity)[index];
        }

        public static bool TryGetSpatialNode(int handle,
            out CombatTargetPickerNode node)
        {
            node = default;
            if (!IsValid(handle)) return false;
            EntityManager manager = GetManager();
            DynamicBuffer<CombatTargetPickerNode> nodes =
                manager.GetBuffer<CombatTargetPickerNode>(Programs[handle]);
            if (nodes.Length != 1) return false;
            node = nodes[0];
            return node.Kind == CombatTargetPickerKind.Nearest ||
                node.Kind == CombatTargetPickerKind.OwnerDistance ||
                node.Kind == CombatTargetPickerKind.OwnerDirection;
        }

        public static bool TryGetRelatedDamageNode(int handle,
            out CombatTargetPickerNode node)
        {
            node = default;
            if (!IsValid(handle)) return false;
            EntityManager manager = GetManager();
            DynamicBuffer<CombatTargetPickerNode> nodes =
                manager.GetBuffer<CombatTargetPickerNode>(Programs[handle]);
            if (nodes.Length != 1 ||
                nodes[0].Kind != CombatTargetPickerKind.RelatedDamageTarget)
                return false;
            node = nodes[0];
            return true;
        }

        private static void SelectNearest(int programHandle, int nodeIndex,
            int selectionHandle, bool sourceIsUnit, int sourceUnitHandle,
            float sourceX, float sourceZ,
            int sourceCamp, Func<int, bool> projectUnit)
        {
            CombatTargetPickerNode node = GetNode(programHandle, nodeIndex,
                CombatTargetPickerKind.Nearest);
            if (sourceIsUnit)
            {
                CombatSpatialSelection.SelectNearest(selectionHandle,
                    sourceUnitHandle, node.TargetCamp, node.LengthMm, projectUnit);
            }
            else
            {
                CombatSpatialSelection.SelectNearest(selectionHandle, sourceX, sourceZ,
                    sourceCamp, node.TargetCamp, node.LengthMm, projectUnit);
            }
        }

        private static void SelectWithinDistance(int programHandle, int nodeIndex,
            int selectionHandle, float sourceX, float sourceZ, int sourceCamp,
            int itemId, Func<int, int, int> lookupWeaponLength,
            Func<int, bool> projectUnit)
        {
            CombatTargetPickerNode node = GetNode(programHandle, nodeIndex,
                CombatTargetPickerKind.OwnerDistance);
            int lengthMm = CombatSpatialSelection.ResolveLengthMm(node.LengthMm,
                node.UseWeaponLength, itemId, lookupWeaponLength);
            CombatSpatialSelection.SelectWithinDistance(selectionHandle, sourceX,
                sourceZ, sourceCamp, node.TargetCamp, lengthMm / 1000f, projectUnit);
        }

        private static void SelectSectorFromAttackTarget(int programHandle,
            int nodeIndex, int selectionHandle, int sourceUnitHandle, int itemId,
            Func<int, bool> hasManagedTarget,
            Func<int, int, int> lookupWeaponLength, Func<int, bool> projectUnit)
        {
            CombatTargetPickerNode node = GetNode(programHandle, nodeIndex,
                CombatTargetPickerKind.OwnerDirection);
            CombatSpatialSelection.SelectSectorFromAttackTarget(selectionHandle,
                sourceUnitHandle, node.LengthMm, node.UseWeaponLength, itemId,
                node.AngleDegrees, hasManagedTarget, lookupWeaponLength, projectUnit);
        }

        private static void SelectAttackTarget(int programHandle, int nodeIndex,
            int selectionHandle, int casterUnitHandle,
            Func<int, bool> projectOptionalUnit)
        {
            GetNode(programHandle, nodeIndex,
                CombatTargetPickerKind.SkillCasterCurTarget);
            CombatIdentitySelection.SelectAttackTarget(selectionHandle,
                casterUnitHandle, projectOptionalUnit);
        }

        private static void SelectDamageTarget(int programHandle, int nodeIndex,
            int selectionHandle, int casterUid, int targetUid,
            Func<int, bool> projectBySource)
        {
            CombatTargetPickerNode node = GetNode(programHandle, nodeIndex,
                CombatTargetPickerKind.RelatedDamageTarget);
            CombatIdentitySelection.SelectDamageTarget(selectionHandle,
                node.DamageTargetType, casterUid, targetUid, projectBySource);
        }

        private static void SelectProjectiles(int programHandle, int nodeIndex,
            int selectionHandle, Func<bool, int> resolveCasterUid,
            Action<int, bool, List<CombatProjectileSelection.PendingProjectile>> collectPending,
            Func<int, int, bool, bool> projectProjectile)
        {
            CombatTargetPickerNode node = GetNode(programHandle, nodeIndex,
                CombatTargetPickerKind.Bullet);
            int casterUid = resolveCasterUid(node.SameCaster);
            List<CombatProjectileSelection.PendingProjectile> pending =
                ListPool<CombatProjectileSelection.PendingProjectile>.Get();
            try
            {
                collectPending(node.BulletId, node.SameCaster, pending);
                CombatProjectileSelection.Select(selectionHandle, node.BulletId,
                    node.SameCaster, casterUid, pending,
                    projectProjectile);
            }
            finally
            {
                ListPool<CombatProjectileSelection.PendingProjectile>.Release(pending);
            }
        }

        private static bool SelectRandom(int programHandle, int nodeIndex,
            int selectionHandle, Func<int> getParentItemId,
            int argItemId, bool hasArgSkill,
            Func<int, int> roll, Func<int, int, int> countSubItem)
        {
            CombatTargetPickerNode node = GetNode(programHandle, nodeIndex,
                CombatTargetPickerKind.RandomFromList);
            int parentItemId = node.NumberType == 1 ? getParentItemId() : 0;
            return CombatRandomTargetSelection.SelectByRule(selectionHandle,
                node.NumberType, node.NumberParameter, parentItemId, argItemId,
                hasArgSkill, node.MeetNumber, roll, countSubItem);
        }

        private static CombatTargetPickerNode GetNode(int handle, int index,
            CombatTargetPickerKind expectedKind)
        {
            CombatTargetPickerNode node = GetNode(handle, index);
            if (node.Kind != expectedKind)
            {
                throw new InvalidOperationException($"Expected picker {expectedKind}, got {node.Kind}.");
            }
            return node;
        }

        public static void Release(ref int handle)
        {
            if (handle != 0 && RefCounts.TryGetValue(handle, out int references) &&
                references > 1)
            {
                RefCounts[handle] = references - 1;
                handle = 0;
                return;
            }
            if (handle != 0 && TryGetManager(out EntityManager manager) &&
                Programs.TryGetValue(handle, out Entity entity) && manager.Exists(entity))
            {
                manager.DestroyEntity(entity);
            }
            Programs.Remove(handle);
            RefCounts.Remove(handle);
            handle = 0;
        }

        private static EntityManager GetManager()
        {
            if (!TryGetManager(out EntityManager manager))
            {
                throw new InvalidOperationException("Combat target pickers require an active ECS World.");
            }
            return manager;
        }

        private static bool TryGetManager(out EntityManager manager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                Programs.Clear();
                RefCounts.Clear();
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
