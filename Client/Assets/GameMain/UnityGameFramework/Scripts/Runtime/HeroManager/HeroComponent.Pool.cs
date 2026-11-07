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
        public Canvas WorldCanvas => m_InstanceWorldCanvas;
        public Transform DisableRoot => m_InstanceDisableRoot;
        public Transform InstanceRoot => m_InstanceRoot;
        private void InitPool()
        {
            OnLoadGameObjectCallback = new LoadAssetCallbacks(OnLoadGameObjCallback);
            PoolEntity = new ObjectPool<EntityQizi>(() => new EntityQizi(), null, null, null, true, 10, 1000);
            EmptyPool = new ObjectPool<GameObject>(() => new GameObject(), (g)=>g.transform.SetParent(m_InstanceRoot), (g) =>
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
            private const string StatusRowName = "Status Badges";
            private const int BadgesPerRow = 4;
            private static readonly Color AllyHealth = new Color(.30f, .66f, .52f);
            private static readonly Color EnemyHealth = new Color(.78f, .32f, .25f);
            private static Sprite[] statusSprites;
            private readonly long[] counts = new long[BattleStatuses.Display.Length];
            private readonly long[] displayedCounts = new long[BattleStatuses.Display.Length];
            private RectTransform statusRow;
            private float nextStatusRefresh;
            private Image healthFill;
            private int activeBadgeCount;
            internal GetGObjSuccessCallback LoadCallback;
            public GameObject HpBarObj;
            public EntityQizi Owner;
            public void Clear()
            {
                Owner = null;
                LoadCallback = null;
                HotGameEntry.HeroManager.PlayingHpBarList.Remove(this);
                if (statusRow != null) statusRow.gameObject.SetActive(false);
                statusRow = null;
                healthFill = null;
                nextStatusRefresh = 0f;
                activeBadgeCount = 0;
                System.Array.Clear(displayedCounts, 0, displayedCounts.Length);
                if (HpBarObj != null)
                {
                    HpBarObj.SetActive(false);
                    HotGameEntry.HeroManager.ReleaseGameObject(HpBarPerfabPath, HpBarObj);
                }
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

            internal void BindHealthFill()
            {
                healthFill = Owner.xuetiao.fillRect.GetComponent<Image>();
                RefreshHealthColor();
            }

            private void RefreshHealthColor()
            {
                if (healthFill == null) return;
                var color = Owner.BelongCamp == CampType.Friend ? AllyHealth : EnemyHealth;
                if (healthFill.color != color) healthFill.color = color;
            }

            internal void RefreshStatusBadges()
            {
                if (HpBarObj == null || Time.unscaledTime < nextStatusRefresh) return;
                nextStatusRefresh = Time.unscaledTime + .10f;
                RefreshHealthColor();
                BattleStatuses.Read(Owner, counts);
                int shown = 0;
                bool changed = false;
                for (int i = 0; i < counts.Length; i++)
                {
                    if (counts[i] > 0) shown++;
                    if (counts[i] != displayedCounts[i]) changed = true;
                }
                if (!changed) return;
                if (shown == 0)
                {
                    statusRow.gameObject.SetActive(false);
                    activeBadgeCount = 0;
                    System.Array.Clear(displayedCounts, 0, displayedCounts.Length);
                    return;
                }
                if (statusRow == null)
                    statusRow = HpBarObj.transform.Find(StatusRowName) as RectTransform ?? CreateStatusRow();
                if (statusRow == null) return;
                statusRow.gameObject.SetActive(true);
                activeBadgeCount = shown;
                int slot = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    var badge = (RectTransform)statusRow.GetChild(i);
                    badge.gameObject.SetActive(counts[i] > 0);
                    displayedCounts[i] = counts[i];
                    if (counts[i] <= 0) continue;
                    int row = slot / BadgesPerRow;
                    int rowCount = Mathf.Min(shown - row * BadgesPerRow, BadgesPerRow);
                    badge.anchoredPosition = new Vector2((slot % BadgesPerRow - (rowCount - 1) * .5f) * 47f, row * 24f);
                    slot++;
                    badge.GetChild(0).GetComponent<Text>().text = counts[i] > 99 ? "99+" : counts[i].ToString();
                }
            }

            internal void SeparateOnScreen(Camera camera, List<Rect> occupied)
            {
                if (HpBarObj == null) return;
                var bar = (RectTransform)HpBarObj.transform;
                var screen = camera.WorldToScreenPoint(bar.position);
                if (screen.z <= 0f) return;
                bar.rotation = camera.transform.rotation;
                Rect local = bar.rect;
                if (activeBadgeCount > 0)
                {
                    float badgeHalfWidth = (Mathf.Min(activeBadgeCount, BadgesPerRow) - 1) * 23.5f + 22f;
                    local.xMin = Mathf.Min(local.xMin, -badgeHalfWidth);
                    local.xMax = Mathf.Max(local.xMax, badgeHalfWidth);
                    local.yMax = 36f + (activeBadgeCount - 1) / BadgesPerRow * 24f;
                }
                Vector3 min = camera.WorldToScreenPoint(bar.TransformPoint(local.min));
                Vector3 max = camera.WorldToScreenPoint(bar.TransformPoint(local.max));
                var bounds = Rect.MinMaxRect(min.x - 2f, min.y - 2f, max.x + 2f, max.y + 2f);
                float lift = 0f;
                for (int i = 0; i < occupied.Count; i++)
                {
                    if (!bounds.Overlaps(occupied[i])) continue;
                    float step = occupied[i].yMax - bounds.yMin + .25f;
                    lift += step;
                    bounds.y += step;
                    i = -1;
                }
                if (lift > 0f)
                {
                    screen.y += lift;
                    bar.position = camera.ScreenToWorldPoint(screen);
                }
                occupied.Add(bounds);
            }

            private RectTransform CreateStatusRow()
            {
                if (statusSprites == null)
                {
                    var atlas = Resources.Load<Texture2D>("BattleStatusBadges");
                    if (atlas == null) return null;
                    int size = atlas.width / counts.Length;
                    statusSprites = new Sprite[counts.Length];
                    for (int i = 0; i < statusSprites.Length; i++)
                        statusSprites[i] = Sprite.Create(atlas, new Rect(i * size, 0, size, atlas.height), new Vector2(.5f, .5f));
                }
                var row = NewRect(StatusRowName, HpBarObj.transform);
                row.anchoredPosition = new Vector2(0f, 25f);
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                for (int i = 0; i < statusSprites.Length; i++)
                {
                    var badge = NewRect(BattleStatuses.Display[i].English, row);
                    badge.sizeDelta = new Vector2(44f, 22f);
                    var image = badge.gameObject.AddComponent<Image>();
                    image.sprite = statusSprites[i];
                    image.raycastTarget = false;
                    var number = NewRect("Stacks", badge);
                    number.sizeDelta = new Vector2(24f, 11f);
                    number.localScale = new Vector3(2f, 1f, 1f);
                    number.anchoredPosition = new Vector2(0f, -4.5f);
                    var label = number.gameObject.AddComponent<Text>();
                    label.font = font;
                    label.fontSize = 10;
                    label.fontStyle = FontStyle.Bold;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    label.raycastTarget = false;
                    label.color = new Color(.98f, .94f, .84f);
                }
                return row;
            }

            private static RectTransform NewRect(string name, Transform parent)
            {
                var obj = new GameObject(name, typeof(RectTransform));
                obj.layer = parent.gameObject.layer;
                var rect = (RectTransform)obj.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                return rect;
            }
        }
        public const string HpBarPerfabPath = "Assets/GameMain/Prefeb/UIPrefab/xuetiao_qizi.prefab";
        public List<HpBar> PlayingHpBarList = new List<HpBar>();
        
        public void AddHpBar(EntityQizi qizi)
        {
            if (qizi.HpBar != null) return;
            var newHpBar = ReferencePool.Acquire<HpBar>();
            newHpBar.Owner = qizi;
            qizi.HpBar = newHpBar;
            GetGObjSuccessCallback load = null;
            load = (obj, path) =>
            {
                // The same pooled HpBar can already belong to a new request when this load returns.
                if (!ReferenceEquals(newHpBar.LoadCallback, load))
                {
                    obj.SetActive(false);
                    ReleaseGameObject(path, obj);
                    return;
                }
                newHpBar.LoadCallback = null;
                OnLoadOneNewHpBarPrefab(newHpBar, obj);
            };
            newHpBar.LoadCallback = load;
            GetNewObjFromPool(HpBarPerfabPath, load);
        }
        private void OnLoadOneNewHpBarPrefab(HpBar oneHpBar, GameObject obj)
        {
            oneHpBar.HpBarObj = obj;
            //设置canvas
            Canvas canvas = m_InstanceWorldCanvas.GetComponent<Canvas>();
            oneHpBar.HpBarObj.transform.SetParent(canvas.transform);
            //设置pos
            oneHpBar.HpBarObj.transform.localScale = new Vector3(0.005f, 0.01f, 1);
            oneHpBar.HpBarObj.transform.rotation = IsTimelineBattle
                ? m_InstanceWorldCanvas.worldCamera.transform.rotation
                : HotGameEntry.HeroManager.CurAreaPoint.CameraRotation;
            //oneHpBar.HpBarObj.transform.forward = Camera.main.transform.forward;
            oneHpBar.HpBarObj.transform.position = oneHpBar.Owner.LogicPosition+Vector3.up*2;
            //绑定棋子血条
            oneHpBar.Owner.xuetiao=obj.transform.Find("xuetiao").GetComponent<Slider>();
            oneHpBar.Owner.WindupProgress = obj.transform.Find("WindupProgress").GetComponent<Slider>();
            oneHpBar.Owner.ShowWindupProgress(-1f);
            oneHpBar.Owner.hudun=obj.transform.GetChild(1).GetChild(1).GetChild(0).Find("hudun").GetComponent<Slider>();
            oneHpBar.BindHealthFill();
            //设置动画回调
            PlayingHpBarList.Add(oneHpBar);
            obj.SetActive(true);
        }
        private void UpdateHpBar()
        {
            var runList = ListPool<HpBar>.Get();
            runList.AddRange(PlayingHpBarList);
            var camera = IsTimelineBattle && !IsTimelineSimulation ? m_InstanceWorldCanvas.worldCamera : null;
            var occupied = camera != null ? ListPool<Rect>.Get() : null;
            foreach (var oneHpBar in runList)
            {
                oneHpBar.UpdataMoveHpBar();
                oneHpBar.RefreshStatusBadges();
                if (occupied != null) oneHpBar.SeparateOnScreen(camera, occupied);
            }
            if (occupied != null) ListPool<Rect>.Release(occupied);
            ListPool<HpBar>.Release(runList);
        }
        
        public class DamageNumber:IReference
        {
            public int num;
            public bool Playing = false;
            public EntityQizi Owner;
            public GameObject NumberObj;
            public Text damageNumText;
            public int TextSize;
            public float animDuration;
            public float allAnimDuration;
            public Vector3 targetPos;
            public float addPosX;
            public void Clear()
            {
                animDuration = -1;
                allAnimDuration = -2;
                num = 0;
                TextSize = 0;
                Owner = null;
                HotGameEntry.HeroManager.ReleaseGameObject(DamageNumberPerfabPath, NumberObj);
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
        public void ShowDamageNum(CauseDamageData data)
        {
            if (IsTimelineSimulation) return;
            var newDamageNumber = ReferencePool.Acquire<DamageNumber>();
            var numPerfabPath = DamageNumberPerfabPath;
            newDamageNumber.num = (int)data.DamageValue;
            newDamageNumber.Owner = data.Target;
            newDamageNumber.targetPos = data.Target.LogicPosition + Vector3.up*0.5f;
            WaitDmgNumberList.Add(newDamageNumber);
            GetNewObjFromPool(numPerfabPath, OnLoadOneNewDamageNumberPrefab);
        }

        private void OnLoadOneNewDamageNumberPrefab(GameObject obj,string path)
        {
            if (WaitDmgNumberList != null && WaitDmgNumberList.Count > 0)
            {
                var oneDmg = WaitDmgNumberList[0];
                oneDmg.NumberObj = obj;
                oneDmg.NumberObj.transform.localScale = new Vector3(0.01f, 0.01f, 1);
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
            var runList = ListPool<DamageNumber>.Get();
            runList.AddRange(PlayingDmgNumberList);
            foreach (var oneDamage in runList)
            {
                oneDamage.UpdateMove(elapseSeconds,realElapseSeconds);
            }
            ListPool<DamageNumber>.Release(runList);
        }

        public void ClearAllDamageNum()
        {
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
        public void GetNewObjFromPool(string path, GetGObjSuccessCallback callback)
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
