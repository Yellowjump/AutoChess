//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;

namespace GameFramework
{
    public static partial class Utility
    {
        /// <summary>
        /// 随机相关的实用函数。
        /// </summary>
        public static class Random
        {
            private static System.Random s_Random;
            private static System.Random AttrRandom
            {
                get
                {
                    if (s_Random != null) return s_Random;
                    Seed = (int)DateTime.UtcNow.Ticks;
                    NextCount = 0;
                    s_Random = new System.Random((int)DateTime.UtcNow.Ticks);
                    return s_Random;
                }
                set => s_Random = value;
            }
            public static int Seed;
            public static int NextCount;
            
            private static System.Random s_RandomNoLogic;
            private static System.Random AttrRandomNoLogic
            {
                get
                {
                    if (s_RandomNoLogic != null) return s_RandomNoLogic;
                    s_RandomNoLogic = new System.Random();
                    return s_RandomNoLogic;
                }
            }
            /// <summary>
            /// 设置随机数种子。
            /// </summary>
            /// <param name="seed">随机数种子。</param>
            public static void SetSeed(int seed)
            {
                Seed = seed;
                NextCount = 0;
                AttrRandom = new System.Random(seed);
            }

            /// <summary>
            /// 返回非负随机数。
            /// </summary>
            /// <returns>大于等于零且小于 System.Int32.MaxValue 的 32 位带符号整数。</returns>
            public static int GetRandom()
            {
                NextCount ++;
                return AttrRandom.Next();
            }

            /// <summary>
            /// 返回一个小于所指定最大值的非负随机数。
            /// </summary>
            /// <param name="maxValue">要生成的随机数的上界（随机数不能取该上界值）。maxValue 必须大于等于零。</param>
            /// <returns>大于等于零且小于 maxValue 的 32 位带符号整数，即：返回值的范围通常包括零但不包括 maxValue。不过，如果 maxValue 等于零，则返回 maxValue。</returns>
            public static int GetRandom(int maxValue)
            {
                NextCount ++;
                return AttrRandom.Next(maxValue);
            }

            /// <summary>
            /// 返回一个指定范围内的随机数。
            /// </summary>
            /// <param name="minValue">返回的随机数的下界（随机数可取该下界值）。</param>
            /// <param name="maxValue">返回的随机数的上界（随机数不能取该上界值）。maxValue 必须大于等于 minValue。</param>
            /// <returns>一个大于等于 minValue 且小于 maxValue 的 32 位带符号整数，即：返回的值范围包括 minValue 但不包括 maxValue。如果 minValue 等于 maxValue，则返回 minValue。</returns>
            public static int GetRandom(int minValue, int maxValue)
            {
                NextCount ++;
                return AttrRandom.Next(minValue, maxValue);
            }

            /// <summary>
            /// 返回一个介于 0.0 和 1.0 之间的随机数。
            /// </summary>
            /// <returns>大于等于 0.0 并且小于 1.0 的双精度浮点数。</returns>
            public static double GetRandomDouble()
            {
                NextCount ++;
                return AttrRandom.NextDouble();
            }

            /// <summary>
            /// 用随机数填充指定字节数组的元素。
            /// </summary>
            /// <param name="buffer">包含随机数的字节数组。</param>
            public static void GetRandomBytes(byte[] buffer)
            {
                NextCount ++;
                AttrRandom.NextBytes(buffer);
            }
            
            /// <summary>
            /// 返回非负随机数。
            /// </summary>
            /// <returns>大于等于零且小于 System.Int32.MaxValue 的 32 位带符号整数。</returns>
            public static int GetRandomNoLogic()
            {
                return AttrRandomNoLogic.Next();
            }

            /// <summary>
            /// 返回一个小于所指定最大值的非负随机数。
            /// </summary>
            /// <param name="maxValue">要生成的随机数的上界（随机数不能取该上界值）。maxValue 必须大于等于零。</param>
            /// <returns>大于等于零且小于 maxValue 的 32 位带符号整数，即：返回值的范围通常包括零但不包括 maxValue。不过，如果 maxValue 等于零，则返回 maxValue。</returns>
            public static int GetRandomNoLogic(int maxValue)
            {
                return AttrRandomNoLogic.Next(maxValue);
            }

            /// <summary>
            /// 返回一个指定范围内的随机数。
            /// </summary>
            /// <param name="minValue">返回的随机数的下界（随机数可取该下界值）。</param>
            /// <param name="maxValue">返回的随机数的上界（随机数不能取该上界值）。maxValue 必须大于等于 minValue。</param>
            /// <returns>一个大于等于 minValue 且小于 maxValue 的 32 位带符号整数，即：返回的值范围包括 minValue 但不包括 maxValue。如果 minValue 等于 maxValue，则返回 minValue。</returns>
            public static int GetRandomNoLogic(int minValue, int maxValue)
            {
                return AttrRandomNoLogic.Next(minValue, maxValue);
            }
            /// <summary>
            /// 返回一个指定范围内的随机数。
            /// </summary>
            /// <param name="minValue">返回的随机数的下界（随机数可取该下界值）。</param>
            /// <param name="maxValue">返回的随机数的上界（随机数不能取该上界值）。maxValue 必须大于等于 minValue。</param>
            /// <returns>一个大于等于 minValue 且小于 maxValue 的双精度浮点数，即：返回的值范围包括 minValue 但不包括 maxValue。如果 minValue 等于 maxValue，则返回 minValue。</returns>
            public static double GetRandomNoLogic(double minValue, double maxValue)
            {
                if (minValue >= maxValue) return minValue;
                var randomValue = AttrRandomNoLogic.NextDouble();
                return minValue + (randomValue * (maxValue - minValue));
            }
            /// <summary>
            /// 返回一个介于 0.0 和 1.0 之间的随机数。
            /// </summary>
            /// <returns>大于等于 0.0 并且小于 1.0 的双精度浮点数。</returns>
            public static double GetRandomDoubleNoLogic()
            {
                return AttrRandomNoLogic.NextDouble();
            }
        }
    }
}
