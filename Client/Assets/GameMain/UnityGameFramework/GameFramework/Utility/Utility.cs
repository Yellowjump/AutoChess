//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;

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
    }
}
