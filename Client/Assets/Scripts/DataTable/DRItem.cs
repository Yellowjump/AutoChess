//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------
// 此文件由工具自动生成，请勿直接修改。
// 生成时间：2024-08-31 11:43:49.377
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
    /// 道具表。
    /// </summary>
    public class DRItem : DataRowBase
    {
        private int m_Id = 0;

        /// <summary>
        /// 获取道具ID。
        /// </summary>
        public override int Id
        {
            get
            {
                return m_Id;
            }
        }

        /// <summary>
        /// 获取道具名。
        /// </summary>
        public string Name
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取道具描述。
        /// </summary>
        public string Decs
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取稀有度。
        /// </summary>
        public int Rarity
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取图片ID。
        /// </summary>
        public int IconID
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取合成材料。
        /// </summary>
        public List<(int,int)> CraftList
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取属性加成。
        /// </summary>
        public List<(int,int)> AttrAdd
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取对应技能ID。
        /// </summary>
        public int SkillID
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
            Name = columnStrings[index++];
            Decs = columnStrings[index++];
            Rarity = int.Parse(columnStrings[index++]);
            IconID = int.Parse(columnStrings[index++]);
                CraftList = DataTableExtension.ParseListIntInt(columnStrings[index++]);
                AttrAdd = DataTableExtension.ParseListIntInt(columnStrings[index++]);
            SkillID = int.Parse(columnStrings[index++]);

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
                    Name = binaryReader.ReadString();
                    Decs = binaryReader.ReadString();
                    Rarity = binaryReader.Read7BitEncodedInt32();
                    IconID = binaryReader.Read7BitEncodedInt32();
                        CraftList = binaryReader.ReadListIntInt();
                        AttrAdd = binaryReader.ReadListIntInt();
                    SkillID = binaryReader.Read7BitEncodedInt32();
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
