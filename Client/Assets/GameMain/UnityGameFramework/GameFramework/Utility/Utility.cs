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
    }
}
