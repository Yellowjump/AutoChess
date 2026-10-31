using System;
using Unity.Collections;
using Unity.Entities;

namespace Zizouqi.CombatDots
{
    public struct CombatTargetCandidate : IBufferElementData
    {
        public int Index;
    }

    public static class CombatRandomTargetSelection
    {
        public static bool SelectByRule(int targetSelectionHandle, int numberType,
            int parameter, int parentItemId, int argItemId, bool hasArgSkill,
            bool meetNumber, Func<int, int> roll, Func<int, int, int> countSubItem)
        {
            int requestedCount;
            switch (numberType)
            {
                case 0: // NumberCheckType.FixedNumber
                    requestedCount = parameter;
                    break;
                case 1: // NumberCheckType.ParentSkillContainSubItemNumber
                    requestedCount = countSubItem(parentItemId, parameter);
                    break;
                case 2: // NumberCheckType.ArgSkillContainSubItemNumber
                    requestedCount = hasArgSkill ? countSubItem(argItemId, parameter) : 0;
                    break;
                default:
                    requestedCount = 0;
                    break;
            }
            if (requestedCount <= 0)
            {
                CombatTargetSelectionBridge.Clear(targetSelectionHandle);
                return false;
            }
            Select(targetSelectionHandle, requestedCount, meetNumber, roll);
            return true;
        }

        // The candidate indices and final targets belong to the current trigger invocation.
        // Each shuffle still draws from the caller's logic RNG in the original order.
        public static void Select(int targetSelectionHandle, int requestedCount,
            bool meetNumber, Func<int, int> roll)
        {
            DynamicBuffer<CombatSelectedTarget> targets =
                CombatTargetSelectionBridge.GetBuffer(targetSelectionHandle);
            DynamicBuffer<CombatTargetChoice> choices =
                CombatTargetSelectionBridge.GetChoices(targetSelectionHandle);
            choices.Clear();
            int candidateCount = targets.Length;
            if (candidateCount <= 0 || requestedCount <= 0)
            {
                return;
            }

            var original = new NativeArray<CombatSelectedTarget>(candidateCount, Allocator.Temp);
            try
            {
                for (int i = 0; i < candidateCount; i++)
                {
                    original[i] = targets[i];
                }
                DynamicBuffer<CombatTargetCandidate> candidates =
                    CombatTargetSelectionBridge.GetCandidates(targetSelectionHandle);
                if (candidateCount <= requestedCount && !meetNumber)
                {
                    Shuffle(candidates, candidateCount, roll);
                    Append(candidates, candidateCount, choices);
                }
                else
                {
                    while (choices.Length < requestedCount)
                    {
                        Shuffle(candidates, candidateCount, roll);
                        Append(candidates, requestedCount - choices.Length, choices);
                    }
                }
                targets.Clear();
                for (int i = 0; i < choices.Length; i++)
                {
                    targets.Add(original[choices[i].CandidateIndex]);
                }
            }
            finally
            {
                original.Dispose();
            }
        }

        private static void Shuffle(DynamicBuffer<CombatTargetCandidate> candidates, int count,
            Func<int, int> roll)
        {
            candidates.Clear();
            for (int i = 0; i < count; i++)
            {
                candidates.Add(new CombatTargetCandidate { Index = i });
            }
            for (int n = count; n > 1;)
            {
                n--;
                int k = roll(n + 1);
                CombatTargetCandidate selected = candidates[k];
                candidates[k] = candidates[n];
                candidates[n] = selected;
            }
        }

        private static void Append(DynamicBuffer<CombatTargetCandidate> candidates, int count,
            DynamicBuffer<CombatTargetChoice> choices)
        {
            for (int i = 0; i < candidates.Length && i < count; i++)
            {
                int index = candidates[i].Index;
                choices.Add(new CombatTargetChoice { CandidateIndex = index });
            }
        }
    }
}
