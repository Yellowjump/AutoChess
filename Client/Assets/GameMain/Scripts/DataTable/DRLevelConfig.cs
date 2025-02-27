//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------
// 此文件由工具自动生成，请勿直接修改。
// 生成时间：2025-02-28 01:00:12.069
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
    /// 关卡配置表。
    /// </summary>
    public class DRLevelConfig : DataRowBase
    {
        private int m_Id = 0;

        /// <summary>
        /// 获取关卡ID。
        /// </summary>
        public override int Id
        {
            get
            {
                return m_Id;
            }
        }

        /// <summary>
        /// 获取关卡点类型。
        /// </summary>
        public int MazePointType
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取关卡信息。
        /// </summary>
        public int LevelInfo
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取显示的UI。
        /// </summary>
        public int ShowUIAssetID
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取放置战斗棋盘还是asset。
        /// </summary>
        public bool BattleOrLoadAsset
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取奖励配置ID。
        /// </summary>
        public int RewardID
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
            MazePointType = int.Parse(columnStrings[index++]);
            LevelInfo = int.Parse(columnStrings[index++]);
            ShowUIAssetID = int.Parse(columnStrings[index++]);
            BattleOrLoadAsset = bool.Parse(columnStrings[index++]);
            RewardID = int.Parse(columnStrings[index++]);
            ParamInt1 = int.Parse(columnStrings[index++]);

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
                    MazePointType = binaryReader.Read7BitEncodedInt32();
                    LevelInfo = binaryReader.Read7BitEncodedInt32();
                    ShowUIAssetID = binaryReader.Read7BitEncodedInt32();
                    BattleOrLoadAsset = binaryReader.ReadBoolean();
                    RewardID = binaryReader.Read7BitEncodedInt32();
                    ParamInt1 = binaryReader.Read7BitEncodedInt32();
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

        private void GeneratePropertyArray()
        {
            m_ParamInt = new KeyValuePair<int, int>[]
            {
                new KeyValuePair<int, int>(1, ParamInt1),
            };
        }


    }
}
