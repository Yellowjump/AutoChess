using System.Collections;
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
        private const int BlueBurstSfxId = 4;
        private static readonly Color Ember = new Color(.83f, .15f, .045f);
        private static readonly Color Flame = new Color(1f, .57f, .17f);
        private static readonly Color Water = new Color(.19f, .81f, .87f, .9f);
        private static readonly Color StoneDust = new Color(.53f, .37f, .22f);
        private static readonly Color StoneGold = new Color(.98f, .77f, .40f);
        private static readonly Color ThunderInk = new Color(.17f, .18f, .28f);
        private static readonly Color ThunderSilver = new Color(.86f, .89f, .97f);
        private static readonly Color ThunderSeal = new Color(.39f, .37f, .55f);
        private static readonly AnimationCurve BrushTaper = new AnimationCurve(
            new Keyframe(0f, .12f), new Keyframe(.26f, 1f), new Keyframe(.75f, .68f), new Keyframe(1f, .12f));
        private readonly List<Stroke> strokes = new List<Stroke>();
        private Material material;
        internal TimelineSlashVisuals Slash { get; private set; }
        private CinemachineImpulseDefinition impulse;
        private float nextSwordChimeTime;
        private float nextStoneImpulseTime;

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
            if (material != null)
            {
                var slashPrefab = Resources.Load<GameObject>("TimelineVfx/BasicSlash");
                var slashTemplate = slashPrefab != null ? slashPrefab.GetComponent<TimelineSlashVisuals>() : null;
                if (slashTemplate != null)
                {
                    Slash = Instantiate(slashTemplate, transform);
                    Slash.Initialize(material);
                }
            }
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

        internal void ShieldRaised(Vector3 position)
        {
            Ring(position, .56f, .29f, .07f, Water);
        }

        internal LineRenderer StoneWarning(Transform owner)
        {
            if (material == null) return null;
            var line = CreateLine("断碑重砸·蓄力", owner, true);
            line.loop = true;
            line.positionCount = 24;
            UpdateStoneWarning(line, owner.position, 0f);
            return line;
        }

        internal static void UpdateStoneWarning(LineRenderer line, Vector3 position, float progress)
        {
            float radius = Mathf.Lerp(.90f, .36f, progress);
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / line.positionCount);
                float edge = i % 4 == 0 ? radius * 1.24f : radius;
                line.SetPosition(i, position + new Vector3(Mathf.Cos(angle) * edge, .085f, Mathf.Sin(angle) * edge));
            }
            var color = Color.Lerp(StoneDust, StoneGold, progress);
            color.a = Mathf.Lerp(.65f, 1f, progress);
            line.startColor = line.endColor = color;
            line.widthMultiplier = Mathf.Lerp(.045f, .095f, progress);
        }

        internal void StoneImpact(Vector3 position)
        {
            Ring(position, 1.15f, .28f, .18f, StoneDust);
            Ring(position, .70f, .17f, .10f, StoneGold);
            if (Time.time < nextStoneImpulseTime) return;
            nextStoneImpulseTime = Time.time + .19f;
            HotGameEntry.Sound.PlaySfxSoundThisFrame((int)EnumSound.ShortBoom, position);
            impulse.CreateEvent(position, new Vector3(.035f, -.135f, 0f));
        }

        internal void BellAbsorbed(Vector3 position)
        {
            Ring(position + Vector3.up * .38f, .52f, .25f, .12f, Water);
        }

        internal void ShieldShattered(Vector3 position)
        {
            var table = GameEntry.DataTable.GetDataTable<DRSfx>("Sfx");
            if (table == null || !table.HasDataRow(BlueBurstSfxId)) return;
            // The burst is anchored to the impact even if the defender dies on this hit.
            var point = HotGameEntry.HeroManager.CreatePosPoint();
            point.LogicPosition = position + Vector3.up * .40f;
            point.DurationMs = Mathf.Max(1, table[BlueBurstSfxId].DurationMs);
            point.RemainMs = point.DurationMs;
            point.AddSfx(BlueBurstSfxId);
        }

        internal void BleedApplied(Vector3 position) =>
            Slash?.StatusBurst(position + Vector3.up * .74f, new Color(.73f, .09f, .14f), .44f);

        internal void Ignited(Vector3 position) =>
            Slash?.StatusBurst(position + Vector3.up * .55f, new Color(1f, .39f, .10f), 1.32f);

        internal void OpeningStruck(Vector3 position) =>
            Slash?.StatusBurst(position + Vector3.up * .78f, new Color(.91f, .80f, .52f), .75f);

        public void Spread(Vector3 from, Vector3 to)
        {
            AddStroke(from + Vector3.up * .35f, to + Vector3.up * .35f, 0f,
                Mathf.Clamp(Vector3.Distance(from, to) * .085f, .25f, .58f), .12f, Flame);
        }

        internal void SwordTurn(Vector3 position)
        {
            Ring(position, .48f, .27f, .075f, new Color(.54f, .89f, 1f));
            if (Time.time < nextSwordChimeTime) return;
            nextSwordChimeTime = Time.time + .32f;
            HotGameEntry.Sound.PlaySfxSoundThisFrame((int)EnumSound.GenFlySword, position);
        }

        internal LineRenderer WaterWarning(Transform owner, Vector3 origin, Vector3 direction,
            Vector3 side, float length, float halfWidth)
        {
            if (material == null) return null;
            var line = CreateLine("逆流预警", owner, true);
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = .065f;
            Vector3 end = origin + direction * length;
            Vector3 edge = side * halfWidth;
            Vector3 lift = Vector3.up * .085f;
            line.SetPositions(new[] { origin - edge + lift, end - edge + lift,
                end + edge + lift, origin + edge + lift });
            line.startColor = line.endColor = Water;
            var wash = CreateLine("逆流水带", line.transform, true);
            wash.widthMultiplier = halfWidth * 2f;
            wash.positionCount = 2;
            wash.SetPosition(0, origin + lift);
            wash.SetPosition(1, end + lift);
            wash.startColor = wash.endColor = new Color(Water.r, Water.g, Water.b, .24f);
            Ring(origin, halfWidth, 1.2f, .07f, Water);
            return line;
        }

        internal void WaterSweep(Vector3 origin, Vector3 direction, Vector3 side, float length, float halfWidth)
        {
            if (material != null) StartCoroutine(AnimateWaterSweep(origin, direction, side, length, halfWidth));
        }

        private IEnumerator AnimateWaterSweep(Vector3 origin, Vector3 direction, Vector3 side,
            float length, float halfWidth)
        {
            var line = CreateLine("逆流浪锋", transform, false);
            line.positionCount = 9;
            line.widthMultiplier = .17f;
            for (float age = 0f; age < .32f; age += Time.deltaTime)
            {
                float progress = age / .32f;
                Vector3 center = origin + direction * (length * progress);
                for (int i = 0; i < line.positionCount; i++)
                {
                    float across = i / (float)(line.positionCount - 1) * 2f - 1f;
                    float crest = 1f - across * across;
                    line.SetPosition(i, center + side * (across * halfWidth) +
                        Vector3.up * (.12f + crest * .22f));
                }
                var color = Water;
                color.a = 1f - progress;
                line.startColor = line.endColor = color;
                yield return null;
            }
            Destroy(line.gameObject);
        }

        internal void WaterSplash(Vector3 position)
        {
            Ring(position, .75f, .36f, .1f, Water);
            Ring(position + Vector3.up * .16f, .38f, .24f, .07f, new Color(.72f, .96f, 1f));
        }

        internal void WaterImpact(Vector3 position)
        {
            HotGameEntry.Sound.PlaySfxSoundThisFrame((int)EnumSound.ConfirmTipShow, position);
            impulse.CreateEvent(position, new Vector3(.04f, -.08f, 0f));
        }

        internal LineRenderer NamelessWarning(Transform owner, Vector3 origin, Vector3 direction,
            float radius, float length)
        {
            return NamelessOutline(owner, origin, direction, radius, length, .065f,
                new Color(.72f, .18f, .12f, .9f));
        }

        internal void NamelessImpact(Vector3 origin, Vector3 direction, float radius, float length, bool hit)
        {
            var cinnabar = new Color(.95f, .36f, .19f);
            var outline = NamelessOutline(transform, origin, direction, radius, length, .18f, cinnabar);
            if (outline != null) StartCoroutine(FadeNamelessOutline(outline, cinnabar));
            if (length == 0f) Ring(origin, radius, .42f, .12f, StoneGold);
            HotGameEntry.Sound.PlaySfxSoundThisFrame((int)EnumSound.ShortBoom, origin);
            if (hit) impulse.CreateEvent(origin, new Vector3(.06f, -.16f, 0f));
        }

        private LineRenderer NamelessOutline(Transform parent, Vector3 origin, Vector3 direction, float radius,
            float length, float width, Color color)
        {
            if (material == null) return null;
            var line = CreateLine(length > 0f ? "无名碑·封名裁线" : "无名碑·封山归墟", parent, true);
            line.loop = true;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            origin += Vector3.up * .105f;
            if (length > 0f)
            {
                var edge = Vector3.Cross(Vector3.up, direction) * radius;
                var end = origin + direction * length;
                line.positionCount = 4;
                line.SetPositions(new[] { origin - edge, end - edge, end + edge, origin + edge });
            }
            else
            {
                line.positionCount = 36;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * 2f * Mathf.PI / line.positionCount;
                    line.SetPosition(i, origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
                }
            }
            return line;
        }

        private static IEnumerator FadeNamelessOutline(LineRenderer outline, Color color)
        {
            for (float age = 0f; age < .32f; age += Time.deltaTime)
            {
                color.a = 1f - age / .32f;
                outline.startColor = outline.endColor = color;
                yield return null;
            }
            Destroy(outline.gameObject);
        }

        internal void LightningTransfer(Vector3 from, Vector3 to)
        {
            if (material != null) StartCoroutine(AnimateLightningTransfer(from, to));
        }

        private IEnumerator AnimateLightningTransfer(Vector3 from, Vector3 to)
        {
            var line = CreateLine("传雷·游丝", transform, false);
            line.positionCount = 9;
            line.widthMultiplier = .10f;
            line.widthCurve = BrushTaper;
            for (float age = 0f; age < .26f; age += Time.deltaTime)
            {
                float head = age / .26f * 1.36f;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float t = Mathf.Clamp01(head - .36f * (1f - i / 8f));
                    line.SetPosition(i, Vector3.Lerp(from, to, t) + Vector3.up * (.23f + Mathf.Sin(t * Mathf.PI) * .65f));
                }
                line.startColor = new Color(ThunderInk.r, ThunderInk.g, ThunderInk.b, .08f);
                line.endColor = Color.Lerp(ThunderSilver, ThunderSeal, age / .26f);
                yield return null;
            }
            Destroy(line.gameObject);
        }

        internal void LightningStrike(Vector3 from, Vector3 to, int hop)
        {
            if (material != null) StartCoroutine(AnimateLightningStrike(from, to, hop));
        }

        private IEnumerator AnimateLightningStrike(Vector3 from, Vector3 to, int hop)
        {
            if (hop > 0) yield return new WaitForSeconds(.055f * hop);
            float strength = 1f - .23f * hop;
            var ink = CreateLine("惊蛰·墨锋", transform, false);
            var core = CreateLine("惊蛰·银芯", transform, false);
            ink.positionCount = core.positionCount = 9;
            ink.widthCurve = core.widthCurve = BrushTaper;
            core.sortingOrder = ink.sortingOrder + 1;
            Vector3 side = Vector3.Cross((to - from).normalized, Vector3.up);
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            side.Normalize();
            for (int i = 0; i < ink.positionCount; i++)
            {
                float t = i / 8f;
                float bend = Mathf.Sin(t * Mathf.PI) * (.24f * Mathf.Sin(i * 9.3f + hop) + .09f * Mathf.Cos(i * 4f));
                Vector3 point = Vector3.Lerp(from, to, t) + Vector3.up * .30f + side * (bend * strength);
                ink.SetPosition(i, point);
                core.SetPosition(i, point);
            }
            Ring(to, .76f * strength, .32f, .10f * strength, ThunderInk);
            if (hop == 0)
            {
                Ring(to + Vector3.up * .015f, .49f, .20f, .075f, ThunderSilver);
                HotGameEntry.Sound.PlaySfxSoundThisFrame((int)EnumSound.ShortBoom, to);
                impulse.CreateEvent(to, new Vector3(.035f, -.085f, 0f));
            }
            for (float age = 0f; age < .25f; age += Time.deltaTime)
            {
                float progress = age / .25f;
                FadeBrush(ink, ThunderInk, .26f * strength, progress, .76f);
                FadeBrush(core, ThunderSilver, .115f * strength, Mathf.Clamp01(progress * 1.6f), 1f);
                yield return null;
            }
            Destroy(ink.gameObject);
            Destroy(core.gameObject);
        }

        internal void LightningMark(Vector3 position, int count, float delay = 0f)
        {
            if (material != null) StartCoroutine(AnimateLightningMark(position, count, delay));
        }

        private IEnumerator AnimateLightningMark(Vector3 position, int count, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var seal = CreateLine("雷印·篆痕", transform, true);
            seal.positionCount = 12;
            seal.widthCurve = BrushTaper;
            var lift = Vector3.up * .09f;
            for (float age = 0f; age < .36f; age += Time.deltaTime)
            {
                float progress = age / .36f;
                float radius = .28f + .19f * Mathf.Sqrt(progress);
                float sweep = Mathf.Min(1f, progress * 5f) * count * .28f * Mathf.PI * 2f;
                for (int i = 0; i < seal.positionCount; i++)
                {
                    float angle = .9f + i / 11f * sweep;
                    seal.SetPosition(i, position + lift + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
                }
                FadeBrush(seal, count >= 3 ? ThunderInk : ThunderSeal, .10f, progress, .94f);
                yield return null;
            }
            Destroy(seal.gameObject);
        }

        private static void FadeBrush(LineRenderer line, Color color, float width, float progress, float opacity)
        {
            color.a = opacity * (1f - Mathf.SmoothStep(0f, 1f, progress));
            line.startColor = line.endColor = color;
            line.widthMultiplier = width * Mathf.Lerp(1f, .3f, progress);
        }

        private void Ring(Vector3 position, float radius, float duration, float width, Color color)
        {
            AddStroke(position + Vector3.up * .07f, position, radius, duration, width, color);
        }

        private void AddStroke(Vector3 from, Vector3 to, float radius, float duration, float width, Color color)
        {
            if (material == null) return;
            var line = CreateLine(radius > 0f ? "战斗波纹" : "飞火", transform, radius > 0f);
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

        private LineRenderer CreateLine(string label, Transform parent, bool groundAligned)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(parent, false);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = groundAligned ? LineAlignment.TransformZ : LineAlignment.View;
            if (groundAligned) obj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return line;
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
