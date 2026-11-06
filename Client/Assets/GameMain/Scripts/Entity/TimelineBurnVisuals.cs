using System.Collections.Generic;
using Cinemachine;
using DataTable;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    // Owned by the foreground battle camera, so all effects leave with the battle.
    public sealed class TimelineBurnVisuals : MonoBehaviour
    {
        private const int ImpulseChannel = 128;
        private const int Segments = 48;
        private static readonly Color Ember = new Color(.83f, .15f, .045f);
        private static readonly Color Flame = new Color(1f, .57f, .17f);
        private readonly List<Stroke> strokes = new List<Stroke>();
        private Material material;
        private CinemachineImpulseDefinition impulse;

        private sealed class Stroke
        {
            public LineRenderer Line;
            public Vector3 Origin;
            public Vector3 Destination;
            public float Radius;
            public float Duration;
            public float Age;
            public Color Color;
        }

        private void Awake()
        {
            var shader = Resources.Load<Shader>("TimelineBurnStroke");
            if (shader != null && shader.isSupported) material = new Material(shader);
            var listener = gameObject.AddComponent<CinemachineImpulseListener>();
            listener.m_ChannelMask = ImpulseChannel;
            listener.m_Gain = 1f;
            impulse = new CinemachineImpulseDefinition
            {
                m_ImpulseChannel = ImpulseChannel,
                m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
                m_ImpulseDuration = .19f,
                m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform
            };
        }

        public void Detonate(Vector3 position, float radius)
        {
            Ring(position, radius, .48f, .15f, Ember);
            Ring(position, radius * .89f, .34f, .065f, Flame);
            HotGameEntry.Sound.PlaySfxSoundThisFrame((int)EnumSound.ShortBoom, position);
            impulse.CreateEvent(position, new Vector3(.07f, -.14f, 0f));
        }

        public void Spread(Vector3 from, Vector3 to)
        {
            AddStroke(from + Vector3.up * .35f, to + Vector3.up * .35f, 0f,
                Mathf.Clamp(Vector3.Distance(from, to) * .085f, .25f, .58f), .12f, Flame);
        }

        private void Ring(Vector3 position, float radius, float duration, float width, Color color)
        {
            AddStroke(position + Vector3.up * .07f, position, radius, duration, width, color);
        }

        private void AddStroke(Vector3 from, Vector3 to, float radius, float duration, float width, Color color)
        {
            if (material == null) return;
            var obj = new GameObject(radius > 0f ? "燃烧火纹" : "飞火");
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = radius > 0f ? LineAlignment.TransformZ : LineAlignment.View;
            if (radius > 0f) obj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            line.loop = radius > 0f;
            line.positionCount = radius > 0f ? Segments : 12;
            line.widthMultiplier = width;
            if (radius <= 0f) line.widthCurve = AnimationCurve.Linear(0f, .05f, 1f, 1f);

            var stroke = new Stroke
            {
                Line = line, Origin = from, Destination = to, Radius = radius,
                Duration = duration, Color = color
            };
            Draw(stroke, 0f);
            strokes.Add(stroke);
        }

        private void Update()
        {
            for (int i = strokes.Count - 1; i >= 0; i--)
            {
                var stroke = strokes[i];
                stroke.Age += Time.deltaTime;
                float progress = stroke.Age / stroke.Duration;
                if (progress < 1f)
                {
                    Draw(stroke, progress);
                    continue;
                }
                Destroy(stroke.Line.gameObject);
                strokes.RemoveAt(i);
                if (stroke.Radius == 0f)
                    Ring(stroke.Destination - Vector3.up * .35f, .42f, .25f, .09f, Ember);
            }
        }

        private static void Draw(Stroke stroke, float progress)
        {
            var line = stroke.Line;
            Color color = stroke.Color;
            if (stroke.Radius > 0f)
            {
                color.a = Mathf.Lerp(.95f, 0f, Mathf.SmoothStep(0f, 1f, progress));
                float radius = stroke.Radius * (1f - .8f * (1f - progress) * (1f - progress));
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * (2f * Mathf.PI / Segments);
                    float edge = radius * (1f + .035f * Mathf.Sin(angle * 9f) + .018f * Mathf.Cos(angle * 17f));
                    line.SetPosition(i, stroke.Origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * edge);
                }
            }
            else
            {
                for (int i = 0; i < line.positionCount; i++)
                {
                    float u = i / (float)(line.positionCount - 1);
                    float travel = Mathf.Clamp01(progress - (1f - u) * .38f);
                    var point = Vector3.Lerp(stroke.Origin, stroke.Destination, travel);
                    point.y += Mathf.Sin(travel * Mathf.PI) * .65f;
                    line.SetPosition(i, point);
                }
            }
            line.startColor = stroke.Radius > 0f ? color : new Color(Ember.r, Ember.g, Ember.b, .18f);
            line.endColor = color;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
