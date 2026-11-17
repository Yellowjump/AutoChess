using UnityEngine;

namespace Entity
{
    internal sealed partial class TimelineCombatVfx
    {
        private static readonly Color SaltWake = new Color(.18f, .40f, .36f);
        private static readonly Color SaltCrystal = new Color(.62f, .86f, .80f);
        private static readonly Color SaltWhite = new Color(.97f, 1f, .87f);
        private static readonly Color ShellDark = new Color(.075f, .15f, .21f);
        private static readonly Color ShellEdge = new Color(.26f, .52f, .56f);
        private static readonly Color PaperWhite = new Color(.88f, .84f, .70f);
        private static readonly Color PaperShade = new Color(.37f, .33f, .27f);

        internal void GuardianSaltNeedle(Vector3 origin, Vector3 forward, float length) =>
            Add(Kind.SaltNeedle, origin, forward * length, .32f, 1f, 0f);

        internal void GuardianSaltHit(Vector3 position, Vector3 forward) =>
            Add(Kind.SaltShatter, position, forward, .33f, 1f, 0f);

        internal void GuardianShellGuard(EntityQizi owner) =>
            Add(Kind.ShellGuard, owner.LogicPosition, owner.OuterShellForward,
                TimelineOuterGuardianCast.ShellDuration, 1f, 0f, owner);

        internal void GuardianShellClash(Vector3 position, Vector3 outward) =>
            Add(Kind.ShellClash, position, outward, .34f, 1f, 0f);

        internal void GuardianPaperSummon(Vector3 position) =>
            Add(Kind.Paper, position, Vector3.zero, .62f, 1f, 0f);

        private void DrawSaltNeedle(Effect effect, float age)
        {
            float length = effect.To.magnitude;
            Vector3 forward = effect.To / length;
            float travel = Mathf.SmoothStep(.16f, 1f, age / .12f);
            float fade = Fade(age - .075f, .23f);
            Vector3 start = effect.From + Vector3.up * .49f;
            Vector3 tip = start + effect.To * travel;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float pointLength = Mathf.Min(.65f, length * .48f);
            for (int i = 0; i < Samples; i++)
            {
                float t = i / (Samples - 1f);
                path[i] = Vector3.Lerp(start, tip, t)
                    + side * (Mathf.Sin(t * 12f - age * 26f) * .10f * Mathf.Sin(t * Mathf.PI));
                widths[i] = Mathf.Sin(t * Mathf.PI) * (.72f + .24f * Mathf.Sin(t * 17f)) + .01f;
            }
            Ribbon(Samples, .32f, SaltWake, fade, BrushProfile);
            Ribbon(Samples, .13f, SaltCrystal, fade * .82f, BrushProfile, bias: .4f);

            path[0] = tip - forward * pointLength;
            path[1] = tip - forward * (pointLength * .63f);
            path[2] = tip - forward * (pointLength * .34f);
            path[3] = tip;
            widths[0] = .12f;
            widths[1] = .67f;
            widths[2] = 1f;
            widths[3] = .022f;
            Ribbon(4, .24f, SaltWake, fade, BrushProfile);
            Ribbon(4, .18f, SaltCrystal, fade, BrushProfile);
            Ribbon(4, .084f, SaltWhite, fade, BrushProfile, bias: .42f);
        }

        private void DrawSaltShatter(Effect effect, float age, Color tint)
        {
            Vector3 center = effect.From + (cameraPosition - effect.From).normalized * .17f;
            float fade = Fade(age - .04f, .28f);
            Flare(center, .36f, .37f, tint, Fade(age, .12f));
            for (int i = 0; i < 9; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(-127f + i * 32f, Vector3.up) * effect.To;
                Vector3 velocity = direction * (1.32f + i % 3 * .59f) + Vector3.up * (.57f + i % 4 * .40f);
                Vector3 chip = center + velocity * (age + .048f) - Vector3.up * (age * age * 4.2f);
                path[0] = chip - velocity * .054f;
                path[1] = chip;
                path[2] = chip + velocity * .06f;
                widths[0] = .025f;
                widths[1] = 1f;
                widths[2] = .026f;
                Ribbon(3, .11f, tint, fade, BrushProfile);
                Ribbon(3, .047f, SaltWhite, fade, BrushProfile, bias: .55f);
            }
        }

        private void DrawShellGuard(Effect effect, float age)
        {
            EntityQizi owner = effect.Owner;
            Vector3 center = owner.GObj != null ? owner.GObj.transform.position : owner.LogicPosition;
            Vector3 forward = effect.To;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float rise = Mathf.SmoothStep(.40f, 1f, age / .16f);
            float fade = Mathf.SmoothStep(.32f, 1f, age / .10f)
                * Mathf.SmoothStep(0f, 1f, owner.OuterShellSeconds / .28f);
            for (int plate = -1; plate <= 1; plate++)
            {
                Vector3 radial = (forward + side * (plate * .69f)).normalized;
                Vector3 tangent = Vector3.Cross(Vector3.up, radial);
                float height = (plate == 0 ? 1.10f : .92f) * rise;
                for (int i = 0; i < Samples; i++)
                {
                    float t = i / (Samples - 1f);
                    path[i] = center + radial * (.80f - t * t * .22f)
                        + Vector3.up * (.055f + height * t)
                        + tangent * (plate * .11f * t);
                    widths[i] = .10f + Mathf.Sin(t * Mathf.PI * .94f) * (1f - t * .28f);
                }
                Ribbon(Samples, .56f, ShellDark, fade, BrushProfile, planeNormal: radial);
                Ribbon(Samples, .44f, ShellEdge, fade * .78f, BrushProfile, planeNormal: radial, bias: .16f);
                Ribbon(Samples, .26f, ShellDark, fade, BrushProfile, planeNormal: radial, bias: -.19f);

                for (int i = 0; i < 5; i++)
                {
                    float t = i / 4f;
                    path[i] = center + radial * (.756f - t * .026f)
                        + tangent * ((t - .5f) * .40f)
                        + Vector3.up * (height * (.36f + t * .23f + Mathf.Sin(i * 2.5f) * .066f));
                    widths[i] = .09f + Mathf.Sin(t * Mathf.PI) * .91f;
                }
                Ribbon(5, .045f, SaltWhite, fade * .74f, BrushProfile, planeNormal: radial);
            }
        }

        private void DrawPaperSummon(Effect effect, float age)
        {
            Vector3 cell = effect.From + Vector3.up * .065f;
            float spread = Mathf.SmoothStep(.31f, 1f, age / .17f);
            float fade = Fade(age - .13f, .48f);
            Vector3 along = (cameraRight + Vector3.forward * .37f);
            along.y = 0f;
            along.Normalize();
            for (int i = 0; i < Samples; i++)
            {
                float t = i / (Samples - 1f);
                path[i] = cell + along * ((t - .5f) * spread * 1.27f);
                widths[i] = Mathf.Sin(t * Mathf.PI) * (.85f + .15f * Mathf.Sin(t * 19f));
            }
            Ribbon(Samples, .89f * spread, Ink, fade, BrushProfile, Vector3.up);
            Ribbon(Samples, .12f * spread, SealInk, fade, BrushProfile, Vector3.up, bias: -.61f);

            for (int strip = 0; strip < 5; strip++)
            {
                Vector3 side = Quaternion.AngleAxis(strip * 137.5f + age * 85f, Vector3.up) * along;
                Vector3 cross = Vector3.Cross(side, Vector3.up);
                for (int i = 0; i < Samples; i++)
                {
                    float t = i / (Samples - 1f);
                    float roll = Mathf.Sin(t * 7.8f + strip + age * 12f);
                    path[i] = cell + side * ((.30f + t * .20f) * spread)
                        + cross * (roll * t * .22f) + Vector3.up * (spread * t * (.90f + strip % 3 * .18f));
                    widths[i] = Mathf.Sin(t * Mathf.PI) * (.75f + .25f * roll) + .04f;
                }
                Ribbon(Samples, .265f, PaperShade, fade, BrushProfile, bias: .22f);
                Ribbon(Samples, .20f, PaperWhite, fade, BrushProfile, bias: -.10f);
            }
            Sparks(cell + Vector3.up * .12f, along, age, .39f, 7, Cinnabar, .76f);
        }
    }
}
