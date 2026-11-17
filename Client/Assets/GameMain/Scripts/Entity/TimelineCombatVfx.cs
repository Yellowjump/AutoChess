using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Entity
{
    internal sealed partial class TimelineCombatVfx : MonoBehaviour
    {
        private const int MaxEffects = 48;
        private const int Samples = 29;
        private static readonly Color Gold = new Color(1f, .56f, .10f);
        private static readonly Color FireCore = new Color(1f, .93f, .66f);
        private static readonly Color Indigo = new Color(.24f, .28f, 1f);
        private static readonly Color Ice = new Color(.39f, .77f, 1f);
        private static readonly Color Silver = new Color(.88f, .97f, 1f);
        private const float BrushProfile = -1f;
        private const float WaterProfile = -2f;
        private const float SoftInkProfile = -5f;
        private static readonly Color Copper = new Color(.88f, .42f, .16f);

        internal const float SealDropTime = .16f;
        private static readonly Color Ink = new Color(.057f, .046f, .042f);
        private static readonly Color Slate = new Color(.24f, .213f, .18f);
        private static readonly Color Dust = new Color(.49f, .43f, .34f);
        private static readonly Color SealInk = new Color(.39f, .041f, .025f);
        private static readonly Color Cinnabar = new Color(.98f, .20f, .12f);
        private static readonly Vector2[][] SealMarks =
        {
            new[] { new Vector2(-.69f, .65f), new Vector2(-.11f, .61f), new Vector2(-.61f, .19f), new Vector2(-.58f, -.44f) },
            new[] { new Vector2(.16f, .59f), new Vector2(.64f, .55f), new Vector2(.45f, .10f), new Vector2(.67f, -.32f) },
            new[] { new Vector2(-.56f, -.63f), new Vector2(.11f, -.33f), new Vector2(.04f, -.66f), new Vector2(.68f, -.59f) }
        };

        private enum Kind
        {
            Fire, Lightning, Slash, Detonation, Shield, Stone, Seal, Pounce, Kiln, Contract,
            Rope, Kite, Marten, PlayerHit, SaltNeedle, SaltShatter, ShellGuard, ShellClash, Paper,
            Undertow, LanternHeal, LanternSnuff
        }

        private struct Effect
        {
            public Kind Kind;
            public Vector3 From;
            public Vector3 To;
            public float Start;
            public float Duration;
            public float Strength;
            public EntityQizi Owner;
        }

        private readonly List<Effect> effects = new List<Effect>(MaxEffects);
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> uv = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> indices = new List<int>();
        private readonly Vector3[] path = new Vector3[Samples];
        private readonly float[] widths = new float[Samples];
        private Mesh mesh;
        private Material material;
        private Matrix4x4 worldToLocal;
        private Vector3 cameraPosition;
        private Vector3 cameraRight;
        private Vector3 cameraUp;
        private IReadOnlyList<EntityQizi> tidegateFighters;
        private float tidegateFloor;

        internal static TimelineCombatVfx Create(Transform parent)
        {
            var shader = Resources.Load<Shader>("TimelineCombatEnergy");
            if (shader == null || !shader.isSupported) return null;

            var root = new GameObject("轮回战斗特效");
            root.transform.SetParent(parent, false);
            var vfx = root.AddComponent<TimelineCombatVfx>();
            vfx.mesh = new Mesh { name = "Timeline Combat Energy" };
            vfx.mesh.MarkDynamic();
            vfx.material = new Material(shader);
            root.AddComponent<MeshFilter>().sharedMesh = vfx.mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = vfx.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return vfx;
        }

        internal void FireSpread(Vector3 from, Vector3 to) =>
            Add(Kind.Fire, from, to, .70f, 1f, 0f);

        internal void LightningStrike(Vector3 from, Vector3 to, int hop) =>
            Add(Kind.Lightning, from, to, hop == 0 ? .67f : .36f, 1f - .23f * hop, .055f * hop);

        internal void SlashImpact(Vector3 position, Vector3 direction) =>
            Add(Kind.Slash, position, direction, .39f, 1f, 0f);

        internal void SetTidegatePresentation(IReadOnlyList<EntityQizi> fighters, float boardHeight)
        {
            tidegateFighters = fighters;
            tidegateFloor = boardHeight - .014f;
        }

        internal void PlayerHit(Vector3 position, Vector3 direction) =>
            Add(Kind.PlayerHit, position, direction, .28f, 1f, 0f);

        internal void FireDetonation(Vector3 position, float radius) =>
            Add(Kind.Detonation, position, position, .84f, radius, 0f);

        internal void ShieldRise(Vector3 position) =>
            Add(Kind.Shield, position, position, .77f, 1f, 0f);

        internal void StoneCrash(Vector3 position, float radius) =>
            Add(Kind.Stone, position, position, .69f, radius, 0f);

        internal void StampSeal(Vector3 position) =>
            Add(Kind.Seal, position, position, .78f, 1f, 0f);

        private void Add(Kind kind, Vector3 from, Vector3 to, float duration, float strength, float delay,
            EntityQizi owner = null)
        {
            if (effects.Count == MaxEffects) effects.RemoveAt(0);
            effects.Add(new Effect
            {
                Kind = kind, From = from, To = to, Start = Time.time + delay,
                Duration = duration, Strength = strength, Owner = owner
            });
        }

        private void LateUpdate()
        {
            if (tidegateFighters == null && effects.Count == 0 && vertices.Count == 0) return;
            var camera = Camera.main;
            DrawFrame(Time.time, camera != null ? camera.transform : transform);
        }

        private void DrawFrame(float time, Transform view)
        {
            vertices.Clear();
            uv.Clear();
            colors.Clear();
            indices.Clear();
            worldToLocal = transform.worldToLocalMatrix;
            cameraPosition = view.position;
            cameraRight = view.right;
            cameraUp = view.up;

            if (tidegateFighters != null)
                foreach (var fighter in tidegateFighters)
                {
                    if (!fighter.IsValid || fighter.GObj == null || !fighter.GObj.activeInHierarchy) continue;
                    DrawTidegateContact(fighter.GObj.transform.position, fighter.UsesLargeFootprint ? 1.26f : .54f);
                }

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                float age = time - effect.Start;
                if (age < 0f) continue;
                if (age >= effect.Duration ||
                    (effect.Owner != null && (!effect.Owner.IsValid || effect.Owner.OuterShellSeconds <= 0f)))
                {
                    effects.RemoveAt(i--);
                    continue;
                }

                switch (effect.Kind)
                {
                    case Kind.Fire: DrawFire(effect, age); break;
                    case Kind.Lightning: DrawLightning(effect, age); break;
                    case Kind.Slash: DrawSlash(effect, age); break;
                    case Kind.Detonation: DrawDetonation(effect, age); break;
                    case Kind.Shield: DrawShield(effect, age); break;
                    case Kind.Stone: DrawStone(effect, age); break;
                    case Kind.Seal: DrawSeal(effect, age); break;
                    case Kind.Pounce: DrawPounce(effect, age); break;
                    case Kind.Kiln: DrawKiln(effect, age); break;
                    case Kind.Contract: DrawContract(effect, age); break;
                    case Kind.Rope: DrawRope(effect, age); break;
                    case Kind.Kite: DrawKite(effect, age); break;
                    case Kind.Marten: DrawMarten(effect, age); break;
                    case Kind.PlayerHit: DrawPlayerHit(effect, age); break;
                    case Kind.SaltNeedle: DrawSaltNeedle(effect, age); break;
                    case Kind.SaltShatter: DrawSaltShatter(effect, age, SaltCrystal); break;
                    case Kind.ShellGuard: DrawShellGuard(effect, age); break;
                    case Kind.ShellClash: DrawSaltShatter(effect, age, Ice); break;
                    case Kind.Paper: DrawPaperSummon(effect, age); break;
                    case Kind.Undertow: DrawUndertow(effect, age); break;
                    case Kind.LanternHeal: DrawLanternHeal(effect, age); break;
                    case Kind.LanternSnuff: DrawLanternSnuff(effect, age); break;
                }
            }

            mesh.Clear();
            if (vertices.Count == 0) return;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
        }

        private void DrawFire(Effect effect, float age)
        {
            const float travel = .28f;
            Vector3 start = effect.From + Vector3.up * .24f;
            Vector3 end = effect.To + Vector3.up * .32f;
            Vector3 forward = (end - start).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float head = Mathf.Clamp01(age / travel);
            float tail = Mathf.Clamp01((age - .12f) / .33f);
            if (head > tail)
            {
                for (int i = 0; i < Samples; i++)
                {
                    float u = i / (Samples - 1f);
                    float t = Mathf.Lerp(tail, head, u);
                    float arc = Mathf.Sin(t * Mathf.PI);
                    path[i] = Vector3.Lerp(start, end, t)
                        + Vector3.up * (arc * (.85f + .29f * Mathf.Sin(t * 10f - age * 8f)))
                        + side * (arc * Mathf.Sin(t * 8f) * .33f);
                    widths[i] = Mathf.Sin(u * Mathf.PI) * (.76f + .24f * Mathf.Sin(t * 16f)) + .02f;
                }
                FireRibbon(.97f, Fade(age - travel, .25f));
            }

            if (age < travel) return;
            float impact = age - travel;
            DrawFlames(effect.To, forward, impact, .94f, 4);
            Sparks(end, side, impact, .36f, 10, Gold, 1.35f);
        }

        private void DrawDetonation(Effect effect, float age)
        {
            Vector3 center = effect.From;
            Vector3 front = Vector3.ProjectOnPlane(cameraPosition - center, Vector3.up).normalized;
            float scale = effect.Strength * .83f;
            DrawFlames(center, front, age, scale, 7);
            Flare(center + Vector3.up * .25f, scale * .80f, scale * .52f, Gold, Fade(age, .25f));
            Sparks(center + Vector3.up * .28f, front, age, .62f, 20, Gold, scale * 2.05f);
        }

        private void DrawFlames(Vector3 center, Vector3 front, float age, float scale, int count)
        {
            float rise = Mathf.SmoothStep(.23f, 1f, age / .19f);
            float fade = Fade(age - .11f, .52f);
            for (int tongue = 0; tongue < count; tongue++)
            {
                float angle = Mathf.Lerp(-106f, 148f, tongue / (count - 1f));
                Vector3 outward = Quaternion.AngleAxis(angle, Vector3.up) * -front;
                Vector3 side = Vector3.Cross(Vector3.up, outward);
                float near = Mathf.Max(0f, Vector3.Dot(front, outward));
                float height = scale * (.70f + .27f * Mathf.Sin(tongue * 2.37f)) * rise
                    * Mathf.Lerp(1.16f, .47f, near);
                float reach = scale * (.52f + (tongue % 3) * .18f) * rise;
                for (int i = 0; i < Samples; i++)
                {
                    float t = i / (Samples - 1f);
                    float arc = Mathf.Sin(t * Mathf.PI);
                    path[i] = center + Vector3.up * (.08f + height * t)
                        + outward * (scale * .1f + reach * t * t)
                        + side * (scale * .26f * arc * Mathf.Cos(t * 4.6f + tongue + age * 6f));
                    widths[i] = arc * (1f - t * .37f) * (.88f + .12f * Mathf.Sin(t * 13f + tongue));
                }
                FireRibbon(scale * (.65f + .14f * Mathf.Cos(tongue * 2.3f)), fade);
            }
        }

        private void FireRibbon(float width, float alpha)
        {
            Ribbon(Samples, width * 1.23f, new Color(.57f, .09f, .045f), alpha, BrushProfile);
            Ribbon(Samples, width * .90f, new Color(1f, .37f, .11f), alpha, BrushProfile);
            Ribbon(Samples, width * .44f, FireCore, alpha, BrushProfile);
        }

        private void DrawLightning(Effect effect, float age)
        {
            float strength = effect.Strength;
            Vector3 from = effect.From + Vector3.up * .25f;
            Vector3 to = effect.To + Vector3.up * .13f;
            Vector3 axis = (from - to).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            if (side.sqrMagnitude < .01f) side = cameraRight;
            side.Normalize();
            float flash = Fade(age, .27f) * strength;
            bool primary = strength > .95f;
            if (flash > 0f)
            {
                const int boltPoints = 11;
                float phase = Mathf.Floor(age * 12f) * .92f;
                for (int i = 0; i < boltPoints; i++)
                {
                    float t = i / (boltPoints - 1f);
                    float arc = Mathf.Sin(t * Mathf.PI);
                    float jitter = Mathf.Sin(i * 2.54f + phase) * .77f + Mathf.Cos(i * 1.79f) * .23f;
                    path[i] = Vector3.Lerp(to, from, t) + side * (arc * jitter * (primary ? .63f : .34f))
                        + Vector3.up * (primary ? 0f : arc * (.18f + jitter * .14f));
                    widths[i] = (.43f + .57f * Mathf.Abs(Mathf.Sin(i * 1.15f + .6f)))
                        * Mathf.Lerp(.54f, 1.25f, t);
                }

                ThunderRibbon(boltPoints, primary ? .80f : .40f, flash);
                Vector3 fork = path[3];
                for (int branch = 0; branch < 3; branch++)
                {
                    Vector3 direction = Quaternion.AngleAxis(48f + branch * 92f, axis) * side;
                    DrawThunderShard(fork, direction * (.64f + branch * .16f) + axis * .55f,
                        .19f * strength, flash, branch);
                }
            }

            float impact = Fade(age, .41f) * strength;
            float reach = Mathf.Lerp(.48f, 1.58f, Mathf.Sqrt(age / effect.Duration)) * strength;
            for (int ray = 0; ray < (primary ? 6 : 3); ray++)
            {
                Vector3 direction = Quaternion.AngleAxis(29f + ray * 137.5f, Vector3.up) * side;
                DrawThunderShard(to, direction * reach + Vector3.up * (reach * (.29f + ray % 3 * .36f)),
                    (.27f + ray % 3 * .08f) * strength, impact, ray);
            }
            Flare(to, .62f * strength, .42f * strength, Indigo, Fade(age, .23f));
            Sparks(to, side, age, .38f, 9, Ice, 1.8f * strength);
        }

        private void DrawThunderShard(Vector3 origin, Vector3 offset, float width, float alpha, int seed)
        {
            if (alpha <= 0f) return;
            path[0] = origin;
            path[1] = origin + offset * .16f;
            path[2] = origin + offset * .55f + cameraRight * (.12f * Mathf.Cos(seed * 3.4f));
            path[3] = origin + offset;
            widths[0] = .24f;
            widths[1] = 1f;
            widths[2] = .54f;
            widths[3] = 0f;
            ThunderRibbon(4, width, alpha);
        }

        private void ThunderRibbon(int count, float width, float alpha)
        {
            Ribbon(count, width * 1.34f, new Color(.19f, .10f, .50f), alpha, BrushProfile);
            Ribbon(count, width, new Color(.46f, .32f, .97f), alpha, BrushProfile);
            Ribbon(count, width * .53f, Silver, alpha, BrushProfile);
        }


        private void DrawPlayerHit(Effect effect, float age)
        {
            Vector3 view = (cameraPosition - effect.From).normalized;
            Vector3 away = effect.To.sqrMagnitude > .01f ? effect.To : cameraRight;
            Vector3 center = effect.From + view * .27f;
            float flash = Fade(age, .14f);
            Flare(center, .39f, .33f, Cinnabar, flash);
            Flare(center, .19f, .17f, FireCore, flash);

            float fade = Fade(age - .045f, .23f);
            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.Lerp(-73f, 73f, i / 6f);
                Vector3 velocity = Quaternion.AngleAxis(angle, Vector3.up) * away * (1.8f + i % 3 * .61f)
                    + Vector3.up * (.82f + i % 4 * .44f);
                Vector3 tip = center + velocity * (age + .033f) - Vector3.up * (age * age * 4.4f);
                path[0] = tip - velocity * (.049f + i % 3 * .017f);
                path[1] = tip;
                widths[0] = .045f;
                widths[1] = .90f;
                Ribbon(2, .101f, Cinnabar, fade, BrushProfile);
                Ribbon(2, .048f, FireCore, fade * .88f, BrushProfile);
            }
        }

        private void DrawSlash(Effect effect, float age)
        {
            Vector3 forward = Vector3.ProjectOnPlane(effect.To, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            Vector3 across = Vector3.Cross(Vector3.up, forward);
            if (Vector3.Dot(across, cameraRight) < 0f) across = -across;
            Vector3 sweep = (across * .84f + Vector3.up * .54f + forward * .35f).normalized;
            Vector3 curve = (Vector3.up * .78f - across * .53f + forward * .35f).normalized;
            if (tidegateFighters != null)
            {
                Vector3 hit = effect.From - forward * .30f;
                float revealInk = Mathf.Lerp(.46f, 1f, Mathf.Clamp01(age / .07f));
                float fadeInk = Fade(age - .025f, .22f);
                for (int i = 0; i < Samples; i++)
                {
                    float u = i / (Samples - 1f);
                    float t = u * revealInk;
                    path[i] = hit + sweep * ((t - .73f) * 1.50f)
                        + curve * (.28f * (Mathf.Sin(t * Mathf.PI) - .75f));
                    widths[i] = Mathf.Sin(u * Mathf.PI) * Mathf.Lerp(1f, .42f, u) + .02f;
                }
                Ribbon(Samples, .39f, Ink, fadeInk * .82f, BrushProfile, bias: -.22f);
                Ribbon(Samples, .093f, FireCore, fadeInk * .91f, BrushProfile, bias: .23f);
                Flare(hit, .19f, .15f, FireCore, Fade(age, .085f));
                Sparks(hit, forward, age, .24f, 5, Copper, 1.18f);
                return;
            }
            float reveal = Mathf.Lerp(.35f, 1f, Mathf.SmoothStep(0f, 1f, age / .10f));
            float fade = Fade(age - .03f, .30f);
            Vector3 center = effect.From + forward * (age * .78f);

            for (int i = 0; i < Samples; i++)
            {
                float u = i / (Samples - 1f);
                float t = u * reveal;
                float arc = Mathf.Sin(t * Mathf.PI);
                path[i] = center + sweep * ((t - .46f) * 2.32f) + curve * (.52f * arc);
                widths[i] = Mathf.Sin(u * Mathf.PI) * Mathf.Lerp(1.35f, .36f, u) + .018f;
            }
            Ribbon(Samples, .83f, Ink, fade, BrushProfile, bias: -.34f);
            Ribbon(Samples, .21f, FireCore, fade, BrushProfile, bias: .23f);

            for (int i = 0; i < Samples; i++)
            {
                float u = i / (Samples - 1f);
                path[i] = path[i] - curve * (.34f + .11f * u) - sweep * .20f;
                widths[i] *= Mathf.Sin(Mathf.PI * u);
            }
            Ribbon(Samples, .135f, Gold, fade * .85f, BrushProfile);
            Sparks(center, sweep, age, .32f, 9, Gold, 1.7f);
        }

        private void DrawTidegateContact(Vector3 position, float radius)
        {
            var center = new Vector3(position.x + radius * .08f, tidegateFloor, position.z + radius * .10f);
            var x = Vector3.right * radius;
            var z = Vector3.forward * (radius * .69f);
            int first = vertices.Count;
            AddVertex(center - x - z, 0f, 0f, SoftInkProfile, Ink, .59f);
            AddVertex(center - x + z, 0f, 1f, SoftInkProfile, Ink, .59f);
            AddVertex(center + x - z, 1f, 0f, SoftInkProfile, Ink, .59f);
            AddVertex(center + x + z, 1f, 1f, SoftInkProfile, Ink, .59f);
            AddQuad(first);
        }

        private void DrawStone(Effect effect, float age)
        {
            Vector3 center = effect.From + Vector3.up * .045f;
            float radius = effect.Strength;
            float reveal = Mathf.SmoothStep(.24f, 1f, age / .13f);
            float fade = Fade(age - .14f, .54f);
            for (int ray = 0; ray < 7; ray++)
            {
                Vector3 direction = Quaternion.AngleAxis(ray * 137.5f + 16f, Vector3.up) * Vector3.right;
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                float reach = radius * (1.03f + .23f * (ray % 4)) * reveal;
                DrawFissure(center, direction, side, reach, .15f, ray, fade);
                Vector3 fork = center + direction * (reach * .38f)
                    + side * (Mathf.Sin(ray * 3.7f + 1.9f) * reach * .13f);
                Vector3 branch = (direction + side * (ray % 2 == 0 ? .84f : -.72f)).normalized;
                DrawFissure(fork, branch, Vector3.Cross(Vector3.up, branch), reach * .50f, .07f, ray + 4, fade);
            }

            for (int i = 0; i < 11; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(18f + i * 137.5f, Vector3.up) * Vector3.right;
                float time = age + i % 3 * .020f;
                float height = Mathf.Max(0f, time * (2.4f + i % 4 * .66f) - 5.4f * time * time);
                Vector3 foot = center + direction * (radius * (.32f + time * (1.65f + i % 3 * .38f)));
                Vector3 shard = foot + Vector3.up * (height * radius);
                float size = radius * (i < 5 ? .62f + i % 3 * .085f : .13f + i % 3 * .037f);
                if (i < 6)
                    Flare(foot + Vector3.up * .13f, radius * (.48f + age * .47f), radius * .22f,
                        Dust, Fade(age - .07f, .55f) * .48f, profile: -5f);
                DrawStoneShard(shard, direction, size, i * .84f + age * 7f, fade);
            }
        }

        private void DrawFissure(Vector3 center, Vector3 forward, Vector3 side, float reach,
            float width, int seed, float fade)
        {
            const int points = 6;
            for (int i = 0; i < points; i++)
            {
                float t = i / (points - 1f);
                float bend = i == 0 ? 0f : Mathf.Sin(i * 1.9f + seed * 3.7f) * reach * .13f;
                path[i] = center + forward * (t * reach) + side * bend;
                widths[i] = (1f - t) * (.74f + .26f * Mathf.Cos(i * 2.6f)) + .025f;
            }
            Ribbon(points, width * 1.5f, Ink, fade, BrushProfile, Vector3.up);
            Ribbon(points, width, Copper, fade, BrushProfile, Vector3.up);
        }

        private void DrawStoneShard(Vector3 center, Vector3 direction, float size, float spin, float fade)
        {
            Quaternion rotation = Quaternion.AngleAxis(spin * Mathf.Rad2Deg, direction);
            Vector3 right = rotation * Vector3.Cross(Vector3.up, direction);
            Vector3 ridge = rotation * (Vector3.up * .94f + direction * .45f) * size;
            Vector3 a = center - direction * size * .61f;
            Vector3 b = center + right * (size * .54f);
            Vector3 c = center + direction * (size * .82f);
            Vector3 d = center - right * (size * .40f);
            Vector3 tip = center + ridge;

            Triangle(a, b, tip, Slate, fade);
            Triangle(b, c, tip, Dust, fade);
            Triangle(c, d, tip, Slate, fade);
            Triangle(d, a, tip, Ink, fade);
            Triangle(a, d, c, Slate, fade);
            Triangle(a, c, b, Slate, fade);
        }

        private void DrawSeal(Effect effect, float age)
        {
            Vector3 center = effect.From + Vector3.up * .065f;
            Vector3 x = Quaternion.AngleAxis(-16f, Vector3.up) * Vector3.right;
            Vector3 z = Vector3.Cross(x, Vector3.up);
            if (age < SealDropTime)
            {
                float t = age / SealDropTime;
                Vector3 elevated = center + Vector3.up * (1.93f * (1f - t * t));
                Vector3 raised = Vector3.Lerp(Vector3.up, z, t * t).normalized;
                DrawSealMark(elevated, x, raised, Mathf.Lerp(.97f, .79f, t), .82f);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = SealCorner(center, x, z, i, .68f);
                    path[0] = corner + Vector3.up * (.45f + (1f - t) * .8f);
                    path[1] = corner + Vector3.up * (.04f + (1f - t) * .35f);
                    widths[0] = .11f;
                    widths[1] = .92f;
                    Ribbon(2, .11f, Cinnabar, t, BrushProfile);
                }
                return;
            }

            float impact = age - SealDropTime;
            float fade = Fade(impact - .08f, .52f);
            float rebound = Fade(impact, .12f);
            float size = .89f + .14f * rebound;
            DrawSealMark(center, x, z, size, fade);
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = SealCorner(center, x, z, i, size);
                Vector3 outward = (corner - center).normalized;
                Sparks(corner, outward, impact, .34f, 4, Cinnabar, 1.05f);
                DrawSealSpatter(corner, outward, impact, fade);
            }
        }

        private void DrawSealMark(Vector3 center, Vector3 x, Vector3 z, float size, float fade)
        {
            for (int i = 0; i < 5; i++)
            {
                path[i] = SealCorner(center, x, z, i % 4, size);
                widths[i] = 1f;
            }
            Vector3 normal = Vector3.Cross(x, z).normalized;
            Ribbon(5, .24f, SealInk, fade, BrushProfile, normal);
            Ribbon(5, .105f, Cinnabar, fade, BrushProfile, normal);

            for (int i = 0; i < 3; i++)
            {
                Vector2[] mark = SealMarks[i];
                for (int j = 0; j < mark.Length; j++)
                {
                    path[j] = center + (x * mark[j].x + z * mark[j].y) * size;
                    widths[j] = j == mark.Length - 1 ? .15f : 1f;
                }
                Ribbon(mark.Length, .265f, SealInk, fade * .91f, BrushProfile, normal);
                Ribbon(mark.Length, .15f, Cinnabar, fade, BrushProfile, normal);
            }
        }

        private static Vector3 SealCorner(Vector3 center, Vector3 x, Vector3 z, int index, float size) =>
            center + (x * (index == 0 || index == 3 ? -1f : 1f) + z * (index < 2 ? -1f : 1f)) * size;

        private void DrawSealSpatter(Vector3 center, Vector3 outward, float age, float fade)
        {
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            float reach = Mathf.Lerp(.19f, .64f, Mathf.SmoothStep(0f, 1f, age / .15f));
            path[0] = center;
            path[1] = center + outward * (reach * .35f) + Vector3.up * .10f;
            path[2] = center + outward * (reach * .72f) + side * .11f + Vector3.up * .17f;
            path[3] = center + outward * reach + side * .075f + Vector3.up * .22f;
            widths[0] = .42f;
            widths[1] = 1f;
            widths[2] = .43f;
            widths[3] = .035f;
            Ribbon(4, .23f, SealInk, fade, BrushProfile);
            Ribbon(4, .09f, Cinnabar, fade, BrushProfile);
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color tint, float fade)
        {
            if (Vector3.Dot(Vector3.Cross(c - a, b - a), cameraPosition - a) <= 0f) return;
            int first = vertices.Count;
            AddVertex(a, 0f, .5f, BrushProfile, tint, fade);
            AddVertex(b, .5f, .5f, BrushProfile, tint, fade);
            AddVertex(c, 1f, .5f, BrushProfile, tint, fade);
            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
        }


        private void DrawShield(Effect effect, float age)
        {
            Vector3 center = effect.From;
            float rise = Mathf.SmoothStep(.20f, 1f, age / .32f);
            float fade = Mathf.SmoothStep(0f, 1f, age / .08f) * Fade(age - .18f, .59f);
            Vector3 forward = Vector3.ProjectOnPlane(cameraPosition - center, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            DrawWaterCaustic(center + Vector3.up * .06f, 1.30f * rise, fade);
            DrawWaterSheet(center, forward, side, rise, age, 1.28f, 3.65f, 1.11f, fade);
            DrawWaterSheet(center, forward, side, rise, age, -1.34f, 3.48f, 1.07f, fade * .87f);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * 2.4f + age * 3f;
                float radius = .76f + rise * .42f + i % 3 * .10f;
                Vector3 tip = center + new Vector3(Mathf.Cos(angle) * radius,
                    .10f + rise * (.29f + i % 5 * .27f), Mathf.Sin(angle) * radius);
                path[0] = tip - (Vector3.up + (tip - center).normalized) * .17f;
                path[1] = tip;
                widths[0] = .06f;
                widths[1] = .73f;
                Ribbon(2, .11f, new Color(.75f, 1f, .98f), fade, BrushProfile);
            }
        }

        private void DrawWaterCaustic(Vector3 center, float radius, float fade)
        {
            int first = vertices.Count;
            var tint = new Color(.21f, .57f, .64f);
            AddVertex(center + new Vector3(-radius, 0f, -radius), 0f, 0f, -4f, tint, fade);
            AddVertex(center + new Vector3(-radius, 0f, radius), 0f, 1f, -4f, tint, fade);
            AddVertex(center + new Vector3(radius, 0f, -radius), 1f, 0f, -4f, tint, fade);
            AddVertex(center + new Vector3(radius, 0f, radius), 1f, 1f, -4f, tint, fade);
            AddQuad(first);
        }

        private void DrawWaterSheet(Vector3 center, Vector3 forward, Vector3 side, float rise, float age,
            float start, float sweep, float radius, float fade)
        {
            int first = vertices.Count;
            for (int i = 0; i < Samples; i++)
            {
                float t = i / (Samples - 1f);
                float angle = start + sweep * t + age * .64f;
                Vector3 radial = forward * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                float arc = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), .68f);
                float crest = .08f + rise * arc * (1.38f + .09f * Mathf.Sin(t * 27f - age * 9f));
                float foot = Mathf.Max(.03f, crest - rise * arc * (.74f + .31f * (1f - t)));
                Vector3 bottom = center + radial * (radius * (1.10f - foot * .22f)) + Vector3.up * foot;
                Vector3 top = center + radial * (radius * (1.13f - crest * .59f)) + Vector3.up * crest;
                var tint = Color.Lerp(new Color(.11f, .51f, .60f), new Color(.28f, .86f, .84f), t);
                AddVertex(bottom, t, 0f, WaterProfile, tint, fade);
                AddVertex(top, t, 1f, WaterProfile, tint, fade);
                if (i > 0) AddQuad(first + i * 2 - 2);
            }
        }


        private void Sparks(Vector3 center, Vector3 direction, float age, float lifetime, int count, Color tint, float speed)
        {
            float fade = Fade(age, lifetime);
            if (fade <= 0f) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = Quaternion.AngleAxis(i * 137.5f, Vector3.up) * direction *
                    (speed * (.65f + i % 5 * .22f)) + Vector3.up * (1.05f + i % 4 * .64f);
                float time = age + i % 3 * .011f;
                Vector3 tip = center + velocity * time - Vector3.up * (4.1f * time * time);
                tip.y = Mathf.Max(center.y - .045f, tip.y);
                path[0] = tip - velocity * (.025f + i % 3 * .015f);
                path[1] = tip;
                widths[0] = .05f;
                widths[1] = .8f;
                float alpha = fade * (.65f + i % 3 * .17f);
                Ribbon(2, .046f + i % 4 * .014f, tint, alpha, profile: 2f);
                if (i % 3 == 0) Flare(tip, .15f, .12f, tint, alpha);
            }
        }

        private void Ribbon(int count, float width, Color tint, float alpha, float profile = 0f,
            Vector3 planeNormal = default, float bias = 0f)
        {
            int first = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 tangent = path[Mathf.Min(i + 1, count - 1)] - path[Mathf.Max(i - 1, 0)];
                Vector3 normal = planeNormal == Vector3.zero ? cameraPosition - path[i] : planeNormal;
                Vector3 across = Vector3.Cross(tangent, normal).normalized;
                Vector3 edge = across * (width * widths[i] * .5f);
                float u = i / (count - 1f);
                AddVertex(path[i] - edge * (1f - bias), u, 0f, profile, tint, alpha);
                AddVertex(path[i] + edge * (1f + bias), u, 1f, profile, tint, alpha);
                if (i > 0) AddQuad(first + i * 2 - 2);
            }
        }

        private void Flare(Vector3 center, float width, float height, Color tint, float alpha, float profile = 1f)
        {
            if (alpha <= 0f) return;
            Vector3 x = cameraRight * width;
            Vector3 y = cameraUp * height;
            int first = vertices.Count;
            AddVertex(center - x - y, 0f, 0f, profile, tint, alpha);
            AddVertex(center - x + y, 0f, 1f, profile, tint, alpha);
            AddVertex(center + x - y, 1f, 0f, profile, tint, alpha);
            AddVertex(center + x + y, 1f, 1f, profile, tint, alpha);
            AddQuad(first);
        }

        private void AddVertex(Vector3 position, float u, float v, float profile, Color tint, float alpha)
        {
            vertices.Add(worldToLocal.MultiplyPoint3x4(position));
            uv.Add(new Vector3(u, v, profile));
            tint.a = alpha;
            colors.Add(tint);
        }

        private void AddQuad(int first)
        {
            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
            indices.Add(first + 2);
            indices.Add(first + 1);
            indices.Add(first + 3);
        }

        private static float Fade(float age, float duration) =>
            1f - Mathf.SmoothStep(0f, 1f, age / duration);

        private void OnDestroy()
        {
            Destroy(mesh);
            Destroy(material);
        }
    }
}
