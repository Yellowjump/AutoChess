using Animancer;
using DataTable;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    public class WaitPlayAnimData:IReference
    {
        public string AnimAssetPath;
        public float Speed;
        public void Clear()
        {
            AnimAssetPath = string.Empty;
            Speed = 1;
        }
    }
    public partial class EntityQizi
    {
        private WaitPlayAnimData _waitPlayAni = null;
        private AnimancerComponent _animancer;
        private void InitAnimation()
        {
            _animancer = GObj.GetComponent<AnimancerComponent>();
            if (_animancer == null)
            {
                _animancer = GObj.AddComponent<AnimancerComponent>();
                _animancer.Animator = GObj.GetComponent<Animator>();
            }
        }

        private void InitAnimationClip()
        {
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                GameEntry.HeroManager.LoadAnimationClip(heroData.IdleAnimID);
                GameEntry.HeroManager.LoadAnimationClip(heroData.RunAnimID);
            }
        }
        private void UpdateAnimCommand()
        {
            if (_animancer != null && _waitPlayAni!=null)
            {
                var anim = GameEntry.HeroManager.GetAnimClipByPath(_waitPlayAni.AnimAssetPath);
                if (anim != null)
                {
                    var state = _animancer.Play(anim,0.25f);
                    state.Speed = _waitPlayAni.Speed;
                    state.Time = 0f;
                }
                ReferencePool.Release(_waitPlayAni);
                _waitPlayAni = null;
            }
        }

        public void AddAnimCommand(int aniAssetID,float speed=1)
        {
            var assetsPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (assetsPathTable.HasDataRow(aniAssetID))
            {
                _waitPlayAni = ReferencePool.Acquire<WaitPlayAnimData>();
                _waitPlayAni.AnimAssetPath =assetsPathTable[aniAssetID].AssetPath;
                _waitPlayAni.Speed = speed;
            }
        }
        public void AddAnimCommandIdle()
        {
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                AddAnimCommand(heroData.IdleAnimID);
            }
        }
        public void AddAnimCommandRun()
        {
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                AddAnimCommand(heroData.RunAnimID);
            }
        }

        private void ReleaseAnim()
        {
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                GameEntry.HeroManager.ReleaseAnimNeed(heroData.IdleAnimID);
                GameEntry.HeroManager.ReleaseAnimNeed(heroData.RunAnimID);
            }
        }
    }
}