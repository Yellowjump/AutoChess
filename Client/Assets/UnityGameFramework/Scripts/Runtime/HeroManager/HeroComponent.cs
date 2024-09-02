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
        public Vector3[][] qigepos = new Vector3[8][]; //保存棋格位置
        public float qigeXOffset = 1f;

        public List<EntityQizi> QiziCSList = new List<EntityQizi>();//保存当前己方棋子 死亡不会移除
        public List<EntityQizi> DirenList = new List<EntityQizi>();//保存当前敌方棋子 死亡不会移除
        public int dangqianliucheng = 0;//保存当前流程，0是prebattle,1是battle
        private Vector2Int[] adjacentOffsetDoubleRow = new Vector2Int[6]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.one, Vector2Int.down + Vector2Int.right
        };
        private Vector2Int[] adjacentOffsetSingleRow = new Vector2Int[6]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.left+Vector2Int.up, Vector2Int.left+ Vector2Int.down
        };
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

        public Vector2Int GetIndexQizi(EntityQizi qizi)
        {
            var qiziUid = qizi.HeroUID;
            for (var rowIndex = 0; rowIndex < qige.Length; rowIndex++)
            {
                var oneline = qige[rowIndex];
                for (int columnIndex = 0; columnIndex < oneline.Length; columnIndex++)
                {
                    if (qige[rowIndex][columnIndex] == qiziUid)
                    {
                        return new Vector2Int(columnIndex, rowIndex);
                    }
                }
            }

            return new Vector2Int(-1, -1);
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
            qige[qizi.rowIndex][qizi.columnIndex] = -1;
            qige[newPos.y][newPos.x] = qizi.HeroUID;
            qizi.LogicPosition = qigepos[newPos.y][newPos.x];
            qizi.columnIndex = newPos.x;
            qizi.rowIndex = newPos.y;
        }

        public Vector2Int Findpath(Vector2Int start, Vector2Int end, float gongjidistance)
        {
            /*if (gongjidistance == 1&&IsSurround(end))//攻击距离为1，先判断是否目标被围了一圈
            {
                return new Vector2Int(-1,-1);
            }*/
            Vector2Int lastpos = start;
            List<Vector2Int> sortList = new List<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, float> xiaoHao = new Dictionary<Vector2Int, float>();
            sortList.Add(start);
            //cameFrom[start] = new Vector2Int(-1,-1);
            xiaoHao[start] = 0f;
            bool find = false;
            while (sortList.Count > 0)
            {
                sortList.Sort((a, b) => (xiaoHao[a] * xiaoHao[a] + getDistance(a, end)).CompareTo((xiaoHao[b] * xiaoHao[b] + getDistance(b, end))));
                Vector2Int current = sortList[0];
                sortList.Remove(current);
                var dis = Utility.TruncateFloat(getDistance(current, end), 4);
                if (dis <= gongjidistance * gongjidistance) //一格斜着因为浮点精度计算出来距离大于1
                {
                    lastpos = current;
                    find = true;
                    break;
                }

                var adjacentOffList = current.y % 2 == 0 ? adjacentOffsetDoubleRow : adjacentOffsetSingleRow;
                for (int adjacentCellIndex = 0; adjacentCellIndex < 6; adjacentCellIndex++)
                {
                    var curAdjacent = current + adjacentOffList[adjacentCellIndex];
                    if (curAdjacent.x < 0 || curAdjacent.x > 6 || curAdjacent.y < 0 || curAdjacent.y > 7)
                    {
                        continue;
                    }

                    if (cameFrom.ContainsKey(curAdjacent) && xiaoHao[curAdjacent] <= xiaoHao[current] + 1) //找到了更短到达newPos的路径
                    {
                        continue;
                    }

                    if (GameEntry.HeroManager.qige[curAdjacent.y][curAdjacent.x] > -1)
                    {
                        continue;
                    }

                    sortList.Add(curAdjacent);
                    cameFrom[curAdjacent] = current;
                    xiaoHao[curAdjacent] = xiaoHao[current] + 1;
                }
            }

            if (find)
            {
                Stack<Vector2Int> trace = new Stack<Vector2Int>();
                Vector2Int pos = lastpos;
                while (pos != start)
                {
                    trace.Push(pos);
                    pos = cameFrom[pos];
                }

                while (trace.Count > 0)
                {
                    Vector2Int p = trace.Pop();
                    return p;
                }

                return lastpos;
            }

            return new Vector2Int(-1, -1);
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

                        if (GameEntry.HeroManager.qige[newpos.x][newpos.y] > -1)
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

                        if (GameEntry.HeroManager.qige[newpos.x][newpos.y] > -1)
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
            return qigepos[row][column];
        }

        public bool CheckInGezi(Vector3 targetPos, out Vector2Int geziPos)
        {
            for (var rowIndex = 0; rowIndex < qigepos.Length; rowIndex++)
            {
                var oneline = qigepos[rowIndex];
                for (int columnIndex = 0; columnIndex < oneline.Length; columnIndex++)
                {
                    var qigeCenterToTargetPos = targetPos - qigepos[rowIndex][columnIndex];
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
        public void OnLogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
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
                oneEntity.OnLogicUpdate(elapseSeconds, realElapseSeconds);
            }

            ListPool<EntityQizi>.Release(tempEntityList);
            OnLogicUpdateBullet(elapseSeconds, realElapseSeconds);
            UpdateDamageNumber(elapseSeconds,realElapseSeconds);
        }

        public void OnEntityDead(EntityQizi qizi)
        {
            if (qizi == null)
            {
                return;
            }

            qige[qizi.rowIndex][qizi.columnIndex] = -1;
            List<EntityQizi> qiziList = qizi.BelongCamp == CampType.Friend ? QiziCSList : DirenList;
            if (qiziList.Count((entity)=>entity.IsValid) == 0)
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
            dangqianliucheng = 0;
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
            DirenList.Clear();
            dangqianliucheng = 0;
            foreach (var oneEntity in QiziCSList)
            {
                oneEntity.ReInit();
                oneEntity.LogicPosition = GetGeziPos(oneEntity.SavePos.y, oneEntity.SavePos.x);
                oneEntity.columnIndex = oneEntity.SavePos.x;
                oneEntity.rowIndex = oneEntity.SavePos.y;
                qige[oneEntity.SavePos.y][oneEntity.SavePos.x] = oneEntity.HeroUID;
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

        
    }
}