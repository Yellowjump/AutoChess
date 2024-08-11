//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------
// 此文件由工具自动生成，请勿直接修改。
// 生成时间：2024-08-11 00:32:20.628
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
    /// 子弹配置表。
    /// </summary>
    public class DRBullet : DataRowBase
    {
        private int m_Id = 0;

        /// <summary>
        /// 获取子弹ID。
        /// </summary>
        public override int Id
        {
            get
            {
                return m_Id;
            }
        }

        /// <summary>
        /// 获取子弹类型。
        /// </summary>
        public int BulletType
        {
            get;
            private set;
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
        /// 获取int参数1。
        /// </summary>
        public int ParamInt1
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取int参数2。
        /// </summary>
        public int ParamInt2
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取int参数3。
        /// </summary>
        public int ParamInt3
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取int参数4。
        /// </summary>
        public int ParamInt4
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取int参数5。
        /// </summary>
        public int ParamInt5
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取str参数1。
        /// </summary>
        public string ParamStr1
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取str参数2。
        /// </summary>
        public string ParamStr2
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
            BulletType = int.Parse(columnStrings[index++]);
            AssetPathID = int.Parse(columnStrings[index++]);
            ParamInt1 = int.Parse(columnStrings[index++]);
            ParamInt2 = int.Parse(columnStrings[index++]);
            ParamInt3 = int.Parse(columnStrings[index++]);
            ParamInt4 = int.Parse(columnStrings[index++]);
            ParamInt5 = int.Parse(columnStrings[index++]);
            ParamStr1 = columnStrings[index++];
            ParamStr2 = columnStrings[index++];

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
                    BulletType = binaryReader.Read7BitEncodedInt32();
                    AssetPathID = binaryReader.Read7BitEncodedInt32();
                    ParamInt1 = binaryReader.Read7BitEncodedInt32();
                    ParamInt2 = binaryReader.Read7BitEncodedInt32();
                    ParamInt3 = binaryReader.Read7BitEncodedInt32();
                    ParamInt4 = binaryReader.Read7BitEncodedInt32();
                    ParamInt5 = binaryReader.Read7BitEncodedInt32();
                    ParamStr1 = binaryReader.ReadString();
                    ParamStr2 = binaryReader.ReadString();
                }
            }

            GeneratePropertyArray();
            return true;
        }

        private KeyValuePair<int, int>[] m_ParamInt = null;

        public int ParamIntCount
        {
            get
            {
                return m_ParamInt.Length;
            }
        }

        public int GetParamInt(int id)
        {
            foreach (KeyValuePair<int, int> i in m_ParamInt)
            {
                if (i.Key == id)
                {
                    return i.Value;
                }
            }

            throw new GameFrameworkException(Utility.Text.Format("GetParamInt with invalid id '{0}'.", id.ToString()));
        }

        public int GetParamIntAt(int index)
        {
            if (index < 0 || index >= m_ParamInt.Length)
            {
                throw new GameFrameworkException(Utility.Text.Format("GetParamIntAt with invalid index '{0}'.", index.ToString()));
            }

            return m_ParamInt[index].Value;
        }

        private KeyValuePair<int, string>[] m_ParamStr = null;

        public int ParamStrCount
        {
            get
            {
                return m_ParamStr.Length;
            }
        }

        public string GetParamStr(int id)
        {
            foreach (KeyValuePair<int, string> i in m_ParamStr)
            {
                if (i.Key == id)
                {
                    return i.Value;
                }
            }

            throw new GameFrameworkException(Utility.Text.Format("GetParamStr with invalid id '{0}'.", id.ToString()));
        }

        public string GetParamStrAt(int index)
        {
            if (index < 0 || index >= m_ParamStr.Length)
            {
                throw new GameFrameworkException(Utility.Text.Format("GetParamStrAt with invalid index '{0}'.", index.ToString()));
            }

            return m_ParamStr[index].Value;
        }

        private void GeneratePropertyArray()
        {
            m_ParamInt = new KeyValuePair<int, int>[]
            {
                new KeyValuePair<int, int>(1, ParamInt1),
                new KeyValuePair<int, int>(2, ParamInt2),
                new KeyValuePair<int, int>(3, ParamInt3),
                new KeyValuePair<int, int>(4, ParamInt4),
                new KeyValuePair<int, int>(5, ParamInt5),
            };

            m_ParamStr = new KeyValuePair<int, string>[]
            {
                new KeyValuePair<int, string>(1, ParamStr1),
                new KeyValuePair<int, string>(2, ParamStr2),
            };
        }


    }
}
