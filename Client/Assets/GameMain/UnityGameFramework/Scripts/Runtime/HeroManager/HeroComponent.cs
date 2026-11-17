//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using Entity;
using SelfEventArg;
using SkillSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace UnityGameFramework.Runtime
{
    /// <summary>
    /// 棋子管理。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game Framework/HeroManager")]
    public sealed partial class HeroComponent : GameFrameworkComponent
    {
        public int[][] qige = new int[8][]; //保存棋格上是否有棋子 -1表示没有，其余是棋子uid 坐标是y，x
        public Vector3[][] qigepos = new Vector3[8][]; //保存棋格位置，相对areaPoint点
        public float qigeXOffset = 1f;

        public List<EntityQizi> QiziCSList = new List<EntityQizi>();//保存当前己方棋子 死亡不会移除
        public List<EntityQizi> DirenList = new List<EntityQizi>();//保存当前敌方棋子 死亡不会移除
        public int dangqianliucheng = 0;//保存当前流程，0是prebattle,1是battle
        public bool IsTimelineBattle { get; internal set; }
        public bool IsTimelineSimulation { get; internal set; }
        public bool InBattle => dangqianliucheng == 1;
        private Vector2Int[] adjacentOffsetDoubleRow = new Vector2Int[6]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.one, Vector2Int.down + Vector2Int.right
        };
        private Vector2Int[] adjacentOffsetSingleRow = new Vector2Int[6]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.left+Vector2Int.up, Vector2Int.left+ Vector2Int.down
        };
        public GameObject QigePrefab;
        [SerializeField]
        private Transform m_QigeRoot = null;

        private Vector3 _qigePosOffset = Vector3.zero;
        public Vector3 QigePosOffset => _qigePosOffset;
        protected override void Awake()
        {
            base.Awake();
            InitQige();
            InitPool();
            if (m_InstanceRoot == null)
            {
                m_InstanceRoot = new GameObject("InstancesActive").transform;
                m_InstanceRoot.SetParent(gameObject.transform);
                m_InstanceRoot.localScale = Vector3.one;
            }
            if (m_InstanceDisableRoot == null)
            {
                m_InstanceDisableRoot = new GameObject("InstancesDisable").transform;
                m_InstanceDisableRoot.SetParent(gameObject.transform);
                m_InstanceDisableRoot.localScale = Vector3.one;
                m_InstanceDisableRoot.gameObject.SetActive(false);
            }
            if (m_QigeRoot == null)
            {
                m_QigeRoot = new GameObject("Instances_QigeRoot").transform;
                m_QigeRoot.SetParent(gameObject.transform);
                m_QigeRoot.localScale = Vector3.one;
            }
            if (m_InstanceWorldCanvas == null)
            {
                m_InstanceWorldCanvas = GameObject.Find("WorldCanvas").GetComponent<Canvas>();
            }
            InitAnimationLoadCallback();
        }

        public void ShowQige(bool show)
        {
            m_QigeRoot.gameObject.SetActive(show);
        }
        private void Start()
        {
            var offsetX = -3.5f;
            var offsetZ = -3;
            for (int i = 0; i < 8; i++)
            {
                var startPosX = i % 2 == 0 ? 0.5f : 0f;
                for (int indexX = 0; indexX < 7; indexX++)
                {
                    Vector3 pos = new Vector3(startPosX + indexX+offsetX, 0, offsetZ+Mathf.Sqrt(3) * i / 2);
                    GameObject qg = Instantiate(QigePrefab, pos, Quaternion.Euler(0, 0, 0));
                    qg.transform.parent = m_QigeRoot;
                    qigepos[i][indexX] = pos;
                }
            }
            var eventComp = GameEntry.GetComponent<EventComponent>();
            eventComp.Subscribe(CMDGetItemEventArgs.EventId,OnCMDGetItem);
            InitSkillDecsFormat();
            
            // 初始化伤害数字系统
            //InitializeDamageNumberSystem();
        }

        void InitQige()
        {
            qige[0] = new int[7] { -1, -1, -1, -1, -1, -1, -1 };
            qige[1] = new int[7] { -1, -1, -1, -1, -1, -1, -1 };
            qige[2] = new int[7] { -1, -1, -1, -1, -1, -1, -1 };
            qige[3] = new int[7] { -1, -1, -1, -1, -1, -1, -1 };
            qige[4] = new int[7] { -1, -1, -1, -1, -1, -1, -1 };
            qige[5] = new int[7] { -1, -1, -1, -1, -1, -1, -1 };
            qige[6] = new int[7] { -1, -1, -1, -1, -1, -1, -1 };
            qige[7] = new int[7] { -1, -1, -1, -1, -1, -1, -1 };
            qigepos[0] = new Vector3[7];
            qigepos[1] = new Vector3[7];
            qigepos[2] = new Vector3[7];
            qigepos[3] = new Vector3[7];
            qigepos[4] = new Vector3[7];
            qigepos[5] = new Vector3[7];
            qigepos[6] = new Vector3[7];
            qigepos[7] = new Vector3[7];
        }

        public float getDistance(Vector2Int a, Vector2Int b)
        {
            try
            {
                return (qigepos[a.y][a.x].x - qigepos[b.y][b.x].x) * (qigepos[a.y][a.x].x - qigepos[b.y][b.x].x) + (qigepos[a.y][a.x].z - qigepos[b.y][b.x].z) * (qigepos[a.y][a.x].z - qigepos[b.y][b.x].z);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            return 0;
        }

        [SerializeField]
        private int[] m_LargeBossHeroIds = Array.Empty<int>();

        [SerializeField]
        private string[] m_LargeBossAppearances = Array.Empty<string>();

        public bool IsLargeBossHero(int heroId, string appearance)
        {
            return Array.IndexOf(m_LargeBossHeroIds, heroId) >= 0 ||
                   (!string.IsNullOrEmpty(appearance) && Array.IndexOf(m_LargeBossAppearances, appearance) >= 0);
        }

        private bool IsBoardCell(Vector2Int cell)
        {
            return cell.y >= 0 && cell.y < qige.Length &&
                   cell.x >= 0 && cell.x < qige[cell.y].Length;
        }

        private IEnumerable<Vector2Int> GetFootprint(EntityQizi qizi, Vector2Int anchor)
        {
            yield return anchor;
            if (!qizi.UsesLargeFootprint)
                yield break;

            var offsets = anchor.y % 2 == 0 ? adjacentOffsetDoubleRow : adjacentOffsetSingleRow;
            foreach (var offset in offsets)
                yield return anchor + offset;
        }

        public Vector2Int GetIndexQizi(EntityQizi qizi)
        {
            var anchor = new Vector2Int(qizi.columnIndex, qizi.rowIndex);
            return IsBoardCell(anchor) && qige[anchor.y][anchor.x] == qizi.HeroUID
                ? anchor : new Vector2Int(-1, -1);
        }

        private bool CanPlace(EntityQizi qizi, Vector2Int anchor)
        {
            foreach (var cell in GetFootprint(qizi, anchor))
            {
                if (!IsBoardCell(cell))
                    return false;

                int occupant = qige[cell.y][cell.x];
                if (occupant != -1 && occupant != qizi.HeroUID)
                    return false;
            }

            return true;
        }

        public void ReleaseFootprint(EntityQizi qizi)
        {
            ReleaseFootprintAt(qizi, new Vector2Int(qizi.columnIndex, qizi.rowIndex));
            if (qizi.ReservedMoveAnchor is Vector2Int destination)
            {
                ReleaseFootprintAt(qizi, destination);
                qizi.ReservedMoveAnchor = null;
            }
        }

        private void ReleaseFootprintAt(EntityQizi qizi, Vector2Int anchor)
        {
            foreach (var cell in GetFootprint(qizi, anchor))
            {
                if (IsBoardCell(cell) && qige[cell.y][cell.x] == qizi.HeroUID)
                    qige[cell.y][cell.x] = -1;
            }
        }

        public bool TryPlace(EntityQizi qizi, Vector2Int anchor)
        {
            if (!CanPlace(qizi, anchor))
                return false;

            ReleaseFootprint(qizi);
            foreach (var cell in GetFootprint(qizi, anchor))
                qige[cell.y][cell.x] = qizi.HeroUID;

            qizi.columnIndex = anchor.x;
            qizi.rowIndex = anchor.y;
            return true;
        }

        public bool TryBeginStep(EntityQizi qizi, Vector2Int destination)
        {
            // A moving unit still needs its old cells until its model arrives.
            var anchor = GetIndexQizi(qizi);
            var offsets = anchor.y % 2 == 0 ? adjacentOffsetDoubleRow : adjacentOffsetSingleRow;
            if (!qizi.IsValid || qizi.ReservedMoveAnchor.HasValue || !IsBoardCell(anchor) ||
                Array.IndexOf(offsets, destination - anchor) < 0 || !CanPlace(qizi, destination))
                return false;

            foreach (var cell in GetFootprint(qizi, destination))
                qige[cell.y][cell.x] = qizi.HeroUID;
            qizi.ReservedMoveAnchor = destination;
            return true;
        }

        public void CompleteStep(EntityQizi qizi)
        {
            if (qizi.ReservedMoveAnchor is Vector2Int destination)
                TryPlace(qizi, destination);
        }

        public void CancelStep(EntityQizi qizi)
        {
            if (!qizi.ReservedMoveAnchor.HasValue)
                return;

            if (!qizi.IsValid)
            {
                ReleaseFootprint(qizi);
                return;
            }

            var anchor = GetIndexQizi(qizi);
            if (TryPlace(qizi, anchor))
                qizi.LogicPosition = GetGeziPos(anchor.y, anchor.x);
        }

        private Vector2Int FindPlacement(EntityQizi qizi, Vector2Int preferred)
        {
            if (CanPlace(qizi, preferred))
                return preferred;

            var best = new Vector2Int(-1, -1);
            float bestDistance = float.PositiveInfinity;
            for (int row = 0; row < qige.Length; row++)
            {
                for (int column = 0; column < qige[row].Length; column++)
                {
                    var cell = new Vector2Int(column, row);
                    if (!CanPlace(qizi, cell))
                        continue;

                    float distance = IsBoardCell(preferred) ? getDistance(cell, preferred) : 0f;
                    if (distance < bestDistance)
                    {
                        best = cell;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        public IEnumerable<Vector3> GetRangePoints(EntityBase entity, Vector2Int? plannedAnchor = null)
        {
            if (entity is EntityQizi qizi)
            {
                if (plannedAnchor.HasValue || qizi.UsesLargeFootprint)
                {
                    var anchor = plannedAnchor ?? GetIndexQizi(qizi);
                    if (IsBoardCell(anchor))
                    {
                        // Reservations block movement. Attack points follow the moving model.
                        var offset = !plannedAnchor.HasValue && qizi.ReservedMoveAnchor.HasValue
                            ? qizi.LogicPosition - GetGeziPos(anchor.y, anchor.x)
                            : Vector3.zero;
                        foreach (var cell in GetFootprint(qizi, anchor))
                            yield return GetGeziPos(cell.y, cell.x) + offset;
                        yield break;
                    }
                }
            }

            yield return entity.LogicPosition;
        }

        public float GetCombatDistanceSquare(EntityBase source, EntityBase target, Vector2Int? plannedAnchor = null)
        {
            if (plannedAnchor.HasValue && !(source is EntityQizi mover && mover.UsesLargeFootprint) &&
                target is EntityQizi singleTarget && !singleTarget.UsesLargeFootprint)
            {
                var targetAnchor = singleTarget.ReservedMoveAnchor ?? GetIndexQizi(singleTarget);
                if (IsBoardCell(targetAnchor))
                    return getDistance(plannedAnchor.Value, targetAnchor);
            }

            if (!plannedAnchor.HasValue &&
                !(source is EntityQizi sourceQizi && sourceQizi.UsesLargeFootprint) &&
                !(target is EntityQizi targetQizi && targetQizi.UsesLargeFootprint))
                return source.GetDistanceSquare(target);

            float nearest = float.PositiveInfinity;
            foreach (var from in GetRangePoints(source, plannedAnchor))
            {
                foreach (var to in GetRangePoints(target, null))
                {
                    float dx = from.x - to.x;
                    float dz = from.z - to.z;
                    nearest = Mathf.Min(nearest, dx * dx + dz * dz);
                }
            }

            return nearest;
        }

        public bool IsInSkillRange(float distanceSquare, int skillRange)
        {
            float range = skillRange / 1000f;
            return skillRange >= 0 && Utility.TruncateFloat(distanceSquare, 4) <= range * range;
        }

        public bool GetQiziByQigeIndex(Vector2Int geziPos, out EntityQizi curPosQizi)
        {
            curPosQizi = null;
            try
            {
                var uid = qige[geziPos.y][geziPos.x];
                foreach (var oneQizi in QiziCSList)
                {
                    if (oneQizi.HeroUID == uid)
                    {
                        curPosQizi = oneQizi;
                        return true;
                    }
                }

                foreach (var oneQizi in DirenList)
                {
                    if (oneQizi.HeroUID == uid)
                    {
                        curPosQizi = oneQizi;
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            return false;
        }

        public void UpdateEntityPos(EntityQizi qizi, Vector2Int newPos)
        {
            var oldPos = GetIndexQizi(qizi);
            if (qizi.BelongCamp == CampType.Friend && IsBoardCell(oldPos) &&
                GetQiziByQigeIndex(newPos, out var other) && other != qizi &&
                other.BelongCamp == CampType.Friend)
            {
                ReleaseFootprint(qizi);
                TryPlace(other, oldPos);
                other.LogicPosition = GetGeziPos(oldPos.y, oldPos.x);
            }

            if (TryPlace(qizi, newPos))
                qizi.LogicPosition = GetGeziPos(newPos.y, newPos.x);
            else if (IsBoardCell(oldPos))
                qizi.LogicPosition = GetGeziPos(oldPos.y, oldPos.x);
        }

        public Vector2Int Findpath(EntityQizi mover, EntityQizi target, int atkRangeMM)
        {
            var start = GetIndexQizi(mover);
            if (target == null || !IsBoardCell(start))
                return new Vector2Int(-1, -1);

            Vector2Int lastpos = start;
            float closestDistance = GetCombatDistanceSquare(mover, target, start);
            List<Vector2Int> sortList = new List<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, float> xiaoHao = new Dictionary<Vector2Int, float>();
            sortList.Add(start);
            //cameFrom[start] = new Vector2Int(-1,-1);
            xiaoHao[start] = 0f;
            while (sortList.Count > 0)
            {
                sortList.Sort((a, b) => (xiaoHao[a] * xiaoHao[a] + GetCombatDistanceSquare(mover, target, a)).CompareTo((xiaoHao[b] * xiaoHao[b] + GetCombatDistanceSquare(mover, target, b))));
                Vector2Int current = sortList[0];
                sortList.Remove(current);
                float distance = GetCombatDistanceSquare(mover, target, current);
                if (IsInSkillRange(distance, atkRangeMM))
                {
                    lastpos = current;
                    break;
                }

                // Large units can still approach when no current anchor is in range.
                if (mover.UsesLargeFootprint && distance < closestDistance)
                {
                    closestDistance = distance;
                    lastpos = current;
                }

                var adjacentOffList = current.y % 2 == 0 ? adjacentOffsetDoubleRow : adjacentOffsetSingleRow;
                for (int adjacentCellIndex = 0; adjacentCellIndex < 6; adjacentCellIndex++)
                {
                    var curAdjacent = current + adjacentOffList[adjacentCellIndex];
                    if (!CanPlace(mover, curAdjacent))
                    {
                        continue;
                    }

                    if (xiaoHao.TryGetValue(curAdjacent, out var previousCost) && previousCost <= xiaoHao[current] + 1) //找到了更短到达newPos的路径
                    {
                        continue;
                    }

                    sortList.Add(curAdjacent);
                    cameFrom[curAdjacent] = current;
                    xiaoHao[curAdjacent] = xiaoHao[current] + 1;
                }
            }

            if (lastpos == start)
                return new Vector2Int(-1, -1);

            while (cameFrom[lastpos] != start)
                lastpos = cameFrom[lastpos];
            return lastpos;
        }

        public bool IsSurround(Vector2Int target)
        {
            //找四个斜线方向的点,x为奇数或者偶数情况不一样
            if (target.x % 2 == 0) //偶数情况
            {
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        Vector2Int newpos = new Vector2Int(target.x + 1 - i * 2, target.y + j);
                        if (newpos.x < 0 || newpos.x > 8 || newpos.y < 0 || newpos.y > 5)
                        {
                            continue;
                        }

                        if (HotGameEntry.HeroManager.qige[newpos.x][newpos.y] > -1)
                        {
                            continue;
                        }

                        return false;
                    }
                }
            }
            else //奇数情况
            {
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        Vector2Int newpos = new Vector2Int(target.x + 1 - i * 2, target.y - j);

                        if (newpos.x < 0 || newpos.x > 8 || newpos.y < 0 || newpos.y > 4)
                        {
                            continue;
                        }

                        if (HotGameEntry.HeroManager.qige[newpos.x][newpos.y] > -1)
                        {
                            continue;
                        }

                        return false;
                    }
                }
            }

            return true;
        }

        public Vector3 GetGeziPos(int row, int column)
        {
            return _qigePosOffset + qigepos[row][column];
        }

        public bool CheckInGezi(Vector3 targetPos, out Vector2Int geziPos)
        {
            for (var rowIndex = 0; rowIndex < qigepos.Length; rowIndex++)
            {
                var oneline = qigepos[rowIndex];
                for (int columnIndex = 0; columnIndex < oneline.Length; columnIndex++)
                {
                    var qigeCenterToTargetPos = targetPos - GetGeziPos(rowIndex,columnIndex);
                    if (qigeCenterToTargetPos.magnitude > qigeXOffset / Mathf.Sqrt(3)) //在外圆外
                    {
                        continue;
                    }

                    var absX = Mathf.Abs(qigeCenterToTargetPos.x);
                    var absY = Mathf.Abs(qigeCenterToTargetPos.z);
                    if (absX > qigeXOffset / 2)
                    {
                        continue;
                    }

                    if (absY < qigeXOffset / 2 / Mathf.Sqrt(3))
                    {
                        geziPos = new Vector2Int(columnIndex, rowIndex);
                        return true;
                    }

                    if (qigeCenterToTargetPos.magnitude <= qigeXOffset / 2) //在内圈内
                    {
                        geziPos = new Vector2Int(columnIndex, rowIndex);
                        return true;
                    }

                    if ((qigeXOffset / 2 - absX) / Mathf.Sqrt(3) + qigeXOffset / 2 / Mathf.Sqrt(3) > absY)
                    {
                        geziPos = new Vector2Int(columnIndex, rowIndex);
                        return true;
                    }
                }
            }

            geziPos = Vector2Int.zero;
            return false;
        }

        /// <summary>
        /// 逻辑update
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        public void OnLogicUpdate(float elapseSeconds, float realElapseSeconds, bool stationaryEnemies = false)
        {
            CaveHazards?.Update(elapseSeconds);
            List<EntityQizi> tempEntityList = ListPool<EntityQizi>.Get();
            //先轮询己方棋子，后续联机的话需要判断 玩家uid来确定先后
            tempEntityList.AddRange(QiziCSList);
            tempEntityList.Sort((a, b) => a.HeroUID.CompareTo(b.HeroUID));
            foreach (var oneEntity in tempEntityList)
            {
                oneEntity.OnLogicUpdate(elapseSeconds, realElapseSeconds);
            }

            tempEntityList.Clear();
            tempEntityList.AddRange(DirenList);
            tempEntityList.Sort((a, b) => a.HeroUID.CompareTo(b.HeroUID));
            foreach (var oneEntity in tempEntityList)
            {
                oneEntity.OnLogicUpdate(elapseSeconds, realElapseSeconds, stationaryEnemies);
            }

            ListPool<EntityQizi>.Release(tempEntityList);
            OnLogicUpdateBullet(elapseSeconds, realElapseSeconds);
            OnLogicUpdatePosUpdate(elapseSeconds, realElapseSeconds);
            UpdateDamageNumber(elapseSeconds,realElapseSeconds);
            UpdateHpBar();
        }

        public void UpdateNoBattle(float elapseSeconds, float realElapseSeconds)
        {
            List<EntityQizi> tempEntityList = ListPool<EntityQizi>.Get();
            //先轮询己方棋子，后续联机的话需要判断 玩家uid来确定先后
            tempEntityList.AddRange(QiziCSList);
            tempEntityList.Sort((a, b) => a.HeroUID.CompareTo(b.HeroUID));
            foreach (var oneEntity in tempEntityList)
            {
                oneEntity.UpdateNoBattle(elapseSeconds,realElapseSeconds);
            }

            tempEntityList.Clear();
            tempEntityList.AddRange(DirenList);
            tempEntityList.Sort((a, b) => a.HeroUID.CompareTo(b.HeroUID));
            foreach (var oneEntity in tempEntityList)
            {
                oneEntity.UpdateNoBattle(elapseSeconds,realElapseSeconds);
            }
            ListPool<EntityQizi>.Release(tempEntityList);
            UpdateDamageNumber(elapseSeconds,realElapseSeconds);
            UpdateHpBar();
        }
        public void OnEntityDead(EntityQizi qizi)
        {
            if (qizi == null)
            {
                return;
            }

            ReleaseFootprint(qizi);
            List<EntityQizi> qiziList = qizi.BelongCamp == CampType.Friend ? QiziCSList : DirenList;
            if (!IsTimelineBattle && qiziList.Count((entity)=>entity.IsValid) == 0)
            {
                GameEntry.Event.FireNow(this, BattleStopEventArgs.Create(qizi.BelongCamp == CampType.Enemy));
            }
        }

        public void StartBattle()
        {
            dangqianliucheng = 1;
            foreach (var oneEntity in QiziCSList)
            {
                oneEntity.SavePos = new Vector2Int(oneEntity.columnIndex, oneEntity.rowIndex);
            }
        }

        public void OnBattleWin()
        {
            foreach (var oneEntity in QiziCSList)
            {
                oneEntity.OnWinBattle();
            }
        }
        public void OnBattleLose()
        {
            foreach (var oneEntity in QiziCSList)
            {
                oneEntity.OnLoseBattle();
            }

            foreach (var oneEntity in DirenList)
            {
                oneEntity.OnWinBattle();
            }
        }
        /// <summary>
        /// 游戏结束回到主界面
        /// </summary>
        public void GameOver()
        {
            FreshQige();
            foreach (var oneEntity in QiziCSList)
            {
                oneEntity.Remove();
            }

            QiziCSList.Clear();
            foreach (var oneEntity in DirenList)
            {
                oneEntity.Remove();
            }

            DirenList.Clear();
            QiziCurUniqueIndex = 0;
            ClearBullet();
            ClearPosPoint();
            ClearAllDamageNum();
            UnloadUnusedAnimation();
            dangqianliucheng = 0;
            CheckToStartCamera();
            CoinNum = 0;
            ClearBagList();
        }

        /// <summary>
        /// 该point通过，清理敌方以及刷新友方棋子
        /// </summary>
        public void FreshBattle()
        {
            FreshQige();
            foreach (var oneEntity in DirenList)
            {
                oneEntity.Remove();
            }

            ClearBullet();
            ClearPosPoint();
            DirenList.Clear();
            UnloadUnusedAnimation();
            ClearAllDamageNum();
            dangqianliucheng = 0;
            foreach (var oneEntity in QiziCSList)
            {
                oneEntity.ReInit();
                UpdateEntityPos(oneEntity, oneEntity.SavePos);
            }
        }

        private void FreshQige()
        {
            for (int y = 0; y < qige.Length; y++)
            {
                for (int x = 0; x < qige[y].Length; x++)
                {
                    qige[y][x] = -1;
                }
            }
        }

        public void MoveQigeRoot(Vector3 newOffset)
        {
            m_QigeRoot.position = newOffset;
            _qigePosOffset = newOffset;
        }
        
    }
}
