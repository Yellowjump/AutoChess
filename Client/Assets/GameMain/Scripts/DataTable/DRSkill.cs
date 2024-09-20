//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------
// 此文件由工具自动生成，请勿直接修改。
// 生成时间：2024-09-20 17:20:34.756
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
	public enum DRSkillField
	{
	    SkillType,
	    TemplateID,
	    SkillAnim,
	    BeforeShakeEndMs,
	    AniDuration,
	    CDMs,
	    TargetType,
	    SkillRange,
	    IntParam1,
	    IntParam2,
	    IntParam3,
	    IntParam4,
	    IntParam5,
	    StringParam1,
	    StringParam2,
	}

    /// <summary>
    /// 资源路径配置表。
    /// </summary>
    public class DRSkill : DataRowBase
    {
        private int m_Id = 0;

        /// <summary>
        /// 获取技能ID。
        /// </summary>
        public override int Id
        {
            get
            {
                return m_Id;
            }
        }

        /// <summary>
        /// 获取技能类型。
        /// </summary>
        public int SkillType
        {
            get;
            private set;
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
        /// 获取技能动画资源ID。
        /// </summary>
        public int SkillAnim
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取动画前摇结束ms。
        /// </summary>
        public int BeforeShakeEndMs
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取动画时间ms。
        /// </summary>
        public int AniDuration
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取默认冷却时间。
        /// </summary>
        public int CDMs
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取目标类型。
        /// </summary>
        public int TargetType
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取技能范围。
        /// </summary>
        public int SkillRange
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
            SkillType = int.Parse(columnStrings[index++]);
            TemplateID = int.Parse(columnStrings[index++]);
            SkillAnim = int.Parse(columnStrings[index++]);
            BeforeShakeEndMs = int.Parse(columnStrings[index++]);
            AniDuration = int.Parse(columnStrings[index++]);
            CDMs = int.Parse(columnStrings[index++]);
            TargetType = int.Parse(columnStrings[index++]);
            SkillRange = int.Parse(columnStrings[index++]);
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
                    SkillType = binaryReader.Read7BitEncodedInt32();
                    TemplateID = binaryReader.Read7BitEncodedInt32();
                    SkillAnim = binaryReader.Read7BitEncodedInt32();
                    BeforeShakeEndMs = binaryReader.Read7BitEncodedInt32();
                    AniDuration = binaryReader.Read7BitEncodedInt32();
                    CDMs = binaryReader.Read7BitEncodedInt32();
                    TargetType = binaryReader.Read7BitEncodedInt32();
                    SkillRange = binaryReader.Read7BitEncodedInt32();
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
		///         <see cref="DRSkillField.SkillType"/> 对应的是 int,
		///         <see cref="DRSkillField.TemplateID"/> 对应的是 int,
		///         <see cref="DRSkillField.SkillAnim"/> 对应的是 int,
		///         <see cref="DRSkillField.BeforeShakeEndMs"/> 对应的是 int,
		///         <see cref="DRSkillField.AniDuration"/> 对应的是 int,
		///         <see cref="DRSkillField.CDMs"/> 对应的是 int,
		///         <see cref="DRSkillField.TargetType"/> 对应的是 int,
		///         <see cref="DRSkillField.SkillRange"/> 对应的是 int,
		///         <see cref="DRSkillField.IntParam1"/> 对应的是 int,
		///         <see cref="DRSkillField.IntParam2"/> 对应的是 int,
		///         <see cref="DRSkillField.IntParam3"/> 对应的是 int,
		///         <see cref="DRSkillField.IntParam4"/> 对应的是 int,
		///         <see cref="DRSkillField.IntParam5"/> 对应的是 int,
		///         <see cref="DRSkillField.StringParam1"/> 对应的是 string,
		///         <see cref="DRSkillField.StringParam2"/> 对应的是 string,
		///     </para>
		/// </typeparam>
		/// <param name="field">枚举值，表示需要获取的表字段</param>
		/// <returns>返回字段值</returns>
		/// <exception cref="ArgumentException">当传入的枚举值无效时抛出异常</exception>
		public T GetFieldValue<T>(DRSkillField field)
		{
		    if (FieldMap.TryGetValue(field, out var func))
		    {
		        var ret = func(this);
		        if (ret.Item2 == typeof(T))
		        {
		            return (T)Convert.ChangeType(ret.Item1, typeof(T));
		        }
		        throw new ArgumentException($"Invalid DRSkillField {field} type:{ret.Item2} errorType:{typeof(T)}");
		    }
		    else
		    {
		        throw new ArgumentException("Invalid DRSkillField value");
		    }
		}
		private static readonly Dictionary<DRSkillField, Func<DRSkill, (object, Type)>> FieldMap = new Dictionary<DRSkillField, Func<DRSkill, (object, Type)>>()
		{
		    { DRSkillField.SkillType, obj => (obj.SkillType, typeof(int)) },
		    { DRSkillField.TemplateID, obj => (obj.TemplateID, typeof(int)) },
		    { DRSkillField.SkillAnim, obj => (obj.SkillAnim, typeof(int)) },
		    { DRSkillField.BeforeShakeEndMs, obj => (obj.BeforeShakeEndMs, typeof(int)) },
		    { DRSkillField.AniDuration, obj => (obj.AniDuration, typeof(int)) },
		    { DRSkillField.CDMs, obj => (obj.CDMs, typeof(int)) },
		    { DRSkillField.TargetType, obj => (obj.TargetType, typeof(int)) },
		    { DRSkillField.SkillRange, obj => (obj.SkillRange, typeof(int)) },
		    { DRSkillField.IntParam1, obj => (obj.IntParam1, typeof(int)) },
		    { DRSkillField.IntParam2, obj => (obj.IntParam2, typeof(int)) },
		    { DRSkillField.IntParam3, obj => (obj.IntParam3, typeof(int)) },
		    { DRSkillField.IntParam4, obj => (obj.IntParam4, typeof(int)) },
		    { DRSkillField.IntParam5, obj => (obj.IntParam5, typeof(int)) },
		    { DRSkillField.StringParam1, obj => (obj.StringParam1, typeof(string)) },
		    { DRSkillField.StringParam2, obj => (obj.StringParam2, typeof(string)) },
		};

    }
}
