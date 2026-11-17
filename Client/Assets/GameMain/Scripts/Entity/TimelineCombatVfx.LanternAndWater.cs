using UnityEngine;

namespace Entity
{
    internal sealed partial class TimelineCombatVfx
    {
        private static readonly Color DeepWater = new Color(.055f, .32f, .36f);
        private static readonly Color Shoal = new Color(.20f, .77f, .72f);
        private static readonly Color Wick = new Color(1f, .72f, .29f);
        private static readonly Color HealJade = new Color(.44f, 1f, .66f);

        internal void Undertow(Vector3 origin, Vector3 forward, float length, float halfWidth) =>
            Add(Kind.Undertow, origin, forward * length, .57f, halfWidth, 0f);

        internal void LanternHeal(Vector3 center, Vector3 lamp, int survivingWicks) =>
            Add(Kind.LanternHeal, center, lamp, .82f, survivingWicks, 0f);

        internal void LanternSnuff(Vector3 center) =>
            Add(Kind.LanternSnuff, center, center, .37f, 1f, 0f);

        private void DrawUndertow(Effect effect, float age)
        {
            float progress = Mathf.Clamp01(age / TimelineUndertow.TravelSeconds);
            Vector3 forward = effect.To.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 head = effect.From + effect.To * progress;
            float halfWidth = effect.Strength;
            float wash = Fade(age - .21f, .35f);
            float curl = Mathf.SmoothStep(.35f, 1f, age / .10f) * Fade(age - .35f, .18f);

            for (int i = 0; i < Samples; i++)
            {
                float t = i / (Samples - 1f);
                path[i] = Vector3.Lerp(effect.From, head, t) + Vector3.up * .055f;
                widths[i] = Mathf.Sin(Mathf.PI * t) * (.68f + .24f * Mathf.Sin(t * 15f - age * 14f)) + .09f;
            }
            Ribbon(Samples, halfWidth * 2.12f, DeepWater, wash * .58f, BrushProfile, Vector3.up);
            Ribbon(Samples, halfWidth * 1.45f, Shoal, wash * .42f, WaterProfile, Vector3.up);

            DrawWaveFace(head, forward, side, halfWidth, age, curl, 0f, 1f);
            DrawWaveFace(head - forward * .43f, forward, side, halfWidth * .90f, age, curl, 1.28f, .62f);

            for (int i = 0; i < 9; i++)
            {
                float t = (i + .4f) / 9f;
                float lateral = (t * 2f - 1f) * halfWidth;
                float lift = curl * (.38f + i % 3 * .15f);
                Vector3 tip = head + side * lateral + forward * (.12f + i % 3 * .16f) + Vector3.up * lift;
                path[0] = tip - forward * (.21f + i % 3 * .052f) - Vector3.up * .08f;
                path[1] = tip;
                widths[0] = .05f;
                widths[1] = .85f;
                Ribbon(2, .112f, Silver, wash * curl, BrushProfile);
            }
        }

        private void DrawWaveFace(Vector3 head, Vector3 forward, Vector3 side, float halfWidth, float age,
            float curl, float phase, float strength)
        {
            int first = vertices.Count;
            for (int i = 0; i < Samples; i++)
            {
                float t = i / (Samples - 1f);
                float across = t * 2f - 1f;
                float envelope = Mathf.Sqrt(Mathf.Max(0f, 1f - across * across));
                float breaker = (.65f + .16f * Mathf.Sin(t * 21f - age * 9f + phase)) * envelope * curl * strength;
                float ridge = Mathf.Sin(t * 16f - age * 12f + phase) * .12f;
                Vector3 foot = head + side * (across * halfWidth) - forward * (.31f + .08f * envelope);
                Vector3 lip = head + side * (across * halfWidth * .96f)
                    + forward * (ridge * envelope - .075f) + Vector3.up * (.045f + breaker);
                AddVertex(foot + Vector3.up * .06f, t, 0f, WaterProfile, DeepWater, curl * strength);
                AddVertex(lip, t, 1f, WaterProfile, Shoal, curl * strength);
                if (i > 0) AddQuad(first + (i - 1) * 2);
            }
        }

        private void DrawLanternHeal(Effect effect, float age)
        {
            float reveal = Mathf.SmoothStep(.21f, 1f, age / .22f);
            float fade = Fade(age - .20f, .56f);
            Vector3 lamp = effect.To;
            int wicks = Mathf.Clamp(Mathf.RoundToInt(effect.Strength), 1, 3);
            for (int wick = 0; wick < wicks; wick++)
            {
                float angle = wick * Mathf.PI * 2f / 3f + .28f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 tangent = Vector3.Cross(Vector3.up, radial);
                Vector3 foot = effect.From + radial * .82f + Vector3.up * .071f;
                for (int i = 0; i < Samples; i++)
                {
                    float u = i / (Samples - 1f);
                    float t = u * reveal;
                    float arc = Mathf.Sin(t * Mathf.PI);
                    path[i] = Vector3.Lerp(foot, lamp, t * t)
                        + tangent * (arc * (.31f + .075f * Mathf.Sin(age * 13f + wick)))
                        + radial * (Mathf.Sin(t * 7f) * arc * .14f)
                        + Vector3.up * (arc * .46f);
                    widths[i] = Mathf.Sin(u * Mathf.PI) * (.68f + .25f * Mathf.Sin(t * 14f + wick)) + .018f;
                }
                Ribbon(Samples, .28f, Wick, fade, BrushProfile);
                Ribbon(Samples, .12f, PaperWhite, fade, BrushProfile, bias: .33f);
                Flare(foot, .26f, .18f, Wick, fade * .74f);
            }

            float centerFlash = Mathf.SmoothStep(0f, 1f, age / .19f) * Fade(age - .15f, .52f);
            Flare(lamp, .48f, .52f, HealJade, centerFlash);
            Flare(lamp, .24f, .28f, Wick, centerFlash);
            Sparks(lamp, cameraRight, age, .57f, 12, Wick, .95f);

            Vector3 marker = lamp + Vector3.up * (.37f + age * .45f);
            float markerFade = Mathf.SmoothStep(0f, 1f, (age - .11f) / .12f) * fade;
            HealMark(marker, Vector3.up, markerFade);
            HealMark(marker, cameraRight, markerFade);
        }

        private void HealMark(Vector3 center, Vector3 axis, float alpha)
        {
            path[0] = center - axis * .22f;
            path[1] = center + axis * .22f;
            widths[0] = .94f;
            widths[1] = .94f;
            Ribbon(2, .09f, HealJade, alpha, BrushProfile);
            Ribbon(2, .042f, PaperWhite, alpha, BrushProfile);
        }

        private void DrawLanternSnuff(Effect effect, float age)
        {
            float fade = Fade(age, effect.Duration);
            Vector3 center = effect.From + Vector3.up * (.66f + age * .80f);
            Flare(center, .47f + age * .55f, .20f, Dust, fade * .58f, profile: -5f);
            Sparks(center, cameraRight, age, .26f, 6, Wick, .64f);
        }
    }
}
