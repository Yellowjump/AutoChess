//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System.Collections.Generic;
using DataTable;
using Entity;
using GameFramework.Resource;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        public ObjectPool<EntityQizi> PoolEntity;
        public Dictionary<string, List<GetGObjSuccessCallback>> WaitAssetLoadThenGetFromPool = new();
        public Dictionary<string, ObjectPool<GameObject>> PoolDic = new();
        public delegate void GetGObjSuccessCallback(GameObject asset);
        private LoadAssetCallbacks OnLoadGameObjectCallback;
        
        [SerializeField]
        private Transform m_InstanceRoot = null;
        [SerializeField]
        private Transform m_InstanceDisableRoot = null;
        private void InitPool()
        {
            PoolEntity = new ObjectPool<EntityQizi>(() => new EntityQizi(), null, null, null, true, 10, 1000);
        }

        public EntityQizi GetNewEntityQizi()
        {
            return PoolEntity?.Get();
        }

        public void ReleaseEntityQizi(EntityQizi qizi)
        {
            PoolEntity?.Release(qizi);
        }
        public void GetHeroObjByID(int id,GetGObjSuccessCallback callback)
        {
            var heroTable = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (heroTable.HasDataRow(id))
            {
                var heroData = heroTable[id];
                var assetID = heroData.AssetID;
                var assetPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
                if (assetPathTable.HasDataRow(assetID))
                {
                    var assetData = assetPathTable[assetID];
                    GetNewObjFromPool(assetData.AssetPath,callback);
                }
            }
        }

        class DamageNumber:IReference
        {
            public int num;
            public bool Playing = false;
            public DamageType CurDamageType;
            public EntityQizi Owner;
            public GameObject NumberObj;
            private float animDuration;
            private float allAnimDuration;
            public Vector3 targetPos;
            public void Clear()
            {
                num = 0;
                CurDamageType = DamageType.PhysicalDamage;
                Owner = null;
                GameEntry.HeroManager.ReleaseGameObject("", NumberObj);
                NumberObj = null;
            }
            public void UpdateMove(float elapseSeconds, float realElapseSeconds)
            {
                if (NumberObj == null)
                {
                    return;
                }
                
                NumberObj.transform.position+=Vector3.Lerp(NumberObj.transform.position,targetPos,animDuration/allAnimDuration);
                //if (animComplete)
                {
                    //PlayingDmgNumberList.Remove(this);
                    ReferencePool.Release(this);
                }
            }
        }

        public const string DamageNumberPerfabPath = "";
        private List<DamageNumber> WaitDmgNumberList = new List<DamageNumber>();
        private List<DamageNumber> PlayingDmgNumberList = new List<DamageNumber>();
        public void ShowDamageNum(CauseDamageData data)
        {
            var newDamageNumber = ReferencePool.Acquire<DamageNumber>();
            var numPerfabPath = DamageNumberPerfabPath;
            newDamageNumber.num = (int)data.DamageValue;
            newDamageNumber.CurDamageType = data.CurDamageType;
            newDamageNumber.Owner = data.Target;
            newDamageNumber.targetPos = data.Target.LogicPosition + Vector3.up*0.2f+Vector3.right*0.2f;
            WaitDmgNumberList.Add(newDamageNumber);
            GetNewObjFromPool(numPerfabPath, OnLoadOneNewDamageNumberPrefab);
        }

        private void OnLoadOneNewDamageNumberPrefab(GameObject obj)
        {
            if (WaitDmgNumberList != null && WaitDmgNumberList.Count > 0)
            {
                var oneDmg = WaitDmgNumberList[0];
                oneDmg.NumberObj = obj;
                //设置 text
                //播放动画
                //设置动画回调
                WaitDmgNumberList.Remove(oneDmg);
                PlayingDmgNumberList.Add(oneDmg);
            }
            else
            {
                ReleaseGameObject(DamageNumberPerfabPath,obj);
            }
        }
        private void UpdateDamageNumber(float elapseSeconds, float realElapseSeconds)
        {
            var runList = ListPool<DamageNumber>.Get();
            runList.AddRange(PlayingDmgNumberList);
            foreach (var oneDamage in runList)
            {
                if (oneDamage.Playing)
                {
                    oneDamage.UpdateMove(elapseSeconds,realElapseSeconds);
                }
            }
            ListPool<DamageNumber>.Release(runList);
        }
        public void GetBulletObjByID(int id,GetGObjSuccessCallback callback)
        {
            var bulletTable = GameEntry.DataTable.GetDataTable<DRBullet>("Bullet");
            if (bulletTable.HasDataRow(id))
            {
                var bulletData = bulletTable[id];
                var assetID = bulletData.AssetPathID;
                var assetPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
                if (assetPathTable.HasDataRow(assetID))
                {
                    var assetData = assetPathTable[assetID];
                    GetNewObjFromPool(assetData.AssetPath,callback);
                }
            }
        }
        private void GetNewObjFromPool(string path, GetGObjSuccessCallback callback)
        {
            if (!path.EndsWith(".prefab"))
            {
                Log.Error("only GameObject");
                return;
            }

            if (PoolDic.ContainsKey(path))
            {
                var targetPool = PoolDic[path];
                var obj = targetPool.Get();
                callback?.Invoke(obj);
            }
            else
            {
                if (WaitAssetLoadThenGetFromPool.ContainsKey(path))
                {
                    var targetWaitCallbackList = WaitAssetLoadThenGetFromPool[path];
                    targetWaitCallbackList.Add(callback);
                }
                else
                {
                    List<GetGObjSuccessCallback> targetWaitCallbackList = ListPool<GetGObjSuccessCallback>.Get();
                    targetWaitCallbackList.Add(callback);
                    WaitAssetLoadThenGetFromPool.Add(path,targetWaitCallbackList);
                    GameEntry.Resource.LoadAsset(path, new LoadAssetCallbacks(OnLoadGameObjCallback));
                }
                
            }
        }

        private void OnLoadGameObjCallback(string path, object asset, float duration, object userData)
        {
            if (!PoolDic.ContainsKey(path))
            {
                if (asset is GameObject gObj)
                {
                    PoolDic.Add(path, new ObjectPool<GameObject>(() => Instantiate(gObj),(g)=>g.transform.SetParent(m_InstanceRoot),(g)=>g.transform.SetParent(m_InstanceDisableRoot),Destroy));
                }
            }
            var targetPool = PoolDic[path];
            if (!WaitAssetLoadThenGetFromPool.ContainsKey(path)) return;
            var targetWaitCallbackList = WaitAssetLoadThenGetFromPool[path];
            if (targetWaitCallbackList.Count != 0)
            {
                foreach (var oneCallback in targetWaitCallbackList)
                {
                    var obj = targetPool.Get();
                    oneCallback?.Invoke(obj);
                }
            }
            WaitAssetLoadThenGetFromPool.Remove(path);
            ListPool<GetGObjSuccessCallback>.Release(targetWaitCallbackList);

        }

        public void ReleaseGameObject(string path, GameObject obj)
        {
            if (obj == null) return;
            if (!PoolDic.ContainsKey(path))
            {
                Log.Error($"HeroComponent.Pool hasNo {path}");
                Destroy(obj);
                return;
            }
            var targetPool = PoolDic[path];
            targetPool.Release(obj);
        }

        public bool RemoveOneWaitAssetLoadThenGet(string path,GetGObjSuccessCallback callback)
        {
            if (WaitAssetLoadThenGetFromPool.ContainsKey(path))
            {
                var targetWaitList = WaitAssetLoadThenGetFromPool[path];
                if (targetWaitList != null && targetWaitList.Count != 0)
                {
                    targetWaitList.Remove(callback);
                    if (targetWaitList.Count == 0)
                    {
                        WaitAssetLoadThenGetFromPool.Remove(path);
                        ListPool<GetGObjSuccessCallback>.Release(targetWaitList);
                    }
                    return true;
                }
            }
            return false;
        }
        public void ReleaseHeroGameObject(int id, GameObject obj,GetGObjSuccessCallback callback)
        {
            var heroTable = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (heroTable.HasDataRow(id))
            {
                var heroData = heroTable[id];
                var assetID = heroData.AssetID;
                var assetPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
                if (assetPathTable.HasDataRow(assetID))
                {
                    var assetData = assetPathTable[assetID];
                    if (obj != null) ReleaseGameObject(assetData.AssetPath, obj);
                    RemoveOneWaitAssetLoadThenGet(assetData.AssetPath, callback);
                }
            }
        }

        public void ReleaseBulletGameObject(int id, GameObject obj,GetGObjSuccessCallback callback)
        {
            var bulletTable = GameEntry.DataTable.GetDataTable<DRBullet>("Bullet");
            if (bulletTable.HasDataRow(id))
            {
                var bulletData = bulletTable[id];
                var assetID = bulletData.AssetPathID;
                var assetPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
                if (assetPathTable.HasDataRow(assetID))
                {
                    var assetData = assetPathTable[assetID];
                    if (obj != null) ReleaseGameObject(assetData.AssetPath, obj);
                    RemoveOneWaitAssetLoadThenGet(assetData.AssetPath, callback);
                }
            }
        }
    }
}