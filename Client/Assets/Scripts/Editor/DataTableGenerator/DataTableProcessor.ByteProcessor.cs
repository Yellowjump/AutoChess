//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System.IO;

namespace DataTable.Editor.DataTableTools
{
    public sealed partial class DataTableProcessor
    {
        private sealed class ByteProcessor : GenericDataProcessor<byte>
        {
            public override bool IsSystem
            {
                get
                {
                    return true;
                }
            }

            public override string LanguageKeyword
            {
                get
                {
                    return "byte";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "byte",
                    "system.byte"
                };
            }

            public override byte Parse(string value)
            {
                return string.IsNullOrEmpty(value) ? (byte)0 : byte.Parse(value);
            }

            public override void WriteToStream(DataTableProcessor dataTableProcessor, BinaryWriter binaryWriter, string value)
            {
                binaryWriter.Write(Parse(value));
            }
        }
    }
}
