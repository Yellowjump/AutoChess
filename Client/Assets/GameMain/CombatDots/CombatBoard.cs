using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Zizouqi.CombatDots
{
    public struct CombatBoard : IComponentData
    {
        public int Phase;
        public float FixedStepAccumulator;
        public float BattleStartTime;
        public bool HasPendingOutcome;
        public bool PendingBattleWon;
        public bool GeometryReady;
        public int NextTriggerRegistrationId;
        public int NextHeroUid;
        public int NextProjectileSpawnOrder;
        public int NextPositionPointSpawnOrder;
        public int BuffTick;
        public int NextBuffActiveOrder;
        public int NextAuraOrder;
        public float PendingBuffDeltaMs;
        public float PendingCooldownDeltaSeconds;
        public float OffsetX;
        public float OffsetY;
        public float OffsetZ;
    }

    public struct CombatBoardCell : IBufferElementData
    {
        public int HeroUid;
        public float X;
        public float Y;
        public float Z;
    }

    // Logic owns the projectile. Presentation requests contain only its identity,
    // so a recycled BulletBase can never be used by a later visual update.
    public struct CombatProjectileVisualRequest : IBufferElementData
    {
        public int ProjectileHandle;
        public int SpawnOrder;
    }

    [InternalBufferCapacity(64)]
    public struct CombatProjectileRoster : IBufferElementData
    {
        public int Handle;
        public int SpawnOrder;
    }

    // Logic records damage presentation without touching UI objects. The
    // presentation phase consumes these events in damage application order.
    [InternalBufferCapacity(32)]
    public struct CombatDamageNumberEvent : IBufferElementData
    {
        public int Value;
        public float3 Position;
    }

    public enum CombatDamageStage : byte
    {
        // These serialized values match SkillSystem.TriggerType.
        BeforeCause = 6,
        BeforeReceive = 7,
        AfterCause = 8,
        AfterReceive = 9
    }

    // Active damage calls form a stack: a trigger can cause another hit before
    // the outer hit has finished its before/after callbacks.
    [InternalBufferCapacity(4)]
    public struct CombatDamageEvent : IBufferElementData
    {
        public Entity Caster;
        public Entity Target;
        public Entity SourceSkill;
        public int CasterHandle;
        public int TargetHandle;
        public float Value;
        public CombatDamageStage Stage;
    }

    public static class CombatBoardBridge
    {
        public const int Rows = 8;
        public const int Columns = 7;
        private static World s_World;
        private static Entity s_Board;
        private static readonly CombatProjectileVisualRequest[] VisualRequestBatch =
            new CombatProjectileVisualRequest[12];

        public static int PushDamageEvent(CombatDamageEvent value)
        {
            GetCells();
            DynamicBuffer<CombatDamageEvent> events =
                s_World.EntityManager.GetBuffer<CombatDamageEvent>(s_Board);
            int index = events.Length;
            events.Add(value);
            return index;
        }

        public static void SetDamageEventStage(int index,
            CombatDamageStage stage, float value)
        {
            if (!TryGetActiveDamageEvents(out DynamicBuffer<CombatDamageEvent> events) ||
                (uint)index >= (uint)events.Length) return;
            CombatDamageEvent damage = events[index];
            damage.Stage = stage;
            damage.Value = value;
            events[index] = damage;
        }

        public static bool TryGetDamageEvent(int index, out CombatDamageEvent value)
        {
            if (TryGetActiveDamageEvents(out DynamicBuffer<CombatDamageEvent> events) &&
                (uint)index < (uint)events.Length)
            {
                value = events[index];
                return true;
            }
            value = default;
            return false;
        }

        public static void PopDamageEvent(int index)
        {
            if (!TryGetActiveDamageEvents(out DynamicBuffer<CombatDamageEvent> events))
                return;
            if ((uint)index >= (uint)events.Length) return;
            if (index != events.Length - 1)
                throw new InvalidOperationException("Nested damage events must finish in stack order.");
            events.RemoveAt(index);
        }

        private static bool TryGetActiveDamageEvents(
            out DynamicBuffer<CombatDamageEvent> events)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || world != s_World || !world.IsCreated ||
                s_Board == Entity.Null || !world.EntityManager.Exists(s_Board) ||
                !world.EntityManager.HasBuffer<CombatDamageEvent>(s_Board))
            {
                events = default;
                return false;
            }
            events = world.EntityManager.GetBuffer<CombatDamageEvent>(s_Board);
            return true;
        }

        public static void QueueDamageNumber(int value, float x, float y, float z)
        {
            GetCells();
            s_World.EntityManager.GetBuffer<CombatDamageNumberEvent>(s_Board).Add(
                new CombatDamageNumberEvent
                {
                    Value = value,
                    Position = new float3(x, y, z)
                });
        }

        public static void DrainDamageNumbers(List<CombatDamageNumberEvent> destination)
        {
            GetCells();
            DynamicBuffer<CombatDamageNumberEvent> events =
                s_World.EntityManager.GetBuffer<CombatDamageNumberEvent>(s_Board);
            for (int i = 0; i < events.Length; i++)
                destination.Add(events[i]);
            events.Clear();
        }

        public static void ClearDamageNumbers()
        {
            GetCells();
            s_World.EntityManager.GetBuffer<CombatDamageNumberEvent>(s_Board).Clear();
        }

        public static void QueueProjectileVisual(int projectileHandle, int spawnOrder)
        {
            GetCells();
            s_World.EntityManager.GetBuffer<CombatProjectileVisualRequest>(s_Board).Add(
                new CombatProjectileVisualRequest
                {
                    ProjectileHandle = projectileHandle,
                    SpawnOrder = spawnOrder
                });
        }

        public static void DrainProjectileVisuals(Func<int, int, bool> isAlive,
            Action<int, int> showVisual)
        {
            GetCells();
            DynamicBuffer<CombatProjectileVisualRequest> requests =
                s_World.EntityManager.GetBuffer<CombatProjectileVisualRequest>(s_Board);
            int scanned = 0;
            int showCount = 0;
            // Skip expired projectiles so a burst of short-lived hits cannot
            // starve visuals that are still alive in the queue.
            while (scanned < requests.Length && scanned < 256 &&
                   showCount < VisualRequestBatch.Length)
            {
                CombatProjectileVisualRequest request = requests[scanned++];
                if (isAlive(request.ProjectileHandle, request.SpawnOrder))
                {
                    VisualRequestBatch[showCount++] = request;
                }
            }
            if (scanned != 0)
            {
                requests.RemoveRange(0, scanned);
            }
            for (int i = 0; i < showCount; i++)
            {
                CombatProjectileVisualRequest request = VisualRequestBatch[i];
                showVisual(request.ProjectileHandle, request.SpawnOrder);
            }
        }

        public static void ClearProjectileVisuals()
        {
            GetCells();
            s_World.EntityManager.GetBuffer<CombatProjectileVisualRequest>(s_Board).Clear();
        }

        private static readonly int[] EvenRowColumnOffsets = { 0, 0, -1, 1, 1, 1 };
        private static readonly int[] OddRowColumnOffsets = { 0, 0, -1, 1, -1, -1 };
        private static readonly int[] RowOffsets = { 1, -1, 0, 0, 1, -1 };

        public static bool IsBattleActive()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }
            GetCells();
            return s_World.EntityManager.GetComponentData<CombatBoard>(s_Board).Phase == 1;
        }

        public static void SetBattleActive(bool active)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.Phase = active ? 1 : 0;
            if (active)
            {
                board.FixedStepAccumulator = 0f;
            }
            manager.SetComponentData(s_Board, board);
        }

        public static void PrepareBattle(float startTime)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.BattleStartTime = startTime;
            board.HasPendingOutcome = false;
            board.PendingBattleWon = false;
            manager.SetComponentData(s_Board, board);
        }

        public static bool HasBattleTimedOut(float now, float maxDuration)
        {
            GetCells();
            CombatBoard board = s_World.EntityManager.GetComponentData<CombatBoard>(s_Board);
            return now - board.BattleStartTime > maxDuration;
        }

        public static void QueueBattleOutcome(bool won)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            if (board.HasPendingOutcome)
            {
                return;
            }
            board.HasPendingOutcome = true;
            board.PendingBattleWon = won;
            manager.SetComponentData(s_Board, board);
        }

        public static bool TryConsumeBattleOutcome(out bool won)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            if (!board.HasPendingOutcome)
            {
                won = false;
                return false;
            }
            won = board.PendingBattleWon;
            board.HasPendingOutcome = false;
            board.PendingBattleWon = false;
            manager.SetComponentData(s_Board, board);
            return true;
        }

        public static void AddFrameTime(float elapsedSeconds)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.FixedStepAccumulator += elapsedSeconds;
            manager.SetComponentData(s_Board, board);
        }

        public static void LimitFixedStepBacklog(float maxSeconds)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            if (board.FixedStepAccumulator > maxSeconds)
            {
                board.FixedStepAccumulator = maxSeconds;
                manager.SetComponentData(s_Board, board);
            }
        }

        public static bool TryConsumeFixedStep(float stepSeconds)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            if (board.FixedStepAccumulator < stepSeconds)
            {
                return false;
            }
            board.FixedStepAccumulator -= stepSeconds;
            manager.SetComponentData(s_Board, board);
            return true;
        }

        public static void ResetFixedStepAccumulator()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.FixedStepAccumulator = 0f;
            manager.SetComponentData(s_Board, board);
        }

        public static int AllocateProjectileSpawnOrder()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            int order = ++board.NextProjectileSpawnOrder;
            manager.SetComponentData(s_Board, board);
            return order;
        }

        public static DynamicBuffer<CombatProjectileRoster> GetProjectileRoster()
        {
            GetCells();
            return s_World.EntityManager.GetBuffer<CombatProjectileRoster>(s_Board);
        }

        public static DynamicBuffer<CombatSimpleTriggerInput> GetSimpleTriggerInputs()
        {
            GetCells();
            return s_World.EntityManager.GetBuffer<CombatSimpleTriggerInput>(s_Board);
        }

        public static DynamicBuffer<CombatSimpleTriggerResult> GetSimpleTriggerResults()
        {
            GetCells();
            return s_World.EntityManager.GetBuffer<CombatSimpleTriggerResult>(s_Board);
        }

        public static DynamicBuffer<CombatSimpleTargetCandidate> GetSimpleTargetCandidates()
        {
            GetCells();
            return s_World.EntityManager.GetBuffer<CombatSimpleTargetCandidate>(s_Board);
        }

        public static void ReadSimpleTriggerSelection(int index,
            out CombatSimpleTriggerInput input,
            out CombatSimpleTriggerResult result)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            input = manager.GetBuffer<CombatSimpleTriggerInput>(s_Board)[index];
            result = manager.GetBuffer<CombatSimpleTriggerResult>(s_Board)[index];
        }

        public static void ClearSimpleTriggerBatch(int count)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            manager.GetBuffer<CombatSimpleTriggerInput>(s_Board)
                .RemoveRange(0, count);
            manager.GetBuffer<CombatSimpleTriggerResult>(s_Board).Clear();
            manager.GetBuffer<CombatSimpleTargetCandidate>(s_Board).Clear();
        }

        public static bool TryGetProjectileRoster(
            out DynamicBuffer<CombatProjectileRoster> roster)
        {
            roster = default;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || world != s_World ||
                s_Board == Entity.Null || !world.EntityManager.Exists(s_Board) ||
                !world.EntityManager.HasBuffer<CombatProjectileRoster>(s_Board))
                return false;
            roster = world.EntityManager.GetBuffer<CombatProjectileRoster>(s_Board);
            return true;
        }

        public static int AllocateTriggerRegistrationId()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            int registrationId = ++board.NextTriggerRegistrationId;
            manager.SetComponentData(s_Board, board);
            return registrationId;
        }

        public static int AllocateHeroUid()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            int uid = ++board.NextHeroUid;
            manager.SetComponentData(s_Board, board);
            return uid;
        }

        public static void ResetHeroUid()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.NextHeroUid = 0;
            manager.SetComponentData(s_Board, board);
        }

        public static int AllocatePositionPointSpawnOrder()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            int order = ++board.NextPositionPointSpawnOrder;
            manager.SetComponentData(s_Board, board);
            return order;
        }

        public static int CurrentBuffTick()
        {
            GetCells();
            return s_World.EntityManager.GetComponentData<CombatBoard>(s_Board).BuffTick;
        }

        public static void AdvanceBuffClock(float deltaMs)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.BuffTick++;
            board.PendingBuffDeltaMs = deltaMs;
            manager.SetComponentData(s_Board, board);
        }

        public static float ConsumeBuffDeltaMs()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            float deltaMs = board.PendingBuffDeltaMs;
            board.PendingBuffDeltaMs = 0f;
            manager.SetComponentData(s_Board, board);
            return deltaMs;
        }

        public static int AllocateBuffActiveOrder()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            int order = ++board.NextBuffActiveOrder;
            manager.SetComponentData(s_Board, board);
            return order;
        }

        public static int AllocateAuraOrder()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            int order = ++board.NextAuraOrder;
            manager.SetComponentData(s_Board, board);
            return order;
        }

        public static void SetCooldownDeltaSeconds(float deltaSeconds)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.PendingCooldownDeltaSeconds = deltaSeconds;
            manager.SetComponentData(s_Board, board);
        }

        public static float ConsumeCooldownDeltaSeconds()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            float deltaSeconds = board.PendingCooldownDeltaSeconds;
            board.PendingCooldownDeltaSeconds = 0f;
            manager.SetComponentData(s_Board, board);
            return deltaSeconds;
        }

        public static void ResetProjectileSpawnOrder()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            manager.GetBuffer<CombatProjectileRoster>(s_Board).Clear();
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.NextProjectileSpawnOrder = 0;
            manager.SetComponentData(s_Board, board);
        }

        public static void ResetPositionPointSpawnOrder()
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.NextPositionPointSpawnOrder = 0;
            manager.SetComponentData(s_Board, board);
        }

        public static int GetOccupant(int row, int column)
        {
            return GetCells()[Index(row, column)].HeroUid;
        }

        public static NativeArray<CombatBoardCell> SnapshotCells(
            Allocator allocator)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            var snapshot = new NativeArray<CombatBoardCell>(cells.Length,
                allocator);
            for (int i = 0; i < cells.Length; i++) snapshot[i] = cells[i];
            return snapshot;
        }

        public static bool FindFirstEmpty(out int row, out int column)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index].HeroUid != -1)
                {
                    continue;
                }
                row = index / Columns;
                column = index % Columns;
                return true;
            }
            row = -1;
            column = -1;
            return false;
        }

        public static void SetOccupant(int row, int column, int heroUid)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            int index = Index(row, column);
            CombatBoardCell cell = cells[index];
            cell.HeroUid = heroUid;
            cells[index] = cell;
        }

        public static void MoveOccupant(int fromRow, int fromColumn,
            int toRow, int toColumn, int heroUid)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            int from = Index(fromRow, fromColumn);
            int to = Index(toRow, toColumn);
            CombatBoardCell fromCell = cells[from];
            fromCell.HeroUid = -1;
            cells[from] = fromCell;
            CombatBoardCell toCell = cells[to];
            toCell.HeroUid = heroUid;
            cells[to] = toCell;
        }

        public static void SwapOccupants(int firstRow, int firstColumn,
            int secondRow, int secondColumn)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            int firstIndex = Index(firstRow, firstColumn);
            int secondIndex = Index(secondRow, secondColumn);
            CombatBoardCell first = cells[firstIndex];
            CombatBoardCell second = cells[secondIndex];
            int firstUid = first.HeroUid;
            first.HeroUid = second.HeroUid;
            second.HeroUid = firstUid;
            cells[firstIndex] = first;
            cells[secondIndex] = second;
        }

        public static bool FindOccupant(int heroUid, out int row, out int column)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index].HeroUid == heroUid)
                {
                    row = index / Columns;
                    column = index % Columns;
                    return true;
                }
            }
            row = -1;
            column = -1;
            return false;
        }

        public static void ClearOccupant(int heroUid)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index].HeroUid != heroUid)
                {
                    continue;
                }
                CombatBoardCell cell = cells[index];
                cell.HeroUid = -1;
                cells[index] = cell;
            }
        }

        public static void Clear()
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            for (int i = 0; i < cells.Length; i++)
            {
                CombatBoardCell cell = cells[i];
                cell.HeroUid = -1;
                cells[i] = cell;
            }
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.GeometryReady = false;
            manager.SetComponentData(s_Board, board);
        }

        public static void EnsureGeometry(Vector3[][] positions)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            if (board.GeometryReady)
            {
                return;
            }
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    int index = Index(row, column);
                    CombatBoardCell cell = cells[index];
                    cell.X = positions[row][column].x;
                    cell.Y = positions[row][column].y;
                    cell.Z = positions[row][column].z;
                    cells[index] = cell;
                }
            }
            board.GeometryReady = true;
            manager.SetComponentData(s_Board, board);
        }

        public static void SetOffset(Vector3 offset)
        {
            GetCells();
            EntityManager manager = s_World.EntityManager;
            CombatBoard board = manager.GetComponentData<CombatBoard>(s_Board);
            board.OffsetX = offset.x;
            board.OffsetY = offset.y;
            board.OffsetZ = offset.z;
            manager.SetComponentData(s_Board, board);
        }

        public static Vector3 GetOffset()
        {
            GetCells();
            CombatBoard board = s_World.EntityManager.GetComponentData<CombatBoard>(s_Board);
            return new Vector3(board.OffsetX, board.OffsetY, board.OffsetZ);
        }

        public static Vector3 GetCellWorldPosition(int row, int column)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            CombatBoardCell cell = cells[Index(row, column)];
            CombatBoard board = s_World.EntityManager.GetComponentData<CombatBoard>(s_Board);
            return new Vector3(board.OffsetX + cell.X, board.OffsetY + cell.Y,
                board.OffsetZ + cell.Z);
        }

        public static bool TryFindCell(Vector3 point, float cellWidth,
            out int row, out int column)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            CombatBoard board = s_World.EntityManager.GetComponentData<CombatBoard>(s_Board);
            Vector3 boardOffset = new Vector3(board.OffsetX, board.OffsetY, board.OffsetZ);
            for (int index = 0; index < cells.Length; index++)
            {
                CombatBoardCell cell = cells[index];
                Vector3 difference = point -
                    (boardOffset + new Vector3(cell.X, cell.Y, cell.Z));
                if (difference.magnitude > cellWidth / Mathf.Sqrt(3))
                {
                    continue;
                }
                float absX = Mathf.Abs(difference.x);
                float absZ = Mathf.Abs(difference.z);
                if (absX > cellWidth / 2)
                {
                    continue;
                }
                if (absZ < cellWidth / 2 / Mathf.Sqrt(3) ||
                    difference.magnitude <= cellWidth / 2 ||
                    (cellWidth / 2 - absX) / Mathf.Sqrt(3) +
                    cellWidth / 2 / Mathf.Sqrt(3) > absZ)
                {
                    row = index / Columns;
                    column = index % Columns;
                    return true;
                }
            }
            row = -1;
            column = -1;
            return false;
        }

        public static bool FindPath(int startRow, int startColumn,
            int endRow, int endColumn, int attackRangeMm,
            out int nextRow, out int nextColumn)
        {
            DynamicBuffer<CombatBoardCell> cells = GetCells();
            int start = Index(startRow, startColumn);
            int end = Index(endRow, endColumn);
            var open = new List<int> { start };
            var cameFrom = new Dictionary<int, int>();
            var cost = new Dictionary<int, float> { [start] = 0f };
            int last = start;
            bool found = false;
            while (open.Count > 0)
            {
                open.Sort((a, b) =>
                    (cost[a] * cost[a] + DistanceSquared(cells[a], cells[end])).CompareTo(
                        cost[b] * cost[b] + DistanceSquared(cells[b], cells[end])));
                int current = open[0];
                open.RemoveAt(0);
                float distance = DistanceSquared(cells[current], cells[end]);
                distance = (float)(Math.Floor(distance * 10000d) / 10000d);
                if (distance <= (attackRangeMm * attackRangeMm) / 1000000f)
                {
                    last = current;
                    found = true;
                    break;
                }

                int row = current / Columns;
                int column = current % Columns;
                int[] columnOffsets = row % 2 == 0 ? EvenRowColumnOffsets : OddRowColumnOffsets;
                for (int i = 0; i < RowOffsets.Length; i++)
                {
                    int adjacentRow = row + RowOffsets[i];
                    int adjacentColumn = column + columnOffsets[i];
                    if (adjacentRow < 0 || adjacentRow >= Rows ||
                        adjacentColumn < 0 || adjacentColumn >= Columns)
                    {
                        continue;
                    }
                    int adjacent = Index(adjacentRow, adjacentColumn);
                    if (cameFrom.ContainsKey(adjacent) && cost[adjacent] <= cost[current] + 1)
                    {
                        continue;
                    }
                    if (cells[adjacent].HeroUid > -1)
                    {
                        continue;
                    }
                    open.Add(adjacent);
                    cameFrom[adjacent] = current;
                    cost[adjacent] = cost[current] + 1;
                }
            }

            if (found)
            {
                int first = last;
                while (first != start)
                {
                    int parent = cameFrom[first];
                    if (parent == start)
                    {
                        break;
                    }
                    first = parent;
                }
                nextRow = first / Columns;
                nextColumn = first % Columns;
                return true;
            }
            nextRow = -1;
            nextColumn = -1;
            return false;
        }

        private static float DistanceSquared(CombatBoardCell a, CombatBoardCell b)
        {
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private static int Index(int row, int column)
        {
            if (row < 0 || row >= Rows || column < 0 || column >= Columns)
            {
                throw new IndexOutOfRangeException("Combat board cell is outside the board.");
            }
            return row * Columns + column;
        }

        private static DynamicBuffer<CombatBoardCell> GetCells()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != s_World)
            {
                s_World = world;
                s_Board = Entity.Null;
            }
            if (world == null || !world.IsCreated)
            {
                throw new InvalidOperationException("Combat board requires an active ECS World.");
            }
            EntityManager manager = world.EntityManager;
            if (s_Board == Entity.Null || !manager.Exists(s_Board))
            {
                s_Board = manager.CreateEntity(typeof(CombatBoard));
                manager.AddBuffer<CombatProjectileVisualRequest>(s_Board);
                manager.AddBuffer<CombatDamageNumberEvent>(s_Board);
                manager.AddBuffer<CombatDamageEvent>(s_Board);
                manager.AddBuffer<CombatProjectileRoster>(s_Board);
                manager.AddBuffer<CombatSimpleTriggerInput>(s_Board);
                manager.AddBuffer<CombatSimpleTriggerResult>(s_Board);
                manager.AddBuffer<CombatSimpleTargetCandidate>(s_Board);
                DynamicBuffer<CombatBoardCell> cells = manager.AddBuffer<CombatBoardCell>(s_Board);
                for (int i = 0; i < Rows * Columns; i++)
                {
                    cells.Add(new CombatBoardCell { HeroUid = -1 });
                }
            }
            else if (!manager.HasBuffer<CombatProjectileVisualRequest>(s_Board))
            {
                manager.AddBuffer<CombatProjectileVisualRequest>(s_Board);
            }
            if (!manager.HasBuffer<CombatDamageNumberEvent>(s_Board))
                manager.AddBuffer<CombatDamageNumberEvent>(s_Board);
            if (!manager.HasBuffer<CombatDamageEvent>(s_Board))
                manager.AddBuffer<CombatDamageEvent>(s_Board);
            if (!manager.HasBuffer<CombatProjectileRoster>(s_Board))
                manager.AddBuffer<CombatProjectileRoster>(s_Board);
            if (!manager.HasBuffer<CombatSimpleTriggerInput>(s_Board))
                manager.AddBuffer<CombatSimpleTriggerInput>(s_Board);
            if (!manager.HasBuffer<CombatSimpleTriggerResult>(s_Board))
                manager.AddBuffer<CombatSimpleTriggerResult>(s_Board);
            if (!manager.HasBuffer<CombatSimpleTargetCandidate>(s_Board))
                manager.AddBuffer<CombatSimpleTargetCandidate>(s_Board);
            return manager.GetBuffer<CombatBoardCell>(s_Board);
        }
    }
}
