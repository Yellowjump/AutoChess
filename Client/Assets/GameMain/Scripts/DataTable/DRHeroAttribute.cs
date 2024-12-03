//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------
// 此文件由工具自动生成，请勿直接修改。
// 生成时间：2024-11-15 01:06:33.153
//------------------------------------------------------------

using GameFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityGameFramework.Runtime;
using SkillSystem;
using Entity;

namespace DataTable
{

    /// <summary>
    /// 角色属性表。
    /// </summary>
    public class DRHeroAttribute : DataRowBase
    {
        private int m_Id = 0;

        /// <summary>
        /// 获取角色属性ID。
        /// </summary>
        public override int Id
        {
            get
            {
                return m_Id;
            }
        }

        /// <summary>
        /// 获取血量。
        /// </summary>
        public int Hp
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取蓝量。
        /// </summary>
        public int Power
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取攻击力。
        /// </summary>
        public int AttackDamage
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取法强。
        /// </summary>
        public int AbilityPower
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取攻速。
        /// </summary>
        public float AttackSpeed
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取护甲。
        /// </summary>
        public int Armor
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取魔抗。
        /// </summary>
        public int MagicResist
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取护甲穿透固定值。
        /// </summary>
        public int ArmorPenetrationNum
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取护甲穿透百分比。
        /// </summary>
        public int ArmorPenetrationPercent
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取法穿固定值。
        /// </summary>
        public int MagicPenetrationNum
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取法穿百分比。
        /// </summary>
        public int MagicPenetrationPercent
        {
            get;
            private set;
        }

        public override bool ParseDataRow(string dataRowString, object userData)
        {
            string[] columnStrings = dataRowString.Split(DataTableExtension.DataSplitSeparators);
            for (int i = 0; i < columnStrings.Length; i++)
            {
                columnStrings[i] = columnStrings[i].Trim(DataTableExtension.DataTrimSeparators);
            }

            int index = 0;
            index++;
            m_Id = int.Parse(columnStrings[index++]);
            Hp = int.Parse(columnStrings[index++]);
            Power = int.Parse(columnStrings[index++]);
            AttackDamage = int.Parse(columnStrings[index++]);
            AbilityPower = int.Parse(columnStrings[index++]);
            AttackSpeed = float.Parse(columnStrings[index++]);
            Armor = int.Parse(columnStrings[index++]);
            MagicResist = int.Parse(columnStrings[index++]);
            ArmorPenetrationNum = int.Parse(columnStrings[index++]);
            ArmorPenetrationPercent = int.Parse(columnStrings[index++]);
            MagicPenetrationNum = int.Parse(columnStrings[index++]);
            MagicPenetrationPercent = int.Parse(columnStrings[index++]);

            GeneratePropertyArray();
            return true;
        }

        public override bool ParseDataRow(byte[] dataRowBytes, int startIndex, int length, object userData)
        {
            using (MemoryStream memoryStream = new MemoryStream(dataRowBytes, startIndex, length, false))
            {
                using (BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8))
                {
                    m_Id = binaryReader.Read7BitEncodedInt32();
                    Hp = binaryReader.Read7BitEncodedInt32();
                    Power = binaryReader.Read7BitEncodedInt32();
                    AttackDamage = binaryReader.Read7BitEncodedInt32();
                    AbilityPower = binaryReader.Read7BitEncodedInt32();
                    AttackSpeed = binaryReader.ReadSingle();
                    Armor = binaryReader.Read7BitEncodedInt32();
                    MagicResist = binaryReader.Read7BitEncodedInt32();
                    ArmorPenetrationNum = binaryReader.Read7BitEncodedInt32();
                    ArmorPenetrationPercent = binaryReader.Read7BitEncodedInt32();
                    MagicPenetrationNum = binaryReader.Read7BitEncodedInt32();
                    MagicPenetrationPercent = binaryReader.Read7BitEncodedInt32();
                }
            }

            GeneratePropertyArray();
            return true;
        }

        private void GeneratePropertyArray()
        {

        }


    }
}
