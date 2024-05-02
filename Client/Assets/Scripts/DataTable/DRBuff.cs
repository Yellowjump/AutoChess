//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------
// 此文件由工具自动生成，请勿直接修改。
// 生成时间：2024-05-01 21:43:12.503
//------------------------------------------------------------

using GameFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityGameFramework.Runtime;
using SkillSystem;

namespace DataTable
{
	public enum DRBuffField
	{
	    TemplateID,
	    Duration,
	    IntParam1,
	    IntParam2,
	    IntParam3,
	    IntParam4,
	    IntParam5,
	    StringParam1,
	    StringParam2,
	}

    /// <summary>
    /// buff表。
    /// </summary>
    public class DRBuff : DataRowBase
    {
        private int m_Id = 0;

        /// <summary>
        /// 获取buffID。
        /// </summary>
        public override int Id
        {
            get
            {
                return m_Id;
            }
        }

        /// <summary>
        /// 获取模板ID。
        /// </summary>
        public int TemplateID
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取持续时间ms。
        /// </summary>
        public int Duration
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取int参数1。
        /// </summary>
        public int IntParam1
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取int参数2。
        /// </summary>
        public int IntParam2
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取int参数3。
        /// </summary>
        public int IntParam3
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取int参数4。
        /// </summary>
        public int IntParam4
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取int参数5。
        /// </summary>
        public int IntParam5
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取str参数1。
        /// </summary>
        public string StringParam1
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取str参数2。
        /// </summary>
        public string StringParam2
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
            TemplateID = int.Parse(columnStrings[index++]);
            Duration = int.Parse(columnStrings[index++]);
            IntParam1 = int.Parse(columnStrings[index++]);
            IntParam2 = int.Parse(columnStrings[index++]);
            IntParam3 = int.Parse(columnStrings[index++]);
            IntParam4 = int.Parse(columnStrings[index++]);
            IntParam5 = int.Parse(columnStrings[index++]);
            StringParam1 = columnStrings[index++];
            StringParam2 = columnStrings[index++];

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
                    TemplateID = binaryReader.Read7BitEncodedInt32();
                    Duration = binaryReader.Read7BitEncodedInt32();
                    IntParam1 = binaryReader.Read7BitEncodedInt32();
                    IntParam2 = binaryReader.Read7BitEncodedInt32();
                    IntParam3 = binaryReader.Read7BitEncodedInt32();
                    IntParam4 = binaryReader.Read7BitEncodedInt32();
                    IntParam5 = binaryReader.Read7BitEncodedInt32();
                    StringParam1 = binaryReader.ReadString();
                    StringParam2 = binaryReader.ReadString();
                }
            }

            GeneratePropertyArray();
            return true;
        }

        private KeyValuePair<int, int>[] m_IntParam = null;

        public int IntParamCount
        {
            get
            {
                return m_IntParam.Length;
            }
        }

        public int GetIntParam(int id)
        {
            foreach (KeyValuePair<int, int> i in m_IntParam)
            {
                if (i.Key == id)
                {
                    return i.Value;
                }
            }

            throw new GameFrameworkException(Utility.Text.Format("GetIntParam with invalid id '{0}'.", id.ToString()));
        }

        public int GetIntParamAt(int index)
        {
            if (index < 0 || index >= m_IntParam.Length)
            {
                throw new GameFrameworkException(Utility.Text.Format("GetIntParamAt with invalid index '{0}'.", index.ToString()));
            }

            return m_IntParam[index].Value;
        }

        private KeyValuePair<int, string>[] m_StringParam = null;

        public int StringParamCount
        {
            get
            {
                return m_StringParam.Length;
            }
        }

        public string GetStringParam(int id)
        {
            foreach (KeyValuePair<int, string> i in m_StringParam)
            {
                if (i.Key == id)
                {
                    return i.Value;
                }
            }

            throw new GameFrameworkException(Utility.Text.Format("GetStringParam with invalid id '{0}'.", id.ToString()));
        }

        public string GetStringParamAt(int index)
        {
            if (index < 0 || index >= m_StringParam.Length)
            {
                throw new GameFrameworkException(Utility.Text.Format("GetStringParamAt with invalid index '{0}'.", index.ToString()));
            }

            return m_StringParam[index].Value;
        }

        private void GeneratePropertyArray()
        {
            m_IntParam = new KeyValuePair<int, int>[]
            {
                new KeyValuePair<int, int>(1, IntParam1),
                new KeyValuePair<int, int>(2, IntParam2),
                new KeyValuePair<int, int>(3, IntParam3),
                new KeyValuePair<int, int>(4, IntParam4),
                new KeyValuePair<int, int>(5, IntParam5),
            };

            m_StringParam = new KeyValuePair<int, string>[]
            {
                new KeyValuePair<int, string>(1, StringParam1),
                new KeyValuePair<int, string>(2, StringParam2),
            };
        }

		/// <summary>
		/// 根据指定的枚举值获取表字段的值。
		/// </summary>
		/// <typeparam name="T"> 
		///     <para>
		///         <see cref="DRBuffField.TemplateID"/> 对应的是 int,
		///         <see cref="DRBuffField.Duration"/> 对应的是 int,
		///         <see cref="DRBuffField.IntParam1"/> 对应的是 int,
		///         <see cref="DRBuffField.IntParam2"/> 对应的是 int,
		///         <see cref="DRBuffField.IntParam3"/> 对应的是 int,
		///         <see cref="DRBuffField.IntParam4"/> 对应的是 int,
		///         <see cref="DRBuffField.IntParam5"/> 对应的是 int,
		///         <see cref="DRBuffField.StringParam1"/> 对应的是 string,
		///         <see cref="DRBuffField.StringParam2"/> 对应的是 string,
		///     </para>
		/// </typeparam>
		/// <param name="field">枚举值，表示需要获取的表字段</param>
		/// <returns>返回字段值</returns>
		/// <exception cref="ArgumentException">当传入的枚举值无效时抛出异常</exception>
		public T GetFieldValue<T>(DRBuffField field)
		{
		    if (FieldMap.TryGetValue(field, out var func))
		    {
		        var ret = func(this);
		        if (ret.Item2 == typeof(T))
		        {
		            return (T)Convert.ChangeType(ret.Item1, typeof(T));
		        }
		        throw new ArgumentException($"Invalid DRBuffField {field} type:{ret.Item2} errorType:{typeof(T)}");
		    }
		    else
		    {
		        throw new ArgumentException("Invalid DRBuffField value");
		    }
		}
		private static readonly Dictionary<DRBuffField, Func<DRBuff, (object, Type)>> FieldMap = new Dictionary<DRBuffField, Func<DRBuff, (object, Type)>>()
		{
		    { DRBuffField.TemplateID, obj => (obj.TemplateID, typeof(int)) },
		    { DRBuffField.Duration, obj => (obj.Duration, typeof(int)) },
		    { DRBuffField.IntParam1, obj => (obj.IntParam1, typeof(int)) },
		    { DRBuffField.IntParam2, obj => (obj.IntParam2, typeof(int)) },
		    { DRBuffField.IntParam3, obj => (obj.IntParam3, typeof(int)) },
		    { DRBuffField.IntParam4, obj => (obj.IntParam4, typeof(int)) },
		    { DRBuffField.IntParam5, obj => (obj.IntParam5, typeof(int)) },
		    { DRBuffField.StringParam1, obj => (obj.StringParam1, typeof(string)) },
		    { DRBuffField.StringParam2, obj => (obj.StringParam2, typeof(string)) },
		};

    }
}
