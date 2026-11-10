using Animancer;
using DataTable;
using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    public class WaitPlayAnimData:IReference
    {
        public string AnimAssetPath;
        public float Speed;
        public bool TraceSlash;
        public void Clear()
        {
            AnimAssetPath = string.Empty;
            Speed = 1;
            TraceSlash = false;
        }
    }
    public partial class EntityQizi
    {
        private const int StoryIdleAnimationId = 6011;
        private const int StoryRunAnimationId = 6012;
        private int idleAnimationId;
        private int runAnimationId;
        private WaitPlayAnimData _waitPlayAni = null;
        private AnimancerComponent _animancer;
        private TimelineMonsterAnimation monsterAnimation;
        private void InitAnimation()
        {
            monsterAnimation = GObj.GetComponent<TimelineMonsterAnimation>();
            if (monsterAnimation != null)
            {
                _animancer = null;
                if (_waitPlayAni != null)
                {
                    ReferencePool.Release(_waitPlayAni);
                    _waitPlayAni = null;
                }
                monsterAnimation.Play(monsterAnimation.Idle);
                return;
            }
            _animancer = GObj.GetComponent<AnimancerComponent>();
            if (_animancer == null)
            {
                _animancer = GObj.AddComponent<AnimancerComponent>();
                _animancer.Animator = GObj.GetComponent<Animator>();
            }
        }

        internal void PlayMonsterWindup(int durationMs, bool lineSlam)
        {
            if (monsterAnimation != null && durationMs > 0)
                monsterAnimation.Play(lineSlam && monsterAnimation.LineWindup != null
                    ? monsterAnimation.LineWindup : monsterAnimation.Windup, durationMs);
        }

        internal void PlayMonsterStrike(int recoveryMs, bool lineSlam)
        {
            if (monsterAnimation != null)
                monsterAnimation.Play(lineSlam && monsterAnimation.LineRecovery != null
                    ? monsterAnimation.LineRecovery : monsterAnimation.Strike, Mathf.Max(1, recoveryMs));
        }

        private void InitAnimationClip()
        {
            if (HotGameEntry.HeroManager.IsTimelineSimulation) return;
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                bool storyRig = !string.IsNullOrEmpty(TimelineAppearance);
                idleAnimationId = storyRig ? StoryIdleAnimationId : heroData.IdleAnimID;
                runAnimationId = storyRig ? StoryRunAnimationId : heroData.RunAnimID;
                HotGameEntry.HeroManager.LoadAnimationClip(idleAnimationId);
                HotGameEntry.HeroManager.LoadAnimationClip(runAnimationId);
                HotGameEntry.HeroManager.LoadAnimationClip(heroData.WinAnimID);
                HotGameEntry.HeroManager.LoadAnimationClip(heroData.LoseAnimID);
            }
        }
        private void UpdateAnimCommand()
        {
            if (_animancer != null && _waitPlayAni!=null)
            {
                if (HotGameEntry.HeroManager.IsAnimationClipLoading(_waitPlayAni.AnimAssetPath)) return;
                var anim = HotGameEntry.HeroManager.GetAnimClipByPath(_waitPlayAni.AnimAssetPath);
                if (anim != null)
                {
                    var state = _animancer.Play(anim,0.25f);
                    state.Speed = _waitPlayAni.Speed;
                    if (!anim.isLooping)
                    {
                        state.Time = 0f;
                    }
                    if (_waitPlayAni.TraceSlash)
                    {
                        var hand = GetWeaponHandle(WeaponHandleType.RightHand);
                        if (hand == null && _animancer.Animator != null && _animancer.Animator.isHuman)
                            hand = _animancer.Animator.GetBoneTransform(HumanBodyBones.RightHand);
                        if (hand != null)
                            HotGameEntry.HeroManager.BurnVisuals?.Slash?.Swing(hand, GObj.transform, state);
                    }
                }
                ReferencePool.Release(_waitPlayAni);
                _waitPlayAni = null;
            }
        }

        public void AddAnimCommand(int aniAssetID, float speed = 1f, bool traceSlash = false)
        {
            if (HotGameEntry.HeroManager.IsTimelineSimulation || monsterAnimation != null) return;
            var assetsPathTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (assetsPathTable.HasDataRow(aniAssetID))
            {
                _waitPlayAni ??= ReferencePool.Acquire<WaitPlayAnimData>();
                _waitPlayAni.AnimAssetPath =assetsPathTable[aniAssetID].AssetPath;
                _waitPlayAni.Speed = speed;
                _waitPlayAni.TraceSlash = traceSlash;
            }
        }
        public void AddAnimCommandIdle()
        {
            if (monsterAnimation != null)
            {
                monsterAnimation.Play(monsterAnimation.Idle);
                return;
            }
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                AddAnimCommand(idleAnimationId);
            }
        }
        public void AddAnimCommandRun()
        {
            if (monsterAnimation != null)
            {
                monsterAnimation.Play(monsterAnimation.Move);
                return;
            }
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                AddAnimCommand(runAnimationId);
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
            monsterAnimation = null;
            if (HotGameEntry.HeroManager.IsTimelineSimulation) return;
            if (_waitPlayAni != null)
            {
                ReferencePool.Release(_waitPlayAni);
                _waitPlayAni = null;
            }
            var tableHero = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (tableHero.HasDataRow(HeroID))
            {
                var heroData = tableHero[HeroID];
                HotGameEntry.HeroManager.ReleaseAnimNeed(idleAnimationId);
                HotGameEntry.HeroManager.ReleaseAnimNeed(runAnimationId);
                HotGameEntry.HeroManager.ReleaseAnimNeed(heroData.WinAnimID);
                HotGameEntry.HeroManager.ReleaseAnimNeed(heroData.LoseAnimID);
            }
        }
    }
}
