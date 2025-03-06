using System.Collections.Generic;
using Animancer;
using DataTable;
using GameFramework;
using GameFramework.Fsm;
using GameFramework.Resource;
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
            if (_animancer != null && _waitPlayAni!=null&&CurAnimationList.ContainsKey(_waitPlayAni.AnimAssetPath))
            {
                var anim = CurAnimationList[_waitPlayAni.AnimAssetPath];
                var state = _animancer.Play(anim,0.25f);
                state.Speed = _waitPlayAni.Speed;
                state.Time = 0f;
                ReferencePool.Release(_waitPlayAni);
                _waitPlayAni = null;
            }
            /*if (animator!=null&&!string.IsNullOrEmpty(_waitPlayAni))
            {
                animator.CrossFade(_waitPlayAni,0.2f);
                _waitPlayAni = string.Empty;
            }*/
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
            if (_waitPlayAni != null)
            {
                ReferencePool.Release(_waitPlayAni);
                _waitPlayAni = null;
            }
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