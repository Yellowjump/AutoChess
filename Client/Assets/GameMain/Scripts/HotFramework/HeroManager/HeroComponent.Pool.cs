//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System.Collections.Generic;
using System.IO;
using DataTable;
using GameFramework.DataTable;
using Entity;
using GameFramework.Resource;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Unity.Profiling;
using Zizouqi.CombatVisuals;
using Zizouqi.CombatDots;


namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        private static readonly ProfilerMarker PoolPrewarmMarker =
            new ProfilerMarker("Combat.Pool.Prewarm");
        private static readonly ProfilerMarker DamageNumberRequestMarker =
            new ProfilerMarker("Combat.Presentation.DamageNumber.Request");
        private static readonly ProfilerMarker DamageNumberSetupMarker =
            new ProfilerMarker("Combat.Presentation.DamageNumber.Setup");
        private static readonly ProfilerMarker DamageNumberUpdateMarker =
            new ProfilerMarker("Combat.Presentation.DamageNumber.Update");
        public ObjectPool<EntityQizi> PoolEntity;
        public ObjectPool<GameObject> EmptyPool;
        private struct PendingGameObjectRequest
        {
            public GetGObjSuccessCallback Callback;
            public bool SkipRootParent;
        }
        private readonly Dictionary<string, List<PendingGameObjectRequest>> WaitAssetLoadThenGetFromPool = new();
        public Dictionary<string, ObjectPool<GameObject>> PoolDic = new();
        public delegate void GetGObjSuccessCallback(GameObject asset,string path);
        private LoadAssetCallbacks OnLoadGameObjectCallback;

        private sealed class HotPoolTracker
        {
            public readonly string Path;
            public readonly List<GameObject> Held = new List<GameObject>();
            public ProfilerCounterValue<int> Active;
            public ProfilerCounterValue<int> Idle;
            public ProfilerCounterValue<int> Total;
            public ProfilerCounterValue<int> Peak;
            public int PeakActive;
            public int TargetTotal;
            public readonly int MinimumTotal;

            public HotPoolTracker(string label, string path)
            {
                Path = path;
                MinimumTotal = label == "Portal" ? 64 : 32;
                Active = new ProfilerCounterValue<int>("Combat.Pool." + label + ".Active");
                Idle = new ProfilerCounterValue<int>("Combat.Pool." + label + ".Idle");
                Total = new ProfilerCounterValue<int>("Combat.Pool." + label + ".Total");
                Peak = new ProfilerCounterValue<int>("Combat.Pool." + label + ".Peak");
            }

            public void Sample(ObjectPool<GameObject> pool)
            {
                Active.Value = pool.CountActive - Held.Count;
                Idle.Value = pool.CountInactive;
                Total.Value = pool.CountAll;
                Peak.Value = PeakActive;
            }
        }

        private readonly Dictionary<string, HotPoolTracker> m_HotPoolTrackers =
            new Dictionary<string, HotPoolTracker>(3);
        private readonly List<HotPoolTracker> m_HotPoolPrewarmOrder =
            new List<HotPoolTracker>(3);
        private readonly List<GameObject> m_HeldEmptyObjects = new List<GameObject>();
        private int m_EmptyPoolPeakActive;
        private int m_EmptyPoolPrewarmTarget;
        private int m_NextHotPoolPrewarm;

        // Prewarming is requested during formation and runs only there. A previous
        // battle's observed peak determines the next target; the first target is small.
        public void PrepareBattlePoolPrewarm()
        {
            FinishBattlePoolPrewarm();
            CombatProjectileBridge.PreparePoolPrewarm();
            CombatPosPointBridge.PreparePoolPrewarm();
            m_HotPoolPrewarmOrder.Clear();
            var paths = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            RegisterHotPool("Portal", paths, 3303);
            RegisterHotPool("HitSfx", paths, 3301);
            RegisterHotPool("DamageNumber", DamageNumberPerfabPath);
            var bullets = GameEntry.DataTable.GetDataTable<DRBullet>("Bullet");
            if (bullets != null && paths != null)
            {
                var visualAssetIds = new HashSet<int>();
                foreach (DRBullet bullet in bullets.GetAllDataRows())
                {
                    RegisterHotPool("Bullet" + bullet.AssetPathID, paths,
                        bullet.AssetPathID);
                    if (visualAssetIds.Add(bullet.AssetPathID) &&
                        paths.HasDataRow(bullet.AssetPathID))
                    {
                        int assetId = bullet.AssetPathID;
                        GetNewObjFromPool(paths[assetId].AssetPath,
                            (source, loadedPath) => OnMeshBulletSourceReady(
                                assetId, source, loadedPath));
                    }
                }
            }
            m_EmptyPoolPrewarmTarget = System.Math.Min(256,
                System.Math.Max(64, m_EmptyPoolPeakActive +
                    System.Math.Max(8, m_EmptyPoolPeakActive / 8)));
            foreach (HotPoolTracker tracker in m_HotPoolPrewarmOrder)
            {
                int headroom = System.Math.Min(64,
                    System.Math.Max(8, tracker.PeakActive / 8));
                tracker.TargetTotal = System.Math.Min(128,
                    System.Math.Max(tracker.MinimumTotal, tracker.PeakActive + headroom));
                if (!PoolDic.ContainsKey(tracker.Path))
                {
                    GetNewObjFromPool(tracker.Path,
                        (obj, loadedPath) => ReleaseGameObject(loadedPath, obj));
                }
            }
        }

        private void OnMeshBulletSourceReady(int assetPathId, GameObject source,
            string path)
        {
            try
            {
                CombatProjectileVisualBridge.RegisterSingleMesh(assetPathId, source);
            }
            catch (System.Exception exception)
            {
                Log.Error($"ECS bullet visual {assetPathId} setup failed; using GameObject pool: {exception}");
            }
            finally
            {
                ReleaseGameObject(path, source);
            }
        }

        public void AdvanceBattlePoolPrewarm()
        {
            CombatProjectileBridge.AdvancePoolPrewarm(32);
            CombatPosPointBridge.AdvancePoolPrewarm(16);
            const int maxGetsPerFrame = 6;
            int gets = 0;
            while (gets < maxGetsPerFrame && EmptyPool != null &&
                   EmptyPool.CountInactive + m_HeldEmptyObjects.Count <
                   m_EmptyPoolPrewarmTarget)
            {
                using (PoolPrewarmMarker.Auto())
                {
                    m_HeldEmptyObjects.Add(EmptyPool.Get());
                }
                gets++;
            }
            while (gets < maxGetsPerFrame && m_HeldEmptyObjects.Count != 0)
            {
                int last = m_HeldEmptyObjects.Count - 1;
                EmptyPool.Release(m_HeldEmptyObjects[last]);
                m_HeldEmptyObjects.RemoveAt(last);
                gets++;
            }
            for (int i = gets; i < maxGetsPerFrame && m_HotPoolPrewarmOrder.Count != 0; i++)
            {
                HotPoolTracker tracker = m_HotPoolPrewarmOrder[m_NextHotPoolPrewarm];
                m_NextHotPoolPrewarm =
                    (m_NextHotPoolPrewarm + 1) % m_HotPoolPrewarmOrder.Count;
                if (PoolDic.TryGetValue(tracker.Path, out ObjectPool<GameObject> pool) &&
                    pool.CountInactive + tracker.Held.Count < tracker.TargetTotal)
                {
                    // Keep idle instances checked out so later Get calls create new ones.
                    GameObject obj;
                    using (PoolPrewarmMarker.Auto())
                    {
                        obj = pool.Get();
                    }
                    if (tracker.Path != DamageNumberPerfabPath)
                        obj.transform.SetParent(m_InstanceDisableRoot);
                    tracker.Held.Add(obj);
                }
                else if (pool != null && tracker.Held.Count != 0)
                {
                    int last = tracker.Held.Count - 1;
                    pool.Release(tracker.Held[last]);
                    tracker.Held.RemoveAt(last);
                }
            }
        }

        public bool IsBattlePoolPrewarmReady()
        {
            if (!CombatProjectileBridge.IsPoolPrewarmReady() ||
                !CombatPosPointBridge.IsPoolPrewarmReady()) return false;
            if (EmptyPool != null &&
                (EmptyPool.CountInactive < m_EmptyPoolPrewarmTarget ||
                 m_HeldEmptyObjects.Count != 0))
            {
                return false;
            }
            foreach (HotPoolTracker tracker in m_HotPoolPrewarmOrder)
            {
                if (!PoolDic.TryGetValue(tracker.Path, out ObjectPool<GameObject> pool) ||
                    pool.CountInactive < tracker.TargetTotal ||
                    tracker.Held.Count != 0)
                {
                    return false;
                }
            }
            return true;
        }

        public void FinishBattlePoolPrewarm()
        {
            if (EmptyPool != null)
            {
                foreach (GameObject obj in m_HeldEmptyObjects)
                {
                    EmptyPool.Release(obj);
                }
            }
            m_HeldEmptyObjects.Clear();
            foreach (HotPoolTracker tracker in m_HotPoolPrewarmOrder)
            {
                if (PoolDic.TryGetValue(tracker.Path, out ObjectPool<GameObject> pool))
                {
                    foreach (GameObject obj in tracker.Held)
                    {
                        pool.Release(obj);
                    }
                }
                tracker.Held.Clear();
            }
            m_NextHotPoolPrewarm = 0;
        }

        private void RegisterHotPool(string label, IDataTable<DRAssetsPath> paths,
            int assetId)
        {
            if (paths != null && paths.HasDataRow(assetId))
            {
                RegisterHotPool(label, paths[assetId].AssetPath);
            }
        }

        private void RegisterHotPool(string label, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!m_HotPoolTrackers.TryGetValue(path, out HotPoolTracker tracker))
            {
                tracker = new HotPoolTracker(label, path);
                m_HotPoolTrackers.Add(path, tracker);
            }
            if (!m_HotPoolPrewarmOrder.Contains(tracker))
            {
                m_HotPoolPrewarmOrder.Add(tracker);
            }
        }

        private void SampleHotPools()
        {
            if (EmptyPool != null)
            {
                int active = EmptyPool.CountActive - m_HeldEmptyObjects.Count;
                if (active > m_EmptyPoolPeakActive)
                {
                    m_EmptyPoolPeakActive = active;
                }
            }
            foreach (HotPoolTracker tracker in m_HotPoolPrewarmOrder)
            {
                if (PoolDic.TryGetValue(tracker.Path, out ObjectPool<GameObject> pool))
                {
                    tracker.Sample(pool);
                }
            }
        }

        private void TrackHotPoolGet(string path, ObjectPool<GameObject> pool)
        {
            if (m_HotPoolTrackers.TryGetValue(path, out HotPoolTracker tracker))
            {
                int active = pool.CountActive - tracker.Held.Count;
                if (active > tracker.PeakActive) tracker.PeakActive = active;
            }
        }
        
        [SerializeField]
        private Transform m_InstanceRoot = null;
        [SerializeField]
        private Transform m_InstanceDisableRoot = null;
        private Canvas m_InstanceWorldCanvas = null;
        public Canvas WorldCanvas => m_InstanceWorldCanvas;
        public Transform DisableRoot => m_InstanceDisableRoot;
        public Transform InstanceRoot => m_InstanceRoot;
        private void InitPool()
        {
            OnLoadGameObjectCallback = new LoadAssetCallbacks(OnLoadGameObjCallback);
            PoolEntity = new ObjectPool<EntityQizi>(() => new EntityQizi(), null, null, null, true, 10, 1000);
            EmptyPool = new ObjectPool<GameObject>(() =>
            {
                GameObject obj = new GameObject();
                obj.transform.SetParent(m_InstanceRoot);
                return obj;
            }, (g) => g.SetActive(true), (g) =>
            {
                if (g.transform.parent != m_InstanceRoot)
                {
                    g.transform.SetParent(m_InstanceRoot);
                }
                g.SetActive(false);
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
            m_EntitiesByUid.Remove(qizi.HeroUID);
            PoolEntity?.Release(qizi);
        }

        public GameObject GetNewEmptyObj(string objName = "")
        {
            var obj = EmptyPool?.Get();
            if (obj != null)
            {
                obj.name = objName;
                return obj;
            }
            return null;
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
                HotGameEntry.HeroManager.ReleaseGameObject(HpBarPerfabPath, HpBarObj);
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
            GetNewObjFromPool(hpBarPerfabPath, OnLoadOneNewHpBarPrefab, true);
        }
        private void OnLoadOneNewHpBarPrefab(GameObject obj,string path)
        {
            if (HotGameEntry.HeroManager.WaitHpBarList != null && HotGameEntry.HeroManager.WaitHpBarList.Count > 0)
            {
                var oneHpBar = HotGameEntry.HeroManager.WaitHpBarList[0];
                oneHpBar.HpBarObj = obj;
                //设置canvas
                Canvas canvas = m_InstanceWorldCanvas.GetComponent<Canvas>();
                oneHpBar.HpBarObj.transform.SetParent(canvas.transform);
                //设置pos
                oneHpBar.HpBarObj.transform.localScale = new Vector3(0.005f, 0.01f, 1);
                oneHpBar.HpBarObj.transform.rotation = HotGameEntry.HeroManager.CurAreaPoint.CameraRotation;
                //oneHpBar.HpBarObj.transform.forward = Camera.main.transform.forward;
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
            public GameObject NumberObj;
            public Text damageNumText;
            public int TextSize;
            public int ShrinkFrames;
            public float animDuration;
            public float allAnimDuration;
            public Vector3 targetPos;
            public float addPosX;
            public void Clear()
            {
                animDuration = -1;
                allAnimDuration = -2;
                num = 0;
                if (damageNumText != null && TextSize > 0)
                    damageNumText.fontSize = TextSize;
                damageNumText = null;
                TextSize = 0;
                ShrinkFrames = 0;
                HotGameEntry.HeroManager.ReleaseGameObject(DamageNumberPerfabPath, NumberObj);
                NumberObj = null;
            }
            public void UpdateMove(float elapseSeconds, float realElapseSeconds)
            {
                if (NumberObj == null)
                {
                    return;
                }
                if (ShrinkFrames < 20)
                {
                    ShrinkFrames++;
                    float scale = 0.01f *
                        (TextSize + 20f - ShrinkFrames) / (TextSize + 20f);
                    NumberObj.transform.localScale =
                        new Vector3(scale, scale, 1f);
                }

                if (animDuration<allAnimDuration)
                {
                    NumberObj.transform.position=Vector3.Lerp(targetPos,targetPos+new Vector3(addPosX/2, 0,1),animDuration/allAnimDuration);
                    animDuration += elapseSeconds;
                }
                else
                {
                    
                    HotGameEntry.HeroManager.PlayingDmgNumberList.Remove(this);
                    ReferencePool.Release(this);
                }
            }
        }


        public const string DamageNumberPerfabPath = "Assets/GameMain/Prefeb/UIPrefab/DamageNumber.prefab";
        private List<DamageNumber> WaitDmgNumberList = new List<DamageNumber>();
        private List<DamageNumber> PlayingDmgNumberList = new List<DamageNumber>();
        private readonly List<CombatDamageNumberEvent> m_DamageNumberEvents =
            new List<CombatDamageNumberEvent>(64);
        private Camera m_DamageNumberCamera;
        private void DrainDamageNumberEvents()
        {
            m_DamageNumberEvents.Clear();
            CombatBoardBridge.DrainDamageNumbers(m_DamageNumberEvents);
            foreach (CombatDamageNumberEvent damageEvent in m_DamageNumberEvents)
            {
                Unity.Mathematics.float3 position = damageEvent.Position;
                ShowDamageNum(damageEvent.Value,
                    new Vector3(position.x, position.y, position.z));
            }
            m_DamageNumberEvents.Clear();
        }

        public void ShowDamageNum(int value, Vector3 position)
        {
            using var sample = DamageNumberRequestMarker.Auto();
            var newDamageNumber = ReferencePool.Acquire<DamageNumber>();
            var numPerfabPath = DamageNumberPerfabPath;
            newDamageNumber.num = value;
            newDamageNumber.targetPos = position;
            WaitDmgNumberList.Add(newDamageNumber);
            GetNewObjFromPool(numPerfabPath, OnLoadOneNewDamageNumberPrefab, true);
        }

        private void OnLoadOneNewDamageNumberPrefab(GameObject obj,string path)
        {
            using var sample = DamageNumberSetupMarker.Auto();
            if (WaitDmgNumberList != null && WaitDmgNumberList.Count > 0)
            {
                var oneDmg = WaitDmgNumberList[0];
                oneDmg.NumberObj = obj;
                oneDmg.NumberObj.transform.localScale = new Vector3(0.01f, 0.01f, 1);
                //设置canvas
                if (oneDmg.NumberObj.transform.parent != m_InstanceWorldCanvas.transform)
                    oneDmg.NumberObj.transform.SetParent(m_InstanceWorldCanvas.transform);
                //设置pos
                if (m_DamageNumberCamera == null)
                    m_DamageNumberCamera = Camera.main;
                if (m_DamageNumberCamera != null)
                    oneDmg.NumberObj.transform.forward =
                        m_DamageNumberCamera.transform.forward;
                oneDmg.NumberObj.transform.position = oneDmg.targetPos;
                //设置 text
                oneDmg.damageNumText = oneDmg.NumberObj.GetComponent<Text>();
                oneDmg.damageNumText.text = oneDmg.num.ToString();
                oneDmg.TextSize = oneDmg.damageNumText.fontSize;
                oneDmg.damageNumText.fontSize += 20;
                oneDmg.ShrinkFrames = 0;
                oneDmg.damageNumText.color = new Color(190/255f,86/255f,11/255f); 
                //设置动画参数
                oneDmg.animDuration = 0f;
                oneDmg.allAnimDuration = 0.8f;
                oneDmg.Playing = true;
                oneDmg.addPosX = (float)Utility.Random.GetRandomNoLogic(-1.2, 1.2);
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
            using var sample = DamageNumberUpdateMarker.Auto();
            for (int i = PlayingDmgNumberList.Count - 1; i >= 0; i--)
            {
                PlayingDmgNumberList[i].UpdateMove(elapseSeconds,
                    realElapseSeconds);
            }
        }

        public void ClearAllDamageNum()
        {
            CombatBoardBridge.ClearDamageNumbers();
            m_DamageNumberEvents.Clear();
            foreach (var oneDamageNum in PlayingDmgNumberList)
            {
                ReferencePool.Release(oneDamageNum);
            }
            PlayingDmgNumberList.Clear();
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

        public void GetSfxByID(int id,GetGObjSuccessCallback callback,
            bool attachToOwner = false)
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
                    GetNewObjFromPool(assetData.AssetPath,callback,attachToOwner);
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
        public void GetNewObjFromPool(string path, GetGObjSuccessCallback callback,
            bool skipRootParent = false)
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
                TrackHotPoolGet(path, targetPool);
                // Callers that immediately attach to an owner or canvas skip the
                // intermediate root reparent, including after asynchronous loads.
                if (!skipRootParent && path != DamageNumberPerfabPath)
                {
                    obj.transform.SetParent(m_InstanceRoot);
                }
                callback?.Invoke(obj,path);
            }
            else
            {
                if (WaitAssetLoadThenGetFromPool.ContainsKey(path))
                {
                    var targetWaitCallbackList = WaitAssetLoadThenGetFromPool[path];
                    targetWaitCallbackList.Add(new PendingGameObjectRequest
                    {
                        Callback = callback,
                        SkipRootParent = skipRootParent
                    });
                }
                else
                {
                    List<PendingGameObjectRequest> targetWaitCallbackList =
                        ListPool<PendingGameObjectRequest>.Get();
                    targetWaitCallbackList.Add(new PendingGameObjectRequest
                    {
                        Callback = callback,
                        SkipRootParent = skipRootParent
                    });
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
                    var instantiateMarker = new ProfilerMarker(
                        "Combat.Pool.Instantiate." + Path.GetFileNameWithoutExtension(path));
                    if (path == DamageNumberPerfabPath)
                    {
                        PoolDic.Add(path, new ObjectPool<GameObject>(() =>
                        {
                            using (instantiateMarker.Auto())
                                return Instantiate(gObj, m_InstanceWorldCanvas.transform, false);
                        }, g => g.SetActive(true), g => g.SetActive(false), Destroy));
                    }
                    else
                    {
                        PoolDic.Add(path, new ObjectPool<GameObject>(() =>
                        {
                            using (instantiateMarker.Auto())
                                return Instantiate(gObj);
                        }, null, g =>
                        {
                            g.transform.SetParent(m_InstanceDisableRoot);
                            g.transform.rotation = Quaternion.identity;
                        }, Destroy));
                    }
                }
            }
            var targetPool = PoolDic[path];
            if (!WaitAssetLoadThenGetFromPool.ContainsKey(path)) return;
            var targetWaitCallbackList = WaitAssetLoadThenGetFromPool[path];
            if (targetWaitCallbackList.Count != 0)
            {
                foreach (PendingGameObjectRequest request in targetWaitCallbackList)
                {
                    var obj = targetPool.Get();
                    TrackHotPoolGet(path, targetPool);
                    if (!request.SkipRootParent && path != DamageNumberPerfabPath)
                    {
                        obj.transform.SetParent(m_InstanceRoot);
                    }
                    request.Callback?.Invoke(obj,path);
                }
            }
            WaitAssetLoadThenGetFromPool.Remove(path);
            ListPool<PendingGameObjectRequest>.Release(targetWaitCallbackList);

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
                    for (int i = 0; i < targetWaitList.Count; i++)
                    {
                        if (targetWaitList[i].Callback == callback)
                        {
                            targetWaitList.RemoveAt(i);
                            break;
                        }
                    }
                    if (targetWaitList.Count == 0)
                    {
                        WaitAssetLoadThenGetFromPool.Remove(path);
                        ListPool<PendingGameObjectRequest>.Release(targetWaitList);
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
