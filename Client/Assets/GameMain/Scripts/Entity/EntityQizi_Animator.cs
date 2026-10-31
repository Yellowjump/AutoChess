using Animancer;
using DataTable;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

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
                HotGameEntry.HeroManager.LoadAnimationClip(heroData.IdleAnimID);
                HotGameEntry.HeroManager.LoadAnimationClip(heroData.RunAnimID);
                HotGameEntry.HeroManager.LoadAnimationClip(heroData.WinAnimID);
                HotGameEntry.HeroManager.LoadAnimationClip(heroData.LoseAnimID);
            }
        }
        private void UpdateAnimCommand()
        {
            if (_animancer == null) return;
            if (m_CombatUnitHandle != 0 &&
                CombatAttributeBridge.TryConsumeAnimation(m_CombatUnitHandle,
                    out int assetId, out float speed))
            {
                var assets = GameEntry.DataTable.GetDataTable<DRAssetsPath>(
                    "AssetsPath");
                if (assets != null && assets.HasDataRow(assetId))
                    PlayAnimation(assets[assetId].AssetPath, speed);
            }
            else if (_waitPlayAni != null)
            {
                PlayAnimation(_waitPlayAni.AnimAssetPath,
                    _waitPlayAni.Speed);
                ReferencePool.Release(_waitPlayAni);
                _waitPlayAni = null;
            }
        }

        private void PlayAnimation(string assetPath, float speed)
        {
            var anim = HotGameEntry.HeroManager.GetAnimClipByPath(assetPath);
            if (anim == null) return;
            var state = _animancer.Play(anim, 0.25f);
            state.Speed = speed;
            if (!anim.isLooping) state.Time = 0f;
        }

        public void AddAnimCommand(int aniAssetID,float speed=1)
        {
            var assetsPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (assetsPathTable.HasDataRow(aniAssetID))
            {
                if (HotGameEntry.HeroManager.InBattle && m_CombatUnitHandle != 0)
                {
                    if (_waitPlayAni != null)
                    {
                        ReferencePool.Release(_waitPlayAni);
                        _waitPlayAni = null;
                    }
                    CombatAttributeBridge.QueueAnimation(m_CombatUnitHandle,
                        aniAssetID, speed);
                }
                else
                {
                    if (_waitPlayAni != null)
                        ReferencePool.Release(_waitPlayAni);
                    _waitPlayAni = ReferencePool.Acquire<WaitPlayAnimData>();
                    _waitPlayAni.AnimAssetPath =
                        assetsPathTable[aniAssetID].AssetPath;
                    _waitPlayAni.Speed = speed;
                }
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
        public void AddAnimCommandWin()
        {
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                AddAnimCommand(heroData.WinAnimID);
            }
        }
        public void AddAnimCommandLose()
        {
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                AddAnimCommand(heroData.LoseAnimID);
            }
        }
        private void ReleaseAnim()
        {
            if (_waitPlayAni != null)
            {
                ReferencePool.Release(_waitPlayAni);
                _waitPlayAni = null;
            }
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                HotGameEntry.HeroManager.ReleaseAnimNeed(heroData.IdleAnimID);
                HotGameEntry.HeroManager.ReleaseAnimNeed(heroData.RunAnimID);
                HotGameEntry.HeroManager.ReleaseAnimNeed(heroData.WinAnimID);
                HotGameEntry.HeroManager.ReleaseAnimNeed(heroData.LoseAnimID);
            }
        }
    }
}
