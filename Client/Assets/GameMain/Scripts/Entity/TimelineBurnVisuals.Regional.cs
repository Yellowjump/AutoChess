using DataTable;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity
{
    public sealed partial class TimelineBurnVisuals
    {
        internal static readonly Color BadgerWarningColor = new(.78f, .64f, .43f, .86f);
        internal static readonly Color KilnWarningColor = new(.99f, .45f, .18f, .92f);
        internal static readonly Color ContractWarningColor = new(.88f, .29f, .23f, .92f);

        internal LineRenderer RegionalWarning(string label, Transform parent, Vector3 start,
            Vector3 forward, float length, float width, Color color)
        {
            if (material == null) return null;
            var side = Vector3.Cross(Vector3.up, forward) * width;
            start += Vector3.up * .11f;
            var end = start + forward * length;
            var line = CreateLine(label, parent, true);
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = .058f;
            line.SetPositions(new[] { start - side, end - side, end + side, start + side });
            line.startColor = line.endColor = color;
            return line;
        }

        internal void RegionalDust(Vector3 origin, Vector3 direction)
        {
            var side = Vector3.Cross(Vector3.up, direction);
            for (int i = -2; i <= 2; i++)
            {
                var start = origin + side * (.13f * i);
                AddStroke(start, start + direction * (.44f + .12f * Mathf.Abs(i)) + side * (.09f * i),
                    0f, .27f, .055f, BadgerWarningColor);
            }
        }

        internal void RegionalPounceImpact(Vector3 position, Vector3 direction)
        {
            if (combatVfx != null) combatVfx.GuardianPounce(position, direction);
            else
            {
                RegionalDust(position, direction);
                Ring(position, .69f, .27f, .105f, BadgerWarningColor);
            }
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.BadgerPounce, position);
            impulse.CreateEvent(position, new Vector3(.055f, -.10f, 0f));
        }

        internal void RegionalKilnImpact(Vector3 origin, Vector3 direction, float length, float halfWidth, bool hit)
        {
            if (combatVfx != null) combatVfx.GuardianKiln(origin, direction, length, halfWidth);
            else RegionalKilnFallback(origin, direction, length);
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.KilnCrack, origin);
            if (hit) impulse.CreateEvent(origin, new Vector3(.07f, -.115f, 0f));
        }

        private void RegionalKilnFallback(Vector3 origin, Vector3 direction, float length)
        {
            var side = Vector3.Cross(Vector3.up, direction);
            var fire = RegionalWarning("灰脊·火纹", transform, origin, direction, length, .46f, Flame);
            if (fire != null)
            {
                fire.widthMultiplier = .12f;
                StartCoroutine(FadeCombatStroke(fire, Flame, .32f));
            }
            for (int i = 0; i < 7; i++)
            {
                var spot = origin + direction * ((i + .45f) * length / 7f);
                Ring(spot, .28f + .075f * (i % 3), .44f, .094f, Ember);
                var spark = spot + Vector3.up * .57f + side * (.17f * (i % 2 == 0 ? 1f : -1f));
                AddStroke(spot, spark, 0f, .29f + .02f * (i % 4), .092f, Flame);
            }
        }

        internal void RegionalContractImpact(Vector3 position, Vector3 forward, float halfWidth, bool hit)
        {
            if (combatVfx != null) combatVfx.GuardianContract(position, forward, halfWidth);
            else RegionalContractFallback(position, forward, halfWidth);
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.ContractStamp, position);
            if (hit) impulse.CreateEvent(position, new Vector3(.035f, -.125f, 0f));
        }

        private void RegionalContractFallback(Vector3 position, Vector3 forward, float halfWidth)
        {
            var seal = RegionalWarning("旧契·朱砂印", transform, position - forward * halfWidth,
                forward, halfWidth * 2f, halfWidth, ContractWarningColor);
            if (seal != null)
            {
                seal.widthMultiplier = .15f;
                StartCoroutine(FadeCombatStroke(seal, ContractWarningColor, .32f));
            }
            var side = Vector3.Cross(Vector3.up, forward);
            var raised = position + Vector3.up * .11f;
            AddStroke(raised - side * .46f, raised + side * .46f, 0f, .29f, .11f, ContractWarningColor);
            AddStroke(raised - forward * .46f, raised + forward * .46f, 0f, .29f, .11f, ContractWarningColor);
        }
    }
}
