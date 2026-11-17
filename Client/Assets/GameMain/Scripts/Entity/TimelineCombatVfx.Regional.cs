using UnityEngine;

namespace Entity
{
    internal sealed partial class TimelineCombatVfx
    {
        private static readonly Color ClawEdge = new Color(.85f, .77f, .62f);
        private static readonly Color FurnaceRed = new Color(.49f, .079f, .036f);
        private static readonly Color FurnaceOrange = new Color(.99f, .32f, .082f);
        private static readonly Color ContractInk = new Color(.041f, .12f, .104f);
        private static readonly Color ContractJade = new Color(.31f, .94f, .75f);

        internal void GuardianPounce(Vector3 position, Vector3 forward) =>
            Add(Kind.Pounce, position, forward, .46f, 1f, 0f);

        internal void GuardianKiln(Vector3 origin, Vector3 forward, float length, float halfWidth) =>
            Add(Kind.Kiln, origin, forward * length, .77f, halfWidth, 0f);

        internal void GuardianContract(Vector3 position, Vector3 forward, float halfWidth) =>
            Add(Kind.Contract, position, forward, .68f, halfWidth, 0f);

        private void DrawPounce(Effect effect, float age)
        {
            Vector3 center = effect.From;
            Vector3 forward = effect.To;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float reach = Mathf.SmoothStep(.47f, 1.4f, age / .15f);
            float fade = Fade(age - .045f, .36f);
            for (int claw = 0; claw < 3; claw++)
            {
                float lane = (claw - 1) * .26f;
                for (int i = 0; i < Samples; i++)
                {
                    float t = i / (Samples - 1f);
                    float sweep = Mathf.Sin(Mathf.PI * t);
                    path[i] = center + forward * ((t - .53f) * reach * (1.72f - claw * .15f))
                        + side * (lane * (1f + t * .56f) + sweep * .25f)
                        + Vector3.up * (.15f + sweep * (.53f - claw * .085f));
                    widths[i] = sweep * (1.32f - t) + .018f;
                }
                float width = claw == 1 ? .43f : .28f;
                Ribbon(Samples, width, Ink, fade, BrushProfile, bias: -.26f);
                Ribbon(Samples, width * .28f, ClawEdge, fade, BrushProfile, bias: .26f);
            }

            for (int i = 0; i < 5; i++)
            {
                float lateral = (i - 2) * .21f;
                Vector3 position = center + side * lateral + forward * (.14f + age * (1.8f + i % 3 * .52f));
                Flare(position + Vector3.up * .12f, .31f + age * .48f, .19f,
                    Dust, fade * .49f, profile: -5f);
                var chip = position + Vector3.up * Mathf.Max(.075f, age * 2.25f - 4.2f * age * age);
                DrawStoneShard(chip, forward, .10f + i % 3 * .035f, age * 7f + i, fade);
            }
        }

        private void DrawKiln(Effect effect, float age)
        {
            Vector3 origin = effect.From;
            Vector3 forward = effect.To.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float length = effect.To.magnitude;
            float halfWidth = effect.Strength;
            float reveal = Mathf.SmoothStep(.12f, 1f, age / .20f);
            float fade = Fade(age - .19f, .54f);

            for (int edge = -1; edge <= 1; edge += 2)
            {
                for (int i = 0; i < Samples; i++)
                {
                    float u = i / (Samples - 1f);
                    float t = u * reveal;
                    path[i] = origin + forward * (length * t) + Vector3.up * .055f
                        + side * (edge * halfWidth * Mathf.Lerp(.33f, .88f, t) + Mathf.Sin(t * 37f) * .037f);
                    widths[i] = (.76f + Mathf.Sin(t * 34f + edge) * .24f) * Mathf.Sin(u * Mathf.PI);
                }
                Ribbon(Samples, .18f, Ink, fade * .80f, BrushProfile, Vector3.up);
            }

            for (int layer = 0; layer < 3; layer++)
            {
                float lane = (layer - 1) * .25f;
                float end = 1f - layer * .082f;
                for (int i = 0; i < Samples; i++)
                {
                    float u = i / (Samples - 1f);
                    float t = u * reveal * end;
                    float roll = Mathf.Sin(t * 21f - age * 14f + layer * 2.15f);
                    path[i] = origin + forward * (t * length)
                        + side * ((lane + roll * .16f) * halfWidth * (.35f + .65f * t))
                        + Vector3.up * (.28f + t * (.18f + roll * .12f) + layer * .052f);
                    widths[i] = Mathf.Sin(u * Mathf.PI) * (.63f + .26f * roll + t * .44f) + .035f;
                }
                Ribbon(Samples, halfWidth * 1.43f, FurnaceRed, fade, BrushProfile);
                Ribbon(Samples, halfWidth * 1.00f, FurnaceOrange, fade, BrushProfile);
                Ribbon(Samples, halfWidth * (.58f - layer * .09f), FireCore, fade, BrushProfile);
            }

            Flare(origin + Vector3.up * .34f, .41f, .32f, Gold, Fade(age, .27f));
            for (int i = 0; i < 16; i++)
            {
                float t = (age * (1.3f + i % 4 * .2f) + i * .063f) % 1f;
                if (t > reveal) continue;
                Vector3 tip = origin + forward * (t * length)
                    + side * (Mathf.Sin(i * 2.41f) * halfWidth * .77f)
                    + Vector3.up * (.10f + i % 4 * .15f);
                path[0] = tip - forward * (.14f + i % 3 * .11f);
                path[1] = tip;
                widths[0] = .04f;
                widths[1] = 1f;
                Ribbon(2, .066f, FireCore, fade * (1f - t), BrushProfile);
            }
        }

        private void DrawContract(Effect effect, float age)
        {
            Vector3 forward = effect.To;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float radius = effect.Strength;
            Vector3 heel = effect.From - forward * (radius * .82f) + Vector3.up * .072f;
            float unfold = Mathf.SmoothStep(.28f, 1f, age / .135f);
            float fade = Fade(age - .095f, .48f);
            for (int spur = 0; spur < 5; spur++)
            {
                float lane = (spur - 2) / 2f;
                for (int i = 0; i < Samples; i++)
                {
                    float t = i / (Samples - 1f);
                    float run = t * unfold;
                    path[i] = heel + forward * (run * radius * (1.78f - .43f * Mathf.Abs(lane)))
                        + side * (radius * (lane * run + Mathf.Sin(t * Mathf.PI) * .10f));
                    widths[i] = Mathf.Sin(t * Mathf.PI) * (.69f + .29f * Mathf.Sin(t * 11f + spur)) + .035f;
                }
                Ribbon(Samples, radius * .63f, ContractInk, fade, BrushProfile, Vector3.up);
            }

            for (int i = 0; i < Samples; i++)
            {
                float t = i / (Samples - 1f);
                float across = Mathf.Lerp(-.95f, .95f, t);
                float bow = 1.22f + Mathf.Sin(t * Mathf.PI) * .53f;
                path[i] = heel + forward * (bow * radius * unfold)
                    + side * (across * radius * unfold) + Vector3.up * (.13f * Mathf.Sin(t * Mathf.PI));
                widths[i] = Mathf.Sin(Mathf.PI * t) * (.43f + .57f * t) + .03f;
            }
            Ribbon(Samples, .37f, ContractInk, fade, BrushProfile, Vector3.up);
            Ribbon(Samples, .22f, ContractJade, fade, BrushProfile, Vector3.up, bias: .42f);

            for (int i = 0; i < 8; i++)
            {
                float across = Mathf.Sin(i * 2.4f);
                Vector3 tip = heel + forward * (radius * (1.15f + age * (.45f + i % 3)))
                    + side * (across * radius * (.63f + age * .52f))
                    + Vector3.up * (age * (1.4f + i % 3 * .54f));
                path[0] = tip - forward * (.09f + i % 3 * .052f) - side * (across * .06f);
                path[1] = tip;
                widths[0] = .19f;
                widths[1] = .76f;
                Ribbon(2, .11f, i % 2 == 0 ? ContractInk : ContractJade, fade, BrushProfile);
            }
        }
    }
}
