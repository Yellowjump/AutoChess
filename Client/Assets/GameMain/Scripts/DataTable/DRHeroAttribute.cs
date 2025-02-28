//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------
// 此文件由工具自动生成，请勿直接修改。
// 生成时间：2025-02-28 18:33:45.285
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
        /// 获取攻速增益。
        /// </summary>
        public int AttackSpeed
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取护盾增益。
        /// </summary>
        public int HudunBoost
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取技能急速。
        /// </summary>
        public int CooldownReduce
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
            AttackSpeed = int.Parse(columnStrings[index++]);
            HudunBoost = int.Parse(columnStrings[index++]);
            CooldownReduce = int.Parse(columnStrings[index++]);

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
                    AttackSpeed = binaryReader.Read7BitEncodedInt32();
                    HudunBoost = binaryReader.Read7BitEncodedInt32();
                    CooldownReduce = binaryReader.Read7BitEncodedInt32();
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
