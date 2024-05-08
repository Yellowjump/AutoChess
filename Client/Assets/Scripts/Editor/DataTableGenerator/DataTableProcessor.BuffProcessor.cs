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
        private sealed class BuffProcessor : GenericDataProcessor<Buff>
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
                    return "Buff";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "Buff",
                    "SkillSystem.Buff"
                };
            }
            public override void WriteToStream(DataTableProcessor dataTableProcessor, BinaryWriter binaryWriter, string value)
            {
                // 解码 Base64 字符串
                byte[] decodedData = Convert.FromBase64String(value);
                binaryWriter.Write(decodedData);
            }

            public override Buff Parse(string value)
            {
                return null;
            }
        }
    }
}
