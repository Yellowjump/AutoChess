//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using Entity;
using SkillSystem;
using UnityEngine;

namespace DataTable
{
    public static class BinaryReaderExtension
    {
        public static Color32 ReadColor32(this BinaryReader binaryReader)
        {
            return new Color32(binaryReader.ReadByte(), binaryReader.ReadByte(), binaryReader.ReadByte(), binaryReader.ReadByte());
        }

        public static Color ReadColor(this BinaryReader binaryReader)
        {
            return new Color(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
        }

        public static DateTime ReadDateTime(this BinaryReader binaryReader)
        {
            return new DateTime(binaryReader.ReadInt64());
        }

        public static Quaternion ReadQuaternion(this BinaryReader binaryReader)
        {
            return new Quaternion(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
        }

        public static Rect ReadRect(this BinaryReader binaryReader)
        {
            return new Rect(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
        }

        public static Vector2 ReadVector2(this BinaryReader binaryReader)
        {
            return new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
        }

        public static Vector3 ReadVector3(this BinaryReader binaryReader)
        {
            return new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
        }

        public static Vector4 ReadVector4(this BinaryReader binaryReader)
        {
            return new Vector4(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
        }

        public static int[] ReadInt32Array(this BinaryReader binaryReader)
        {
            int length = binaryReader.ReadInt32();
            int[] intArray = new int[length];
            for (int i = 0; i < length; i++)
            {
                intArray[i] = binaryReader.ReadInt32();
            }

            return intArray;
        }
        public static Skill ReadSkill(this BinaryReader binaryReader)
        {
            var newSkill =  SkillFactory.CreateNewSkill();
            newSkill.ReadFromFile(binaryReader);
            return newSkill;
        }
        public static Buff ReadBuff(this BinaryReader binaryReader)
        {
            var newBuff =  SkillFactory.CreateNewBuff();
            newBuff.ReadFromFile(binaryReader);
            return newBuff;
        }

        public static EnemyInfo ReadEnemyInfo(this BinaryReader binaryReader)
        {
            var newInfo = new EnemyInfo();
            int length = binaryReader.ReadInt32();
            for (int i = 0; i < length; i++)
            {
                var newOneInfo = new OneEnemyInfo();
                newOneInfo.Pos = new Vector2Int(binaryReader.ReadInt32(), binaryReader.ReadInt32());
                newOneInfo.HeroID = binaryReader.ReadInt32();
                newInfo.InfoList.Add(newOneInfo);
            }
            return newInfo;
        }

        public static List<(int, int)> ReadListIntInt(this BinaryReader binaryReader)
        {
            List<(int, int)> listIntInt = new();
            int length = binaryReader.ReadInt32();
            for (int i = 0; i < length; i++)
            {
                listIntInt.Add((binaryReader.ReadInt32(),binaryReader.ReadInt32()));
            }
            return listIntInt;
        }
    }
}
