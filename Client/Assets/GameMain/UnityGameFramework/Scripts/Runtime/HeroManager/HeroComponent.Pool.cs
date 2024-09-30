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
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        public ObjectPool<EntityQizi> PoolEntity;
        public ObjectPool<GameObject> EmptyPool;
        public Dictionary<string, List<GetGObjSuccessCallback>> WaitAssetLoadThenGetFromPool = new();
        public Dictionary<string, ObjectPool<GameObject>> PoolDic = new();
        public delegate void GetGObjSuccessCallback(GameObject asset,string path);
        private LoadAssetCallbacks OnLoadGameObjectCallback;
        
        [SerializeField]
        private Transform m_InstanceRoot = null;
        [SerializeField]
        private Transform m_InstanceDisableRoot = null;
        private Canvas m_InstanceWorldCanvas = null;
        private void InitPool()
        {
            OnLoadGameObjectCallback = new LoadAssetCallbacks(OnLoadGameObjCallback);
            PoolEntity = new ObjectPool<EntityQizi>(() => new EntityQizi(), null, null, null, true, 10, 1000);
            EmptyPool = new ObjectPool<GameObject>(() => new GameObject(), null, (g) =>
            {
                g.transform.SetParent(m_InstanceDisableRoot);
                g.transform.rotation = Quaternion.identity;
                g.transform.localScale = Vector3.one;
            }, Destroy);
        }

        public EntityQizi GetNewEntityQizi()
        {
            return PoolEntity?.Get();
        }

        public void ReleaseEntityQizi(EntityQizi qizi)
        {
            PoolEntity?.Release(qizi);
        }

        public GameObject GetNewEmptyObj()
        {
            return EmptyPool?.Get();
        }

        public void ReleaseEmptyObj(GameObject obj)
        {
            EmptyPool?.Release(obj);
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

        public class HpBar : IReference
        {
            public GameObject HpBarObj;
            public EntityQizi Owner;
            public void Clear()
            {
                Owner = null;
                GameEntry.HeroManager.ReleaseGameObject(HpBarPerfabPath, HpBarObj);
                HpBarObj = null;
            }

            public void UpdataMoveHpBar()
            {
                if (HpBarObj == null)
                {
                    return;
                }
                HpBarObj.transform.position = Owner.LogicPosition+Vector3.up*2;
            }
        }
        public const string HpBarPerfabPath = "Assets/GameMain/Prefeb/UIPrefab/xuetiao_qizi.prefab";
        public List<HpBar> WaitHpBarList = new List<HpBar>();
        public List<HpBar> PlayingHpBarList = new List<HpBar>();
        
        public void AddHpBar(EntityQizi qizi)
        {
            var newHpBar = ReferencePool.Acquire<HpBar>();
            var hpBarPerfabPath = HpBarPerfabPath;
            newHpBar.Owner = qizi;
            qizi.HpBar = newHpBar;
            WaitHpBarList.Add(newHpBar);
            GetNewObjFromPool(hpBarPerfabPath, OnLoadOneNewHpBarPrefab);
        }
        private void OnLoadOneNewHpBarPrefab(GameObject obj,string path)
        {
            if (GameEntry.HeroManager.WaitHpBarList != null && GameEntry.HeroManager.WaitHpBarList.Count > 0)
            {
                var oneHpBar = GameEntry.HeroManager.WaitHpBarList[0];
                oneHpBar.HpBarObj = obj;
                //设置canvas
                Canvas canvas = m_InstanceWorldCanvas.GetComponent<Canvas>();
                oneHpBar.HpBarObj.transform.SetParent(canvas.transform);
                //设置pos
                oneHpBar.HpBarObj.transform.forward = Camera.main.transform.forward;
                oneHpBar.HpBarObj.transform.position = oneHpBar.Owner.LogicPosition+Vector3.up*2;
                //绑定棋子血条
                oneHpBar.Owner.xuetiao=obj.transform.Find("xuetiao").GetComponent<Slider>();
                oneHpBar.Owner.power= obj.transform.Find("pow").GetComponent<Slider>();
                oneHpBar.Owner.hudun=obj.transform.GetChild(1).GetChild(1).GetChild(0).Find("hudun").GetComponent<Slider>();
                //设置动画回调
                WaitHpBarList.Remove(oneHpBar);
                PlayingHpBarList.Add(oneHpBar);
            }
            else
            {
                ReleaseGameObject(HpBarPerfabPath,obj);
            }
        }
        private void UpdateHpBar()
        {
            var runList = ListPool<HpBar>.Get();
            runList.AddRange(PlayingHpBarList);
            foreach (var oneHpBar in runList)
            {
                oneHpBar.UpdataMoveHpBar();
            }
            ListPool<HpBar>.Release(runList);
        }
        
        public class DamageNumber:IReference
        {
            public int num;
            public bool Playing = false;
            public DamageType CurDamageType;
            public EntityQizi Owner;
            public GameObject NumberObj;
            public Text damageNumText;
            public int TextSize;
            public float animDuration;
            public float allAnimDuration;
            public Vector3 targetPos;
            public void Clear()
            {
                animDuration = -1;
                allAnimDuration = -2;
                num = 0;
                TextSize = 0;
                CurDamageType = DamageType.PhysicalDamage;
                Owner = null;
                GameEntry.HeroManager.ReleaseGameObject(DamageNumberPerfabPath, NumberObj);
                NumberObj = null;
            }
            public void UpdateMove(float elapseSeconds, float realElapseSeconds)
            {
                if (NumberObj == null)
                {
                    return;
                }
                if (damageNumText.fontSize >TextSize )
                {
                    damageNumText.fontSize --;
                }

                if (animDuration!=null&&animDuration<allAnimDuration)
                {
                    NumberObj.transform.position=Vector3.Lerp(targetPos,targetPos+new Vector3(allAnimDuration/3, allAnimDuration*1.5f,0),animDuration/allAnimDuration);
                    animDuration += elapseSeconds;
                }
                else
                {
                    
                    GameEntry.HeroManager.PlayingDmgNumberList.Remove(this);
                    ReferencePool.Release(this);
                }
            }
        }


        public const string DamageNumberPerfabPath = "Assets/GameMain/Prefeb/UIPrefab/DamageNumber.prefab";
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

        private void OnLoadOneNewDamageNumberPrefab(GameObject obj,string path)
        {
            if (WaitDmgNumberList != null && WaitDmgNumberList.Count > 0)
            {
                var oneDmg = WaitDmgNumberList[0];
                oneDmg.NumberObj = obj;
                //设置canvas
                Canvas canvas = m_InstanceWorldCanvas.GetComponent<Canvas>();
                oneDmg.NumberObj.transform.SetParent(canvas.transform);
                //设置pos
                oneDmg.NumberObj.transform.forward = Camera.main.transform.forward;
                oneDmg.NumberObj.transform.position = oneDmg.targetPos;
                //设置 text
                oneDmg.damageNumText = oneDmg.NumberObj.GetComponent<Text>();
                oneDmg.damageNumText.text = oneDmg.num.ToString();
                oneDmg.TextSize = oneDmg.damageNumText.fontSize;
                oneDmg.damageNumText.fontSize += 20;
                switch (oneDmg.CurDamageType)
                {
                    case DamageType.PhysicalDamage:
                        oneDmg.damageNumText.color = new Color(190/255f,86/255f,11/255f); 
                        break;
                    case DamageType.MagicDamage:
                        oneDmg.damageNumText.color = Color.blue;
                        break;
                    case DamageType.TrueDamage:
                        oneDmg.damageNumText.color = Color.white;
                        break;
                    default:
                        break;
                }
                //设置动画参数
                oneDmg.animDuration = 0f;
                oneDmg.allAnimDuration = 0.8f;
                oneDmg.Playing = true;
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
                oneDamage.UpdateMove(elapseSeconds,realElapseSeconds);
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

        public void GetSfxByID(int id,GetGObjSuccessCallback callback)
        {
            var sfxTable = GameEntry.DataTable.GetDataTable<DRSfx>("Sfx");
            if (sfxTable.HasDataRow(id))
            {
                var sfxData = sfxTable[id];
                var assetID = sfxData.AssetPathID;
                var assetPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
                if (assetPathTable.HasDataRow(assetID))
                {
                    var assetData = assetPathTable[assetID];
                    GetNewObjFromPool(assetData.AssetPath,callback);
                }
            }
        }

        public void GetPrefabByAssetID(int assetID,GetGObjSuccessCallback callback)
        {
            var assetPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (assetPathTable.HasDataRow(assetID))
            {
                var assetData = assetPathTable[assetID];
                GetNewObjFromPool(assetData.AssetPath,callback);
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
                callback?.Invoke(obj,path);
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
                    GameEntry.Resource.LoadAsset(path, OnLoadGameObjectCallback);
                }
                
            }
        }

        private void OnLoadGameObjCallback(string path, object asset, float duration, object userData)
        {
            if (!PoolDic.ContainsKey(path))
            {
                if (asset is GameObject gObj)
                {
                    PoolDic.Add(path, new ObjectPool<GameObject>(() => Instantiate(gObj),(g)=>g.transform.SetParent(m_InstanceRoot),
                        (g)=>
                    {
                        g.transform.SetParent(m_InstanceDisableRoot);
                        g.transform.rotation = Quaternion.identity;
                        g.transform.localScale = Vector3.one;
                    },Destroy));
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
                    oneCallback?.Invoke(obj,path);
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
        public void ReleaseSfxGameObject(int id, GameObject obj,GetGObjSuccessCallback callback)
        {
            var sfxTable = GameEntry.DataTable.GetDataTable<DRSfx>("Sfx");
            if (sfxTable.HasDataRow(id))
            {
                var bulletData = sfxTable[id];
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

        public void ReleaseAssetObj(int assetID, GameObject obj, GetGObjSuccessCallback callback)
        {
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