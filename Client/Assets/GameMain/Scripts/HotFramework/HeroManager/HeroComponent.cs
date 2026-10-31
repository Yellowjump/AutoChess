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
using Entity;
using Entity.Bullet;
using SelfEventArg;
using SkillSystem;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using Zizouqi.CombatDots;

namespace UnityGameFramework.Runtime
{
    /// <summary>
    /// 棋子管理。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game Framework/HeroManager")]
    public sealed partial class HeroComponent : GameFrameworkComponent
    {
        private static readonly ProfilerMarker TrackingBulletMarker =
            new ProfilerMarker("Combat.Bullet.Tracking");
        private static readonly ProfilerMarker OrbitBulletMarker =
            new ProfilerMarker("Combat.Bullet.Orbit");
        private static readonly ProfilerMarker PenetratingBulletMarker =
            new ProfilerMarker("Combat.Bullet.Penetrating");
        private static readonly ProfilerMarker MarkPointBulletMarker =
            new ProfilerMarker("Combat.Bullet.MarkPoint");
        private static readonly ProfilerMarker OtherBulletMarker =
            new ProfilerMarker("Combat.Bullet.Other");
        private static readonly ProfilerMarker UnitPresentationMarker =
            new ProfilerMarker("Combat.Presentation.Units");

        public Vector3[][] qigepos = new Vector3[8][]; //保存棋格位置，相对areaPoint点
        public float qigeXOffset = 1f;

        public List<EntityQizi> QiziCSList = new List<EntityQizi>();//保存当前己方棋子 死亡不会移除
        public List<EntityQizi> DirenList = new List<EntityQizi>();//保存当前敌方棋子 死亡不会移除
        private readonly Dictionary<int, EntityQizi> m_EntitiesByUid =
            new Dictionary<int, EntityQizi>();
        public bool InBattle => CombatBoardBridge.IsBattleActive();
        public GameObject QigePrefab;
        [SerializeField]
        private Transform m_QigeRoot = null;

        public Vector3 QigePosOffset => CombatBoardBridge.GetOffset();
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
            qigepos[0] = new Vector3[7];
            qigepos[1] = new Vector3[7];
            qigepos[2] = new Vector3[7];
            qigepos[3] = new Vector3[7];
            qigepos[4] = new Vector3[7];
            qigepos[5] = new Vector3[7];
            qigepos[6] = new Vector3[7];
            qigepos[7] = new Vector3[7];
        }

        public Vector2Int GetIndexQizi(EntityQizi qizi)
        {
            return CombatBoardBridge.FindOccupant(qizi.HeroUID, out int row, out int column)
                ? new Vector2Int(column, row) : new Vector2Int(-1, -1);
        }

        public bool GetQiziByQigeIndex(Vector2Int geziPos, out EntityQizi curPosQizi)
        {
            curPosQizi = null;
            try
            {
                var uid = CombatBoardBridge.GetOccupant(geziPos.y, geziPos.x);
                return m_EntitiesByUid.TryGetValue(uid, out curPosQizi);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            return false;
        }

        public void UpdateEntityPos(EntityQizi qizi, Vector2Int newPos)
        {
            Vector2Int oldPos = GetIndexQizi(qizi);
            CombatBoardBridge.MoveOccupant(oldPos.y, oldPos.x,
                newPos.y, newPos.x, qizi.HeroUID);
            qizi.LogicPosition = GetGeziPos(newPos.y,newPos.x);
        }

        public void SwapEntityPos(EntityQizi first, EntityQizi second)
        {
            if (first == second)
            {
                return;
            }
            Vector2Int firstPos = GetIndexQizi(first);
            Vector2Int secondPos = GetIndexQizi(second);
            CombatBoardBridge.SwapOccupants(firstPos.y, firstPos.x,
                secondPos.y, secondPos.x);
            first.LogicPosition = GetGeziPos(secondPos.y, secondPos.x);
            second.LogicPosition = GetGeziPos(firstPos.y, firstPos.x);
        }

        public Vector2Int Findpath(Vector2Int start, Vector2Int end, int atkRangeMM)
        {
            if (start.x < 0 || start.x >= CombatBoardBridge.Columns ||
                start.y < 0 || start.y >= CombatBoardBridge.Rows ||
                end.x < 0 || end.x >= CombatBoardBridge.Columns ||
                end.y < 0 || end.y >= CombatBoardBridge.Rows)
            {
                return start;
            }
            CombatBoardBridge.EnsureGeometry(qigepos);
            return CombatBoardBridge.FindPath(start.y, start.x, end.y, end.x, atkRangeMM,
                out int row, out int column)
                ? new Vector2Int(column, row) : new Vector2Int(-1, -1);
        }

        public Vector3 GetGeziPos(int row, int column)
        {
            CombatBoardBridge.EnsureGeometry(qigepos);
            return CombatBoardBridge.GetCellWorldPosition(row, column);
        }

        public bool CheckInGezi(Vector3 targetPos, out Vector2Int geziPos)
        {
            CombatBoardBridge.EnsureGeometry(qigepos);
            if (CombatBoardBridge.TryFindCell(targetPos, qigeXOffset,
                    out int row, out int column))
            {
                geziPos = new Vector2Int(column, row);
                return true;
            }
            geziPos = Vector2Int.zero;
            return false;
        }

        /// <summary>
        /// 逻辑update
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        public void OnLogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            //先轮询己方棋子，后续联机的话需要判断 玩家uid来确定先后
            SkillSystem.CombatTriggerSimpleQueue.BeginStep();
            SkillSystem.CombatTriggerProjectileSpawnQueue.BeginStep();
            try
            {
            Zizouqi.CombatDots.CombatFrameScheduler.AdvanceBattle(elapseSeconds,
                uid =>
                {
                    if (m_EntitiesByUid.TryGetValue(uid, out EntityQizi oneEntity))
                    {
                        oneEntity.PrepareSkillDecisions();
                    }
                },
                (uid, skillPlans) =>
                {
                    if (m_EntitiesByUid.TryGetValue(uid, out EntityQizi oneEntity))
                    {
                        oneEntity.OnLogicUpdate(elapseSeconds, realElapseSeconds,
                            skillPlans);
                    }
                },
                (handle, broadphase, bezierSteps, hitCandidates, motion,
                    orbitCandidates) =>
                {
                    if (m_BulletsByProjectileHandle.TryGetValue(handle, out BulletBase bullet))
                    {
                        switch (bullet)
                        {
                            case BulletTracking tracking:
                                using (TrackingBulletMarker.Auto())
                                    UpdateSimpleTrackingProjectile(tracking,
                                        elapseSeconds, realElapseSeconds, motion);
                                break;
                            case BulletRotateOwner orbit:
                                using (OrbitBulletMarker.Auto())
                                    orbit.LogicUpdate(elapseSeconds,
                                        realElapseSeconds, motion,
                                        orbitCandidates);
                                break;
                            case BulletPenetrating penetrating:
                                using (PenetratingBulletMarker.Auto())
                                    penetrating.LogicUpdate(elapseSeconds, realElapseSeconds,
                                        broadphase, bezierSteps, hitCandidates);
                                break;
                            case BulletMarkPoint mark:
                                using (MarkPointBulletMarker.Auto())
                                    mark.LogicUpdate(elapseSeconds,
                                        realElapseSeconds, motion);
                                break;
                            default:
                                using (OtherBulletMarker.Auto())
                                    bullet.LogicUpdate(elapseSeconds, realElapseSeconds);
                                break;
                        }
                    }
                },
                (handle, tickDue) =>
                {
                    if (!m_PosPointsByHandle.TryGetValue(handle, out EntityPosPoint point))
                    {
                        return;
                    }
                    point.CommitTimedProjectiles();
                    point.LogicUpdate(elapseSeconds, realElapseSeconds, tickDue);
                    if (m_PosPointsByHandle.ContainsKey(handle) && !point.IsValid)
                    {
                        DestoryPosPoint(point);
                    }
                }, SkillSystem.CombatTriggerProjectileSpawnQueue.CommitProjectiles,
                    PlayProjectileActiveAudio,
                    SkillSystem.CombatTriggerSimpleQueue.Flush);
            }
            finally
            {
                try
                {
                    SkillSystem.CombatTriggerSimpleQueue.EndStep();
                }
                finally
                {
                    SkillSystem.CombatTriggerProjectileSpawnQueue.EndStep();
                }
            }
        }

        public void UpdateBattlePresentation(float elapseSeconds, float realElapseSeconds)
        {
            DrainDamageNumberEvents();
            UpdatePointPresentation();
            UpdateBulletVisuals();
            using (UnitPresentationMarker.Auto())
            {
                foreach (EntityQizi unit in m_EntitiesByUid.Values)
                {
                    unit.UpdateCombatPresentation();
                }
            }
            UpdateDamageNumber(elapseSeconds, realElapseSeconds);
            UpdateHpBar();
            SampleHotPools();
        }

        public void UpdateNoBattle(float elapseSeconds, float realElapseSeconds)
        {
            //先轮询己方棋子，后续联机的话需要判断 玩家uid来确定先后
            Zizouqi.CombatDots.CombatFrameScheduler.VisitUnits(uid =>
            {
                if (m_EntitiesByUid.TryGetValue(uid, out EntityQizi oneEntity))
                {
                    oneEntity.UpdateNoBattle(elapseSeconds,realElapseSeconds);
                }
            });
            UpdateDamageNumber(elapseSeconds,realElapseSeconds);
            UpdateHpBar();
            SampleHotPools();
        }
        public void OnEntityDead(EntityQizi qizi)
        {
            if (qizi == null)
            {
                return;
            }

            if (CombatBattleResolutionBridge.ResolveUnitDeath(qizi.CombatUnitHandle,
                    out bool battleWon))
            {
                GameEntry.Event.FireNow(this, BattleStopEventArgs.Create(battleWon));
            }
        }

        public void StartBattle()
        {
            FinishBattlePoolPrewarm();
            CombatBoardBridge.ClearProjectileVisuals();
            CombatBoardBridge.ClearDamageNumbers();
            CombatBoardBridge.SetBattleActive(true);
            foreach (var oneEntity in QiziCSList)
            {
                if (CombatBoardBridge.FindOccupant(oneEntity.HeroUID,
                        out int row, out int column))
                {
                    CombatAttributeBridge.SetDeploymentCell(oneEntity.CombatUnitHandle,
                        row, column);
                }
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
            CombatBoardBridge.ResetHeroUid();
            ClearBullet();
            ClearPosPoint();
            ClearAllDamageNum();
            UnloadUnusedAnimation();
            CombatBoardBridge.SetBattleActive(false);
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
            CombatBoardBridge.SetBattleActive(false);
            foreach (var oneEntity in QiziCSList)
            {
                CombatAttributeBridge.GetDeploymentCell(oneEntity.CombatUnitHandle,
                    out int row, out int column);
                oneEntity.ReInit();
                CombatAttributeBridge.SetDeploymentCell(oneEntity.CombatUnitHandle,
                    row, column);
                oneEntity.LogicPosition = GetGeziPos(row, column);
                CombatBoardBridge.SetOccupant(row, column, oneEntity.HeroUID);
            }
        }

        private void FreshQige()
        {
            CombatBoardBridge.Clear();
        }

        public void MoveQigeRoot(Vector3 newOffset)
        {
            m_QigeRoot.position = newOffset;
            CombatBoardBridge.SetOffset(newOffset);
        }
        
    }
}
