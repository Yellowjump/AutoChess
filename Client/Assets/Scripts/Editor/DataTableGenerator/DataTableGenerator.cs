//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DataTable.Editor.DataTableTools
{
    public sealed class DataTableGenerator
    {
        private const string DataTablePath = "Assets/Data/DataTables";
        private const string CSharpCodePath = "Assets/Scripts/DataTable";
        private const string CSharpCodeTemplateFileName = "Assets/Scripts/DataTable/DataTableCodeTemplate.txt";
        private static readonly Regex EndWithNumberRegex = new Regex(@"\d+$");
        private static readonly Regex NameRegex = new Regex(@"^[A-Z][A-Za-z0-9_]*$");

        private static string[] GenerateEnumDataTables =
        {
            "Skill"
        };

        public static DataTableProcessor CreateDataTableProcessor(string dataTableName)
        {
            return new DataTableProcessor(Utility.Path.GetRegularPath(Path.Combine(DataTablePath, dataTableName + ".txt")), Encoding.GetEncoding("GB2312"), 1, 2, null, 3, 4, 1);
        }

        public static bool CheckRawData(DataTableProcessor dataTableProcessor, string dataTableName)
        {
            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                string name = dataTableProcessor.GetName(i);
                if (string.IsNullOrEmpty(name) || name == "#")
                {
                    continue;
                }

                if (!NameRegex.IsMatch(name))
                {
                    Debug.LogWarning(Utility.Text.Format("Check raw data failure. DataTableName='{0}' Name='{1}'", dataTableName, name));
                    return false;
                }
            }

            return true;
        }

        public static void GenerateDataFile(DataTableProcessor dataTableProcessor, string dataTableName)
        {
            string binaryDataFileName = Utility.Path.GetRegularPath(Path.Combine(DataTablePath, dataTableName + ".bytes"));
            if (!dataTableProcessor.GenerateDataFile(binaryDataFileName) && File.Exists(binaryDataFileName))
            {
                File.Delete(binaryDataFileName);
            }
        }

        public static void GenerateCodeFile(DataTableProcessor dataTableProcessor, string dataTableName)
        {
            dataTableProcessor.SetCodeTemplate(CSharpCodeTemplateFileName, Encoding.UTF8);
            dataTableProcessor.SetCodeGenerator(DataTableCodeGenerator);

            string csharpCodeFileName = Utility.Path.GetRegularPath(Path.Combine(CSharpCodePath, "DR" + dataTableName + ".cs"));
            if (!dataTableProcessor.GenerateCodeFile(csharpCodeFileName, Encoding.UTF8, dataTableName) && File.Exists(csharpCodeFileName))
            {
                File.Delete(csharpCodeFileName);
            }
        }

        private static void DataTableCodeGenerator(DataTableProcessor dataTableProcessor, StringBuilder codeContent, object userData)
        {
            string dataTableName = (string)userData;

            codeContent.Replace("__DATA_TABLE_CREATE_TIME__", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            codeContent.Replace("__DATA_TABLE_NAME_SPACE__", "DataTable");
            codeContent.Replace("__DATA_TABLE_CLASS_NAME__", "DR" + dataTableName);
            codeContent.Replace("__DATA_TABLE_COMMENT__", dataTableProcessor.GetValue(0, 1) + "。");
            codeContent.Replace("__DATA_TABLE_ID_COMMENT__", "获取" + dataTableProcessor.GetComment(dataTableProcessor.IdColumn) + "。");
            codeContent.Replace("__DATA_TABLE_PROPERTIES__", GenerateDataTableProperties(dataTableProcessor));
            codeContent.Replace("__DATA_TABLE_PARSER__", GenerateDataTableParser(dataTableProcessor));
            codeContent.Replace("__DATA_TABLE_PROPERTY_ARRAY__", GenerateDataTablePropertyArray(dataTableProcessor));

            codeContent.Replace("__DATA_TABLE_PROPERTIES_ENUM__", GenerateDataTablePropertiesEnum(dataTableProcessor, dataTableName));
            codeContent.Replace("__DATA_TABLE_PROPERTY_GET_BY_ENUM__", GenerateDataTablePropertyGetByEnum(dataTableProcessor, dataTableName));
        }

        private static string GenerateDataTableProperties(DataTableProcessor dataTableProcessor)
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool firstProperty = true;
            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i))
                {
                    // 注释列
                    continue;
                }

                if (dataTableProcessor.IsIdColumn(i))
                {
                    // 编号列
                    continue;
                }

                if (firstProperty)
                {
                    firstProperty = false;
                }
                else
                {
                    stringBuilder.AppendLine().AppendLine();
                }

                stringBuilder
                    .AppendLine("        /// <summary>")
                    .AppendFormat("        /// 获取{0}。", dataTableProcessor.GetComment(i)).AppendLine()
                    .AppendLine("        /// </summary>")
                    .AppendFormat("        public {0} {1}", dataTableProcessor.GetLanguageKeyword(i), dataTableProcessor.GetName(i)).AppendLine()
                    .AppendLine("        {")
                    .AppendLine("            get;")
                    .AppendLine("            private set;")
                    .Append("        }");
            }

            return stringBuilder.ToString();
        }

        private static string GenerateDataTableParser(DataTableProcessor dataTableProcessor)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder
                .AppendLine("        public override bool ParseDataRow(string dataRowString, object userData)")
                .AppendLine("        {")
                .AppendLine("            string[] columnStrings = dataRowString.Split(DataTableExtension.DataSplitSeparators);")
                .AppendLine("            for (int i = 0; i < columnStrings.Length; i++)")
                .AppendLine("            {")
                .AppendLine("                columnStrings[i] = columnStrings[i].Trim(DataTableExtension.DataTrimSeparators);")
                .AppendLine("            }")
                .AppendLine()
                .AppendLine("            int index = 0;");

            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i))
                {
                    // 注释列
                    stringBuilder.AppendLine("            index++;");
                    continue;
                }

                if (dataTableProcessor.IsIdColumn(i))
                {
                    // 编号列
                    stringBuilder.AppendLine("            m_Id = int.Parse(columnStrings[index++]);");
                    continue;
                }

                string languageKeyword = dataTableProcessor.GetLanguageKeyword(i);

                if (dataTableProcessor.IsSystem(i))
                {
                    if (languageKeyword == "string")
                    {
                        stringBuilder.AppendFormat("            {0} = columnStrings[index++];", dataTableProcessor.GetName(i)).AppendLine();
                    }
                    else
                    {
                        stringBuilder.AppendFormat("            {0} = {1}.Parse(columnStrings[index++]);", dataTableProcessor.GetName(i), languageKeyword).AppendLine();
                    }
                }
                else if (languageKeyword.Contains("[]"))
                {
                    string readFunctionName = dataTableProcessor.GetType(i).Name.Replace("[]", "Array");
                    stringBuilder.AppendFormat("                {0} = DataTableExtension.Parse{1}(columnStrings[index++]);", dataTableProcessor.GetName(i), readFunctionName).AppendLine();
                }
                else
                {
                    stringBuilder.AppendFormat("            {0} = DataTableExtension.Parse{1}(columnStrings[index++]);", dataTableProcessor.GetName(i), dataTableProcessor.GetType(i).Name).AppendLine();
                }
            }

            stringBuilder.AppendLine()
                .AppendLine("            GeneratePropertyArray();")
                .AppendLine("            return true;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        public override bool ParseDataRow(byte[] dataRowBytes, int startIndex, int length, object userData)")
                .AppendLine("        {")
                .AppendLine("            using (MemoryStream memoryStream = new MemoryStream(dataRowBytes, startIndex, length, false))")
                .AppendLine("            {")
                .AppendLine("                using (BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8))")
                .AppendLine("                {");

            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i))
                {
                    // 注释列
                    continue;
                }

                if (dataTableProcessor.IsIdColumn(i))
                {
                    // 编号列
                    stringBuilder.AppendLine("                    m_Id = binaryReader.Read7BitEncodedInt32();");
                    continue;
                }

                string languageKeyword = dataTableProcessor.GetLanguageKeyword(i);
                if (languageKeyword == "int" || languageKeyword == "uint" || languageKeyword == "long" || languageKeyword == "ulong")
                {
                    stringBuilder.AppendFormat("                    {0} = binaryReader.Read7BitEncoded{1}();", dataTableProcessor.GetName(i), dataTableProcessor.GetType(i).Name).AppendLine();
                }
                else if (languageKeyword.Contains("[]"))
                {
                    string readFunctionName = dataTableProcessor.GetType(i).Name.Replace("[]", "Array");
                    stringBuilder.AppendFormat("                        {0} = binaryReader.Read{1}();", dataTableProcessor.GetName(i), readFunctionName).AppendLine();
                }
                else
                {
                    stringBuilder.AppendFormat("                    {0} = binaryReader.Read{1}();", dataTableProcessor.GetName(i), dataTableProcessor.GetType(i).Name).AppendLine();
                }
            }

            stringBuilder
                .AppendLine("                }")
                .AppendLine("            }")
                .AppendLine()
                .AppendLine("            GeneratePropertyArray();")
                .AppendLine("            return true;")
                .Append("        }");

            return stringBuilder.ToString();
        }

        private static string GenerateDataTablePropertyArray(DataTableProcessor dataTableProcessor)
        {
            List<PropertyCollection> propertyCollections = new List<PropertyCollection>();
            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i))
                {
                    // 注释列
                    continue;
                }

                if (dataTableProcessor.IsIdColumn(i))
                {
                    // 编号列
                    continue;
                }

                string name = dataTableProcessor.GetName(i);
                if (!EndWithNumberRegex.IsMatch(name))
                {
                    continue;
                }

                string propertyCollectionName = EndWithNumberRegex.Replace(name, string.Empty);
                int id = int.Parse(EndWithNumberRegex.Match(name).Value);

                PropertyCollection propertyCollection = null;
                foreach (PropertyCollection pc in propertyCollections)
                {
                    if (pc.Name == propertyCollectionName)
                    {
                        propertyCollection = pc;
                        break;
                    }
                }

                if (propertyCollection == null)
                {
                    propertyCollection = new PropertyCollection(propertyCollectionName, dataTableProcessor.GetLanguageKeyword(i));
                    propertyCollections.Add(propertyCollection);
                }

                propertyCollection.AddItem(id, name);
            }

            StringBuilder stringBuilder = new StringBuilder();
            bool firstProperty = true;
            foreach (PropertyCollection propertyCollection in propertyCollections)
            {
                if (firstProperty)
                {
                    firstProperty = false;
                }
                else
                {
                    stringBuilder.AppendLine().AppendLine();
                }

                stringBuilder
                    .AppendFormat("        private KeyValuePair<int, {1}>[] m_{0} = null;", propertyCollection.Name, propertyCollection.LanguageKeyword).AppendLine()
                    .AppendLine()
                    .AppendFormat("        public int {0}Count", propertyCollection.Name).AppendLine()
                    .AppendLine("        {")
                    .AppendLine("            get")
                    .AppendLine("            {")
                    .AppendFormat("                return m_{0}.Length;", propertyCollection.Name).AppendLine()
                    .AppendLine("            }")
                    .AppendLine("        }")
                    .AppendLine()
                    .AppendFormat("        public {1} Get{0}(int id)", propertyCollection.Name, propertyCollection.LanguageKeyword).AppendLine()
                    .AppendLine("        {")
                    .AppendFormat("            foreach (KeyValuePair<int, {1}> i in m_{0})", propertyCollection.Name, propertyCollection.LanguageKeyword).AppendLine()
                    .AppendLine("            {")
                    .AppendLine("                if (i.Key == id)")
                    .AppendLine("                {")
                    .AppendLine("                    return i.Value;")
                    .AppendLine("                }")
                    .AppendLine("            }")
                    .AppendLine()
                    .AppendFormat("            throw new GameFrameworkException(Utility.Text.Format(\"Get{0} with invalid id '{{0}}'.\", id.ToString()));", propertyCollection.Name).AppendLine()
                    .AppendLine("        }")
                    .AppendLine()
                    .AppendFormat("        public {1} Get{0}At(int index)", propertyCollection.Name, propertyCollection.LanguageKeyword).AppendLine()
                    .AppendLine("        {")
                    .AppendFormat("            if (index < 0 || index >= m_{0}.Length)", propertyCollection.Name).AppendLine()
                    .AppendLine("            {")
                    .AppendFormat("                throw new GameFrameworkException(Utility.Text.Format(\"Get{0}At with invalid index '{{0}}'.\", index.ToString()));", propertyCollection.Name).AppendLine()
                    .AppendLine("            }")
                    .AppendLine()
                    .AppendFormat("            return m_{0}[index].Value;", propertyCollection.Name).AppendLine()
                    .Append("        }");
            }

            if (propertyCollections.Count > 0)
            {
                stringBuilder.AppendLine().AppendLine();
            }

            stringBuilder
                .AppendLine("        private void GeneratePropertyArray()")
                .AppendLine("        {");

            firstProperty = true;
            foreach (PropertyCollection propertyCollection in propertyCollections)
            {
                if (firstProperty)
                {
                    firstProperty = false;
                }
                else
                {
                    stringBuilder.AppendLine().AppendLine();
                }

                stringBuilder
                    .AppendFormat("            m_{0} = new KeyValuePair<int, {1}>[]", propertyCollection.Name, propertyCollection.LanguageKeyword).AppendLine()
                    .AppendLine("            {");

                int itemCount = propertyCollection.ItemCount;
                for (int i = 0; i < itemCount; i++)
                {
                    KeyValuePair<int, string> item = propertyCollection.GetItem(i);
                    stringBuilder.AppendFormat("                new KeyValuePair<int, {0}>({1}, {2}),", propertyCollection.LanguageKeyword, item.Key.ToString(), item.Value).AppendLine();
                }

                stringBuilder.Append("            };");
            }

            stringBuilder
                .AppendLine()
                .Append("        }");

            return stringBuilder.ToString();
        }

        private sealed class PropertyCollection
        {
            private readonly string m_Name;
            private readonly string m_LanguageKeyword;
            private readonly List<KeyValuePair<int, string>> m_Items;

            public PropertyCollection(string name, string languageKeyword)
            {
                m_Name = name;
                m_LanguageKeyword = languageKeyword;
                m_Items = new List<KeyValuePair<int, string>>();
            }

            public string Name
            {
                get { return m_Name; }
            }

            public string LanguageKeyword
            {
                get { return m_LanguageKeyword; }
            }

            public int ItemCount
            {
                get { return m_Items.Count; }
            }

            public KeyValuePair<int, string> GetItem(int index)
            {
                if (index < 0 || index >= m_Items.Count)
                {
                    throw new GameFrameworkException(Utility.Text.Format("GetItem with invalid index '{0}'.", index.ToString()));
                }

                return m_Items[index];
            }

            public void AddItem(int id, string propertyName)
            {
                m_Items.Add(new KeyValuePair<int, string>(id, propertyName));
            }
        }

        private static string GenerateDataTablePropertiesEnum(DataTableProcessor dataTableProcessor, string dataTableName)
        {
            if (!GenerateEnumDataTables.Contains(dataTableName))
            {
                return string.Empty;
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"\tpublic enum DR{dataTableName}Field");
            stringBuilder.AppendLine("\t{");

            bool firstProperty = true;
            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i) || dataTableProcessor.IsIdColumn(i))
                {
                    // 注释列或编号列
                    continue;
                }

                stringBuilder.AppendFormat("\t    {0},", dataTableProcessor.GetName(i)).AppendLine();
            }

            stringBuilder.AppendLine("\t}");
            return stringBuilder.ToString();
        }

        private static string GenerateDataTablePropertyGetByEnum(DataTableProcessor dataTableProcessor, string dataTableName)
        {
            if (!GenerateEnumDataTables.Contains(dataTableName))
            {
                return string.Empty;
            }

            string enumFieldName = $"DR{dataTableName}Field";
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder
                .AppendLine("\t\t/// <summary>")
                .AppendLine("\t\t/// 根据指定的枚举值获取表字段的值。")
                .AppendLine("\t\t/// </summary>")
                .AppendLine("\t\t/// <typeparam name=\"T\"> ")
                .AppendLine("\t\t///     <para>");

            // 生成枚举字段对应的类型注释
            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i))
                {
                    // 注释列
                    continue;
                }

                if (dataTableProcessor.IsIdColumn(i))
                {
                    // 编号列
                    continue;
                }

                stringBuilder
                    .AppendFormat("\t\t///         <see cref=\"{0}.{1}\"/> 对应的是 {2},", enumFieldName, dataTableProcessor.GetName(i),dataTableProcessor.GetLanguageKeyword(i)).AppendLine();
            }

            stringBuilder.AppendLine("\t\t///     </para>")
                .AppendLine("\t\t/// </typeparam>")
                .AppendFormat("\t\t/// <param name=\"field\">枚举值，表示需要获取的表字段</param>").AppendLine()
                .AppendFormat("\t\t/// <returns>返回字段值</returns>").AppendLine()
                .AppendFormat("\t\t/// <exception cref=\"ArgumentException\">当传入的枚举值无效时抛出异常</exception>").AppendLine()
                .AppendFormat("\t\tpublic T GetFieldValue<T>({0} field)", enumFieldName).AppendLine()
                .AppendLine("\t\t{")
                .AppendLine("\t\t    if (FieldMap.TryGetValue(field, out var func))")
                .AppendLine("\t\t    {")
                .AppendLine("\t\t        var ret = func(this);")
                .AppendLine("\t\t        if (ret.Item2 == typeof(T))")
                .AppendLine("\t\t        {")
                .AppendLine("\t\t            return (T)Convert.ChangeType(ret.Item1, typeof(T));")
                .AppendLine("\t\t        }")
                .AppendFormat("\t\t        throw new ArgumentException($\"Invalid {0} {{field}} type:{{ret.Item2}} errorType:{{typeof(T)}}\");", enumFieldName).AppendLine()
                .AppendLine("\t\t    }")
                .AppendLine("\t\t    else")
                .AppendLine("\t\t    {")
                .AppendLine("\t\t        throw new ArgumentException(\"Invalid " + enumFieldName + " value\");")
                .AppendLine("\t\t    }")
                .AppendLine("\t\t}");
            
            
            stringBuilder.AppendFormat("\t\tprivate static readonly Dictionary<{0}, Func<{1}, (object, Type)>> FieldMap = new Dictionary<{0}, Func<{1}, (object, Type)>>()",enumFieldName,"DR" + dataTableName).AppendLine();
            stringBuilder.AppendLine("\t\t{");

            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i) || dataTableProcessor.IsIdColumn(i))
                {
                    // 跳过注释列和编号列
                    continue;
                }

                string fieldName = dataTableProcessor.GetName(i);
                string fieldType = dataTableProcessor.GetLanguageKeyword(i);

                stringBuilder.AppendLine($"\t\t    {{ {enumFieldName}.{fieldName}, obj => (obj.{fieldName}, typeof({fieldType})) }},");
            }

            stringBuilder.AppendLine("\t\t};");
            return stringBuilder.ToString();
        }
    }
}