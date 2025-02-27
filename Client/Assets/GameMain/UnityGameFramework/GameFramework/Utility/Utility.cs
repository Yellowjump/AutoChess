//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using System.Collections.Generic;
namespace GameFramework
{
    /// <summary>
    /// 实用函数集。
    /// </summary>
    public static partial class Utility
    {
        public static float TruncateFloat(float value, int digits)
        {
            var factor = Math.Pow(10, digits);
            return (float)(Math.Floor(value * factor) / factor);
        }
        public static void Shuffle<T>(this List<T> list,bool logicRandom=true)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = logicRandom?Random.GetRandom(n + 1):Random.GetRandomNoLogic(n+1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        public static void ShuffleWithWeight<T>(this List<T> list, List<int> weightList, bool logicRandom = true)
        {
            if (list.Count != weightList.Count)
            {
                throw new GameFrameworkException ("List and weights must have the same length.");
            }

            // Create a list to hold weighted indices
            List<int> weightedIndices = new List<int>();

            // Populate weighted indices based on weights
            for (int i = 0; i < weightList.Count; i++)
            {
                for (int j = 0; j < weightList[i]; j++)
                {
                    weightedIndices.Add(i);
                }
            }

            int n = list.Count;
            for (int i = 0; i < n - 1; i++)
            {
                // Get a random index from i to n-1 inclusive
                int k = logicRandom?Random.GetRandom(weightedIndices.Count):Random.GetRandomNoLogic(weightedIndices.Count);

                // Swap elements using weighted indices
                int index = weightedIndices[k];
                (list[index], list[i]) = (list[i], list[index]);
                weightedIndices.RemoveAll((item) => item == index);
                for (var index1 = 0; index1 < weightedIndices.Count; index1++)
                {
                    var item = weightedIndices[index1];
                    if (item == i)
                    {
                        weightedIndices[index1] = index;
                    }
                }
            }
        }
    }
}
