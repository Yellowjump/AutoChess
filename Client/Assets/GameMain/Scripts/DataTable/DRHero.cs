//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------
// 此文件由工具自动生成，请勿直接修改。
// 生成时间：2024-09-20 17:20:34.748
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
    /// 角色表。
    /// </summary>
    public class DRHero : DataRowBase
    {
        private int m_Id = 0;

        /// <summary>
        /// 获取角色ID。
        /// </summary>
        public override int Id
        {
            get
            {
                return m_Id;
            }
        }

        /// <summary>
        /// 获取技能ID。
        /// </summary>
        public int PassiveSkillID
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取技能ID。
        /// </summary>
        public int SpSkillID
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取默认初始携带道具。
        /// </summary>
        public int[] DefaultItemID
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取属性ID。
        /// </summary>
        public int AttributeID
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取外观资源ID。
        /// </summary>
        public int AssetID
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取idle动画。
        /// </summary>
        public int IdleAnimID
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取run动画。
        /// </summary>
        public int RunAnimID
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
            index++;
            PassiveSkillID = int.Parse(columnStrings[index++]);
            SpSkillID = int.Parse(columnStrings[index++]);
                DefaultItemID = DataTableExtension.ParseInt32Array(columnStrings[index++]);
            AttributeID = int.Parse(columnStrings[index++]);
            AssetID = int.Parse(columnStrings[index++]);
            IdleAnimID = int.Parse(columnStrings[index++]);
            RunAnimID = int.Parse(columnStrings[index++]);
            index++;

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
                    PassiveSkillID = binaryReader.Read7BitEncodedInt32();
                    SpSkillID = binaryReader.Read7BitEncodedInt32();
                        DefaultItemID = binaryReader.ReadInt32Array();
                    AttributeID = binaryReader.Read7BitEncodedInt32();
                    AssetID = binaryReader.Read7BitEncodedInt32();
                    IdleAnimID = binaryReader.Read7BitEncodedInt32();
                    RunAnimID = binaryReader.Read7BitEncodedInt32();
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
