using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using DataTable;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    // Owned by the foreground battle camera, so all effects leave with the battle.
    public sealed partial class TimelineBurnVisuals : MonoBehaviour
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
        private TimelineCombatVfx combatVfx;
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

        internal void SetTidegatePresentation(IReadOnlyList<EntityQizi> fighters, float boardHeight)
        {
            if (combatVfx != null) combatVfx.SetTidegatePresentation(fighters, boardHeight);
            if (Slash != null) Slash.UseTidegateInk = true;
        }

        private void Awake()
        {
            combatVfx = TimelineCombatVfx.Create(transform);
            var shader = Resources.Load<Shader>("TimelineBurnStroke");
            if (shader != null && shader.isSupported) material = new Material(shader);
            if (material != null)
            {
                var slashPrefab = Resources.Load<GameObject>("TimelineVfx/BasicSlash");
                var slashTemplate = slashPrefab != null ? slashPrefab.GetComponent<TimelineSlashVisuals>() : null;
                if (slashTemplate != null)
                {
                    Slash = Instantiate(slashTemplate, transform);
                    Slash.Initialize(material, combatVfx);
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
            if (combatVfx != null) combatVfx.FireDetonation(position, radius);
            else
            {
                Ring(position, radius, .48f, .15f, Ember);
                Ring(position, radius * .89f, .34f, .065f, Flame);
            }
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.EmberImpact, position);
            impulse.CreateEvent(position, new Vector3(.07f, -.14f, 0f));
        }

        internal void ShieldRaised(Vector3 position)
        {
            if (combatVfx != null) combatVfx.ShieldRise(position);
            else Ring(position, .56f, .29f, .07f, Water);
        }

        internal LineRenderer StoneWarning(Transform owner, Vector3 position, float radius)
        {
            if (material == null) return null;
            var line = CreateLine("断碑重砸·蓄力", owner, true);
            line.loop = true;
            line.positionCount = 24;
            UpdateStoneWarning(line, position, radius, 0f);
            return line;
        }

        internal static void UpdateStoneWarning(LineRenderer line, Vector3 position, float radius, float progress)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / line.positionCount);
                line.SetPosition(i, position + new Vector3(Mathf.Cos(angle) * radius, .085f, Mathf.Sin(angle) * radius));
            }
            var color = Color.Lerp(StoneDust, StoneGold, progress);
            color.a = Mathf.Lerp(.65f, 1f, progress);
            line.startColor = line.endColor = color;
            line.widthMultiplier = Mathf.Lerp(.045f, .095f, progress);
        }

        internal void StoneImpact(Vector3 position, float radius)
        {
            if (combatVfx != null) combatVfx.StoneCrash(position, radius);
            else
            {
                Ring(position, radius * .92f, .28f, .18f, StoneDust);
                Ring(position, radius * .58f, .17f, .10f, StoneGold);
            }
            if (Time.time < nextStoneImpulseTime) return;
            nextStoneImpulseTime = Time.time + .19f;
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

        internal void PlayerHit(Vector3 position, Vector3 direction)
        {
            if (combatVfx != null) combatVfx.PlayerHit(position, direction);
            else Slash?.StatusBurst(position, new Color(1f, .32f, .17f), .52f, drift: direction * 1.35f);
        }

        internal void BleedApplied(Vector3 position) =>
            Slash?.StatusBurst(position + Vector3.up * .74f, new Color(.73f, .09f, .14f), .44f);

        internal void Ignited(Vector3 position) =>
            Slash?.StatusBurst(position + Vector3.up * .55f, new Color(1f, .39f, .10f), 1.32f);

        internal void OpeningStruck(Vector3 position) =>
            Slash?.StatusBurst(position + Vector3.up * .78f, new Color(.91f, .80f, .52f), .75f);

        internal void OpeningSealStamped(Vector3 position)
        {
            if (combatVfx != null)
            {
                combatVfx.StampSeal(position);
                StartCoroutine(PlaySealImpact(position));
            }
            else if (material != null) StartCoroutine(AnimateOpeningSeal(position));
        }

        private IEnumerator PlaySealImpact(Vector3 position)
        {
            yield return new WaitForSeconds(TimelineCombatVfx.SealDropTime);
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.SealImpact, position);
        }

        private IEnumerator AnimateOpeningSeal(Vector3 position)
        {
            var stamp = new GameObject("封隙印").transform;
            stamp.SetParent(transform, false);
            var border = CreateLine("印框", stamp, true);
            border.useWorldSpace = false;
            border.loop = true;
            border.widthMultiplier = .05f;
            border.positionCount = 4;
            border.SetPositions(new[]
            {
                new Vector3(-.36f, -.36f), new Vector3(.36f, -.36f),
                new Vector3(.36f, .36f), new Vector3(-.36f, .36f)
            });
            var mark = CreateLine("印纹", stamp, true);
            mark.useWorldSpace = false;
            mark.widthMultiplier = .045f;
            mark.positionCount = 9;
            mark.SetPositions(new[]
            {
                new Vector3(-.23f, .22f), new Vector3(.23f, .22f), new Vector3(.23f, -.22f),
                new Vector3(-.23f, -.22f), new Vector3(-.23f, .08f), new Vector3(.08f, .08f),
                new Vector3(.08f, -.08f), new Vector3(-.08f, -.08f), new Vector3(-.08f, .22f)
            });
            stamp.rotation = Quaternion.Euler(0, -16, 0);
            var cinnabar = new Color(.94f, .25f, .15f, .9f);
            border.startColor = border.endColor = mark.startColor = mark.endColor = cinnabar;
            Vector3 ground = position + Vector3.up * .12f;
            const float fallTime = .12f;
            for (float age = 0; age < fallTime; age += Time.deltaTime)
            {
                float t = age / fallTime;
                stamp.position = ground + Vector3.up * (.86f * (1f - t * t));
                stamp.localScale = Vector3.one * Mathf.Lerp(1.22f, 1f, t);
                yield return null;
            }
            stamp.position = ground;
            stamp.localScale = Vector3.one;
            Ring(position, .66f, .28f, .055f, cinnabar);
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.SealImpact, position);
            const float holdTime = .42f;
            for (float age = 0; age < holdTime; age += Time.deltaTime)
            {
                cinnabar.a = .96f * (1f - Mathf.SmoothStep(0, 1, age / holdTime));
                border.startColor = border.endColor = mark.startColor = mark.endColor = cinnabar;
                yield return null;
            }
            Destroy(stamp.gameObject);
        }

        public void Spread(Vector3 from, Vector3 to)
        {
            if (combatVfx != null)
            {
                combatVfx.FireSpread(from, to);
                return;
            }
            AddStroke(from + Vector3.up * .35f, to + Vector3.up * .35f, 0f,
                Mathf.Clamp(Vector3.Distance(from, to) * .085f, .25f, .58f), .12f, Flame);
        }

        internal void SwordTurn(Vector3 position)
        {
            Ring(position, .48f, .27f, .075f, new Color(.54f, .89f, 1f));
            if (Time.time < nextSwordChimeTime) return;
            nextSwordChimeTime = Time.time + .32f;
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.GenFlySword, position);
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

        internal LineRenderer ReleaseUndertow(Transform owner, Vector3 origin, Vector3 forward,
            float length, float halfWidth)
        {
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.WaterImpact, origin);
            if (combatVfx != null)
            {
                combatVfx.Undertow(origin, forward, length, halfWidth);
                return null;
            }
            if (material == null) return null;
            var line = CreateLine("逆流浪锋", owner, false);
            line.positionCount = 9;
            line.widthMultiplier = .17f;
            return line;
        }

        internal static void UpdateWaterCrest(LineRenderer line, Vector3 center, Vector3 side, float halfWidth, float progress)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float across = i / (float)(line.positionCount - 1) * 2f - 1f;
                float crest = 1f - across * across;
                line.SetPosition(i, center + side * (across * halfWidth) + Vector3.up * (.12f + crest * .22f));
            }
            var color = Water;
            color.a = Mathf.Lerp(1f, .55f, progress);
            line.startColor = line.endColor = color;
        }

        internal void WaterSplash(Vector3 position)
        {
            Ring(position, .75f, .36f, .1f, Water);
            Ring(position + Vector3.up * .16f, .38f, .24f, .07f, new Color(.72f, .96f, 1f));
        }

        internal void WaterImpact(Vector3 position)
        {
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.WaterImpact, position);
            impulse.CreateEvent(position, new Vector3(.04f, -.08f, 0f));
        }

        internal LineRenderer SaltSurgeWarning(Transform owner, Vector3 origin, Vector3 direction,
            Vector3 side, float length, float halfWidth, bool drained)
        {
            if (material == null) return null;
            var line = CreateLine("抽潮·渠线", owner, true);
            line.loop = true;
            line.positionCount = 4;
            var start = origin + Vector3.up * .09f;
            var end = start + direction * length;
            var edge = side * halfWidth;
            line.SetPositions(new[] { start - edge, end - edge, end + edge, start + edge });
            UpdateSaltSurgeWarning(line, 0f, drained);
            Ring(origin, .8f, .32f, .085f, StoneGold);
            return line;
        }

        internal static void UpdateSaltSurgeWarning(LineRenderer line, float progress, bool drained)
        {
            var color = Color.Lerp(drained ? StoneDust : Water, StoneGold, Mathf.SmoothStep(0f, 1f, progress));
            color.a = Mathf.Lerp(.55f, 1f, progress);
            line.startColor = line.endColor = color;
            line.widthMultiplier = Mathf.Lerp(.055f, .13f, progress);
        }

        internal void SaltSurgeRelease(Vector3 origin, Vector3 direction, Vector3 side,
            float length, float halfWidth, bool drained)
        {
            if (material == null) return;
            StartCoroutine(AnimateSaltSurge(origin, direction, side, length, halfWidth, drained));
        }

        private IEnumerator AnimateSaltSurge(Vector3 origin, Vector3 direction, Vector3 side,
            float length, float halfWidth, bool drained)
        {
            var crest = CreateLine("抽潮·浪锋", transform, false);
            crest.positionCount = 13;
            const float duration = .33f;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = age / duration;
                float along = drained ? Mathf.Sqrt(t) : 1f - Mathf.Pow(t, .72f);
                float height = drained ? .22f : .54f;
                var center = origin + direction * (along * length);
                crest.widthMultiplier = Mathf.Lerp(drained ? .17f : .32f, .05f, t);
                var color = Color.Lerp(drained ? new Color(.71f, .76f, .68f) : Water, Color.white, .33f);
                color.a = 1f - t;
                crest.startColor = crest.endColor = color;
                for (int i = 0; i < crest.positionCount; i++)
                {
                    float u = 2f * i / (crest.positionCount - 1) - 1f;
                    crest.SetPosition(i, center + side * (u * halfWidth) +
                        Vector3.up * (.08f + height * (1f - u * u)));
                }
                yield return null;
            }
            Destroy(crest.gameObject);
        }

        internal void SaltSurgeImpact(Vector3 position, Vector3 direction, bool drained)
        {
            Ring(position, drained ? .75f : 1.15f, .24f, .14f, StoneGold);
            Slash?.StatusBurst(position + Vector3.up * .35f, drained ? StoneGold : Water, 1.7f);
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.WaterImpact, position);
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.HeavyImpact, position);
            impulse?.CreateEvent(position, (drained ? direction : -direction) * .08f + Vector3.down * .14f);
        }

        internal void SaltBiteImpact(Vector3 position)
        {
            Slash?.StatusBurst(position + Vector3.up * .55f, StoneGold, 1.1f);
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.HeavyImpact, position);
        }

        internal void SaltPullTrail(Vector3 from, Vector3 to)
        {
            if (material == null) return;
            StartCoroutine(AnimateSaltPullTrail(from, to));
        }

        private IEnumerator AnimateSaltPullTrail(Vector3 from, Vector3 to)
        {
            var trail = CreateLine("盐渠·拖痕", transform, true);
            trail.positionCount = 2;
            var lift = Vector3.up * .13f;
            trail.SetPosition(1, to + lift);
            const float duration = .23f;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = age / duration;
                trail.SetPosition(0, Vector3.Lerp(from, to, t * .8f) + lift);
                FadeBrush(trail, Water, .24f, t, .95f);
                yield return null;
            }
            Destroy(trail.gameObject);
        }

        internal LineRenderer NamelessWarning(Transform owner, TimelineNamelessSteleCast.Pattern pattern,
            TimelineNamelessSteleCast.ImpactArea area, out LineRenderer buildup)
        {
            buildup = null;
            if (material == null) return null;
            var outline = NamelessOutline(owner, pattern, area);
            buildup = CreateLine("无名碑·蓄势", outline.transform, true);
            buildup.loop = area.Length <= 0f;
            buildup.positionCount = buildup.loop ? area.OutlinePoints : 2;
            UpdateNamelessWarning(outline, buildup, pattern, area, 0f);
            return outline;
        }

        internal static void UpdateNamelessWarning(LineRenderer outline, LineRenderer buildup,
            TimelineNamelessSteleCast.Pattern pattern, TimelineNamelessSteleCast.ImpactArea area, float progress)
        {
            var color = NamelessColor(pattern);
            color.a = Mathf.Lerp(.60f, 1f, progress);
            outline.startColor = outline.endColor = color;
            outline.widthMultiplier = Mathf.Lerp(.047f, .095f, progress);

            var lift = Vector3.up * .11f;
            if (area.Length > 0f)
            {
                // The outline is the full hit corridor. Its center stroke shows the locked direction.
                buildup.SetPosition(0, area.Origin + lift);
                buildup.SetPosition(1, area.Origin + area.Forward * area.Length * progress + lift);
                buildup.widthMultiplier = .17f;
            }
            else
            {
                float scale = pattern == TimelineNamelessSteleCast.Pattern.Seal
                    ? Mathf.Lerp(.18f, .96f, progress * progress) : Mathf.Lerp(.90f, .22f, progress);
                for (int i = 0; i < buildup.positionCount; i++)
                    buildup.SetPosition(i, Vector3.Lerp(area.Origin, area.OutlinePoint(i), scale) + lift);
                buildup.widthMultiplier = Mathf.Lerp(.04f, .15f, progress);
            }
            color.a *= .65f;
            buildup.startColor = buildup.endColor = color;
        }

        internal void NamelessImpact(TimelineNamelessSteleCast.Pattern pattern,
            TimelineNamelessSteleCast.ImpactArea area, bool hit)
        {
            bool seal = pattern == TimelineNamelessSteleCast.Pattern.Seal;
            if (material != null)
            {
                var outline = NamelessOutline(transform, pattern, area);
                var color = NamelessColor(pattern);
                outline.widthMultiplier = seal ? .22f : .14f;
                StartCoroutine(FadeCombatStroke(outline, color, seal ? .62f : .28f));

                if (seal)
                {
                    // Eight fractures rise inside the same disk that applied damage.
                    for (int i = 0; i < area.OutlinePoints; i += 6)
                    {
                        var end = area.OutlinePoint(i);
                        var fracture = CreateLine("封山归墟·地脉", outline.transform, false);
                        fracture.positionCount = 3;
                        fracture.widthMultiplier = .095f;
                        fracture.SetPosition(0, area.Origin + Vector3.up * .10f);
                        fracture.SetPosition(1, Vector3.Lerp(area.Origin, end, .58f) + Vector3.up * .44f);
                        fracture.SetPosition(2, end + Vector3.up * .10f);
                        StartCoroutine(FadeCombatStroke(fracture, new Color(1f, .76f, .55f), .43f));
                    }
                }
                else if (area.Length > 0f)
                {
                    var slash = CreateLine("封名裁线·碑光", outline.transform, true);
                    slash.positionCount = 2;
                    slash.widthMultiplier = area.Radius * 2f;
                    slash.SetPosition(0, area.Origin + Vector3.up * .115f);
                    slash.SetPosition(1, area.Origin + area.Forward * area.Length + Vector3.up * .115f);
                    StartCoroutine(FadeCombatStroke(slash, color, .22f, .54f));
                }
                else
                {
                    Ring(area.Origin, area.Radius * .92f, .24f, .08f, StoneGold);
                }
            }

            HotGameEntry.Sound.PlayTimelineCue(seal ? EnumSound.SealImpact : EnumSound.HeavyImpact, area.Origin);
            if (hit || seal)
                impulse.CreateEvent(area.Origin, seal ? new Vector3(.09f, -.21f, 0f) : new Vector3(.04f, -.11f, 0f));
        }

        private LineRenderer NamelessOutline(Transform parent, TimelineNamelessSteleCast.Pattern pattern,
            TimelineNamelessSteleCast.ImpactArea area)
        {
            string name = pattern switch
            {
                TimelineNamelessSteleCast.Pattern.Smash => "无名碑·碎印冲击",
                TimelineNamelessSteleCast.Pattern.Line => "无名碑·封名裁线",
                _ => "无名碑·封山归墟"
            };
            var line = CreateLine(name, parent, true);
            line.loop = true;
            line.positionCount = area.OutlinePoints;
            for (int i = 0; i < line.positionCount; i++)
                line.SetPosition(i, area.OutlinePoint(i) + Vector3.up * .105f);
            return line;
        }

        private static Color NamelessColor(TimelineNamelessSteleCast.Pattern pattern) => pattern switch
        {
            TimelineNamelessSteleCast.Pattern.Smash => new Color(1f, .69f, .30f),
            TimelineNamelessSteleCast.Pattern.Line => new Color(.96f, .83f, .61f),
            _ => new Color(1f, .32f, .25f)
        };

        private static IEnumerator FadeCombatStroke(LineRenderer line, Color color, float duration, float alpha = 1f)
        {
            for (float age = 0f; age < duration && line != null; age += Time.deltaTime)
            {
                color.a = alpha * (1f - age / duration);
                line.startColor = line.endColor = color;
                yield return null;
            }
            if (line != null) Destroy(line.gameObject);
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
            if (combatVfx != null)
            {
                combatVfx.LightningStrike(from, to, hop);
                if (hop == 0) LightningImpact(to);
            }
            else if (material != null)
            {
                StartCoroutine(AnimateLightningStrike(from, to, hop));
            }
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
                LightningImpact(to);
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

        private void LightningImpact(Vector3 position)
        {
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.ThunderImpact, position);
            impulse.CreateEvent(position, new Vector3(.035f, -.085f, 0f));
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
            if (mirrorMesh != null) Destroy(mirrorMesh);
            if (mirrorMetal != null) Destroy(mirrorMetal);
            if (rayMaterial != null) Destroy(rayMaterial);
            if (tideBellMetal != null) Destroy(tideBellMetal);
            if (cloudMaterial != null) Destroy(cloudMaterial);
            if (shellShieldMesh != null) Destroy(shellShieldMesh);
            if (lavaMesh != null) Destroy(lavaMesh);
            if (lavaMaterial != null) Destroy(lavaMaterial);
            if (material != null) Destroy(material);
        }
    }
}
