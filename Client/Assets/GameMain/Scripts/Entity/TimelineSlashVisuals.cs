using System.Collections;
using Animancer;
using UnityEngine;
using UnityEngine.Rendering;

namespace Entity
{
    public sealed class TimelineSlashVisuals : MonoBehaviour
    {
        internal const int BasicSlashSkillId = 1;
        internal const int LongSlashSkillId = 1001;

        [SerializeField] private Color arcColor = new Color(.94f, .91f, .79f);
        [SerializeField] private Color sparkColor = new Color(1f, .62f, .24f);
        [SerializeField, Min(.05f)] private float arcDuration = .19f;
        [SerializeField, Min(0f)] private float swingStartTime = .33f;
        [SerializeField, Min(0f)] private float swingEndTime = .52f;
        [SerializeField, Min(0f)] private float bladeReach = .28f;
        [SerializeField, Min(.01f)] private float arcWidth = .20f;
        [SerializeField, Range(1, 24)] private int sparkCount = 9;

        private ParticleSystem sparks;
        private Material material;
        private TimelineCombatVfx combatVfx;
        internal bool UseTidegateInk { get; set; }

        internal void Initialize(Material strokeMaterial, TimelineCombatVfx energy)
        {
            material = strokeMaterial;
            combatVfx = energy;
            sparks = gameObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = 128;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = .65f;
            var emission = sparks.emission;
            emission.enabled = false;
            var shape = sparks.shape;
            shape.enabled = false;
            var tint = sparks.colorOverLifetime;
            tint.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .35f), new GradientAlphaKey(0f, 1f) });
            tint.color = fade;
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.45f;
            renderer.velocityScale = .1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            sparks.Play();
        }

        internal void Swing(Transform hand, Transform actor, AnimancerState animation)
        {
            var trail = new GameObject("挥砍刀光").AddComponent<TrailRenderer>();
            trail.transform.SetParent(transform, false);
            trail.sharedMaterial = material;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.time = UseTidegateInk ? .11f : arcDuration;
            trail.minVertexDistance = .035f;
            trail.widthMultiplier = UseTidegateInk ? arcWidth * .65f : arcWidth;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            trail.startColor = arcColor;
            trail.endColor = new Color(arcColor.r, arcColor.g, arcColor.b, 0f);
            trail.emitting = false;
            StartCoroutine(TraceSwing(trail, hand, actor, animation));
        }

        private IEnumerator TraceSwing(TrailRenderer trail, Transform hand, Transform actor, AnimancerState animation)
        {
            var afterPose = new WaitForEndOfFrame();
            yield return afterPose;
            while (hand != null && hand.gameObject.activeInHierarchy && actor != null &&
                   animation.IsValid() && animation.IsPlaying && animation.Time < swingEndTime)
            {
                if (animation.Time >= swingStartTime)
                {
                    // Some rigs swing without a spawned weapon, so extend from the grip.
                    Vector3 reach = hand.position - (actor.position + Vector3.up * .8f);
                    trail.transform.position = hand.position + reach.normalized * bladeReach;
                    trail.emitting = true;
                }
                yield return afterPose;
            }
            trail.emitting = false;
            if (UseTidegateInk)
            {
                Destroy(trail.gameObject, trail.time);
                yield break;
            }
            trail.Clear();
            Destroy(trail.gameObject);
        }

        internal void Impact(Vector3 position, Vector3 direction)
        {
            combatVfx?.SlashImpact(position, direction);
            if (UseTidegateInk && combatVfx != null) return;
            direction.y = 0f;
            direction.Normalize();
            sparks.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                startColor = new Color(1f, .9f, .65f),
                startLifetime = .07f,
                startSize = .14f
            }, 1);
            for (int i = 0; i < sparkCount; i++)
            {
                float fan = Mathf.Lerp(-74f, 74f, (i + .5f) / sparkCount);
                Vector3 velocity = Quaternion.AngleAxis(fan, Vector3.up) * direction * (1.45f + i % 3 * .6f);
                velocity.y = .8f + i % 4 * .42f;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = velocity,
                    startColor = Color.Lerp(sparkColor, Color.white, i % 3 * .19f),
                    startLifetime = .15f + i % 4 * .035f,
                    startSize = .045f + i % 3 * .02f
                }, 1);
            }
        }

        internal void StatusBurst(Vector3 position, Color color, float lift, float size = .063f, Vector3 drift = default)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                var velocity = drift + new Vector3(Mathf.Cos(angle) * .8f, lift + i % 3 * .35f, Mathf.Sin(angle) * .8f);
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = velocity,
                    startColor = Color.Lerp(color, Color.white, i % 3 * .10f),
                    startLifetime = .22f + i % 3 * .045f,
                    startSize = size + i % 2 * .026f
                }, 1);
            }
        }
    }
}
