//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Entity;
using UnityEngine;
using GameFramework.Resource;
using UnityGameFramework.Runtime;
using GameFramework.DataTable;
using SkillSystem;

namespace DataTable
{
    public static class DataTableExtension
    {
        internal static readonly char[] DataSplitSeparators = new char[] { '\t' };
        internal static readonly char[] DataTrimSeparators = new char[] { '\"' };
        public static Color32 ParseColor32(string value)
        {
            string[] splitValue = value.Split(',');
            return new Color32(byte.Parse(splitValue[0]), byte.Parse(splitValue[1]), byte.Parse(splitValue[2]), byte.Parse(splitValue[3]));
        }

        public static Color ParseColor(string value)
        {
            string[] splitValue = value.Split(',');
            return new Color(float.Parse(splitValue[0]), float.Parse(splitValue[1]), float.Parse(splitValue[2]), float.Parse(splitValue[3]));
        }

        public static Quaternion ParseQuaternion(string value)
        {
            string[] splitValue = value.Split(',');
            return new Quaternion(float.Parse(splitValue[0]), float.Parse(splitValue[1]), float.Parse(splitValue[2]), float.Parse(splitValue[3]));
        }

        public static Rect ParseRect(string value)
        {
            string[] splitValue = value.Split(',');
            return new Rect(float.Parse(splitValue[0]), float.Parse(splitValue[1]), float.Parse(splitValue[2]), float.Parse(splitValue[3]));
        }

        public static Vector2 ParseVector2(string value)
        {
            string[] splitValue = value.Split(',');
            return new Vector2(float.Parse(splitValue[0]), float.Parse(splitValue[1]));
        }

        public static Vector3 ParseVector3(string value)
        {
            string[] splitValue = value.Split(',');
            return new Vector3(float.Parse(splitValue[0]), float.Parse(splitValue[1]), float.Parse(splitValue[2]));
        }

        public static Vector4 ParseVector4(string value)
        {
            string[] splitValue = value.Split(',');
            return new Vector4(float.Parse(splitValue[0]), float.Parse(splitValue[1]), float.Parse(splitValue[2]), float.Parse(splitValue[3]));
        }

        public static int[] ParseInt32Array(string value)
        {
            string[] splitValue = value.Split(',');
            int[] result = new int[splitValue.Length];
            for (int i = 0; i < splitValue.Length; i++)
            {
                result[i] = int.Parse(splitValue[i]);
            }

            return result;
        }
        public static Skill ParseSkill(string value)
        {
            Skill newSkill = SkillFactory.CreateNewSkill();
            // 解码 Base64 字符串
            byte[] decodedData = Convert.FromBase64String(value);
            using (MemoryStream memoryStream = new MemoryStream(decodedData, 0, decodedData.Length, false))
            {
                using (BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8))
                {
                    newSkill.ReadFromFile(binaryReader);
                }
            }
            return newSkill;
        }
        public static Buff ParseBuff(string value)
        {
            Buff newBuff = SkillFactory.CreateNewBuff();
            // 解码 Base64 字符串
            byte[] decodedData = Convert.FromBase64String(value);
            using (MemoryStream memoryStream = new MemoryStream(decodedData, 0, decodedData.Length, false))
            {
                using (BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8))
                {
                    newBuff.ReadFromFile(binaryReader);
                }
            }
            return newBuff;
        }

        public static EnemyInfo ParseEnemyInfo(string value)
        {
            EnemyInfo newInfo = new EnemyInfo();
            string[] splitValue = value.Split('|');
            for (int i = 0; i < splitValue.Length; i++)
            {
                string[] splitIntValue = splitValue[i].Split(',');
                if (splitIntValue.Length != 3)
                {
                    Log.Error("EnemyInfo Error");
                    return newInfo;
                }
                OneEnemyInfo oneInfo = new OneEnemyInfo();
                oneInfo.Pos = new Vector2Int(int.Parse(splitIntValue[0]), int.Parse(splitIntValue[1]));
                oneInfo.HeroID = int.Parse(splitIntValue[2]);
                newInfo.InfoList.Add(oneInfo);
            }
            return newInfo;
        }

        public static List<(int, int)> ParseListIntInt(string value)
        {
            List<(int,int)> result = new();
            if (string.IsNullOrEmpty(value))
            {
                return result;
            }
            string[] splitValue = value.Split(';');
            for (int i = 0; i < splitValue.Length; i++)
            {
                string[] splitInt = splitValue[i].Split(",");
                if (splitInt.Length != 2)
                {
                    Log.Error("Format Error");
                    throw new GameFrameworkException("Format Error");
                    continue;
                }
                result.Add((int.Parse(splitInt[0]),int.Parse(splitInt[1])));
            }

            return result;
        }
    }
}
