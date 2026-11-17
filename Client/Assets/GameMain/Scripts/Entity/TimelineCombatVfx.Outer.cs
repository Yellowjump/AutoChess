using UnityEngine;

namespace Entity
{
    internal sealed partial class TimelineCombatVfx
    {
        private static readonly Color RopeShadow = new Color(.27f, .15f, .081f);
        private static readonly Color RopeFiber = new Color(.90f, .68f, .36f);
        private static readonly Color ColdInk = new Color(.12f, .27f, .34f);
        private static readonly Color FrostEdge = new Color(.66f, .94f, .98f);
        private static readonly Color Feather = new Color(.17f, .23f, .32f);

        internal void GuardianRope(Vector3 origin, Vector3 forward, float length, float halfWidth) =>
            Add(Kind.Rope, origin, forward * length, .61f, halfWidth, 0f);

        internal void GuardianKite(Vector3 center, Vector3 forward, float radius) =>
            Add(Kind.Kite, center, forward, .73f, radius, 0f);

        internal void GuardianMarten(Vector3 center, Vector3 forward, float radius) =>
            Add(Kind.Marten, center, forward, .54f, radius, 0f);

        private void DrawRope(Effect effect, float age)
        {
            Vector3 origin = effect.From;
            float length = effect.To.magnitude;
            Vector3 forward = effect.To / length;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float tighten = Mathf.SmoothStep(0f, 1f, age / .10f);
            float retract = Mathf.SmoothStep(0f, .88f, (age - .18f) / .31f);
            float fade = Fade(age - .16f, .42f);

            for (int i = 0; i < Samples; i++)
            {
                float t = i / (Samples - 1f);
                path[i] = origin + forward * (t * length * (1f - retract))
                    + Vector3.up * (.48f - Mathf.Sin(t * Mathf.PI) * .14f * (1f - tighten));
                widths[i] = 1f - .36f * t;
            }
            Ribbon(Samples, .19f, RopeShadow, fade, BrushProfile);
            for (int strand = 0; strand < 2; strand++)
            {
                for (int i = 0; i < Samples; i++)
                {
                    float t = i / (Samples - 1f);
                    float phase = t * (1f - retract) * length * 27f - age * 13f + strand * Mathf.PI;
                    path[i] = origin + forward * (t * length * (1f - retract))
                        + side * (Mathf.Sin(phase) * .057f)
                        + Vector3.up * (.48f + Mathf.Cos(phase) * .05f
                            - Mathf.Sin(t * Mathf.PI) * .14f * (1f - tighten));
                }
                Ribbon(Samples, .069f, strand == 0 ? RopeFiber : ClawEdge, fade, BrushProfile);
            }

            for (int end = -1; end <= 1; end += 2)
            {
                float root = end < 0 ? .34f : .66f;
                for (int i = 0; i < Samples; i++)
                {
                    float t = i / (Samples - 1f);
                    float fork = Mathf.Sin(t * Mathf.PI * 1.18f) * t;
                    path[i] = origin + forward * ((root + end * t * .19f) * length * (1f - retract))
                        + side * (end * fork * .37f) + Vector3.up * (.46f + Mathf.Abs(fork) * .34f);
                    widths[i] = Mathf.Sin(t * Mathf.PI) * .79f + .02f;
                }
                Ribbon(Samples, .11f, RopeFiber, fade * .82f, BrushProfile);
            }

            Vector3 scuff = origin + forward * (length * (1f - retract));
            for (int i = 0; i < 4; i++)
            {
                Vector3 dust = scuff + forward * (age * (1.2f + i * .82f)) + side * ((i - 1.5f) * effect.Strength * .55f);
                Flare(dust + Vector3.up * .09f, .29f + age * .45f, .13f,
                    RopeFiber, Fade(age - .10f, .37f) * .46f, profile: -5f);
            }
        }

        private void DrawKite(Effect effect, float age)
        {
            Vector3 center = effect.From;
            Vector3 forward = effect.To;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float radius = effect.Strength;
            float descent = Mathf.SmoothStep(.93f, .31f, age / .27f);
            float fallFade = Fade(age - .045f, .31f);
            for (int wing = -1; wing <= 1; wing += 2)
            {
                for (int i = 0; i < Samples; i++)
                {
                    float u = i / (Samples - 1f);
                    float arc = Mathf.Sin(u * Mathf.PI);
                    path[i] = center - forward * (radius * (.14f + u * u * .57f))
                        + side * (wing * radius * (u * .43f + arc * .10f))
                        + Vector3.up * (.09f + radius * (descent * u * 1.72f + arc * .27f));
                    widths[i] = arc * (.62f + u * .40f) + .025f;
                }
                Ribbon(Samples, .54f, ColdInk, fallFade, BrushProfile, bias: -.30f * wing);
                Ribbon(Samples, .22f, Ice, fallFade, BrushProfile, bias: .34f * wing);
                Ribbon(Samples, .085f, Silver, fallFade, BrushProfile, bias: .64f * wing);
            }

            float burst = Mathf.SmoothStep(.20f, 1f, age / .22f);
            float dustFade = Fade(age - .09f, .53f);
            for (int streak = 0; streak < 7; streak++)
            {
                Vector3 outward = Quaternion.AngleAxis(streak * 137.5f + 13f, Vector3.up) * forward;
                Vector3 tip = center + outward * (radius * burst * (.58f + streak % 3 * .17f));
                path[0] = center + outward * .16f + Vector3.up * .11f;
                path[1] = Vector3.Lerp(path[0], tip, .52f) + Vector3.up * .30f;
                path[2] = tip + Vector3.up * .20f;
                widths[0] = .32f;
                widths[1] = 1f;
                widths[2] = .015f;
                Ribbon(3, .30f, Feather, dustFade * .7f, BrushProfile);
                Ribbon(3, .12f, Silver, dustFade * .76f, BrushProfile);
                Flare(tip + Vector3.up * .12f, .35f + age * .34f, .15f, Dust, dustFade * .43f, profile: -5f);

                Vector3 feather = center + outward * (radius * (.34f + streak % 3 * .21f) * burst)
                    + Vector3.up * Mathf.Max(.10f, (.75f + streak % 4 * .18f) - age * (1.15f + streak % 3));
                path[0] = feather - outward * .08f;
                path[1] = feather + side * (.07f * Mathf.Sin(age * 13f + streak));
                path[2] = feather + outward * .17f - Vector3.up * .105f;
                widths[0] = .13f;
                widths[1] = .89f;
                widths[2] = .025f;
                Ribbon(3, .15f, Feather, dustFade, BrushProfile);
            }
        }

        private void DrawMarten(Effect effect, float age)
        {
            Vector3 center = effect.From;
            Vector3 forward = effect.To;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float radius = effect.Strength;
            float reveal = Mathf.SmoothStep(.46f, 1f, age / .11f);
            float fade = Fade(age - .047f, .39f);
            for (int blade = 0; blade < 2; blade++)
            {
                for (int i = 0; i < Samples; i++)
                {
                    float u = i / (Samples - 1f);
                    float angle = Mathf.Lerp(-1.95f + blade * .37f, 1.87f - blade * .13f, u * reveal)
                        + age * 2.2f;
                    float edge = radius * (.77f + blade * .11f + u * .11f);
                    path[i] = center + (side * Mathf.Sin(angle) + forward * Mathf.Cos(angle)) * edge
                        + Vector3.up * (.15f + blade * .17f + u * u * .32f);
                    widths[i] = Mathf.Sin(u * Mathf.PI) * (.42f + .79f * u) + .026f;
                }
                Ribbon(Samples, .36f - blade * .06f, ColdInk, fade * .94f, BrushProfile, Vector3.up);
                Ribbon(Samples, .27f - blade * .05f, FrostEdge, fade, BrushProfile, Vector3.up, bias: .22f);
                Ribbon(Samples, .115f, Silver, fade, BrushProfile, Vector3.up, bias: .65f);
            }

            for (int i = 0; i < 9; i++)
            {
                Vector3 outward = Quaternion.AngleAxis(i * 137.5f, Vector3.up) * forward;
                Vector3 crystal = center + outward * (radius * (.39f + age * (1.5f + i % 3 * .6f)))
                    + Vector3.up * Mathf.Max(.10f, age * (2.4f + i % 3 * .32f) - age * age * 4.6f);
                path[0] = crystal - outward * .12f - Vector3.up * .055f;
                path[1] = crystal;
                path[2] = crystal + outward * .095f + Vector3.up * .15f;
                widths[0] = .035f;
                widths[1] = 1f;
                widths[2] = .035f;
                Ribbon(3, .108f + i % 3 * .023f, Ice, fade, BrushProfile);
                Ribbon(3, .049f, Silver, fade, BrushProfile, bias: .65f);
            }
            Flare(center + Vector3.up * .09f, radius, .19f, FrostEdge, fade * .47f, profile: -5f);
        }
    }
}
