//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using SkillSystem;
using UnityEngine;

namespace DataTable.Editor.DataTableTools
{
    public sealed partial class DataTableProcessor
    {
        private sealed class SkillProcessor : GenericDataProcessor<Skill>
        {
            public override bool IsSystem
            {
                get
                {
                    return false;
                }
            }

            public override string LanguageKeyword
            {
                get
                {
                    return "Skill";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "Skill",
                    "SkillSystem.Skill"
                };
            }
            public override void WriteToStream(DataTableProcessor dataTableProcessor, BinaryWriter binaryWriter, string value)
            {
                // 解码 Base64 字符串
                byte[] decodedData = Convert.FromBase64String(value);
                binaryWriter.Write(decodedData);
            }

            public override Skill Parse(string value)
            {
                return null;
            }
        }
    }
}
