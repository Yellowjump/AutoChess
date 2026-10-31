using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Pool;

namespace Zizouqi.CombatDots
{
    public enum CombatSelectedTargetKind : byte
    {
        None,
        Unit,
        Projectile,
        PositionPoint,
        Managed
    }

    public struct CombatSelectedTarget : IBufferElementData
    {
        public CombatSelectedTargetKind Kind;
        public int Handle;
    }

    public struct CombatTargetChoice : IBufferElementData
    {
        public int CandidateIndex;
    }

    public static class CombatTargetSelectionBridge
    {
        private static readonly Dictionary<int, Entity> Selections = new Dictionary<int, Entity>();
        private static readonly Stack<Entity> FreeSelections = new Stack<Entity>();
        private const int MaxCachedSelections = 32;
        private static World s_World;
        private static int s_NextHandle;

        public static void Create(ref int handle)
        {
            Release(ref handle);
            EntityManager manager = GetManager();
            Entity entity;
            if (FreeSelections.Count != 0)
            {
                entity = FreeSelections.Pop();
            }
            else
            {
                entity = manager.CreateEntity();
                manager.AddBuffer<CombatSelectedTarget>(entity);
                manager.AddBuffer<CombatTargetCandidate>(entity);
                manager.AddBuffer<CombatTargetChoice>(entity);
            }
            handle = ++s_NextHandle;
            Selections.Add(handle, entity);
        }

        public static void Append(int handle, CombatSelectedTarget target)
        {
            GetBuffer(handle).Add(target);
        }

        public static void Clear(int handle)
        {
            GetBuffer(handle).Clear();
            GetCandidates(handle).Clear();
            GetChoices(handle).Clear();
        }

        public static NativeArray<CombatSelectedTarget> Snapshot(int handle)
        {
            DynamicBuffer<CombatSelectedTarget> buffer = GetBuffer(handle);
            var targets = new NativeArray<CombatSelectedTarget>(buffer.Length,
                Allocator.Temp);
            for (int i = 0; i < buffer.Length; i++)
            {
                targets[i] = buffer[i];
            }
            return targets;
        }

        public static int GetChoiceCount(int handle)
        {
            return GetChoices(handle).Length;
        }

        public static int GetChoiceIndex(int handle, int offset)
        {
            return GetChoices(handle)[offset].CandidateIndex;
        }

        internal static DynamicBuffer<CombatSelectedTarget> GetBuffer(int handle)
        {
            EntityManager manager = GetManager();
            return manager.GetBuffer<CombatSelectedTarget>(GetEntity(handle, manager));
        }

        internal static DynamicBuffer<CombatTargetCandidate> GetCandidates(int handle)
        {
            EntityManager manager = GetManager();
            return manager.GetBuffer<CombatTargetCandidate>(GetEntity(handle, manager));
        }

        internal static DynamicBuffer<CombatTargetChoice> GetChoices(int handle)
        {
            EntityManager manager = GetManager();
            return manager.GetBuffer<CombatTargetChoice>(GetEntity(handle, manager));
        }

        private static Entity GetEntity(int handle, EntityManager manager)
        {
            if (!Selections.TryGetValue(handle, out Entity entity) || !manager.Exists(entity))
            {
                throw new InvalidOperationException("Combat target selection has been released.");
            }
            return entity;
        }

        public static void Release(ref int handle)
        {
            if (handle != 0 && TryGetManager(out EntityManager manager) &&
                Selections.TryGetValue(handle, out Entity entity) && manager.Exists(entity))
            {
                manager.GetBuffer<CombatSelectedTarget>(entity).Clear();
                manager.GetBuffer<CombatTargetCandidate>(entity).Clear();
                manager.GetBuffer<CombatTargetChoice>(entity).Clear();
                if (FreeSelections.Count < MaxCachedSelections)
                {
                    FreeSelections.Push(entity);
                }
                else
                {
                    manager.DestroyEntity(entity);
                }
            }
            Selections.Remove(handle);
            handle = 0;
        }

        private static EntityManager GetManager()
        {
            if (!TryGetManager(out EntityManager manager))
            {
                throw new InvalidOperationException("Combat target selection requires an active ECS World.");
            }
            return manager;
        }

        private static bool TryGetManager(out EntityManager manager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                if (s_World != null && s_World.IsCreated)
                {
                    EntityManager previousManager = s_World.EntityManager;
                    foreach (Entity cached in FreeSelections)
                    {
                        if (previousManager.Exists(cached))
                        {
                            previousManager.DestroyEntity(cached);
                        }
                    }
                }
                Selections.Clear();
                FreeSelections.Clear();
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

    // Spatial rules append ECS unit handles to the current invocation. The
    // projection callback only resolves the hot-update object for commands.
    public static class CombatSpatialSelection
    {
        public static int ResolveLengthMm(int authoredLengthMm, bool useWeapon,
            int itemId, Func<int, int, int> lookupWeaponLength)
        {
            return useWeapon && itemId != 0
                ? lookupWeaponLength(itemId, authoredLengthMm)
                : authoredLengthMm;
        }

        public static void SelectNearest(int selectionHandle, int sourceUnitHandle,
            int targetCamp, int rangeMm, Func<int, bool> projectUnit)
        {
            if (rangeMm <= 0)
            {
                rangeMm = int.MaxValue;
            }
            CombatTargetQueries.FindNearest(sourceUnitHandle, targetCamp, rangeMm,
                out int heroUid);
            AppendProjectedUnit(selectionHandle, heroUid, projectUnit);
        }

        public static void SelectNearest(int selectionHandle, float sourceX,
            float sourceZ, int sourceCamp, int targetCamp, int rangeMm,
            Func<int, bool> projectUnit)
        {
            if (rangeMm <= 0)
            {
                rangeMm = int.MaxValue;
            }
            CombatTargetQueries.FindNearest(sourceX, sourceZ, sourceCamp,
                targetCamp, rangeMm, out int heroUid);
            AppendProjectedUnit(selectionHandle, heroUid, projectUnit);
        }

        public static void SelectWithinDistance(int selectionHandle, float x, float z,
            int sourceCamp, int targetCamp, float radius,
            Func<int, bool> projectUnit)
        {
            List<int> heroUids = ListPool<int>.Get();
            try
            {
                CombatTargetQueries.CollectWithinDistance(x, z, sourceCamp,
                    targetCamp, radius, heroUids);
                foreach (int heroUid in heroUids)
                {
                    AppendProjectedUnit(selectionHandle, heroUid, projectUnit);
                }
            }
            finally
            {
                ListPool<int>.Release(heroUids);
            }
        }

        public static void SelectSector(int selectionHandle, float x, float z,
            float facingX, float facingZ, int sourceCamp, float radius,
            float angleDegrees, Func<int, bool> projectUnit)
        {
            List<int> heroUids = ListPool<int>.Get();
            try
            {
                CombatTargetQueries.CollectSector(x, z, facingX, facingZ,
                    sourceCamp, radius, angleDegrees, heroUids);
                foreach (int heroUid in heroUids)
                {
                    AppendProjectedUnit(selectionHandle, heroUid, projectUnit);
                }
            }
            finally
            {
                ListPool<int>.Release(heroUids);
            }
        }

        public static void SelectSectorFromAttackTarget(int selectionHandle,
            int sourceUnitHandle, int authoredLengthMm, bool useWeapon,
            int itemId, float angleDegrees, Func<int, bool> hasManagedTarget,
            Func<int, int, int> lookupWeaponLength, Func<int, bool> projectUnit)
        {
            int targetUid = CombatAttributeBridge.GetAttackTargetUid(sourceUnitHandle);
            if (targetUid == 0 || !hasManagedTarget(targetUid) ||
                !CombatAttributeBridge.TryGetHandleByUid(targetUid, out int targetHandle))
            {
                return;
            }
            int lengthMm = ResolveLengthMm(authoredLengthMm, useWeapon, itemId,
                lookupWeaponLength);
            CombatAttributeBridge.GetPosition(sourceUnitHandle,
                out float sourceX, out _, out float sourceZ);
            CombatAttributeBridge.GetPosition(targetHandle,
                out float targetX, out _, out float targetZ);
            SelectSector(selectionHandle, sourceX, sourceZ, targetX - sourceX,
                targetZ - sourceZ, CombatAttributeBridge.GetCamp(sourceUnitHandle),
                lengthMm / 1000f, angleDegrees, projectUnit);
        }

        private static void AppendProjectedUnit(int selectionHandle, int heroUid,
            Func<int, bool> projectUnit)
        {
            if (heroUid == 0 ||
                !CombatAttributeBridge.TryGetHandleByUid(heroUid, out int unitHandle) ||
                !projectUnit(heroUid))
            {
                return;
            }
            CombatTargetSelectionBridge.Append(selectionHandle, new CombatSelectedTarget
            {
                Kind = CombatSelectedTargetKind.Unit,
                Handle = unitHandle
            });
        }
    }

    public static class CombatIdentitySelection
    {
        public static void SelectAttackTarget(int selectionHandle, int casterUnitHandle,
            Func<int, bool> projectOptionalUnit)
        {
            int targetUid = CombatAttributeBridge.GetAttackTargetUid(casterUnitHandle);
            AppendOptionalUnit(selectionHandle, targetUid,
                projectOptionalUnit(targetUid));
        }

        public static void SelectDamageTarget(int selectionHandle, int damageTargetType,
            int casterUid, int targetUid, Func<int, bool> projectBySource)
        {
            int source = damageTargetType == 0 ? 0 : 1;
            AppendOptionalUnit(selectionHandle,
                source == 0 ? casterUid : targetUid, projectBySource(source));
        }

        private static void AppendOptionalUnit(int selectionHandle, int heroUid,
            bool projected)
        {
            int unitHandle = 0;
            if (projected)
            {
                CombatAttributeBridge.TryGetHandleByUid(heroUid, out unitHandle);
            }
            CombatTargetSelectionBridge.Append(selectionHandle, new CombatSelectedTarget
            {
                Kind = projected ? CombatSelectedTargetKind.Unit :
                    CombatSelectedTargetKind.None,
                Handle = unitHandle
            });
        }
    }

    public static class CombatProjectileSelection
    {
        public struct PendingProjectile
        {
            public int Handle;
            public int SpawnOrder;
        }

        private struct Candidate
        {
            public int Handle;
            public int SpawnOrder;
            public bool Pending;
        }

        public static void Select(int selectionHandle, int bulletId, bool sameCaster,
            int casterUid, IReadOnlyList<PendingProjectile> pendingProjectiles,
            Func<int, int, bool, bool> projectProjectile)
        {
            List<int> registeredHandles = ListPool<int>.Get();
            List<Candidate> candidates = ListPool<Candidate>.Get();
            try
            {
                CombatProjectileBridge.CollectMatchingHandles(bulletId, sameCaster,
                    casterUid, registeredHandles);
                foreach (int handle in registeredHandles)
                {
                    candidates.Add(new Candidate
                    {
                        Handle = handle,
                        SpawnOrder = CombatProjectileBridge.GetSpawnOrder(handle)
                    });
                }
                foreach (PendingProjectile pending in pendingProjectiles)
                {
                    candidates.Add(new Candidate
                    {
                        Handle = pending.Handle,
                        SpawnOrder = pending.SpawnOrder,
                        Pending = true
                    });
                }
                candidates.Sort((a, b) => a.SpawnOrder.CompareTo(b.SpawnOrder));
                foreach (Candidate candidate in candidates)
                {
                    if (!projectProjectile(candidate.Handle, candidate.SpawnOrder,
                            candidate.Pending))
                    {
                        continue;
                    }
                    CombatTargetSelectionBridge.Append(selectionHandle, new CombatSelectedTarget
                    {
                        Kind = CombatSelectedTargetKind.Projectile,
                        Handle = candidate.Handle
                    });
                }
            }
            finally
            {
                ListPool<Candidate>.Release(candidates);
                ListPool<int>.Release(registeredHandles);
            }
        }
    }

    // Own the synchronous condition -> target selection -> command boundary.
    // Each recursive trigger call gets a separate ECS target buffer.
    public static class CombatTriggerInvocationBridge
    {
        public static void Execute(int conditionProgramHandle, CombatConditionContext context,
            Func<int> rollPercent, Func<int, int, bool> itemContains,
            Action<int> pickTargets, Action<int> executeCommands)
        {
            if (!CombatConditionProgramBridge.Check(conditionProgramHandle, context,
                    rollPercent, itemContains))
            {
                return;
            }

            int targetSelectionHandle = 0;
            try
            {
                CombatTargetSelectionBridge.Create(ref targetSelectionHandle);
                pickTargets(targetSelectionHandle);
                executeCommands(targetSelectionHandle);
            }
            finally
            {
                CombatTargetSelectionBridge.Release(ref targetSelectionHandle);
            }
        }
    }
}
