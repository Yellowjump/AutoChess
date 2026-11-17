using System.Collections.Generic;
using System.Collections;
using DataTable;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    public sealed partial class TimelineBurnVisuals
    {
        private static readonly Color Salt = new(.62f,.87f,.80f,.94f);
        private static readonly Color Hemp = new(.78f,.66f,.41f,.94f);
        private static readonly Color LanternGold = new(.99f,.70f,.24f,.91f);
        private static readonly Color Frost = new(.75f,.87f,.94f,.93f);
        private static readonly Color KiteDust = new(.78f,.66f,.49f,.92f);
        private static readonly Color ShellGreen = new(.44f,.77f,.71f,.93f);
        private static readonly Color Paper = new(.92f,.82f,.59f,.93f);

        internal LineRenderer CaveWarning(Vector3 point, int kind, float radius, out LineRenderer charge)
        {
            charge = null;
            if (material == null) return null;
            var tint = CaveTint(kind);
            var outline = OuterRing(CaveName(kind), transform, point, radius, tint);
            charge = CreateLine("洞窟·地脉蓄势", outline.transform, true);
            charge.positionCount = kind is TimelineCaveHazards.Marten or TimelineCaveHazards.Golem ? 6 : 5;
            UpdateCaveWarning(outline, charge, point, kind, radius, 0f);
            return outline;
        }

        internal static void UpdateCaveWarning(LineRenderer outline, LineRenderer charge, Vector3 center,
            int kind, float radius, float progress)
        {
            var tint = CaveTint(kind);
            tint.a = Mathf.Lerp(.60f, 1f, progress);
            outline.startColor = outline.endColor = tint;
            outline.widthMultiplier = Mathf.Lerp(.043f, .092f, progress);

            bool frost = kind is TimelineCaveHazards.Marten or TimelineCaveHazards.Golem;
            float reach = radius * Mathf.Lerp(.12f, .78f, progress);
            for (int i = 0; i < charge.positionCount; i++)
            {
                float angle = i * (frost ? Mathf.PI * 2f / 3f : .66f);
                float distance = i % 2 == 0 ? reach : 0f;
                charge.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * distance, .112f, Mathf.Sin(angle) * distance));
            }
            tint.a *= .71f;
            charge.startColor = charge.endColor = tint;
            charge.widthMultiplier = Mathf.Lerp(.026f, .066f, progress);
        }

        internal void CaveImpact(LineRenderer outline, Vector3 center, int kind, float radius)
        {
            if (outline == null) return;
            bool frost = kind is TimelineCaveHazards.Marten or TimelineCaveHazards.Golem;
            var tint = CaveTint(kind);
            var rim = tint;
            rim.a = frost ? .60f : .92f;
            outline.startColor = outline.endColor = rim;
            outline.widthMultiplier = frost ? .046f : .105f;

            var effect = kind switch
            {
                TimelineCaveHazards.Marten => (Count: 3, Height: .24f, Width: .048f, Duration: .38f),
                TimelineCaveHazards.Golem => (Count: 5, Height: .62f, Width: .092f, Duration: .56f),
                TimelineCaveHazards.Lizard => (Count: 3, Height: .60f, Width: .11f, Duration: .29f),
                _ => (Count: 4, Height: 1.08f, Width: .16f, Duration: .34f)
            };
            for (int i = 0; i < effect.Count; i++)
            {
                float angle = i * (Mathf.PI * 2f / effect.Count);
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tip = center + direction * (radius * .81f) + Vector3.up * (effect.Height * (i % 2 == 0 ? 1f : .67f));
                var stroke = CreateLine(CaveName(kind), outline.transform, false);
                stroke.positionCount = 3;
                stroke.widthMultiplier = effect.Width;
                stroke.widthCurve = BrushTaper;
                stroke.SetPosition(0, center + direction * (radius * .12f) + Vector3.up * .10f);
                stroke.SetPosition(1, Vector3.Lerp(center, tip, .72f));
                stroke.SetPosition(2, tip);
                StartCoroutine(FadeCombatStroke(stroke, tint, effect.Duration));
            }
        }

        private static string CaveName(int kind) => kind switch
        {
            TimelineCaveHazards.Marten => "踏雪留痕",
            TimelineCaveHazards.Golem => "寒髓碎地",
            TimelineCaveHazards.Lizard => "熔息喷火",
            _ => "翻炉喷脉"
        };

        private static Color CaveTint(int kind) => kind switch
        {
            TimelineCaveHazards.Marten => new Color(.67f, .96f, 1f),
            TimelineCaveHazards.Golem => new Color(.28f, .72f, 1f),
            TimelineCaveHazards.Lizard => new Color(1f, .66f, .21f),
            _ => new Color(1f, .29f, .10f)
        };

        internal LineRenderer OuterWarning(int kind, Transform anchor, Vector3 origin, Vector3 aim, Vector3 direction,
            float length, float halfWidth)
        {
            if (material == null) return null;
            switch (kind)
            {
                case TimelineOuterGuardianCast.Eel:
                case TimelineOuterGuardianCast.Rope:
                    return RegionalWarning(kind == TimelineOuterGuardianCast.Eel ? "盐针三叩" : "断索拖步",
                        anchor, origin, direction, length, halfWidth, kind == TimelineOuterGuardianCast.Eel ? Salt : Hemp);
                case TimelineOuterGuardianCast.Lantern:
                    return LanternWarning(anchor, origin);
                case TimelineOuterGuardianCast.Marten:
                    RegionalDust(origin, direction);
                    return OuterRing("折雪回身", anchor, aim, halfWidth, Frost);
                case TimelineOuterGuardianCast.Kite:
                    var circle = OuterRing("借梁坠击", anchor, aim, halfWidth, KiteDust);
                    var shadow = OuterRing("借梁坠击·投影", circle.transform, aim, halfWidth * .2f, KiteDust);
                    for (int i = 0; i < shadow.positionCount; i++)
                        shadow.SetPosition(i, shadow.GetPosition(i) + Vector3.up * .014f);
                    return circle;
                case TimelineOuterGuardianCast.Crab:
                    return ShellArc(anchor, origin, direction);
                default:
                    return RegisterWarning(anchor, origin);
            }
        }

        internal static void UpdateOuterAreaWarning(LineRenderer line, int kind, Vector3 aim, float radius, float progress)
        {
            var tint = kind == TimelineOuterGuardianCast.Eel ? Salt : kind == TimelineOuterGuardianCast.Rope ? Hemp :
                kind == TimelineOuterGuardianCast.Marten ? Frost : KiteDust;
            tint.a = Mathf.Lerp(.64f, 1f, progress);
            line.startColor = line.endColor = tint;
            line.widthMultiplier = Mathf.Lerp(.055f, .103f, progress);
            if (kind != TimelineOuterGuardianCast.Kite) return;
            var shadow = line.transform.GetChild(0).GetComponent<LineRenderer>();
            float size = radius * Mathf.Lerp(.25f, .88f, progress);
            for (int i = 0; i < shadow.positionCount; i++)
            {
                float angle = Mathf.PI * 2f * i / shadow.positionCount;
                shadow.SetPosition(i, aim + new Vector3(Mathf.Cos(angle) * size, .114f, Mathf.Sin(angle) * size));
            }
        }

        internal void OuterCorridorImpact(int kind, Vector3 origin, Vector3 forward, float length, float halfWidth)
        {
            if (kind == TimelineOuterGuardianCast.Eel && combatVfx != null)
            {
                combatVfx.GuardianSaltNeedle(origin, forward, length);
                return;
            }
            if (kind == TimelineOuterGuardianCast.Rope && combatVfx != null)
            {
                combatVfx.GuardianRope(origin, forward, length, halfWidth);
                return;
            }
            bool needle = kind == TimelineOuterGuardianCast.Eel;
            var tint = needle ? Salt : Hemp;
            var boundary = RegionalWarning(needle ? "盐针三叩·针带" : "断索拖步·索迹",
                transform, origin, forward, length, halfWidth, tint);
            if (boundary == null) return;
            StartCoroutine(FadeCombatStroke(boundary, tint, needle ? .13f : .29f, .75f));
            var stroke = CreateLine(needle ? "盐针三叩·针锋" : "断索拖步·绷索", boundary.transform, false);
            stroke.positionCount = needle ? 2 : 9;
            stroke.widthMultiplier = needle ? .095f : .15f;
            var side = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < stroke.positionCount; i++)
            {
                float progress = i / (float)(stroke.positionCount - 1);
                float swing = needle ? 0f : Mathf.Sin(progress * Mathf.PI * 6f) * halfWidth * .65f;
                stroke.SetPosition(i, origin + forward * (length * progress) + side * swing + Vector3.up * .29f);
            }
            StartCoroutine(FadeCombatStroke(stroke, tint, needle ? .12f : .26f));
        }

        internal void OuterKiteImpact(Vector3 center, Vector3 forward, float radius)
        {
            if (combatVfx != null)
            {
                combatVfx.GuardianKite(center, forward, radius);
                impulse.CreateEvent(center, new Vector3(.05f, -.13f, 0f));
                return;
            }
            if (material == null) return;
            var rim = OuterRing("借梁坠击·震圈", transform, center, radius, KiteDust);
            rim.widthMultiplier = .13f;
            StartCoroutine(FadeCombatStroke(rim, KiteDust, .36f));
            Ring(center, radius * .85f, .34f, .10f, LanternGold);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f;
                var ground = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius * .55f);
                AddStroke(ground + Vector3.up * (1.20f + i * .12f), ground, 0f, .20f, .09f, KiteDust);
            }
            impulse.CreateEvent(center, new Vector3(.05f, -.13f, 0f));
        }

        private static readonly Color RegisterRed = new(.88f, .24f, .15f);
        private const float RegisterRadius = .73f;

        private LineRenderer RegisterWarning(Transform anchor, Vector3 center)
        {
            var rim = OuterRing("借名成形", anchor, center, RegisterRadius, RegisterRed);
            for (int i = 0; i < 3; i++)
            {
                var slip = CreateLine("借名成形·纸签", rim.transform, false);
                slip.loop = true;
                slip.positionCount = 4;
                slip.widthMultiplier = .044f;
            }
            UpdateRegisterWarning(rim, center, 0f);
            return rim;
        }

        internal static void UpdateRegisterWarning(LineRenderer rim, Vector3 center, float progress)
        {
            var glow = RegisterRed;
            glow.a = Mathf.Lerp(.51f, 1f, progress);
            rim.startColor = rim.endColor = glow;
            rim.widthMultiplier = Mathf.Lerp(.043f, .104f, progress);
            for (int i = 0; i < rim.positionCount; i++)
            {
                float angle = 2f * Mathf.PI * i / rim.positionCount;
                rim.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * RegisterRadius, .105f,
                    Mathf.Sin(angle) * RegisterRadius));
            }
            for (int i = 0; i < rim.transform.childCount; i++)
            {
                var slip = rim.transform.GetChild(i).GetComponent<LineRenderer>();
                float angle = (i / 3f + progress * .56f) * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tangent = Vector3.Cross(Vector3.up, radial) * .071f;
                var point = center + radial * Mathf.Lerp(.69f, .46f, progress) +
                    Vector3.up * Mathf.Lerp(.15f, 1.51f, progress);
                var lean = Vector3.up * .15f + radial * .048f;
                slip.startColor = Paper;
                slip.endColor = glow;
                slip.SetPosition(0, point - tangent - lean);
                slip.SetPosition(1, point + tangent - lean);
                slip.SetPosition(2, point + tangent + lean);
                slip.SetPosition(3, point - tangent + lean);
            }
        }

        internal void PaperSummon(EntityQizi shadow)
        {
            if (shadow.GObj == null) return;
            if (combatVfx != null) combatVfx.GuardianPaperSummon(shadow.LogicPosition);
            else if (material != null) StartCoroutine(AnimatePaperSummon(shadow.LogicPosition));
            StartCoroutine(AnimatePaperStand(shadow));
        }

        private IEnumerator AnimatePaperSummon(Vector3 cell)
        {
            var ink = new Color(.22f, .09f, .09f, .98f);
            var vermilion = RegisterRed;
            var outer = OuterRing("纸影·落格朱印", transform, cell, .58f, vermilion);
            var inner = OuterRing("纸影·墨印", transform, cell, .41f, ink);
            inner.positionCount = 4;
            Slash?.StatusBurst(cell + Vector3.up * .22f, Paper, 1.5f, .13f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f;
                var side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                AddStroke(cell + side * .38f + Vector3.up * .10f,
                    cell + side * .24f + Vector3.up * 1.19f, 0f, .33f + i * .035f, .085f, Paper);
            }
            const float duration = .55f;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float progress = age / duration;
                float spread = Mathf.Lerp(.2f, 1f, Mathf.Clamp01(progress * 4f));
                for (int i = 0; i < inner.positionCount; i++)
                {
                    float angle = i * Mathf.PI / 2f - progress * Mathf.PI;
                    inner.SetPosition(i, cell + new Vector3(Mathf.Cos(angle) * spread * .46f, .12f,
                        Mathf.Sin(angle) * spread * .46f));
                }
                FadeBrush(outer, vermilion, .105f, progress, 1f);
                FadeBrush(inner, ink, .19f, progress, 1f);
                yield return null;
            }
            Destroy(outer.gameObject);
            Destroy(inner.gameObject);
        }

        private static IEnumerator AnimatePaperStand(EntityQizi shadow)
        {
            var model = shadow.GObj.transform;
            var scale = model.localScale;
            const float duration = .55f / 3.3f;
            for (float age = 0f; age < duration && model != null && shadow.IsValid; age += Time.deltaTime)
            {
                float stand = Mathf.SmoothStep(0f, 1f, age / duration);
                model.localScale = Vector3.Scale(scale, new Vector3(Mathf.Lerp(.43f, 1f, stand),
                    Mathf.Lerp(.10f, 1f, stand), Mathf.Lerp(.25f, 1f, stand)));
                yield return null;
            }
            if (model != null && shadow.IsValid) model.localScale = scale;
        }

        internal void OuterMartenImpact(Vector3 center, Vector3 forward, float radius)
        {
            if (combatVfx != null)
            {
                combatVfx.GuardianMarten(center, forward, radius);
                return;
            }
            if (material == null) return;
            var rim = OuterRing("折雪回身·雪锋", transform, center, radius, Frost);
            StartCoroutine(FadeCombatStroke(rim, Frost, .30f));
            var across = Vector3.Cross(Vector3.up, forward);
            for (int side = -1; side <= 1; side += 2)
                AddStroke(center - forward * (radius * .46f) + across * (side * radius * .62f) + Vector3.up * .52f,
                    center + forward * (radius * .63f) + across * (side * radius * .19f) + Vector3.up * .16f,
                    0f, .26f, .11f, Frost);
        }

        internal void LanternImpact(Vector3 point, bool completed)
        {
            if (combatVfx != null)
            {
                if (!completed) combatVfx.LanternSnuff(point);
                return;
            }
            Ring(point, .66f, .60f, .095f, completed ? LanternGold : Frost);
            if (completed)
                AddStroke(point, point + Vector3.up * 1.3f, 0, .45f, .25f, LanternGold);
        }

        private static readonly Color LavaOrange = new(1f, .38f, .075f);
        private static readonly Color LavaGold = new(1f, .76f, .23f);

        internal LineRenderer LavaWarning(Transform anchor, Vector3 center)
        {
            if (material == null) return null;
            var ring = OuterRing("熔岩溅射·预警", anchor, center, TimelineCaveHazards.LavaRadius, LavaGold);
            var projectile = CreateLine("熔岩溅射·飞火", ring.transform, false);
            projectile.positionCount = 5;
            projectile.widthMultiplier = .14f;
            projectile.widthCurve = BrushTaper;
            projectile.startColor = projectile.endColor = LavaGold;
            projectile.enabled = false;
            return ring;
        }

        internal static void UpdateLavaWarning(LineRenderer ring, Vector3 origin, Vector3 center, float progress)
        {
            ring.widthMultiplier = Mathf.Lerp(.059f, .105f, progress);
            var projectile = ring.transform.GetChild(0).GetComponent<LineRenderer>();
            if (progress < .72f) return;
            projectile.enabled = true;
            float flight = Mathf.InverseLerp(.72f, 1f, progress);
            var start = origin + Vector3.up * 1.50f;
            var end = center + Vector3.up * .12f;
            for (int i = 0; i < projectile.positionCount; i++)
            {
                float t = Mathf.Clamp01(flight - .19f * (1f - i / (float)(projectile.positionCount - 1)));
                projectile.SetPosition(i, Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .87f));
            }
        }

        private static readonly int LavaAge = Shader.PropertyToID("_Age");
        private static readonly int LavaPulseAmount = Shader.PropertyToID("_Pulse");
        private static readonly int LavaCenters = Shader.PropertyToID("_Centers");
        private readonly Vector4[] lavaCenters = new Vector4[TimelineRegionalGuardianCast.MaxLavaTargets];
        private Material lavaMaterial;
        private Mesh lavaMesh;
        private MaterialPropertyBlock lavaProperties;

        internal MeshRenderer LavaPool(IReadOnlyList<Vector3> centers)
        {
            if (lavaMaterial == null)
            {
                var shader = Resources.Load<Shader>("TimelineLavaPool");
                if (shader == null || !shader.isSupported) return null;
                lavaMaterial = new Material(shader);
                lavaProperties = new MaterialPropertyBlock();
                lavaMesh = BuildLavaMesh();
            }

            float radius = TimelineCaveHazards.LavaRadius;
            Vector3 min = centers[0], max = centers[0];
            for (int i = 0; i < lavaCenters.Length; i++)
            {
                if (i >= centers.Count)
                {
                    lavaCenters[i] = Vector4.zero;
                    continue;
                }
                var center = centers[i];
                min = Vector3.Min(min, center);
                max = Vector3.Max(max, center);
                lavaCenters[i] = new Vector4(center.x, center.z, radius, 1f);
                if (material != null) LavaDroplets(center, 7, .52f);
            }

            var pool = new GameObject("熔岩溅射·熔岩池");
            pool.transform.SetParent(transform, false);
            pool.transform.SetPositionAndRotation((min + max) * .5f + Vector3.up * .10f, Quaternion.identity);
            pool.transform.localScale = new Vector3(max.x - min.x + radius * 2f, 1f, max.z - min.z + radius * 2f);
            pool.AddComponent<MeshFilter>().sharedMesh = lavaMesh;
            var surface = pool.AddComponent<MeshRenderer>();
            surface.sharedMaterial = lavaMaterial;
            surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            surface.receiveShadows = false;
            lavaProperties.Clear();
            lavaProperties.SetVectorArray(LavaCenters, lavaCenters);
            surface.SetPropertyBlock(lavaProperties);
            return surface;
        }

        private static Mesh BuildLavaMesh()
        {
            const int cells = 48;
            const int stride = cells + 1;
            var vertices = new Vector3[stride * stride];
            var triangles = new int[cells * cells * 6];
            int next = 0;
            for (int row = 0; row <= cells; row++)
            for (int col = 0; col <= cells; col++)
            {
                int index = row * stride + col;
                vertices[index] = new Vector3(col / (float)cells - .5f, 0f, row / (float)cells - .5f);
                if (row == cells || col == cells) continue;
                triangles[next++] = index;
                triangles[next++] = index + stride;
                triangles[next++] = index + 1;
                triangles[next++] = index + 1;
                triangles[next++] = index + stride;
                triangles[next++] = index + stride + 1;
            }

            var mesh = new Mesh { name = "Lava pool relief", vertices = vertices, triangles = triangles };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, .5f, 1f));
            mesh.UploadMeshData(true);
            return mesh;
        }

        internal void UpdateLavaPool(MeshRenderer surface, float age, float pulse)
        {
            surface.GetPropertyBlock(lavaProperties);
            lavaProperties.SetFloat(LavaAge, age);
            lavaProperties.SetFloat(LavaPulseAmount, pulse);
            surface.SetPropertyBlock(lavaProperties);
        }

        internal void LavaExtinguish(Vector3 center)
        {
            if (material != null) LavaDroplets(center, 3, .23f);
        }

        private void LavaDroplets(Vector3 center, int count, float height)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.4f;
                var edge = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * TimelineCaveHazards.LavaRadius;
                var from = center + edge * .25f + Vector3.up * .09f;
                var to = center + edge * (.52f + .12f * (i % 3)) + Vector3.up * (height + .07f * (i % 3));
                AddStroke(from, to, 0f, .26f + .045f * (i % 3), .052f, i % 2 == 0 ? LavaGold : LavaOrange);
            }
        }

        internal void LanternHeal(EntityQizi owner)
        {
            if (owner.GObj == null) return;
            var lamp = owner.GObj.transform.Find("Body/Paper body/Frosted watch lantern");
            var center = owner.LogicPosition;
            var light = lamp != null ? lamp.position : center + Vector3.up * 1.4f;
            if (combatVfx != null)
            {
                combatVfx.LanternHeal(center, light, owner.LanternHitsLeft);
                return;
            }
            if (material == null) return;
            var jade = new Color(.32f, 1f, .61f);
            Ring(center, .98f, .65f, .14f, jade);
            Ring(center + Vector3.up * .17f, .72f, .46f, .11f, LanternGold);
            Slash?.StatusBurst(light, LanternGold, 1.7f, .34f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                var edge = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var start = center + edge * .85f + Vector3.up * .10f;
                AddStroke(start, center + edge * .21f + Vector3.up * (1.55f + i % 3 * .18f),
                    0f, .52f + i % 3 * .09f, .14f, jade);
            }

            // A gold-green plus makes the heal readable even when the models overlap.
            var marker = light + Vector3.up * .31f;
            var across = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            HealStroke(Vector3.up);
            HealStroke(across);

            void HealStroke(Vector3 direction)
            {
                var line = CreateLine("续灯守夜·回血", transform, false);
                line.positionCount = 2;
                line.widthMultiplier = .11f;
                line.SetPosition(0, marker - direction * .24f);
                line.SetPosition(1, marker + direction * .24f);
                StartCoroutine(FadeCombatStroke(line, jade, .58f));
            }
        }

        private const float LanternRingRadius = .74f;

        private LineRenderer LanternWarning(Transform anchor, Vector3 center)
        {
            var ring = OuterRing("续灯守夜", anchor, center, LanternRingRadius, LanternGold);
            for (int i = 0; i < TimelineOuterGuardianCast.LanternHitsToInterrupt; i++)
            {
                var flame = CreateLine("续灯守夜·灯芯", ring.transform, false);
                flame.positionCount = 9;
                flame.widthMultiplier = .15f;
                flame.widthCurve = BrushTaper;
            }
            UpdateLanternWarning(ring, center, TimelineOuterGuardianCast.LanternHitsToInterrupt, 0f);
            return ring;
        }

        internal static void UpdateLanternWarning(LineRenderer ring, Vector3 center, int hitsLeft, float progress)
        {
            var tint = hitsLeft > 0 ? LanternGold : new Color(.43f, .48f, .51f);
            tint.a = hitsLeft > 0 ? Mathf.Lerp(.64f, 1f, progress) : .55f;
            ring.startColor = ring.endColor = tint;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / ring.positionCount);
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * LanternRingRadius, .10f,
                    Mathf.Sin(angle) * LanternRingRadius));
            }

            int count = ring.transform.childCount;
            for (int i = 0; i < count; i++)
            {
                var flame = ring.transform.GetChild(i).GetComponent<LineRenderer>();
                flame.enabled = i < hitsLeft;
                if (!flame.enabled) continue;
                float angle = i * (2f * Mathf.PI / count);
                var foot = center + new Vector3(Mathf.Cos(angle) * .49f, .12f, Mathf.Sin(angle) * .49f);
                flame.startColor = flame.endColor = tint;
                var toward = (center - foot);
                toward.y = 0f;
                var tangent = Vector3.Cross(Vector3.up, toward);
                for (int j = 0; j < flame.positionCount; j++)
                {
                    float t = j / (flame.positionCount - 1f);
                    flame.SetPosition(j, foot + Vector3.up * (t * Mathf.Lerp(.24f, .78f, progress))
                        + toward * (t * t * .33f)
                        + tangent * (Mathf.Sin(t * 7f + progress * 7f + i) * t * .23f));
                }
            }
        }

        private LineRenderer ShellArc(Transform anchor, Vector3 center, Vector3 forward)
        {
            var arc = CreateLine("合壳听潮·正面", anchor, true);
            arc.positionCount = 19;
            UpdateShellArc(arc, center, forward, 0f, active: false);
            return arc;
        }

        internal static void UpdateShellArc(LineRenderer arc, Vector3 center, Vector3 forward, float progress, bool active)
        {
            const float radius = .90f;
            var side = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < arc.positionCount; i++)
            {
                float angle = (i / (float)(arc.positionCount - 1) - .5f) * Mathf.PI;
                arc.SetPosition(i, center + Vector3.up * .12f +
                    (forward * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * radius);
            }
            var tint = ShellGreen;
            tint.a = active ? Mathf.Lerp(.45f, 1f, progress) : Mathf.Lerp(.48f, .86f, progress);
            arc.startColor = arc.endColor = tint;
            arc.widthMultiplier = active ? .115f : .064f;
        }

        private Mesh shellShieldMesh;

        internal void ShowShellGuard(EntityQizi owner)
        {
            if (owner.GObj == null) return;
            if (combatVfx != null)
            {
                combatVfx.GuardianShellGuard(owner);
                return;
            }
            if (material == null) return;
            var model = owner.GObj.transform;
            var shield = new GameObject("合壳听潮·正面护罩");
            shield.transform.SetParent(transform, false);
            shield.AddComponent<MeshFilter>().sharedMesh = shellShieldMesh ??= BuildShellShieldMesh();
            var renderer = shield.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var arc = ShellArc(shield.transform, owner.LogicPosition, owner.OuterShellForward);
            StartCoroutine(AnimateShellGuard(owner, model, shield, arc));
        }

        internal void ShellClash(Vector3 position, Vector3 outward)
        {
            combatVfx?.GuardianShellClash(position, outward);
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.ShieldImpact, position);
        }

        internal void SaltNeedleHit(Vector3 position, Vector3 forward)
        {
            combatVfx?.GuardianSaltHit(position, forward);
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.WaterImpact, position);
        }

        private IEnumerator AnimateShellGuard(EntityQizi owner, Transform model, GameObject shield, LineRenderer arc)
        {
            while (shield != null && model != null && owner.IsValid && owner.OuterShellSeconds > 0f)
            {
                shield.transform.SetPositionAndRotation(owner.LogicPosition, Quaternion.LookRotation(owner.OuterShellForward));
                UpdateShellArc(arc, owner.LogicPosition, owner.OuterShellForward,
                    owner.OuterShellSeconds / TimelineOuterGuardianCast.ShellDuration, active: true);
                yield return null;
            }
            if (shield != null) Destroy(shield);
        }

        private static Mesh BuildShellShieldMesh()
        {
            const int columns = 24, rows = 10;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var colors = new Color[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var indices = new int[columns * rows * 6];
            for (int row = 0; row <= rows; row++)
            for (int column = 0; column <= columns; column++)
            {
                int index = row * (columns + 1) + column;
                float vertical = row / (float)rows;
                float angle = (column / (float)columns - .5f) * Mathf.PI;
                float radius = .90f * Mathf.Sqrt(1f - .96f * vertical * vertical);
                vertices[index] = new Vector3(Mathf.Sin(angle) * radius, .11f + vertical * 1.34f,
                    Mathf.Cos(angle) * radius);
                bool rim = row is 0 or rows || column is 0 or columns;
                bool rib = row == 4 || column % 6 == 0;
                colors[index] = rim ? new Color(.58f, 1f, .91f, .73f)
                    : rib ? new Color(.35f, .95f, .82f, .42f) : new Color(.11f, .73f, .66f, .22f);
                uvs[index] = new Vector2(column / (float)columns, .5f);
                if (row == rows || column == columns) continue;
                int nextRow = index + columns + 1;
                int triangle = (row * columns + column) * 6;
                indices[triangle] = index;
                indices[triangle + 1] = nextRow;
                indices[triangle + 2] = index + 1;
                indices[triangle + 3] = nextRow;
                indices[triangle + 4] = nextRow + 1;
                indices[triangle + 5] = index + 1;
            }

            var mesh = new Mesh { name = "Tidebone front shell shield" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            return mesh;
        }

        private LineRenderer OuterRing(string label, Transform anchor, Vector3 point, float radius, Color tint)
        {
            var line = CreateLine(label, anchor, true);
            line.loop = true;
            line.positionCount = 36;
            line.widthMultiplier = .074f;
            for (int i = 0; i < line.positionCount; i++)
            {
                float a = Mathf.PI * 2f * i / line.positionCount;
                line.SetPosition(i, point + new Vector3(radius * Mathf.Cos(a), .10f, radius * Mathf.Sin(a)));
            }
            line.startColor = line.endColor = tint;
            return line;
        }
    }
}
