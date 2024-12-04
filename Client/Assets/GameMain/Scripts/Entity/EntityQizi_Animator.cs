using System.Collections.Generic;
using Animancer;
using DataTable;
using GameFramework.Fsm;
using GameFramework.Resource;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    public partial class EntityQizi
    {
        private string _waitPlayAni = string.Empty;
        private AnimancerComponent _animancer;
        private HashSet<string> _waitLoadAnimList = new HashSet<string>();
        private Dictionary<string,AnimationClip> CurAnimationList = new Dictionary<string,AnimationClip>();
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
            var tableSkill = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
            var assetsPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            foreach (var oneAnimSkill in NormalSkillList)
            {
                if (tableSkill.HasDataRow(oneAnimSkill.SkillID))
                {
                    var skillData = tableSkill[oneAnimSkill.SkillID];
                    if (assetsPathTable.HasDataRow(skillData.SkillAnim))
                    {
                        _waitLoadAnimList.Add(assetsPathTable[skillData.SkillAnim].AssetPath);
                    }
                }
            }
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                if (assetsPathTable.HasDataRow(heroData.IdleAnimID))
                {
                    _waitLoadAnimList.Add(assetsPathTable[heroData.IdleAnimID].AssetPath);
                }
                if (assetsPathTable.HasDataRow(heroData.RunAnimID))
                {
                    _waitLoadAnimList.Add(assetsPathTable[heroData.RunAnimID].AssetPath);
                }
            }
            foreach (var animPath in _waitLoadAnimList)
            {
                GameEntry.Resource.LoadAsset(animPath,new LoadAssetCallbacks(OnLoadAnimClipObjCallback));
            }
        }

        private void OnLoadAnimClipObjCallback(string path, object asset, float duration, object userData)
        {
            if (!_waitLoadAnimList.Contains(path))
            {
                GameEntry.Resource.UnloadAsset(asset);
                return;
            }
            _waitLoadAnimList.Remove(path);
            if (asset is AnimationClip clip)
            {
                CurAnimationList.Add(path,clip);
            }
        }
        private void UpdateAnimCommand()
        {
            if (_animancer != null && !string.IsNullOrEmpty(_waitPlayAni)&&CurAnimationList.ContainsKey(_waitPlayAni))
            {
                var anim = CurAnimationList[_waitPlayAni];
                _animancer.Play(anim,0.25f);
            }
            /*if (animator!=null&&!string.IsNullOrEmpty(_waitPlayAni))
            {
                animator.CrossFade(_waitPlayAni,0.2f);
                _waitPlayAni = string.Empty;
            }*/
        }

        public void AddAnimCommand(int aniAssetID)
        {
            var assetsPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (assetsPathTable.HasDataRow(aniAssetID))
            {
                _waitPlayAni =assetsPathTable[aniAssetID].AssetPath;
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
            _waitLoadAnimList.Clear();
            foreach (var keyValue in CurAnimationList)
            {
                GameEntry.Resource.UnloadAsset(keyValue.Value);
            }
            CurAnimationList.Clear();
            _animancer.Stop();
        }
    }
}