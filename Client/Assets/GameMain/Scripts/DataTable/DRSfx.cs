//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------
// 此文件由工具自动生成，请勿直接修改。
// 生成时间：2025-03-09 05:53:29.722
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
    /// 特效配置表。
    /// </summary>
    public class DRSfx : DataRowBase
    {
        private int m_Id = 0;

        /// <summary>
        /// 获取特效ID。
        /// </summary>
        public override int Id
        {
            get
            {
                return m_Id;
            }
        }

        /// <summary>
        /// 获取对应资源表ID。
        /// </summary>
        public int AssetPathID
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取持续时间。
        /// </summary>
        public int DurationMs
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取是否跟随持有者。
        /// </summary>
        public bool FollowOwner
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取是否单独存在。
        /// </summary>
        public bool IsOnlyOne
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取位置偏移。
        /// </summary>
        public Vector3 PosOffset
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取位置随机偏移限制。
        /// </summary>
        public Vector3 PosRandom
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取大小。
        /// </summary>
        public Vector3 SizeOffset
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
            AssetPathID = int.Parse(columnStrings[index++]);
            DurationMs = int.Parse(columnStrings[index++]);
            FollowOwner = bool.Parse(columnStrings[index++]);
            IsOnlyOne = bool.Parse(columnStrings[index++]);
            PosOffset = DataTableExtension.ParseVector3(columnStrings[index++]);
            PosRandom = DataTableExtension.ParseVector3(columnStrings[index++]);
            SizeOffset = DataTableExtension.ParseVector3(columnStrings[index++]);

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
                    AssetPathID = binaryReader.Read7BitEncodedInt32();
                    DurationMs = binaryReader.Read7BitEncodedInt32();
                    FollowOwner = binaryReader.ReadBoolean();
                    IsOnlyOne = binaryReader.ReadBoolean();
                    PosOffset = binaryReader.ReadVector3();
                    PosRandom = binaryReader.ReadVector3();
                    SizeOffset = binaryReader.ReadVector3();
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
