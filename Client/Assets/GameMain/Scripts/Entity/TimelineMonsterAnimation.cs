using UnityEngine;

namespace Entity
{
    public sealed class TimelineMonsterAnimation : MonoBehaviour
    {
        public AnimationClip Idle;
        public AnimationClip Move;
        public AnimationClip Windup;
        public AnimationClip Strike;

        private AnimationClip playing;
        private float duration;
        private float elapsed;

        public void Play(AnimationClip clip, float durationMs = 0f)
        {
            if (playing == clip && clip.isLooping) return;
            playing = clip;
            duration = durationMs > 0f ? durationMs / 1000f : clip.length;
            elapsed = 0f;
            clip.SampleAnimation(gameObject, 0f);
        }

        public void Advance(float seconds)
        {
            if (playing == null || elapsed >= duration) return;
            elapsed = playing.isLooping
                ? Mathf.Repeat(elapsed + seconds, duration)
                : Mathf.Min(elapsed + seconds, duration);
            playing.SampleAnimation(gameObject, elapsed / duration * playing.length);
        }
    }
}
