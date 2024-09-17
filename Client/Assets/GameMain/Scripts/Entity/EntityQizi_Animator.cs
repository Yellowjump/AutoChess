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
        private List<string> _waitLoadAnimList = new List<string>();
        private Dictionary<string,AnimationClip> CurAnimationList = new Dictionary<string,AnimationClip>();
        private void InitAnimation()
        {
            _animancer = GObj.GetComponent<AnimancerComponent>();
            if (_animancer == null)
            {
                _animancer = GObj.AddComponent<AnimancerComponent>();
            }
        }

        private void InitAnimationClip()
        {
            var tableSkill = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
            foreach (var oneAnimSkill in NormalSkillList)
            {
                if (tableSkill.HasDataRow(oneAnimSkill.SkillID))
                {
                    var skillData = tableSkill[oneAnimSkill.SkillID];
                    _waitLoadAnimList.Add(skillData.SkillAnim);
                    
                }
            }
            _waitLoadAnimList.Add("Assets/GameMain/Prefeb/Charactor/enemy0/Z_Run.anim");
            _waitLoadAnimList.Add("Assets/GameMain/Prefeb/Charactor/enemy0/Z_Idle.anim");
            foreach (var animPath in _waitLoadAnimList)
            {
                GameEntry.Resource.LoadAsset(animPath,new LoadAssetCallbacks(OnLoadAnimClipObjCallback));
            }
        }

        private void OnLoadAnimClipObjCallback(string path, object asset, float duration, object userData)
        {
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
                _animancer.Play(anim);
            }
            /*if (animator!=null&&!string.IsNullOrEmpty(_waitPlayAni))
            {
                animator.CrossFade(_waitPlayAni,0.2f);
                _waitPlayAni = string.Empty;
            }*/
        }

        public void AddAnimCommand(string aniName)
        {
            _waitPlayAni = aniName;
        }
    }
}