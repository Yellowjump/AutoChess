using System.Collections.Generic;
using DataTable;
using GameFramework.Resource;
using UnityEngine;
using UnityEngine.Pool;

namespace UnityGameFramework.Runtime
{
    public partial class HeroComponent
    {
        private Dictionary<string,int> _waitLoadAnimList = new Dictionary<string,int>();
        private Dictionary<string,AnimationClip> _curAnimationList = new Dictionary<string,AnimationClip>();
        private LoadAssetCallbacks _loadAssetCallbacks;

        private void InitAnimationLoadCallback()
        {
            _loadAssetCallbacks = new LoadAssetCallbacks(OnLoadAnimClipObjCallback);
        }
        public void LoadAnimationClip(int assetID)
        {
            var assetsPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (assetsPathTable.HasDataRow(assetID))
            {
                var assetPath = assetsPathTable[assetID].AssetPath;
                if (_waitLoadAnimList.ContainsKey(assetPath))
                {
                    _waitLoadAnimList[assetPath]++;
                }
                else
                {
                    _waitLoadAnimList.Add(assetPath,1);
                    GameEntry.Resource.LoadAsset(assetPath,_loadAssetCallbacks);
                }
            }
        }
        private void OnLoadAnimClipObjCallback(string path, object asset, float duration, object userData)
        {
            if (!_waitLoadAnimList.ContainsKey(path))
            {
                GameEntry.Resource.UnloadAsset(asset);
                return;
            }
            if (asset is AnimationClip clip)
            {
                _curAnimationList.Add(path,clip);
            }
            else
            {
                _waitLoadAnimList.Remove(path);
                GameEntry.Resource.UnloadAsset(asset);
            }
        }
        public AnimationClip GetAnimClipByPath(string animPath)
        {
            if (_curAnimationList.ContainsKey(animPath))
            {
                return _curAnimationList[animPath];
            }
            Log.Error($"No AnimationClip Path:{animPath}");
            return null;
        }

        public void ReleaseAnimNeed(int assetID)
        {
            var assetsPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (assetsPathTable.HasDataRow(assetID))
            {
                var assetPath = assetsPathTable[assetID].AssetPath;
                if (_waitLoadAnimList.ContainsKey(assetPath))
                {
                    _waitLoadAnimList[assetPath]--;
                }
                else
                {
                    Log.Error($"Now Not Load Animation Clip AssetID:{assetID} name:{assetPath}");
                }
            }
        }

        public void UnloadUnusedAnimation()
        {
            var keysToRemove = ListPool<string>.Get();
            foreach (var kvp in _waitLoadAnimList)
            {
                if (kvp.Value <= 0)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                if (_curAnimationList.TryGetValue(key, out var animClip))
                {
                    GameEntry.Resource.UnloadAsset(animClip);
                    _curAnimationList.Remove(key);
                }
                _waitLoadAnimList.Remove(key);
            }
            ListPool<string>.Release(keysToRemove);
        }
    }
}